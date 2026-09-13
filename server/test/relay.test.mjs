import assert from "node:assert/strict";
import { once } from "node:events";
import test from "node:test";
import WebSocket, { WebSocketServer } from "ws";
import { createRelayServer } from "../src/server.mjs";
import { createGameSessions } from "../src/game-sessions.mjs";
import { createDemoDefinition, createDemoScenario } from "../src/demo-scenario.mjs";
import { createEncounterWorld } from "../src/encounter-world.mjs";

const START = JSON.stringify({ type: "gym.start", character: "maya" });

test("world channel advances server positions and rejects forged destinations and premature reset", async () => {
  const games = createGameSessions({ definitionFactory: createDemoDefinition, encounterFactory: createDemoScenario });
  const { relay, url } = await makeRelay({ apiKey: "", gameSessions: games, worldFactory: createEncounterWorld });
  try {
    const client = await openClient(url.replace('/live', '/game'));
    const until = async type => { let event; do { event = await nextJson(client); } while (event.type !== type); return event; };
    client.send(JSON.stringify({ type: "game.create" }));
    const ready = await until("game.ready");
    const first = await until("game.world");
    assert.ok(first.world.actors.player.position);
    client.send(JSON.stringify({ type: "game.walk", loopId: ready.snapshot.loopId, sequence: 1, destination: { x: 1, z: -5 } }));
    assert.equal((await until("game.move_result")).accepted, true);
    const moved = await until("game.world");
    assert.notDeepEqual(moved.world.actors.player.position, first.world.actors.player.position);
    client.send(JSON.stringify({ type: "game.walk", loopId: ready.snapshot.loopId, sequence: 2, destination: { x: 99999, z: 0 } }));
    const rejected = await until("game.move_result");
    assert.equal(rejected.accepted, false);
    client.send(JSON.stringify({ type: "game.reset", loopId: ready.snapshot.loopId, revision: ready.snapshot.revision }));
    assert.equal((await until("game.error")).code, "reset_unavailable");
    client.send(JSON.stringify({ type: "game.pause", paused: true }));
    assert.equal((await until("game.pause")).paused, true);
  } finally { await relay.close(); }
});

test("typed gameplay request reaches validated interpreter and commits through the active NPC lease", async () => {
  const games = createGameSessions({ definitionFactory: () => ({ authorizeAction: () => true }) });
  const created = games.create();
  const upstream = await makeUpstream({ onConnection(socket) {
    socket.on("message", raw => {
      if (JSON.parse(raw).type === "session.start") socket.send(JSON.stringify({ type: "session.started", session: { id: "live_game" } }));
    });
  } });
  const { relay, url } = await makeRelay({ upstreamUrl: upstream.url, gameSessions: games,
    intentModel: "test-interpreter", intentInterpreter: async body => {
      assert.equal(body.model, "test-interpreter");
      return { status: "completed", output: [{ type: "message", role: "assistant", status: "completed",
        content: [{ type: "output_text", text: JSON.stringify({ kind: "action_proposal", action: "wait", targetId: null, mood: null, clarification: null }) }] }] };
    },
  });
  try {
    const client = await openClient(url);
    client.send(JSON.stringify({ type: "gym.start", character: "maya", ...created.credentials }));
    let event;
    do { event = await nextJson(client); } while (event.status !== "ready");
    client.send(JSON.stringify({ type: "game.text", requestId: "wait_request", text: "Please wait here." }));
    do { event = await nextJson(client); } while (event.type !== "game.intent_result");
    assert.equal(event.ok, true);
    assert.equal(games.publicState(created.credentials).snapshot.actors.maya.action.type, "wait");
    assert.equal(games.history(created.credentials).history.fragments[0].source, "typed");
  } finally { await closeRelay(relay, upstream); }
});

test("game channel creates/resumes safe state and cannot inject private world mutations", async () => {
  const games = createGameSessions({ definitionFactory: () => ({ npcs: { theo: { secrets: ["hidden"] } } }) });
  const { relay, url } = await makeRelay({ gameSessions: games, apiKey: "" });
  try {
    const client = await openClient(url.replace('/live', '/game'));
    client.send(JSON.stringify({ type: "game.create" }));
    const ready = await nextJson(client);
    assert.equal(ready.type, "game.ready");
    assert.doesNotMatch(JSON.stringify(ready), /hidden|secrets|knowledge/);
    client.send(JSON.stringify({ type: "game.confirmFact", factId: "invented" }));
    assert.equal((await nextJson(client)).code, "unsupported_event");
    const second = await openClient(url.replace('/live', '/game'));
    second.send(JSON.stringify({ type: "game.resume", ...ready.credentials }));
    const resumed = await nextJson(second);
    assert.deepEqual(resumed.snapshot, ready.snapshot);
    const bad = await openClient(url.replace('/live', '/game'));
    bad.send(JSON.stringify({ type: "game.resume", gameId: ready.credentials.gameId, resumeToken: "wrong" }));
    assert.equal((await nextJson(bad)).code, "unauthorized");
  } finally { await relay.close(); }
});

test("game conversations restore private NPC context and persist transcripts across connections", async () => {
  const games = createGameSessions({ definitionFactory: () => ({
    npcs: { maya: { objective: "Stay with my friend" }, theo: { secrets: ["PRIVATE_THEO"] } },
  }) });
  const created = games.create();
  const starts = [];
  const upstream = await makeUpstream({ onConnection(socket) {
    socket.on("message", raw => {
      const event = JSON.parse(raw);
      if (event.type === "session.start") {
        starts.push(event);
        socket.send(JSON.stringify({ type: "session.started", session: { id: `live_${starts.length}` } }));
        socket.send(JSON.stringify({ type: "session.input_transcript.delta", event_id: "t1", delta: "Please wait.", start_ms: 0, end_ms: 100 }));
      } else if (event.type === "session.close") socket.send(JSON.stringify({ type: "session.closed", usage: { seconds: 1 } }));
    });
  } });
  const { relay, url } = await makeRelay({ upstreamUrl: upstream.url, gameSessions: games });
  try {
    for (let attempt = 0; attempt < 2; attempt++) {
      const client = await openClient(url);
      client.send(JSON.stringify({ type: "gym.start", character: "maya", ...created.credentials }));
      let received;
      do { received = await nextJson(client); } while (received.type !== "session.input_transcript.delta");
      const closed = once(client, "close");
      client.send(JSON.stringify({ type: "session.close" }));
      await closed;
    }
    assert.match(starts[0].session.instructions, /Stay with my friend/);
    assert.doesNotMatch(starts[0].session.instructions, /PRIVATE_THEO/);
    assert.equal(starts[1].session.input[0].content[0].text, "Please wait.");
    assert.equal(games.history(created.credentials).history.fragments.length, 2);
  } finally { await closeRelay(relay, upstream); }
});

async function openClient(url) {
  const socket = new WebSocket(url);
  const messages = [];
  const waiters = [];
  socket.on("message", data => {
    const value = JSON.parse(data.toString());
    const waiter = waiters.shift();
    if (waiter) waiter(value); else messages.push(value);
  });
  socket.nextJson = () => messages.length ? Promise.resolve(messages.shift()) : new Promise(resolve => waiters.push(resolve));
  await once(socket, "open");
  return socket;
}

function nextJson(socket) {
  return socket.nextJson();
}

async function makeUpstream({ onConnection } = {}) {
  const server = new WebSocketServer({ port: 0, host: "127.0.0.1" });
  await once(server, "listening");
  server.on("connection", (socket, request) => onConnection?.(socket, request));
  const { port } = server.address();
  return { url: `ws://127.0.0.1:${port}`, close: () => new Promise(resolve => server.close(resolve)) };
}

async function makeRelay(overrides = {}) {
  const relay = createRelayServer({
    host: "127.0.0.1",
    port: 0,
    apiKey: "test-key",
    startupTimeoutMs: 200,
    startTimeoutMs: 200,
    closeTimeoutMs: 200,
    maxDurationMs: 2_000,
    maxSessions: 2,
    maxPendingConnections: 4,
    maxMessageBytes: 8_192,
    maxBufferedBytes: 8_192,
    ...overrides,
  });
  await relay.listen();
  const { port } = relay.address();
  return { relay, url: `ws://127.0.0.1:${port}/live` };
}

async function closeRelay(relay, upstream) {
  await relay.close();
  await upstream?.close();
}

test("health is not ready when the OpenAI key is missing", async () => {
  const { relay } = await makeRelay({ apiKey: "" });
  try {
    const response = await fetch(`http://127.0.0.1:${relay.address().port}/health`);
    assert.equal(response.status, 503);
    assert.deepEqual(await response.json(), { ready: false });
  } finally { await relay.close(); }
});

test("a missing OpenAI key cannot start a billable session", async () => {
  const { relay, url } = await makeRelay({ apiKey: "" });
  try {
    const client = await openClient(url);
    client.send(START);
    assert.deepEqual(await nextJson(client), { type: "gym.status", status: "error", code: "missing_api_key" });
    await once(client, "close");
  } finally { await relay.close(); }
});

test("non-loopback serving requires an access token", () => {
  assert.throws(() => createRelayServer({ host: "0.0.0.0", apiKey: "key", accessToken: "" }), /ACCESS_TOKEN/);
});

test("rejects missing or wrong access tokens before opening upstream", async () => {
  let upstreamConnections = 0;
  const upstream = await makeUpstream({ onConnection: () => upstreamConnections++ });
  const { relay, url } = await makeRelay({ accessToken: "secret", upstreamUrl: upstream.url });
  try {
    for (const token of [undefined, "wrong"]) {
      const client = await openClient(url);
      client.send(JSON.stringify({ type: "gym.start", character: "maya", ...(token ? { token } : {}) }));
      assert.deepEqual(await nextJson(client), { type: "gym.status", status: "error", code: "unauthorized" });
      await once(client, "close");
    }
    assert.equal(upstreamConnections, 0);
  } finally { await closeRelay(relay, upstream); }
});

test("sends server-owned GPT-Live start and becomes ready only after session.started", async () => {
  let requestHeaders;
  let start;
  const upstream = await makeUpstream({ onConnection(socket, request) {
    requestHeaders = request.headers;
    socket.once("message", data => {
      start = JSON.parse(data.toString());
      socket.send(JSON.stringify({ type: "session.started", session: { id: "live_1" } }));
    });
  }});
  const { relay, url } = await makeRelay({ upstreamUrl: upstream.url });
  try {
    const client = await openClient(url);
    client.send(START);
    assert.deepEqual(await nextJson(client), { type: "gym.status", status: "connecting" });
    assert.deepEqual(await nextJson(client), { type: "session.started", session: { id: "live_1" } });
    assert.deepEqual(await nextJson(client), { type: "gym.status", status: "ready" });
    assert.equal(requestHeaders.authorization, "Bearer test-key");
    assert.equal(start.type, "session.start");
    assert.equal(start.session.model, "gpt-live-1");
    assert.deepEqual(start.session.audio, { format: { type: "audio/pcm", rate: 24000 }, output: { voice: "gleam" } });
    assert.match(start.session.instructions, /Maya/);
    assert.equal(start.session.store, false);
    client.close();
  } finally { await closeRelay(relay, upstream); }
});

test("rejects inherited object names as characters without opening upstream", async () => {
  let upstreamConnections = 0;
  const upstream = await makeUpstream({ onConnection: () => upstreamConnections++ });
  const { relay, url } = await makeRelay({ upstreamUrl: upstream.url });
  try {
    for (const character of ["constructor", "__proto__", "toString"]) {
      const client = await openClient(url);
      client.send(JSON.stringify({ type: "gym.start", character }));
      assert.equal((await nextJson(client)).code, "unknown_character");
      await once(client, "close");
    }
    assert.equal(upstreamConnections, 0);
  } finally { await closeRelay(relay, upstream); }
});

test("pre-start connections have a timeout and a separate capacity bound", async () => {
  const { relay, url } = await makeRelay({ startTimeoutMs: 200, maxPendingConnections: 1 });
  try {
    const waiting = await openClient(url);
    const excess = new WebSocket(url);
    const [, response] = await once(excess, "unexpected-response");
    assert.equal(response.statusCode, 503); response.resume();
    assert.equal((await nextJson(waiting)).code, "start_timeout");
    await once(waiting, "close");
    const replacement = await openClient(url);
    replacement.close(); await once(replacement, "close");
  } finally { await relay.close(); }
});

test("rejects WebSocket upgrades beyond the total active plus pending cap with HTTP 503", async () => {
  const upstream = await makeUpstream({ onConnection(socket) {
    socket.on("message", data => {
      if (JSON.parse(data.toString()).type === "session.start") {
        socket.send(JSON.stringify({ type: "session.started", session: { id: "live_cap" } }));
      }
    });
  }});
  const { relay, url } = await makeRelay({ upstreamUrl: upstream.url, maxSessions: 1, maxPendingConnections: 1 });
  try {
    const active = await openClient(url); active.send(START);
    await nextJson(active); await nextJson(active); await nextJson(active);
    const pending = await openClient(url);
    const excess = new WebSocket(url);
    const [, response] = await once(excess, "unexpected-response");
    assert.equal(response.statusCode, 503);
    response.resume();
    pending.terminate(); active.terminate();
  } finally { await closeRelay(relay, upstream); }
});

test("rejects audio before ready and rejects client-controlled event types", async () => {
  const upstream = await makeUpstream({ onConnection() {} });
  const { relay, url } = await makeRelay({ upstreamUrl: upstream.url });
  try {
    const client = await openClient(url);
    client.send(START);
    await nextJson(client);
    client.send(JSON.stringify({ type: "session.input_audio.append", audio: "AAAAAA==" }));
    assert.deepEqual(await nextJson(client), { type: "gym.status", status: "error", code: "not_ready" });
    client.send(JSON.stringify({ type: "session.update", session: { model: "other" } }));
    assert.deepEqual(await nextJson(client), { type: "gym.status", status: "error", code: "unsupported_event" });
    client.close();
  } finally { await closeRelay(relay, upstream); }
});

test("forwards only allowed commands and native events after ready", async () => {
  const received = [];
  const upstream = await makeUpstream({ onConnection(socket) {
    socket.on("message", data => {
      const event = JSON.parse(data.toString());
      received.push(event);
      if (event.type === "session.start") socket.send(JSON.stringify({ type: "session.started", session: { id: "live_2" } }));
      if (event.type === "session.input_audio.append") {
        socket.send(JSON.stringify({ type: "session.input_transcript.delta", delta: "hello" }));
        socket.send(JSON.stringify({ type: "session.output_audio.delta", delta: "AQI=" }));
      }
    });
  }});
  const { relay, url } = await makeRelay({ upstreamUrl: upstream.url });
  try {
    const client = await openClient(url);
    client.send(START);
    await nextJson(client); await nextJson(client); await nextJson(client);
    client.send(JSON.stringify({ type: "session.input_audio.append", audio: "AAAAAA==" }));
    assert.equal((await nextJson(client)).type, "session.input_transcript.delta");
    assert.deepEqual(await nextJson(client), { type: "session.output_audio.delta", delta: "AQI=" });
    assert.deepEqual(received.at(-1), { type: "session.input_audio.append", audio: "AAAAAA==" });
    client.close();
  } finally { await closeRelay(relay, upstream); }
});

test("graceful close forwards final usage before closing the client", async () => {
  let closeCommands = 0;
  const upstream = await makeUpstream({ onConnection(socket) {
    socket.on("message", data => {
      const event = JSON.parse(data.toString());
      if (event.type === "session.start") socket.send(JSON.stringify({ type: "session.started", session: { id: "live_3" } }));
      if (event.type === "session.close") {
        closeCommands++;
        if (closeCommands === 1) socket.send(JSON.stringify({ type: "session.closed", reason: "close_requested", usage: { seconds: 3 } }));
      }
    });
  }});
  const { relay, url } = await makeRelay({ upstreamUrl: upstream.url });
  try {
    const client = await openClient(url);
    client.send(START);
    await nextJson(client); await nextJson(client); await nextJson(client);
    client.send(JSON.stringify({ type: "session.close" }));
    assert.deepEqual(await nextJson(client), { type: "session.closed", reason: "close_requested", usage: { seconds: 3 } });
    assert.deepEqual(await nextJson(client), { type: "gym.status", status: "closed", finalUsage: { seconds: 3 }, finalUsageConfirmed: true });
    await once(client, "close");
    assert.equal(closeCommands, 1);
  } finally { await closeRelay(relay, upstream); }
});

test("audio and mute commands already in flight are ignored while closing", async () => {
  const received = [];
  let upstreamSocket;
  const upstream = await makeUpstream({ onConnection(socket) {
    upstreamSocket = socket;
    socket.on("message", data => {
      const event = JSON.parse(data.toString()); received.push(event.type);
      if (event.type === "session.start") socket.send(JSON.stringify({ type: "session.started", session: { id: "live_closing" } }));
    });
  }});
  const { relay, url } = await makeRelay({ upstreamUrl: upstream.url });
  try {
    const client = await openClient(url); client.send(START);
    await nextJson(client); await nextJson(client); await nextJson(client);
    client.send(JSON.stringify({ type: "session.close" }));
    client.send(JSON.stringify({ type: "session.input_audio.append", audio: "AAAAAA==" }));
    client.send(JSON.stringify({ type: "session.input_audio.mute" }));
    await new Promise(resolve => setTimeout(resolve, 20));
    assert.deepEqual(received, ["session.start", "session.close"]);
    upstreamSocket.send(JSON.stringify({ type: "session.closed", reason: "close_requested", usage: { seconds: 1 } }));
    assert.equal((await nextJson(client)).type, "session.closed");
    assert.equal((await nextJson(client)).status, "closed");
    await once(client, "close");
  } finally { await closeRelay(relay, upstream); }
});

test("startup timeout releases capacity and reports unconfirmed usage", async () => {
  const upstream = await makeUpstream({ onConnection() {} });
  const { relay, url } = await makeRelay({ upstreamUrl: upstream.url, startupTimeoutMs: 30, maxSessions: 1 });
  try {
    const first = await openClient(url); first.send(START); await nextJson(first);
    const status = await nextJson(first);
    assert.deepEqual(status, { type: "gym.status", status: "error", code: "startup_timeout", finalUsageConfirmed: false });
    await once(first, "close");
    const second = await openClient(url); second.send(START);
    assert.deepEqual(await nextJson(second), { type: "gym.status", status: "connecting" });
    second.close();
  } finally { await closeRelay(relay, upstream); }
});

test("invalid, oversized, and excess-session messages are bounded", async () => {
  const upstream = await makeUpstream({ onConnection() {} });
  const { relay, url } = await makeRelay({ upstreamUrl: upstream.url, maxSessions: 1, maxMessageBytes: 80 });
  try {
    const active = await openClient(url); active.send(START); await nextJson(active);
    const excess = await openClient(url); excess.send(START);
    assert.equal((await nextJson(excess)).code, "capacity"); await once(excess, "close");
    const malformed = await openClient(url); malformed.send("{");
    assert.equal((await nextJson(malformed)).code, "invalid_message"); await once(malformed, "close");
    const oversized = await openClient(url); oversized.send(JSON.stringify({ type: "gym.start", character: "maya", padding: "x".repeat(100) }));
    assert.equal((await nextJson(oversized)).code, "message_too_large"); await once(oversized, "close");
    active.close();
  } finally { await closeRelay(relay, upstream); }
});

test("client disconnect requests upstream close and waits only to the close timeout", async () => {
  let closeReceived = false;
  let upstreamClosed = false;
  const upstream = await makeUpstream({ onConnection(socket) {
    socket.on("message", data => {
      const event = JSON.parse(data.toString());
      if (event.type === "session.start") socket.send(JSON.stringify({ type: "session.started", session: { id: "live_4" } }));
      if (event.type === "session.close") closeReceived = true;
    });
    socket.on("close", () => upstreamClosed = true);
  }});
  const { relay, url } = await makeRelay({ upstreamUrl: upstream.url, closeTimeoutMs: 30 });
  try {
    const client = await openClient(url); client.send(START);
    await nextJson(client); await nextJson(client); await nextJson(client);
    client.close(); await once(client, "close");
    await new Promise(resolve => setTimeout(resolve, 60));
    assert.equal(closeReceived, true);
    assert.equal(upstreamClosed, true);
  } finally { await closeRelay(relay, upstream); }
});

for (const clientClosesAfterCommand of [false, true]) {
  test(`client disconnect ${clientClosesAfterCommand ? "after its close command" : "from ready state"} keeps upstream alive for final usage`, async () => {
    let closeReceived = false;
    let upstreamClosed = false;
    let upstreamCloseCode;
    let resolveUpstreamClose;
    const upstreamClose = new Promise(resolve => resolveUpstreamClose = resolve);
    const upstream = await makeUpstream({ onConnection(socket) {
      socket.on("message", data => {
        const event = JSON.parse(data.toString());
        if (event.type === "session.start") socket.send(JSON.stringify({ type: "session.started", session: { id: "live_finalize" } }));
        if (event.type === "session.close" && !closeReceived) {
          closeReceived = true;
          setTimeout(() => {
            if (socket.readyState === WebSocket.OPEN) {
              socket.send(JSON.stringify({ type: "session.closed", reason: "close_requested", usage: { seconds: 7 } }));
            }
          }, 30);
        }
      });
      socket.on("close", code => { upstreamClosed = true; upstreamCloseCode = code; resolveUpstreamClose(); });
    }});
    const { relay, url } = await makeRelay({ upstreamUrl: upstream.url, closeTimeoutMs: 150 });
    try {
      const client = await openClient(url); client.send(START);
      await nextJson(client); await nextJson(client); await nextJson(client);
      if (clientClosesAfterCommand) client.send(JSON.stringify({ type: "session.close" }));
      client.close(); await once(client, "close");
      await new Promise(resolve => setTimeout(resolve, 10));
      assert.equal(closeReceived, true);
      assert.equal(upstreamClosed, false);
      await Promise.race([upstreamClose, new Promise(resolve => setTimeout(resolve, 500))]);
      assert.equal(upstreamClosed, true);
      assert.equal(upstreamCloseCode, 1000);
    } finally { await closeRelay(relay, upstream); }
  });
}

test("maximum duration requests a graceful upstream close", async () => {
  let closeReceived = false;
  const upstream = await makeUpstream({ onConnection(socket) {
    socket.on("message", data => {
      const event = JSON.parse(data.toString());
      if (event.type === "session.start") socket.send(JSON.stringify({ type: "session.started", session: { id: "live_duration" } }));
      if (event.type === "session.close") {
        closeReceived = true;
        socket.send(JSON.stringify({ type: "session.closed", reason: "close_requested", usage: { seconds: 0 } }));
      }
    });
  }});
  const { relay, url } = await makeRelay({ upstreamUrl: upstream.url, maxDurationMs: 20 });
  try {
    const client = await openClient(url); client.send(START);
    await nextJson(client); await nextJson(client); await nextJson(client);
    assert.equal((await nextJson(client)).type, "session.closed");
    await nextJson(client); await once(client, "close");
    assert.equal(closeReceived, true);
  } finally { await closeRelay(relay, upstream); }
});

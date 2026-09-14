import assert from "node:assert/strict";
import { once } from "node:events";
import nodeTest from "node:test";
const test = (name, run) => nodeTest(name, { timeout: 30_000 }, run);
import WebSocket, { WebSocketServer } from "ws";
import { createRelayServer } from "../src/server.mjs";
import { createGameSessions } from "../src/game-sessions.mjs";
import { createDemoDefinition, createDemoScenario } from "../src/demo-scenario.mjs";
import { createEncounterWorld } from "../src/encounter-world.mjs";

const START = JSON.stringify({ type: "gym.start", character: "maya" });

test("paused game rejects new voice before proximity checks or upstream dial; unpause permits attach", async () => {
  let dials = 0, proximityChecks = 0;
  const games = createGameSessions({ definitionFactory: createDemoDefinition, encounterFactory: createDemoScenario });
  const upstream = await makeUpstream({ onConnection(socket) {
    dials++;
    socket.on("message", raw => {
      const event = JSON.parse(raw);
      if (event.type === "session.start") socket.send(JSON.stringify({ type: "session.started", session: { id: "pause_regression" } }));
      if (event.type === "session.close") socket.send(JSON.stringify({ type: "session.closed", usage: {} }));
    });
  } });
  const { relay, url } = await makeRelay({ upstreamUrl: upstream.url, gameSessions: games,
    worldFactory: options => ({ ...createEncounterWorld(options), canConverse() { proximityChecks++; return { ok: true }; } }) });
  try {
    const game = await openClient(url.replace('/live', '/game'));
    game.send(JSON.stringify({ type: "game.create" }));
    const ready = await untilJson(game, "game.ready");
    assert.equal((await untilJson(game, "game.pause")).paused, false);
    game.send(JSON.stringify({ type: "game.pause", paused: true }));
    assert.equal((await untilJson(game, "game.pause")).paused, true);
    const rejected = await openClient(url);
    rejected.send(JSON.stringify({ type: "gym.start", character: "maya", ...ready.credentials }));
    let rejection;
    do { rejection = await nextJson(rejected); } while (rejection.type !== "gym.status" || rejection.status !== "error");
    assert.equal(rejection.code, "game_paused");
    await waitClosed(rejected);
    assert.equal(dials, 0, "Paused request must not dial the provider");
    assert.equal(proximityChecks, 0, "Pause rejection precedes proximity evaluation");
    game.send(JSON.stringify({ type: "game.pause", paused: false }));
    assert.equal((await untilJson(game, "game.pause")).paused, false);
    const voice = await openClient(url);
    voice.send(JSON.stringify({ type: "gym.start", character: "maya", ...ready.credentials }));
    let status;
    do { status = await nextJson(voice); } while (status.type !== "gym.status" || status.status !== "ready");
    assert.equal(dials, 1);
    assert.ok(proximityChecks > 0);
    voice.send(JSON.stringify({ type: "session.close" }));
    assert.ok((await untilJson(voice, "session.closed")).usage);
    await waitClosed(voice);
  } finally { await closeRelay(relay, upstream); }
});

test("world pause is authoritative across peers, reconnects and independent new games", async () => {
  const games = createGameSessions({ definitionFactory: createDemoDefinition, encounterFactory: createDemoScenario });
  const { relay, url } = await makeRelay({ apiKey: "", gameSessions: games, worldFactory: createEncounterWorld });
  const address = url.replace('/live', '/game');
  const send = (client, event) => client.send(JSON.stringify(event));
  const join = async (event, expectedPaused) => {
    const client = await openClient(address);
    send(client, event);
    const ready = await nextJson(client);
    assert.equal(ready.type, "game.ready");
    assert.equal(ready.ok, true);
    assert.deepEqual(await nextJson(client), { type: "game.pause", paused: expectedPaused });
    assert.equal((await nextJson(client)).type, "game.world", "pause precedes initial world snapshot");
    return { client, ready };
  };
  try {
    const first = await join({ type: "game.create" }, false);
    const second = await join({ type: "game.resume", ...first.ready.credentials }, false);
    send(first.client, { type: "game.pause", paused: true });
    for (const client of [first.client, second.client])
      assert.deepEqual(await untilJson(client, "game.pause"), { type: "game.pause", paused: true });
    // A reconnect inherits the existing record's pause even after its old socket closes.
    const disconnected = waitClosed(second.client);
    second.client.close(); await disconnected;
    const reconnected = await join({ type: "game.resume", ...first.ready.credentials }, true);
    send(reconnected.client, { type: "game.walk", loopId: first.ready.snapshot.loopId, sequence: 1, destination: { x: 0, z: -6 } });
    assert.equal((await untilJson(reconnected.client, "game.move_result")).reason, "paused");
    const independent = await join({ type: "game.create" }, false);
    assert.notEqual(independent.ready.credentials.gameId, first.ready.credentials.gameId);
    send(reconnected.client, { type: "game.pause", paused: false });
    for (const client of [first.client, reconnected.client])
      assert.deepEqual(await untilJson(client, "game.pause"), { type: "game.pause", paused: false });
    send(first.client, { type: "game.walk", loopId: first.ready.snapshot.loopId, sequence: 1, destination: { x: 0, z: -6 } });
    assert.equal((await untilJson(first.client, "game.move_result")).accepted, true);
    // A later resume also sees the unpaused state, not a sticky reconnect default.
    await join({ type: "game.resume", ...first.ready.credentials }, false);
  } finally { await bounded(relay.close(), "relay close"); }
});

test("paused nonterminal restart retains discoveries and resets the night without resuming it", async () => {
  const games = createGameSessions({ definitionFactory: createDemoDefinition, encounterFactory: createDemoScenario });
  const { relay, url } = await makeRelay({ apiKey: "", gameSessions: games, worldFactory: createEncounterWorld });
  const send = (client, event) => client.send(JSON.stringify(event));
  const fence = snapshot => ({ loopId: snapshot.loopId, revision: snapshot.revision });
  try {
    const client = await openClient(url.replace('/live', '/game'));
    send(client, { type: "game.create" });
    const ready = await untilJson(client, "game.ready");
    const initial = (await untilJson(client, "game.world")).world;
    send(client, { type: "game.walk", loopId: ready.snapshot.loopId, sequence: 1, destination: { x: 1, z: -5 } });
    assert.equal((await untilJson(client, "game.move_result")).accepted, true);
    let moved;
    do { moved = (await untilJson(client, "game.world")).world; } while (moved.elapsedSeconds === 0);
    assert.notDeepEqual(moved.actors.player.position, initial.actors.player.position);
    send(client, { type: "game.pause", paused: true });
    assert.equal((await untilJson(client, "game.pause")).paused, true);

    // Trusted setup creates a discovery and accepted NPC behavior; neither is
    // a player RPC. The operation under test goes through the public socket.
    const credentials = ready.credentials;
    const attached = games.attach(credentials, 'maya');
    const lease = attached.lease.leaseId;
    const learned = games.trustedWorld(credentials, 'observeVisibility', {
      ...fence(attached.snapshot), observerId: 'maya', inRecognitionArea: true, visibleActorIds: ['theo', 'affair_partner'],
    });
    assert.equal(learned.outcome.accepted, true);
    const waiting = games.submitAction(credentials, lease, {
      ...fence(learned.snapshot), requestId: 'before-restart', actorId: 'maya', type: 'wait',
    });
    assert.equal(waiting.outcome.accepted, true);
    assert.equal(waiting.snapshot.recording, true);
    assert.equal(waiting.snapshot.actors.maya.action.type, 'wait');
    assert.ok(games.npcContext(credentials, lease).context.knownFacts.some(f => f.factId === 'affair_seen'));

    send(client, { type: "game.reset", ...fence(learned.snapshot) });
    assert.equal((await untilJson(client, "game.reset_result")).outcome.reason, 'stale_revision');
    assert.equal(games.publicState(credentials).snapshot.loopId, ready.snapshot.loopId);
    send(client, { type: "game.reset", ...fence(waiting.snapshot) });
    const reset = await untilJson(client, "game.reset_result");
    assert.equal(reset.outcome.accepted, true);
    assert.notEqual(reset.snapshot.loopId, ready.snapshot.loopId);
    assert.deepEqual(reset.snapshot.playerDiscoveries, waiting.snapshot.playerDiscoveries);
    assert.equal(reset.snapshot.recording, false);
    assert.equal(reset.snapshot.phase, 'exploring');
    assert.deepEqual(reset.snapshot.actors, ready.snapshot.actors);
    assert.equal(reset.snapshot.elapsedSeconds, 0);
    assert.equal(games.npcContext(credentials, lease).ok, false, 'Previous voice lease is invalidated');
    const fresh = games.attach(credentials, 'maya');
    assert.deepEqual(games.npcContext(credentials, fresh.lease.leaseId).context.knownFacts, []);
    games.detach(credentials, fresh.lease.leaseId);
    send(client, { type: "game.reset", ...fence(waiting.snapshot) });
    assert.equal((await untilJson(client, "game.reset_result")).outcome.reason, 'stale_loop');
    let restored;
    do { restored = (await untilJson(client, "game.world")).world; } while (restored.loopId !== reset.snapshot.loopId);
    for (const [id, actor] of Object.entries(initial.actors))
      assert.deepEqual(restored.actors[id].position, actor.position, id + ' spawn restored');
    for (let i = 0; i < 3; i++) {
      const held = (await untilJson(client, "game.world")).world;
      assert.equal(held.elapsedSeconds, 0, 'Restart must preserve the authoritative pause');
      assert.deepEqual(held.actors, restored.actors);
    }
    send(client, { type: "game.pause", paused: false });
    assert.equal((await untilJson(client, "game.pause")).paused, false);
    let resumed;
    do { resumed = (await untilJson(client, "game.world")).world; } while (resumed.elapsedSeconds === 0);
    assert.equal(resumed.loopId, reset.snapshot.loopId);
  } finally { await bounded(relay.close(), "relay close"); }
});

test("world channel advances server positions and rejects forged destinations and premature reset", async () => {
  const games = createGameSessions({ definitionFactory: createDemoDefinition, encounterFactory: createDemoScenario });
  const { relay, url } = await makeRelay({ apiKey: "", gameSessions: games, worldFactory: createEncounterWorld });
  try {
    const client = await openClient(url.replace('/live', '/game'));
    const until = type => untilJson(client, type);
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
  } finally { await bounded(relay.close(), "relay close"); }
});

test("world approach protocol exposes eligibility, correlates rejection and shares walk/stop sequencing", async () => {
  const games = createGameSessions({ definitionFactory: createDemoDefinition, encounterFactory: createDemoScenario });
  const { relay, url } = await makeRelay({ apiKey: "", gameSessions: games, worldFactory: createEncounterWorld });
  try {
    const client = await openClient(url.replace('/live', '/game'));
    client.send(JSON.stringify({ type: "game.create" }));
    const ready = await untilJson(client, "game.ready"), initial = await untilJson(client, "game.world");
    assert.equal(initial.world.conversations.ren.eligible, false);
    const send = event => client.send(JSON.stringify({ loopId: ready.snapshot.loopId, ...event }));
    send({ type: "game.approach", sequence: 1, npcId: "affair_partner" });
    const rejected = await untilJson(client, "game.approach_result");
    assert.equal(rejected.reason, "unknown_npc"); assert.equal(rejected.sequence, 1);
    assert.equal(rejected.loopId, ready.snapshot.loopId); assert.equal(rejected.npcId, "affair_partner");
    send({ type: "game.approach", sequence: 1, npcId: "ren" });
    const approach = await untilJson(client, "game.approach_result");
    assert.equal(approach.accepted, true); assert.equal(approach.npcId, "ren");
    assert.ok(Number.isSafeInteger(approach.frame)); assert.ok(Number.isFinite(approach.destination.x));
    send({ type: "game.walk", sequence: 2, destination: { x: 0, z: -6 } });
    assert.equal((await untilJson(client, "game.move_result")).accepted, true);
    let arrived;
    for (let i = 0; i < 30; i++) {
      const update = await untilJson(client, "game.world");
      if (update.world.sequence === 2 && Math.hypot(update.world.actors.player.position.x, update.world.actors.player.position.z + 6) < 0.02) { arrived = update.world; break; }
    }
    assert.ok(arrived, "newer walk must replace the Ren approach destination");
    assert.equal(arrived.conversations.ren.eligible, false);
    assert.deepEqual((await untilJson(client, "game.world")).world.actors.player.position, { x: 0, z: -6 });
    send({ type: "game.approach", sequence: 3, npcId: "ren" });
    assert.equal((await untilJson(client, "game.approach_result")).accepted, true);
    send({ type: "game.stop", sequence: 4 });
    assert.equal((await untilJson(client, "game.move_result")).accepted, true);
    send({ type: "game.walk", sequence: 4, destination: { x: 0, z: -6 } });
    assert.equal((await untilJson(client, "game.move_result")).reason, "stale_sequence");
    send({ type: "game.approach", loopId: "obsolete", sequence: 5, npcId: "ren" });
    const stale = await untilJson(client, "game.approach_result");
    assert.equal(stale.reason, "stale_loop"); assert.equal(stale.loopId, "obsolete"); assert.equal(stale.sequence, 5);
    send({ type: "game.pause", paused: true }); await untilJson(client, "game.pause");
    send({ type: "game.approach", sequence: 5, npcId: "ren" });
    assert.equal((await untilJson(client, "game.approach_result")).reason, "paused");
    send({ type: "game.walk", sequence: 5, destination: { x: 0, z: -6 } });
    assert.equal((await untilJson(client, "game.move_result")).reason, "paused");
    send({ type: "game.stop", sequence: 5 });
    assert.equal((await untilJson(client, "game.move_result")).accepted, true, "paused/invalid commands must not consume sequence and stop must cancel during pause");
  } finally { await bounded(relay.close(), "relay close"); }
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
  } finally { await bounded(relay.close(), "relay close"); }
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
      const closed = waitClosed(client);
      client.send(JSON.stringify({ type: "session.close" }));
      await closed;
    }
    assert.match(starts[0].session.instructions, /Stay with my friend/);
    assert.doesNotMatch(starts[0].session.instructions, /PRIVATE_THEO/);
    assert.equal(starts[1].session.input[0].content[0].text, "Please wait.");
    assert.equal(games.history(created.credentials).history.fragments.length, 2);
  } finally { await closeRelay(relay, upstream); }
});

const IO_TIMEOUT_MS = 10_000;

function bounded(promise, label, timeoutMs = IO_TIMEOUT_MS) {
  let timer;
  return Promise.race([promise, new Promise((_, reject) => {
    timer = setTimeout(() => reject(new Error(`Test harness timed out: ${label}`)), timeoutMs);
  })]).finally(() => clearTimeout(timer));
}

async function openClient(url) {
  const socket = new WebSocket(url);
  const messages = [];
  const waiters = [];
  let terminal;
  let resolveClosed;
  const closed = new Promise(resolve => { resolveClosed = resolve; });
  const fail = error => {
    terminal ??= error;
    for (const waiter of waiters.splice(0)) { clearTimeout(waiter.timer); waiter.reject(terminal); }
  };
  socket.on("error", error => fail(new Error(`Test WebSocket error: ${error.message}`)));
  socket.on("close", (code, reason) => {
    fail(new Error(`Test WebSocket closed before expected message: ${code} ${reason.toString()}`));
    resolveClosed([code, reason]);
  });
  socket.on("message", data => {
    let value;
    try { value = JSON.parse(data.toString()); }
    catch (error) { fail(new Error(`Invalid JSON from test socket: ${error.message}`)); socket.terminate(); return; }
    const waiter = waiters.shift();
    if (waiter) { clearTimeout(waiter.timer); waiter.resolve(value); }
    else if (messages.length < 1024) messages.push(value);
    else { fail(new Error("Test mailbox exceeded 1024 messages")); socket.terminate(); }
  });
  socket.nextJson = (timeoutMs = IO_TIMEOUT_MS) => {
    // Final error/status messages remain readable even if close already arrived.
    if (messages.length) return Promise.resolve(messages.shift());
    if (terminal) return Promise.reject(terminal);
    return new Promise((resolve, reject) => {
      const waiter = { resolve, reject, timer: null };
      waiter.timer = setTimeout(() => {
        const index = waiters.indexOf(waiter);
        if (index >= 0) waiters.splice(index, 1);
        reject(new Error("Test harness timed out waiting for WebSocket JSON"));
      }, timeoutMs);
      waiters.push(waiter);
    });
  };
  socket.waitClosed = () => bounded(closed, "WebSocket close");
  try { await bounded(once(socket, "open"), "WebSocket open"); }
  catch (error) { socket.terminate(); throw error; }
  return socket;
}

function nextJson(socket, timeoutMs) { return socket.nextJson(timeoutMs); }
function waitClosed(socket) { return socket.waitClosed(); }

async function untilJson(socket, type) {
  const deadline = Date.now() + IO_TIMEOUT_MS;
  while (Date.now() < deadline) {
    const event = await nextJson(socket, Math.max(1, deadline - Date.now()));
    if (event.type === type) return event;
  }
  throw new Error(`Test harness timed out waiting for ${type}`);
}

async function makeUpstream({ onConnection } = {}) {
  const server = new WebSocketServer({ port: 0, host: "127.0.0.1" });
  await bounded(once(server, "listening"), "upstream listening");
  server.on("connection", (socket, request) => onConnection?.(socket, request));
  const { port } = server.address();
  return { url: `ws://127.0.0.1:${port}`, close: () => { for (const client of server.clients) client.terminate(); return bounded(new Promise(resolve => server.close(resolve)), "upstream close"); } };
}

async function makeRelay(overrides = {}) {
  const relay = createRelayServer({
    host: "127.0.0.1",
    port: 0,
    apiKey: "test-key",
    startupTimeoutMs: 5_000,
    startTimeoutMs: 5_000,
    closeTimeoutMs: 5_000,
    maxDurationMs: 30_000,
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
  try { await bounded(relay.close(), "relay close"); }
  finally { await upstream?.close(); }
}

test("health is not ready when the OpenAI key is missing", async () => {
  const { relay } = await makeRelay({ apiKey: "" });
  try {
    const response = await fetch(`http://127.0.0.1:${relay.address().port}/health`, { signal: AbortSignal.timeout(IO_TIMEOUT_MS) });
    assert.equal(response.status, 503);
    assert.deepEqual(await response.json(), { ready: false });
  } finally { await bounded(relay.close(), "relay close"); }
});

test("a missing OpenAI key cannot start a billable session", async () => {
  const { relay, url } = await makeRelay({ apiKey: "" });
  try {
    const client = await openClient(url);
    client.send(START);
    assert.deepEqual(await nextJson(client), { type: "gym.status", status: "error", code: "missing_api_key" });
    await waitClosed(client);
  } finally { await bounded(relay.close(), "relay close"); }
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
      await waitClosed(client);
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
      await waitClosed(client);
    }
    assert.equal(upstreamConnections, 0);
  } finally { await closeRelay(relay, upstream); }
});

test("pre-start connections have a timeout and a separate capacity bound", async () => {
  const { relay, url } = await makeRelay({ startTimeoutMs: 200, maxPendingConnections: 1 });
  try {
    const waiting = await openClient(url);
    const excess = new WebSocket(url);
    const [, response] = await bounded(once(excess, "unexpected-response"), "rejected upgrade");
    assert.equal(response.statusCode, 503); response.resume();
    assert.equal((await nextJson(waiting)).code, "start_timeout");
    await waitClosed(waiting);
    const replacement = await openClient(url);
    replacement.close(); await waitClosed(replacement);
  } finally { await bounded(relay.close(), "relay close"); }
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
    const [, response] = await bounded(once(excess, "unexpected-response"), "rejected upgrade");
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
    await waitClosed(client);
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
    await waitClosed(client);
  } finally { await closeRelay(relay, upstream); }
});

test("startup timeout releases capacity and reports unconfirmed usage", async () => {
  const upstream = await makeUpstream({ onConnection() {} });
  const { relay, url } = await makeRelay({ upstreamUrl: upstream.url, startupTimeoutMs: 30, maxSessions: 1 });
  try {
    const first = await openClient(url); first.send(START); await nextJson(first);
    const status = await nextJson(first);
    assert.deepEqual(status, { type: "gym.status", status: "error", code: "startup_timeout", finalUsageConfirmed: false });
    await waitClosed(first);
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
    assert.equal((await nextJson(excess)).code, "capacity"); await waitClosed(excess);
    const malformed = await openClient(url); malformed.send("{");
    assert.equal((await nextJson(malformed)).code, "invalid_message"); await waitClosed(malformed);
    const oversized = await openClient(url); oversized.send(JSON.stringify({ type: "gym.start", character: "maya", padding: "x".repeat(100) }));
    assert.equal((await nextJson(oversized)).code, "message_too_large"); await waitClosed(oversized);
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
    client.close(); await waitClosed(client);
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
      client.close(); await waitClosed(client);
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
    await nextJson(client); await waitClosed(client);
    assert.equal(closeReceived, true);
  } finally { await closeRelay(relay, upstream); }
});

test("test mailbox bounds silence, drains final messages, and remembers an already closed socket", async () => {
  let peer;
  const upstream = await makeUpstream({ onConnection(socket) { peer = socket; } });
  let client;
  try {
    client = await openClient(upstream.url);
    await assert.rejects(nextJson(client, 20), /timed out waiting for WebSocket JSON/);
    // The expired waiter must be removed so a later event is not swallowed.
    peer.send(JSON.stringify({ type: "final" }));
    peer.close(1000, "fixture_done");
    await waitClosed(client);
    assert.deepEqual(await nextJson(client), { type: "final" });
    await assert.rejects(nextJson(client), /closed before expected message/);
    assert.equal((await waitClosed(client))[0], 1000);
  } finally { client?.terminate(); await upstream.close(); }
});

test("test mailbox rejects pending readers when the peer closes", async () => {
  let peer;
  const upstream = await makeUpstream({ onConnection(socket) { peer = socket; } });
  let client;
  try {
    client = await openClient(upstream.url);
    const rejected = assert.rejects(nextJson(client), /closed before expected message/);
    peer.close(1000, "no_message");
    await rejected;
    await waitClosed(client);
  } finally { client?.terminate(); await upstream.close(); }
});

import http from "node:http";
import { timingSafeEqual } from "node:crypto";
import WebSocket, { WebSocketServer } from "ws";
import { isLoopbackHost, validateConfig } from "./config.mjs";
import { buildSessionStart } from "./prompts.mjs";
import { createGameplayDelegation } from "./gameplay-delegation.mjs";
import { createIntentInterpreter } from "./responses-client.mjs";

const OPEN = WebSocket.OPEN;
const ALLOWED_AFTER_READY = new Set([
  "session.input_audio.append",
  "session.input_audio.mute",
  "session.input_audio.unmute",
  "session.close",
]);

function sendJson(socket, value, maxBufferedBytes) {
  if (socket.readyState !== OPEN) return false;
  const payload = JSON.stringify(value);
  if (socket.bufferedAmount + Buffer.byteLength(payload) > maxBufferedBytes) return false;
  socket.send(payload);
  return true;
}

function status(socket, config, statusValue, details = {}) {
  sendJson(socket, { type: "gym.status", status: statusValue, ...details }, config.maxBufferedBytes);
}

function closeSocket(socket, code = 1000, reason = "") {
  if (socket.readyState === OPEN || socket.readyState === WebSocket.CONNECTING) socket.close(code, reason);
}

function parseMessage(data, isBinary, maxMessageBytes) {
  if (isBinary) return { error: "invalid_message" };
  if (data.length > maxMessageBytes) return { error: "message_too_large" };
  try {
    const value = JSON.parse(data.toString("utf8"));
    if (!value || typeof value !== "object" || Array.isArray(value) || typeof value.type !== "string") {
      return { error: "invalid_message" };
    }
    return { value };
  } catch {
    return { error: "invalid_message" };
  }
}

function normalizeCommand(event) {
  if (event.type === "session.input_audio.append") {
    if (typeof event.audio !== "string" || !/^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/.test(event.audio)) return null;
    const audio = Buffer.from(event.audio, "base64");
    if (audio.length === 0 || audio.length % 2 !== 0) return null;
    return { type: event.type, audio: event.audio };
  }
  return { type: event.type };
}

function tokenMatches(actual, expected) {
  if (!expected || typeof actual !== "string") return !expected;
  const actualBytes = Buffer.from(actual);
  const expectedBytes = Buffer.from(expected);
  return actualBytes.length === expectedBytes.length && timingSafeEqual(actualBytes, expectedBytes);
}

export function createRelayServer(options = {}) {
  const config = validateConfig({
    host: "127.0.0.1", port: 8080, apiKey: "", accessToken: "",
    upstreamUrl: "wss://api.openai.com/v1/live/sessions",
    startTimeoutMs: 10_000,
    startupTimeoutMs: 15_000, closeTimeoutMs: 15_000,
    maxDurationMs: 10 * 60_000, maxSessions: 8,
    maxPendingConnections: 32,
    maxMessageBytes: 256 * 1024, maxBufferedBytes: 1024 * 1024,
    ...options,
  });
  const sessions = new Set();
  const pending = new Set();
  const worlds = new Map();
  const worldTimer = config.worldFactory ? setInterval(() => {
    config.gameSessions.cleanup();
    for (const [id, record] of worlds) {
      if (!record.clients.size) {
        if (!config.gameSessions.isAlive(record.credentials)) worlds.delete(id);
        continue;
      }
      record.lastSeen = Date.now();
      if (!config.gameSessions.authorize(record.credentials).ok) { worlds.delete(id); continue; }
      const voiceActive = [...sessions].some(s => s.gameCredentials?.gameId === id && s.phase !== "closed");
      try {
        const result = record.world.tick(0.1, { paused: record.paused, voiceActive });
        if (result.ok) for (const client of record.clients) sendJson(client, { type: "game.world", world: record.world.snapshot() }, config.maxBufferedBytes);
      } catch {
        record.paused = true;
        for (const client of record.clients) sendJson(client, { type: "game.error", code: "world_update_failed" }, config.maxBufferedBytes);
      }
    }
  }, 100) : null;
  worldTimer?.unref();
  let listening = false;

  const httpServer = http.createServer((request, response) => {
    if (request.method === "GET" && request.url === "/health") {
      const ready = Boolean(config.apiKey);
      response.writeHead(ready ? 200 : 503, { "content-type": "application/json", "cache-control": "no-store" });
      response.end(JSON.stringify({ ready }));
      return;
    }
    response.writeHead(404).end();
  });
  const wsServer = new WebSocketServer({ noServer: true, maxPayload: config.maxMessageBytes * 2 });

  httpServer.on("upgrade", (request, socket, head) => {
    let pathname;
    try { pathname = new URL(request.url, "http://relay.invalid").pathname; } catch { socket.destroy(); return; }
    if (pathname !== "/live" && !(pathname === "/game" && config.gameSessions)) { socket.write("HTTP/1.1 404 Not Found\r\nConnection: close\r\n\r\n"); socket.destroy(); return; }
    if (pending.size >= config.maxPendingConnections || wsServer.clients.size >= config.maxSessions + config.maxPendingConnections) {
      socket.write("HTTP/1.1 503 Service Unavailable\r\nConnection: close\r\nContent-Length: 0\r\n\r\n");
      socket.destroy();
      return;
    }
    wsServer.handleUpgrade(request, socket, head, client => wsServer.emit("connection", client, request));
  });

  wsServer.on("connection", (client, request) => {
    if (new URL(request.url, "http://relay.invalid").pathname === "/game") {
      let credentials;
      let worldRecord;
      let unsubscribe = () => {};
      const timer = setTimeout(() => closeSocket(client, 1008, "start_timeout"), config.startTimeoutMs);
      timer.unref?.();
      const respond = value => {
        if (!sendJson(client, value, config.maxBufferedBytes)) closeSocket(client, 1011, "client_backpressure");
      };
      client.on("error", () => closeSocket(client, 1011, "client_error"));
      client.on("close", () => { clearTimeout(timer); unsubscribe(); worldRecord?.clients.delete(client); });
      client.on("message", (raw, binary) => {
        const parsed = parseMessage(raw, binary, config.maxMessageBytes);
        if (parsed.error) { closeSocket(client, 1008, parsed.error); return; }
        const event = parsed.value;
        if (!credentials) {
          if (!tokenMatches(event.token, config.accessToken)) { closeSocket(client, 1008, "unauthorized"); return; }
          if (event.type !== "game.create" && event.type !== "game.resume") {
            respond({ type: "game.error", code: "invalid_start" }); return;
          }
          const supplied = { gameId: event.gameId, resumeToken: event.resumeToken };
          const result = event.type === "game.create" ? config.gameSessions.create() : config.gameSessions.resume(supplied);
          if (!result.ok) { respond({ type: "game.error", code: result.reason }); return; }
          credentials = result.credentials ?? supplied;
          if (config.worldFactory) {
            worldRecord = worlds.get(credentials.gameId);
            if (!worldRecord) {
              worldRecord = { credentials, world: config.worldFactory({ registry: config.gameSessions, credentials }), clients: new Set(), paused: false, lastSeen: Date.now() };
              worlds.set(credentials.gameId, worldRecord);
            }
            worldRecord.clients.add(client);
          }
          const subscription = config.gameSessions.subscribe(credentials, update => {
            if (update.type === "expired") { respond({ type: "game.error", code: "expired" }); closeSocket(client, 1008, "expired"); }
            else respond({ type: "game.state", snapshot: update.snapshot });
          });
          if (!subscription.ok) { respond({ type: "game.error", code: subscription.reason }); closeSocket(client, 1013, "capacity"); return; }
          unsubscribe = subscription.unsubscribe;
          clearTimeout(timer);
          respond({ type: "game.ready", ...result, credentials });
          if (worldRecord) respond({ type: "game.world", world: worldRecord.world.snapshot() });
          return;
        }
        // World facts and action commits are intentionally absent from player RPCs.
        if (event.type === "game.walk" && worldRecord) {
          respond({ type: "game.move_result", ...worldRecord.world.input({ type: "move_to", loopId: event.loopId, sequence: event.sequence, destination: event.destination }) });
        } else if (event.type === "game.pause" && worldRecord && typeof event.paused === "boolean") {
          worldRecord.paused = event.paused;
          respond({ type: "game.pause", paused: worldRecord.paused });
        } else if (event.type === "game.reset" && worldRecord) {
          const current = config.gameSessions.publicState(credentials);
          if (!current.ok || !["catastrophe", "unresolved", "victory"].includes(current.snapshot.phase)) respond({ type: "game.error", code: "reset_unavailable" });
          else respond({ type: "game.reset_result", ...worldRecord.world.reset({ loopId: event.loopId, revision: event.revision }) });
        } else if (event.type === "game.snapshot") {
          const result = config.gameSessions.publicState(credentials);
          respond(result.ok ? { type: "game.state", snapshot: result.snapshot } : { type: "game.error", code: result.reason });
        } else if (event.type === "game.history") {
          const result = config.gameSessions.history(credentials, { npcId: event.npcId, loopIndex: event.loopIndex });
          respond(result.ok ? { type: "game.history", ...result.history } : { type: "game.error", code: result.reason });
        } else respond({ type: "game.error", code: "unsupported_event" });
      });
      return;
    }
    const context = { client, upstream: null, phase: "awaiting_start", timers: new Set(), released: false, finalized: false };
    client.on("error", () => closeSocket(client, 1011, "client_error"));

    const addTimer = (callback, ms) => {
      const timer = setTimeout(callback, ms);
      timer.unref?.(); context.timers.add(timer); return timer;
    };
    const clearTimers = () => { for (const timer of context.timers) clearTimeout(timer); context.timers.clear(); };
    const release = () => { if (!context.released) { context.released = true; sessions.delete(context); pending.delete(context); } };
    const terminateUpstream = () => {
      context.delegation?.dispose();
      context.gameUnsubscribe?.();
      if (context.gameLease) config.gameSessions?.detach(context.gameCredentials, context.gameLease);
      if (context.upstream && context.upstream.readyState !== WebSocket.CLOSED) context.upstream.terminate();
      release(); clearTimers();
    };
    const fail = (code, closeCode = 1008, extra = {}) => {
      if (context.phase === "closed") return;
      context.phase = "closed";
      status(client, config, "error", { code, ...extra });
      closeSocket(client, closeCode, code);
      terminateUpstream();
    };
    const beginClose = () => {
      if (context.phase === "closing" || context.phase === "closed") return;
      context.phase = "closing";
      context.delegation?.dispose();
      if (context.upstream?.readyState === OPEN) sendJson(context.upstream, { type: "session.close" }, config.maxBufferedBytes);
      addTimer(() => {
        if (!context.finalized) {
          if (client.readyState === OPEN) status(client, config, "error", { code: "close_timeout", finalUsageConfirmed: false });
          closeSocket(client, 1011, "close_timeout"); terminateUpstream();
        }
      }, config.closeTimeoutMs);
    };

    if (pending.size >= config.maxPendingConnections) {
      context.phase = "closed";
      client.terminate();
      return;
    }
    pending.add(context);
    const startTimer = addTimer(() => fail("start_timeout", 1008), config.startTimeoutMs);

    client.on("message", (data, isBinary) => {
      const parsed = parseMessage(data, isBinary, config.maxMessageBytes);
      if (parsed.error) { fail(parsed.error, parsed.error === "message_too_large" ? 1009 : 1008); return; }
      const event = parsed.value;
      if (context.phase === "awaiting_start") {
        if (event.type !== "gym.start" || typeof event.character !== "string") { fail("invalid_start"); return; }
        if (!tokenMatches(event.token, config.accessToken)) { fail("unauthorized", 1008); return; }
        if (!config.apiKey) { fail("missing_api_key", 1011); return; }
        let start = buildSessionStart(event.character);
        if (!start) { fail("unknown_character"); return; }
        if (sessions.size >= config.maxSessions) { fail("capacity", 1013); return; }
        if (event.gameId !== undefined || event.resumeToken !== undefined) {
          if (!config.gameSessions) { fail("gameplay_unavailable"); return; }
          context.gameCredentials = { gameId: event.gameId, resumeToken: event.resumeToken };
          const world = worlds.get(event.gameId);
          if (world && !world.world.canConverse(event.character).ok) { fail("character_out_of_range"); return; }
          const attached = config.gameSessions.attach(context.gameCredentials, event.character);
          if (!attached.ok) { fail("game_unauthorized"); return; }
          context.gameLease = attached.lease.leaseId;
          const projection = config.gameSessions.npcContext(context.gameCredentials, context.gameLease);
          if (!projection.ok) { fail("game_unauthorized"); return; }
          start = buildSessionStart(event.character, { context: projection.context, history: projection.history });
          start.session.delegation = { type: "client" };
          // A new character conversation invalidates the previous transport as well as its tools.
          for (const previous of sessions) {
            if (previous !== context && previous.gameCredentials?.gameId === event.gameId) {
              previous.invalidate?.();
            }
          }
          context.invalidate = () => fail("conversation_superseded");
          context.refreshGame = () => {
            const latest = config.gameSessions.npcContext(context.gameCredentials, context.gameLease);
            if (!latest.ok) { fail("conversation_superseded"); return; }
            if (context.phase !== "ready") return;
            const { revision, mood, action, knownFacts } = latest.context;
            if (context.lastGameRevision === revision) return;
            context.lastGameRevision = revision;
            const update = JSON.stringify({ revision, mood, action, knownFacts });
            const characters = Array.from(update);
            for (let offset = 0; offset < characters.length; offset += 80) {
              const chunk = characters.slice(offset, offset + 80).join("");
              if (!sendJson(context.upstream, { type: "session.thinking.append",
                event_id: `game_context_${revision}_${offset}`, delegation_id: null,
                content: `Current character state, revision ${revision}, part ${1 + offset / 80}: ${chunk}`,
              }, config.maxBufferedBytes)) { fail("upstream_backpressure", 1011); return; }
            }
          };
          const subscription = config.gameSessions.subscribe(context.gameCredentials, context.refreshGame);
          if (!subscription.ok) { fail("game_subscription_failed"); return; }
          context.gameUnsubscribe = subscription.unsubscribe;
        }
        clearTimeout(startTimer); context.timers.delete(startTimer); pending.delete(context);
        sessions.add(context); context.phase = "connecting";
        status(client, config, "connecting");
        const headers = { Authorization: `Bearer ${config.apiKey}`, "User-Agent": "lucid-loop-live-relay/0.1" };
        context.upstream = new WebSocket(config.upstreamUrl, { headers, maxPayload: config.maxMessageBytes * 2 });
        const startupTimer = addTimer(() => fail("startup_timeout", 1011, { finalUsageConfirmed: false }), config.startupTimeoutMs);
        context.upstream.on("open", () => {
          if (context.phase !== "connecting") { terminateUpstream(); return; }
          if (!sendJson(context.upstream, start, config.maxBufferedBytes)) fail("upstream_backpressure", 1011, { finalUsageConfirmed: false });
        });
        context.upstream.on("message", (raw, upstreamBinary) => {
          const incoming = parseMessage(raw, upstreamBinary, config.maxMessageBytes);
          if (incoming.error) { fail("invalid_upstream_event", 1011, { finalUsageConfirmed: false }); return; }
          const nativeEvent = incoming.value;
          if (context.gameLease && nativeEvent.type !== "session.closed") {
            const projection = config.gameSessions.npcContext(context.gameCredentials, context.gameLease);
            if (!projection.ok) { fail("conversation_superseded"); return; }
            if (nativeEvent.type === "session.input_transcript.delta" || nativeEvent.type === "session.output_transcript.delta") {
              config.gameSessions.appendTranscript(context.gameCredentials, context.gameLease, nativeEvent);
            }
          }
          if (nativeEvent.type === "session.closed") {
            context.delegation?.dispose();
            context.gameUnsubscribe?.();
            if (client.readyState === OPEN) sendJson(client, nativeEvent, config.maxBufferedBytes);
            context.finalized = true; context.phase = "closed"; clearTimers();
            if (context.gameLease) config.gameSessions.detach(context.gameCredentials, context.gameLease);
            if (client.readyState === OPEN) {
              status(client, config, "closed", { finalUsage: nativeEvent.usage ?? null, finalUsageConfirmed: true });
              closeSocket(client, 1000, "session_closed");
            }
            closeSocket(context.upstream, 1000, "session_closed"); release();
            return;
          }
          if (client.readyState === OPEN && !sendJson(client, nativeEvent, config.maxBufferedBytes)) {
            fail("client_backpressure", 1011, { finalUsageConfirmed: false }); return;
          }
          if (nativeEvent.type === "session.started" && context.phase === "connecting") {
            clearTimeout(startupTimer); context.timers.delete(startupTimer);
            context.phase = "ready";
            if (context.gameLease && config.intentModel) {
              context.delegation = createGameplayDelegation({
                registry: config.gameSessions, credentials: context.gameCredentials,
                leaseId: context.gameLease, model: config.intentModel,
                interpret: config.intentInterpreter ?? createIntentInterpreter({ apiKey: config.apiKey }),
                sendLive: event => {
                  if (context.phase !== "ready" || !sendJson(context.upstream, event, config.maxBufferedBytes)) throw new Error("not_ready");
                },
                onResult: result => {
                  if (context.phase === "ready") sendJson(client, { type: "game.intent_result", ...result }, config.maxBufferedBytes);
                },
              });
            }
            status(client, config, "ready");
            context.refreshGame?.();
            addTimer(() => beginClose(), config.maxDurationMs);
          }
          if (nativeEvent.type === "session.delegation.created" && context.phase === "ready") {
            if (context.delegation) void context.delegation.onDelegation(nativeEvent);
            else if (context.gameLease) status(client, config, "error", { code: "intent_backend_unavailable" });
          }
        });
        context.upstream.on("error", () => {
          if (!context.finalized && client.readyState === OPEN) fail("upstream_error", 1011, { finalUsageConfirmed: false });
        });
        context.upstream.on("close", () => {
          context.delegation?.dispose();
          context.gameUnsubscribe?.();
          if (context.gameLease) config.gameSessions.detach(context.gameCredentials, context.gameLease);
          if (!context.finalized && context.phase !== "awaiting_start" && client.readyState === OPEN) {
            status(client, config, "error", { code: "upstream_closed", finalUsageConfirmed: false }); closeSocket(client, 1011, "upstream_closed");
          }
          context.phase = "closed";
          release(); clearTimers();
        });
        return;
      }
      if (event.type === "game.text" && context.gameLease) {
        if (context.phase !== "ready" || !context.delegation) { status(client, config, "error", { code: "intent_backend_unavailable" }); return; }
        void context.delegation.onTyped({ requestId: event.requestId, text: event.text });
        return;
      }
      if (!ALLOWED_AFTER_READY.has(event.type)) { status(client, config, "error", { code: "unsupported_event" }); return; }
      if (context.phase === "closing") return;
      if (context.phase !== "ready") { status(client, config, "error", { code: "not_ready" }); return; }
      const command = normalizeCommand(event);
      if (!command) { status(client, config, "error", { code: "invalid_message" }); return; }
      if (event.type === "session.close") { beginClose(); return; }
      if (!sendJson(context.upstream, command, config.maxBufferedBytes)) { fail("upstream_backpressure", 1011, { finalUsageConfirmed: false }); return; }
    });
    client.on("close", () => {
      if (!context.finalized && context.phase === "ready") beginClose();
      else if (!context.finalized && context.phase === "closing") return;
      else if (!context.finalized) terminateUpstream();
    });
  });

  return {
    async listen() {
      if (listening) return;
      await new Promise((resolve, reject) => {
        httpServer.once("error", reject);
        httpServer.listen(config.port, config.host, () => { httpServer.off("error", reject); resolve(); });
      });
      listening = true;
    },
    address() { return httpServer.address(); },
    async close() {
      if (worldTimer) clearInterval(worldTimer);
      worlds.clear();
      for (const context of sessions) { context.client.terminate(); context.upstream?.terminate(); }
      sessions.clear();
      for (const client of wsServer.clients) client.terminate();
      await new Promise(resolve => wsServer.close(resolve));
      if (listening) await new Promise(resolve => httpServer.close(resolve));
      listening = false;
    },
  };
}

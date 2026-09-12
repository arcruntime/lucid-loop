import http from "node:http";
import { randomUUID } from "node:crypto";
import WebSocket, { WebSocketServer } from "ws";
import { buildSessionStart } from "./prompts.mjs";

function send(socket, event) {
  if (socket.readyState === WebSocket.OPEN) socket.send(JSON.stringify(event));
}

export function createFixtureServer({ host = "127.0.0.1", port = 8081 } = {}) {
  const httpServer = http.createServer((request, response) => {
    if (request.url === "/health") {
      response.writeHead(200, { "content-type": "application/json", "cache-control": "no-store" });
      response.end(JSON.stringify({ ready: true, fixture: true }));
    } else response.writeHead(404).end();
  });
  const wsServer = new WebSocketServer({ noServer: true, maxPayload: 256 * 1024 });
  httpServer.on("upgrade", (request, socket, head) => {
    if (request.url !== "/live") { socket.destroy(); return; }
    wsServer.handleUpgrade(request, socket, head, client => wsServer.emit("connection", client));
  });
  wsServer.on("connection", client => {
    let ready = false;
    let closed = false;
    client.on("message", raw => {
      let event;
      try { event = JSON.parse(raw.toString()); } catch { send(client, { type: "gym.status", status: "error", code: "invalid_message" }); return; }
      if (!ready) {
        const start = event.type === "gym.start" && buildSessionStart(event.character);
        if (!start) { send(client, { type: "gym.status", status: "error", code: "invalid_start" }); client.close(1008); return; }
        ready = true;
        send(client, { type: "gym.status", status: "connecting" });
        send(client, {
          type: "session.started", event_id: `fixture_${randomUUID()}`,
          session: { id: `live_fixture_${randomUUID()}`, ...start.session },
        });
        send(client, { type: "gym.status", status: "ready" });
        return;
      }
      if (closed) return;
      if (event.type === "session.input_audio.append") {
        send(client, { type: "session.input_transcript.delta", event_id: `fixture_${randomUUID()}`, delta: "Fixture audio received.", start_ms: 0, end_ms: 100 });
        send(client, { type: "session.output_transcript.delta", event_id: `fixture_${randomUUID()}`, delta: "Hello from the local fixture.", start_ms: 100, end_ms: 200 });
        send(client, { type: "session.output_audio.delta", event_id: `fixture_${randomUUID()}`, delta: Buffer.alloc(2400).toString("base64") });
      } else if (event.type === "session.input_audio.mute") {
        send(client, { type: "session.input_audio.muted", event_id: `fixture_${randomUUID()}` });
      } else if (event.type === "session.input_audio.unmute") {
        send(client, { type: "session.input_audio.unmuted", event_id: `fixture_${randomUUID()}` });
      } else if (event.type === "session.close") {
        closed = true;
        send(client, { type: "session.closed", event_id: `fixture_${randomUUID()}`, reason: "close_requested", usage: { seconds: 0 } });
        send(client, { type: "gym.status", status: "closed", finalUsage: { seconds: 0 }, finalUsageConfirmed: true });
        client.close(1000, "session_closed");
      } else send(client, { type: "gym.status", status: "error", code: "unsupported_event" });
    });
  });
  let listening = false;
  return {
    async listen() { await new Promise((resolve, reject) => { httpServer.once("error", reject); httpServer.listen(port, host, resolve); }); listening = true; },
    address() { return httpServer.address(); },
    async close() {
      for (const client of wsServer.clients) client.terminate();
      await new Promise(resolve => wsServer.close(resolve));
      if (listening) await new Promise(resolve => httpServer.close(resolve));
      listening = false;
    },
  };
}

import test from "node:test";
import assert from "node:assert/strict";
import WebSocket from "ws";
import { createRelayServer } from "../src/server.mjs";
import { createGameSessions } from "../src/game-sessions.mjs";

const enabled = process.env.RUN_GPT_LIVE_SMOKE === "1" && Boolean(process.env.OPENAI_API_KEY);

test("opt-in real Live and Responses bridge commits a typed wait request", { skip: !enabled, timeout: 60000 }, async () => {
  const games = createGameSessions({ definitionFactory: () => ({ authorizeAction: command => command.actorId === "maya" && command.type === "wait" }) });
  const created = games.create();
  const relay = createRelayServer({ host: "127.0.0.1", port: 0,
    apiKey: process.env.OPENAI_API_KEY, intentModel: process.env.INTENT_MODEL || "gpt-5.6-luna", gameSessions: games });
  await relay.listen();
  const socket = new WebSocket(`ws://127.0.0.1:${relay.address().port}/live`);
  let audioTimer, timeout;
  let committed = false;
  try {
    await new Promise((resolve, reject) => {
      timeout = setTimeout(() => reject(new Error("Live gameplay smoke timed out")), 50000);
      socket.on("error", () => reject(new Error("Relay connection failed")));
      socket.on("open", () => socket.send(JSON.stringify({ type: "gym.start", character: "maya", ...created.credentials })));
      socket.on("message", raw => {
        const event = JSON.parse(raw);
        if (event.type === "gym.status" && event.status === "ready") {
          // Live's frame clock must keep progressing even in typed-only mode.
          const silence = Buffer.alloc(2400 * 2).toString("base64");
          audioTimer = setInterval(() => {
            if (socket.readyState === WebSocket.OPEN && !committed) socket.send(JSON.stringify({ type: "session.input_audio.append", audio: silence }));
          }, 100);
          socket.send(JSON.stringify({ type: "game.text", requestId: "live_smoke_wait", text: "Please wait right here while I go speak to Luca." }));
        } else if (event.type === "game.intent_result") {
          if (!event.ok || games.publicState(created.credentials).snapshot.actors.maya.action.type !== "wait") {
            reject(new Error("Real interpreter did not commit the requested wait")); return;
          }
          committed = true;
          clearInterval(audioTimer);
          socket.send(JSON.stringify({ type: "session.close" }));
        } else if (event.type === "session.closed") {
          try { assert.equal(committed, true); assert.ok(event.usage); resolve(); } catch (error) { reject(error); }
        } else if (event.type === "gym.status" && event.status === "error") reject(new Error("Live relay reported an error"));
      });
    });
  } finally {
    clearInterval(audioTimer); clearTimeout(timeout); socket.terminate(); await relay.close();
  }
});

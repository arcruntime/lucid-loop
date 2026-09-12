import assert from "node:assert/strict";
import { once } from "node:events";
import test from "node:test";
import WebSocket from "ws";

const enabled = process.env.RUN_GPT_LIVE_SMOKE === "1" && Boolean(process.env.OPENAI_API_KEY);

test("opt-in GPT-Live session starts and closes with final usage", { skip: !enabled, timeout: 30_000 }, async () => {
  const socket = new WebSocket("wss://api.openai.com/v1/live/sessions", {
    headers: {
      Authorization: `Bearer ${process.env.OPENAI_API_KEY}`,
      "User-Agent": "lucid-loop-live-smoke/0.1",
    },
  });
  let started = false;
  let closed;
  socket.on("message", raw => {
    const event = JSON.parse(raw.toString());
    if (event.type === "session.started") {
      started = true;
      socket.send(JSON.stringify({ type: "session.close" }));
    } else if (event.type === "session.closed") {
      closed = event;
      socket.close();
    } else if (event.type === "error") {
      socket.close(1011, "live_error");
    }
  });
  await once(socket, "open");
  socket.send(JSON.stringify({
    type: "session.start",
    session: {
      model: "gpt-live-1",
      instructions: "This is a transport smoke test. Do not speak unless audio is received.",
      audio: { format: { type: "audio/pcm", rate: 24000 }, output: { voice: "marin" } },
      store: false,
    },
  }));
  await once(socket, "close");
  assert.equal(started, true);
  assert.equal(closed?.type, "session.closed");
  assert.ok(closed?.usage);
});

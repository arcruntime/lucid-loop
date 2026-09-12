import assert from "node:assert/strict";
import { once } from "node:events";
import test from "node:test";
import WebSocket from "ws";
import { createFixtureServer } from "../src/fixture.mjs";

function makeInbox(socket) {
  const messages = [];
  const waiters = [];
  socket.on("message", data => {
    const value = JSON.parse(data.toString());
    const waiter = waiters.shift();
    if (waiter) waiter(value); else messages.push(value);
  });
  return () => messages.length ? Promise.resolve(messages.shift()) : new Promise(resolve => waiters.push(resolve));
}

test("local fixture speaks the production client protocol without an API key", async () => {
  const fixture = createFixtureServer({ host: "127.0.0.1", port: 0 });
  await fixture.listen();
  const client = new WebSocket(`ws://127.0.0.1:${fixture.address().port}/live`);
  const next = makeInbox(client);
  try {
    await once(client, "open");
    client.send(JSON.stringify({ type: "gym.start", character: "maya" }));
    assert.deepEqual(await next(), { type: "gym.status", status: "connecting" });
    assert.equal((await next()).type, "session.started");
    assert.deepEqual(await next(), { type: "gym.status", status: "ready" });
    client.send(JSON.stringify({ type: "session.input_audio.append", audio: "AAAAAA==" }));
    const inputTranscript = await next();
    assert.equal(inputTranscript.type, "session.input_transcript.delta");
    assert.equal(inputTranscript.delta, "Fixture audio received.");
    assert.equal((await next()).type, "session.output_transcript.delta");
    assert.equal((await next()).type, "session.output_audio.delta");
    client.send(JSON.stringify({ type: "session.close" }));
    assert.equal((await next()).type, "session.closed");
    assert.deepEqual(await next(), { type: "gym.status", status: "closed", finalUsage: { seconds: 0 }, finalUsageConfirmed: true });
    await once(client, "close");
  } finally {
    client.terminate();
    await fixture.close();
  }
});

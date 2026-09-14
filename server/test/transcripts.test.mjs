import test from "node:test";
import assert from "node:assert/strict";
import { createTranscriptStore } from "../src/transcripts.mjs";

const delta = (id, text, role = "input", start = 0, end = 100) => ({
  type: `session.${role}_transcript.delta`, event_id: id, delta: text, start_ms: start, end_ms: end,
});

test("preserves overlapping original fragments and deduplicates within session", () => {
  const store = createTranscriptStore();
  assert.equal(store.append("s1", "maya", 1, delta("1", "Wait ")), true);
  store.append("s1", "maya", 1, delta("2", "Okay", "output", 20, 120));
  store.append("s1", "maya", 1, delta("3", "here.", "input", 80, 160));
  assert.equal(store.append("s1", "maya", 1, delta("1", "Wait ")), false);
  const snapshot = store.snapshot();
  assert.deepEqual(snapshot.fragments.map(f => f.text), ["Wait ", "Okay", "here."]);
  snapshot.fragments[0].text = "mutated";
  assert.equal(store.snapshot().fragments[0].text, "Wait ");
  assert.equal(store.append("s2", "maya", 1, delta("1", "again")), true);
});

test("history isolates NPC and loop and preserves spaces and repetitions", () => {
  const store = createTranscriptStore();
  store.append("s1", "luca", 1, delta("1", "Secret"));
  store.append("s2", "maya", 1, delta("1", "I "));
  store.append("s2", "maya", 1, delta("2", "I know"));
  store.append("s3", "maya", 2, delta("1", "New loop"));
  assert.deepEqual(store.history("maya", 1), [{ type: "message", role: "user",
    content: [{ type: "input_text", text: "I I know" }] }]);
  assert.deepEqual(store.history("theo", 1), []);
  assert.equal(store.history("maya", 1, { maxChars: 4 })[0].content[0].text, "know");
});

test("rejects malformed input and reports bounded history exhaustion", () => {
  const store = createTranscriptStore({ maxFragments: 1, maxCharacters: 10 });
  assert.equal(store.append("s", "maya", 1, delta("bad", "x", "input", 5, 1)), false);
  assert.equal(store.append("s", "maya", 1, delta("1", "okay")), true);
  assert.equal(store.append("s", "maya", 1, delta("2", "more")), false);
  assert.equal(store.snapshot().dropped, 1);
  assert.equal(store.snapshot().fragments.length, 1);
});

test('speech memory bounds serialized metadata and escaped Unicode without losing original source correlation', async () => {
  const { createHash } = await import('node:crypto');
  const store = createTranscriptStore();
  const hugeId = 'provider-event-'.repeat(20000);
  const text = '\u0000"\\\u65e5'.repeat(4000);
  assert.equal(store.append('prior', 'theo', 'loop1', delta(hugeId, text, 'output')), true);
  const memory = store.speechMemory('theo', 'loop1');
  assert.ok(Buffer.byteLength(JSON.stringify(memory)) <= 32768);
  assert.equal(memory.incomplete, true);
  assert.equal(memory.observations[0].excerpt, true);
  assert.deepEqual(memory.observations[0].eventId, {
    sha256: createHash('sha256').update(hugeId).digest('hex'), digestOfOriginal: true,
  });
  assert.equal(store.snapshot().fragments[0].eventId, hugeId);
  assert.ok(text.startsWith(memory.observations[0].text));
  assert.deepEqual(store.speechMemory('theo', 'loop1', { excludeSessionId: 'prior' }).observations, []);
});

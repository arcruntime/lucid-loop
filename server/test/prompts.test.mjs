import assert from "node:assert/strict";
import test from "node:test";
import { buildSessionStart } from "../src/prompts.mjs";

test("gameplay startup uses only its supplied projection and clones history", () => {
  const history = [{ type: "message", role: "user", content: [{ type: "input_text", text: "Wait here." }] }];
  const start = buildSessionStart("maya", { context: { mood: "intimate", knowledge: [] }, history });
  assert.match(start.session.instructions, /application owns all world changes/);
  assert.match(start.session.instructions, /intimate/);
  history[0].content[0].text = "changed";
  assert.equal(start.session.input[0].content[0].text, "Wait here.");
  assert.equal(buildSessionStart("missing"), null);
});

for (const fixture of [
  { id: "maya", role: "close friend", voice: "gleam" },
  { id: "ren", role: "resident DJ", voice: "quartz" },
  { id: "luca", role: "bartender", voice: "meridian" },
  { id: "theo", role: "socialite", voice: "vesper" },
]) {
  test(`${fixture.id} uses the approved role and first audition voice`, () => {
    const session = buildSessionStart(fixture.id).session;
    assert.match(session.instructions, new RegExp(fixture.role, "i"));
    assert.equal(session.audio.output.voice, fixture.voice);
  });
}

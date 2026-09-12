import assert from "node:assert/strict";
import test from "node:test";
import { buildSessionStart } from "../src/prompts.mjs";

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

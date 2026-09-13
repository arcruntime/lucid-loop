import test from "node:test";
import assert from "node:assert/strict";
import { createIntentInterpreter } from "../src/responses-client.mjs";

test("interpreter sends server credentials only to the fixed Responses endpoint", async () => {
  const interpret = createIntentInterpreter({ apiKey: "test-project-secret", fetchImpl: async (url, request) => {
    assert.equal(url, "https://api.openai.com/v1/responses");
    assert.equal(request.headers.authorization, "Bearer test-project-secret");
    assert.deepEqual(JSON.parse(request.body), { model: "fixture", store: false });
    assert.ok(request.signal);
    return new Response(JSON.stringify({ status: "completed", output: [] }));
  } });
  assert.equal((await interpret({ model: "fixture", store: false })).status, "completed");
});

test("provider errors are sanitized and oversized successful bodies are bounded", async () => {
  const failure = createIntentInterpreter({ apiKey: "fixture", fetchImpl: async () => new Response("private diagnostics", { status: 500 }) });
  await assert.rejects(failure({}), error => error.message === "intent_upstream_error");
  const large = createIntentInterpreter({ apiKey: "fixture", maxBytes: 10, fetchImpl: async () => new Response("x".repeat(100)) });
  await assert.rejects(large({}), /intent_response_too_large/);
  await assert.rejects(createIntentInterpreter({})({}), /missing_api_key/);
});

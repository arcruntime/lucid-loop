// Project credentials stay on the relay. Never include provider bodies in client errors.
export function createIntentInterpreter({ apiKey, fetchImpl = fetch, timeoutMs = 15000, maxBytes = 262144 } = {}) {
  return async (body, { signal } = {}) => {
    if (!apiKey) throw new Error("missing_api_key");
    const response = await fetchImpl("https://api.openai.com/v1/responses", {
      method: "POST",
      headers: { authorization: `Bearer ${apiKey}`, "content-type": "application/json" },
      body: JSON.stringify(body),
      signal: signal ? AbortSignal.any([signal, AbortSignal.timeout(timeoutMs)]) : AbortSignal.timeout(timeoutMs),
    });
    if (!response.ok) { await response.body?.cancel(); throw new Error("intent_upstream_error"); }
    const reader = response.body?.getReader();
    if (!reader) throw new Error("intent_empty_response");
    const chunks = [];
    let size = 0;
    try {
      for (;;) {
        const { done, value } = await reader.read();
        if (done) break;
        size += value.byteLength;
        if (size > maxBytes) { await reader.cancel(); throw new Error("intent_response_too_large"); }
        chunks.push(Buffer.from(value));
      }
    } finally { reader.releaseLock(); }
    return JSON.parse(Buffer.concat(chunks).toString("utf8"));
  };
}

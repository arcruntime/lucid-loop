import { createHash } from 'node:crypto';
// Live fragments are timed observations, not completed turns or action triggers.
export function createTranscriptStore({ maxFragments = 4096, maxCharacters = 262144 } = {}) {
  if (!Number.isSafeInteger(maxFragments) || maxFragments < 1 ||
      !Number.isSafeInteger(maxCharacters) || maxCharacters < 1) throw new TypeError("Invalid transcript limits");
  const fragments = [];
  const seen = new Set();
  let characters = 0;
  let sequence = 0;
  let dropped = 0;
  return {
    append(sessionId, npcId, loopId, event) {
      if (typeof sessionId !== "string" || !sessionId || typeof npcId !== "string" || !npcId ||
          !(typeof loopId === "string" && loopId.length > 0 || Number.isSafeInteger(loopId) && loopId > 0)) return false;
      const role = event?.type === "session.input_transcript.delta" ? "user" :
        event?.type === "session.output_transcript.delta" ? "assistant" : null;
      if (!role || typeof event.delta !== "string" || !event.delta ||
          typeof event.event_id !== "string" || !event.event_id ||
          !Number.isFinite(event.start_ms) || !Number.isFinite(event.end_ms) ||
          event.start_ms < 0 || event.end_ms < event.start_ms) return false;
      const key = JSON.stringify([sessionId, event.event_id]);
      if (seen.has(key)) return false;
      // Stop accepting at the bounded session-history budget rather than silently
      // rewriting history or accepting duplicate IDs after eviction.
      if (fragments.length >= maxFragments || characters + event.delta.length > maxCharacters) {
        dropped++; return false;
      }
      seen.add(key);
      characters += event.delta.length;
      fragments.push({ sequence: ++sequence, sessionId, npcId, loopId, role,
        source: "voice", eventId: event.event_id, text: event.delta, startMs: event.start_ms, endMs: event.end_ms });
      return true;
    },
    appendTyped(sessionId, npcId, loopId, { requestId, text } = {}) {
      if (typeof sessionId !== "string" || !sessionId || typeof npcId !== "string" || !npcId ||
          typeof loopId !== "string" || !loopId || typeof requestId !== "string" ||
          !/^[a-zA-Z0-9_.:-]{1,128}$/.test(requestId) || typeof text !== "string" || !text.trim() || text.length > 4000) return false;
      const key = JSON.stringify([sessionId, "typed", requestId]);
      if (seen.has(key)) return false;
      if (fragments.length >= maxFragments || characters + text.length > maxCharacters) { dropped++; return false; }
      seen.add(key); characters += text.length;
      fragments.push({ sequence: ++sequence, sessionId, npcId, loopId, role: "user", source: "typed",
        eventId: requestId, requestId, text, startMs: null, endMs: null });
      return true;
    },
    snapshot({ npcId, loopId } = {}) {
      return { fragments: fragments.filter(f => (npcId === undefined || f.npcId === npcId) &&
        (loopId === undefined || f.loopId === loopId)).map(f => ({ ...f })), dropped };
    },
    // Preserve early attributable NPC speech beyond the sliding startup excerpt.
    // These may be incomplete clauses or lies: never classify them as facts or finished turns.
    speechMemory(npcId, loopId, { maxFragments = 128, maxChars = 16000, excludeSessionId = null } = {}) {
      if (!Number.isSafeInteger(maxFragments) || maxFragments < 1 || maxFragments > 128 ||
          !Number.isSafeInteger(maxChars) || maxChars < 1 || maxChars > 16000) throw new TypeError("Invalid speech memory limits");
      const eligible = fragments.filter(f => f.npcId === npcId && f.loopId === loopId &&
        f.role === "assistant" && f.sessionId !== excludeSessionId);
      // Source IDs originate upstream and need not be short. A digest preserves exact
      // correlation to the stored original without admitting unbounded prompt metadata.
      const sourceId = value => Buffer.byteLength(value) <= 128 ? value :
        { sha256: createHash('sha256').update(value).digest('hex'), digestOfOriginal: true };
      const memory = { kind: "unverified_npc_speech_fragments", audience: [npcId, "player"], observations: [], incomplete: dropped > 0 };
      let remaining = maxChars;
      for (const fragment of eligible) {
        if (memory.observations.length >= maxFragments || remaining === 0) break;
        const observation = { ...fragment, sessionId: sourceId(fragment.sessionId), eventId: sourceId(fragment.eventId),
          text: fragment.text.slice(0, remaining), excerpt: fragment.text.length > remaining };
        memory.observations.push(observation);
        // Includes escaped/control characters, multi-byte text and all source metadata.
        // Reserve the longer false spelling so setting incomplete cannot exceed the cap.
        if (Buffer.byteLength(JSON.stringify({ ...memory, incomplete: false })) > 32768) {
          let low = 0, high = observation.text.length;
          const original = observation.text; observation.excerpt = true;
          while (low < high) {
            const middle = Math.ceil((low + high) / 2); observation.text = original.slice(0, middle);
            if (Buffer.byteLength(JSON.stringify({ ...memory, incomplete: false })) <= 32768) low = middle;
            else high = middle - 1;
          }
          observation.text = original.slice(0, low);
          if (!observation.text) memory.observations.pop();
          memory.incomplete = true; break;
        }
        remaining -= observation.text.length;
      }
      memory.incomplete ||= memory.observations.length < eligible.length || memory.observations.some(f => f.excerpt);
      return memory;
    },
    // A bounded startup excerpt. Adjacent same-speaker fragments concatenate
    // verbatim; this grouping is presentation only and never proves turn finality.
    history(npcId, loopId, { maxMessages = 64, maxChars = 2000 } = {}) {
      if (!Number.isSafeInteger(maxMessages) || maxMessages < 1 || maxMessages > 128 ||
          !Number.isSafeInteger(maxChars) || maxChars < 1 || maxChars > 2000) throw new TypeError("Invalid history limits");
      const groups = [];
      for (const f of fragments) {
        if (f.npcId !== npcId || f.loopId !== loopId) continue;
        const previous = groups.at(-1);
        if (previous?.role === f.role && previous.sessionId === f.sessionId) previous.text += f.text;
        else groups.push({ role: f.role, text: f.text, sessionId: f.sessionId });
      }
      const selected = [];
      let remaining = maxChars;
      for (let i = groups.length - 1; i >= 0 && selected.length < maxMessages && remaining > 0; i--) {
        const { role, text } = groups[i];
        const excerpt = text.slice(-remaining);
        remaining -= excerpt.length;
        selected.unshift({ type: "message", role, content: [{
          type: role === "assistant" ? "output_text" : "input_text", text: excerpt,
        }] });
      }
      return selected;
    },
  };
}

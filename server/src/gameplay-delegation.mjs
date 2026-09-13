import { createHash } from 'node:crypto';
import { buildIntentRequest, parseIntentResponse, toEncounterCommand } from './gameplay-intent.mjs';

const validId = value => typeof value === 'string' && /^[a-zA-Z0-9_.:-]{1,128}$/.test(value);
const MAX_REINTERPRETATIONS = 3;
// A conservative byte budget stays below 500 tokens without assuming characters equal tokens.
function shortText(text) {
  let result = '';
  for (const character of text) {
    if (Buffer.byteLength(result + character, 'utf8') > 400) break;
    result += character;
  }
  return result;
}

function typedParts(text) {
  const parts = [];
  let part = '';
  for (const character of text) {
    if (Buffer.byteLength(JSON.stringify(part + character), 'utf8') > 300) {
      parts.push(part); part = '';
    }
    part += character;
  }
  if (part) parts.push(part);
  return parts;
}

/** One interpreter at a time for one server-owned conversation lease.
 * interpret(body, {signal}) is injected; this module performs no network IO.
 * dispose aborts waiting and signals cooperative cancellation; late work cannot commit.
 */
export function createGameplayDelegation({ registry, credentials, leaseId, model,
  interpret, sendLive, onResult = () => {}, maxRequests = 256 } = {}) {
  if (!registry || !['npcContext', 'history', 'appendTyped', 'submitAction'].every(key => typeof registry[key] === 'function') ||
      typeof interpret !== 'function' || typeof sendLive !== 'function' || typeof onResult !== 'function' ||
      !Number.isSafeInteger(maxRequests) || maxRequests < 1) throw new TypeError('Invalid delegation bridge options');
  const auth = structuredClone(credentials);
  const seen = new Set();
  let pending = null;
  let disposed = false;

  function report(result) {
    try { onResult(result); } catch { /* UI reporting cannot retry an action. */ }
    return result;
  }
  function current(context, revision = context.revision) {
    if (disposed) return false;
    const latest = registry.npcContext(auth, leaseId);
    return latest.ok === true && latest.context.npcId === context.npcId &&
      latest.context.loopId === context.loopId && latest.context.revision === revision;
  }
  function userEvidence(context) {
    const stored = registry.history(auth, { npcId: context.npcId });
    if (!stored.ok || !Array.isArray(stored.history?.fragments) || stored.history.dropped > 0)
      throw new Error('history_unavailable');
    let watermark = 0;
    for (const fragment of stored.history.fragments) {
      if (fragment.npcId !== context.npcId || fragment.loopId !== context.loopId || fragment.role !== 'user') continue;
      if (!Number.isSafeInteger(fragment.sequence) || fragment.sequence < 1) throw new Error('invalid_history_sequence');
      watermark = Math.max(watermark, fragment.sequence);
    }
    return { fragments: stored.history.fragments, watermark };
  }
  async function run(trigger, key, delegationId, sourceRequestId) {
    const base = { requestId: sourceRequestId, delegationId };
    if (disposed) return report({ ...base, ok: false, reason: 'disposed' });
    if (seen.has(key)) return report({ ...base, ok: false, reason: 'duplicate_request' });
    // This bridge still does not queue additional delegation/typed triggers. Voice
    // fragments already persisted by the relay are reconciled below; a second typed
    // submission rejected here is not persisted and must be retried by the caller.
    if (pending) return report({ ...base, ok: false, reason: 'busy' });
    if (seen.size >= maxRequests) return report({ ...base, ok: false, reason: 'request_capacity' });
    const projection = registry.npcContext(auth, leaseId);
    if (!projection.ok) return report({ ...base, ok: false, reason: 'stale_lease' });
    const context = structuredClone(projection.context);
    let evidence;
    try { evidence = userEvidence(context); }
    catch { return report({ ...base, ok: false, reason: 'history_unavailable' }); }
    let body;
    try {
      body = buildIntentRequest({ model, context, trigger, fragments: evidence.fragments });
    } catch {
      return report({ ...base, ok: false, reason: 'invalid_request' });
    }
    const actionId = `intent:${createHash('sha256').update(JSON.stringify([context.loopId, context.npcId, key])).digest('hex')}`;
    if (trigger.type === 'typed') {
      const storedTyped = registry.appendTyped(auth, leaseId, { requestId: trigger.requestId, text: trigger.text });
      if (!storedTyped.ok || storedTyped.appended !== true) return report({ ...base, ok: false, reason: 'typed_history_rejected' });
      try {
        evidence = userEvidence(context);
        body = buildIntentRequest({ model, context, trigger, fragments: evidence.fragments });
      } catch { return report({ ...base, ok: false, reason: 'history_unavailable' }); }
    }
    const controller = new AbortController();
    pending = controller;
    seen.add(key);
    let onAbort;
    try {
      const canceled = new Promise((_, reject) => {
        onAbort = () => reject(new Error('disposed'));
        controller.signal.addEventListener('abort', onAbort, { once: true });
      });
      // Typed text is context data, never trusted instructions. Preserve it completely
      // across bounded appends; the interpreter receives the original full submission.
      if (trigger.type === 'typed') {
        const parts = typedParts(trigger.text);
        for (let i = 0; i < parts.length; i++) {
          if (!current(context)) return report({ ...base, ok: false, reason: disposed ? 'disposed' : 'stale_context' });
          const sent = await Promise.race([Promise.resolve().then(() => sendLive({
            type: 'session.thinking.append', event_id: `typed_${actionId.slice(7)}_${i}`,
            delegation_id: null,
            content: `Player typed user data, part ${i + 1} of ${parts.length}: ${JSON.stringify(parts[i])}`,
          })), canceled]);
          if (sent === false) return report({ ...base, ok: false, reason: 'typed_context_delivery_failed' });
        }
      }
      if (!current(context)) return report({ ...base, ok: false, reason: disposed ? 'disposed' : 'stale_context' });
      let intent, command, executionResult, decisionReason;
      let reinterpretations = 0;
      for (;;) {
        const raw = await Promise.race([Promise.resolve().then(() => {
          if (controller.signal.aborted) throw new Error('disposed');
          return interpret(body, { signal: controller.signal });
        }), canceled]);
        if (!current(context)) return report({ ...base, ok: false, reason: disposed ? 'disposed' : 'stale_context' });
        try { intent = parseIntentResponse(raw, context); }
        catch { return report({ ...base, ok: false, reason: 'invalid_interpretation' }); }
        let latest;
        try { latest = userEvidence(context); }
        catch { return report({ ...base, ok: false, reason: 'history_unavailable' }); }
        if (latest.watermark !== evidence.watermark) {
          if (reinterpretations >= MAX_REINTERPRETATIONS) {
            // New fragments are not themselves a cancellation or a new completed turn.
            // Hold all mutations when bounded semantic review cannot catch up.
            intent = { kind: 'clarification', action: null, targetId: null, mood: null,
              clarification: 'Before I act, what would you like me to do now?' };
            command = null; decisionReason = 'user_evidence_unsettled';
            break;
          }
          evidence = latest;
          try { body = buildIntentRequest({ model, context, trigger, fragments: latest.fragments }); }
          catch { return report({ ...base, ok: false, reason: 'history_unavailable' }); }
          reinterpretations++;
          continue;
        }
        command = toEncounterCommand(intent, { ...context, requestId: actionId });
        // No await from the latest user watermark check through the synchronous
        // authority call. World/lease checks remain independent of transcript freshness.
        if (!current(context)) return report({ ...base, ok: false, reason: disposed ? 'disposed' : 'stale_context' });
        if (command) executionResult = registry.submitAction(auth, leaseId, command);
        break;
      }
      let committed = false;
      let outcome;
      let revision = context.revision;
      let content;
      let type = 'session.thinking.append';
      if (command) {
        const result = executionResult;
        outcome = result.outcome;
        committed = result.ok === true && outcome?.accepted === true;
        if (!committed) {
          content = 'The requested game action was not performed. Do not describe it as successful. Ask the player what they would like to try next.';
        } else {
          revision = outcome.revision;
          type = 'session.commentary.append';
          const summaries = {
            wait: 'Maya agreed to wait and is now waiting at her current location.',
            follow: 'Maya agreed to follow and is now following the player.',
            request_music: `Ren accepted the music request. The music mood is now ${command.mood}.`,
            agree_private_approach: 'Maya has agreed to approach Theo privately instead of making a public accusation.',
            agree_distance: 'Theo has agreed to keep his distance and not grab Maya’s phone.',
            stop_recording: 'Maya has stopped recording. She keeps the evidence already captured.',
            mediate: 'Luca has accepted the private mediation. Maya and Theo are separating.',
          };
          content = summaries[command.type];
          if (command.type === 'ask_about_exposure') {
            // Only speak the authored fact after the server has accepted its disclosure gate.
            const fact = outcome.event;
            if (fact?.type === 'clue_disclosed' && fact.factId === 'exposure_fear' && typeof fact.text === 'string') content = `Luca’s confirmed personal observation: ${fact.text}`;
            else { type = 'session.thinking.append'; content = 'The server accepted the disclosure request, but its fact text is unavailable. Do not invent the observation.'; }
          }
        }
      } else if (intent.kind === 'clarification') {
        type = 'session.commentary.append';
        content = intent.clarification;
      } else {
        if (trigger.type === 'typed') {
          type = 'session.instructions.append';
          content = 'Respond in character to the latest typed player message provided as user data. No supported game action was identified and no world state changed. Use only your permitted knowledge; do not claim an action succeeded.';
        } else content = 'No supported game action was identified in that request. No world state changed. Continue the conversation using your permitted knowledge.';
      }
      const result = { ...base, ok: true, kind: intent.kind, committed, reinterpretations,
        ...(decisionReason ? { reason: decisionReason } : {}), ...(outcome ? { outcome } : {}) };
      // Subscriber callbacks may have changed state or detached the lease during commit.
      if (!current(context, revision)) return report({ ...result, delivered: false, reason: 'stale_context' });
      try {
        const sent = await sendLive({ type, event_id: `game_${actionId.slice(7)}`, delegation_id: delegationId, content: shortText(content) });
        return report({ ...result, delivered: sent !== false });
      } catch {
        return report({ ...result, delivered: false, reason: 'delivery_failed' });
      }
    } catch {
      return report({ ...base, ok: false, reason: disposed ? 'disposed' : 'interpretation_failed' });
    } finally {
      controller.signal.removeEventListener('abort', onAbort);
      if (pending === controller) pending = null;
    }
  }
  return Object.freeze({
    onTyped({ requestId, text } = {}) {
      if (!validId(requestId)) return Promise.resolve(report({ ok: false, reason: 'invalid_request' }));
      return run({ type: 'typed', requestId, text }, `typed:${requestId}`, null, requestId);
    },
    onDelegation(event) {
      if (event?.type !== 'session.delegation.created' || event.delegation?.target !== 'client' || !validId(event.delegation.id)) {
        return Promise.resolve(report({ ok: false, reason: 'invalid_delegation' }));
      }
      return run(event, `delegation:${event.delegation.id}`, event.delegation.id, event.delegation.id);
    },
    dispose() { disposed = true; pending?.abort(); },
  });
}

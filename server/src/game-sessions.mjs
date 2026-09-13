import { randomBytes, timingSafeEqual } from 'node:crypto';
import { createEncounter, NPC_IDS } from './encounter.mjs';
import { createTranscriptStore } from './transcripts.mjs';

/** In-memory single-player sessions. Credentials/leases are bearer secrets.
 * No durable persistence: restart/expiry discards a game. All methods are server APIs.
 * Send publicSnapshot/history to the player; npcContext ONLY to the selected NPC model.
 * trustedFull and trusted mutation methods MUST NOT be exposed as arbitrary client RPCs.
 */
export function createGameSessions({ definitionFactory, encounterFactory = createEncounter, maxGames = 64, idleTtlMs = 30 * 60_000,
  maxRevisions = 65536, maxSubscribersPerGame = 8, transcriptOptions = {}, now = Date.now } = {}) {
  if (typeof definitionFactory !== 'function') throw new TypeError('Authored definitionFactory required');
  if (![maxGames, idleTtlMs, maxRevisions, maxSubscribersPerGame].every(n => Number.isSafeInteger(n) && n > 0)) throw new TypeError('Invalid registry limits');
  const games = new Map();
  const token = () => randomBytes(32).toString('base64url');
  const unauthorized = () => ({ ok: false, reason: 'unauthorized' });
  function equal(a, b) {
    if (typeof a !== 'string' || a.length !== b.length) return false;
    const bytes = Buffer.from(a); const expected = Buffer.from(b);
    return bytes.length === expected.length && timingSafeEqual(bytes, expected);
  }
  function cleanup() {
    const time = now(); let removed = 0;
    for (const [id, game] of games) if (game.expiresAt <= time) { games.delete(id); for (const listener of game.listeners) { try { listener({ type: 'expired', gameId: id }); } catch {} } game.listeners.clear(); removed++; }
    return removed;
  }
  function auth(credentials, touch = true) {
    cleanup(); const game = games.get(credentials?.gameId);
    if (!game || !equal(credentials?.resumeToken, game.resumeToken)) return null;
    if (touch) game.expiresAt = now() + idleTtlMs;
    return game;
  }
  function leased(credentials, leaseId) {
    const game = auth(credentials);
    return game && game.active && equal(leaseId, game.active.leaseId) ? game : null;
  }
  function publicSnapshot(game) {
    const s = game.encounter.snapshot();
    // Explicit allowlist. Never return NPC memory/secrets, private events or canonical definition.
    return { gameId: game.id, loopId: s.loopId, loopIndex: s.loopIndex, revision: s.revision,
      mood: s.mood, actors: s.actors, playerDiscoveries: s.playerDiscoveries.map(discovery => ({
        ...discovery, text: game.factTexts[discovery.factId] ?? "A clue was discovered.",
      })),
      ...(s.scenario ? { elapsedSeconds: s.scenario.elapsedSeconds, durationSeconds: s.scenario.durationSeconds } : {}),
      recording: s.recording, catastrophe: s.catastrophe, victory: s.victory, phase: s.phase };
  }
  function budget(game) { return game.encounter.snapshot().revision < maxRevisions; }
  function result(game, outcome) {
    if (outcome.accepted) for (const listener of game.listeners) {
      try { listener({ type: 'state', snapshot: publicSnapshot(game) }); } catch {}
    }
    return { ok: true, outcome, snapshot: publicSnapshot(game) };
  }
  return Object.freeze({
    cleanup,
    isAlive: credentials => Boolean(auth(credentials, false)),
    create() {
      cleanup(); if (games.size >= maxGames) return { ok: false, reason: 'capacity' };
      const id = token();
      const definition = definitionFactory(id);
      const encounter = encounterFactory({ ...definition, id });
      const game = { id, resumeToken: token(), encounter, factTexts: Object.fromEntries(Object.entries(definition.facts ?? {}).map(([factId, fact]) => [factId, fact.text])), transcripts: createTranscriptStore(transcriptOptions),
        active: null, listeners: new Set(), generations: Object.fromEntries(NPC_IDS.map(id => [id, 0])), expiresAt: now() + idleTtlMs };
      games.set(id, game);
      return { ok: true, credentials: { gameId: id, resumeToken: game.resumeToken }, snapshot: publicSnapshot(game) };
    },
    authorize(credentials) { return auth(credentials) ? { ok: true, gameId: credentials.gameId } : unauthorized(); },
    publicState(credentials) {
      const game = auth(credentials); return game ? { ok: true, snapshot: publicSnapshot(game) } : unauthorized();
    },
    subscribe(credentials, listener) {
      const game = auth(credentials); if (!game) return unauthorized();
      if (typeof listener !== 'function') return { ok: false, reason: 'invalid_listener' };
      if (game.listeners.size >= maxSubscribersPerGame) return { ok: false, reason: 'subscriber_capacity' };
      game.listeners.add(listener);
      return { ok: true, unsubscribe: () => game.listeners.delete(listener) };
    },
    resume(credentials) {
      const game = auth(credentials); return game ? { ok: true, snapshot: publicSnapshot(game) } : unauthorized();
    },
    attach(credentials, npcId) {
      const game = auth(credentials); if (!game) return unauthorized();
      if (!NPC_IDS.includes(npcId)) return { ok: false, reason: 'unknown_npc' };
      // One active voice conversation per game. A new attach fences every prior connection.
      game.active = { leaseId: token(), npcId, generation: ++game.generations[npcId] };
      return { ok: true, lease: { ...game.active }, snapshot: publicSnapshot(game) };
    },
    detach(credentials, leaseId) {
      const game = leased(credentials, leaseId); if (!game) return unauthorized();
      game.active = null; return { ok: true };
    },
    publicSnapshot(credentials) {
      const game = auth(credentials); return game ? { ok: true, snapshot: publicSnapshot(game) } : unauthorized();
    },
    npcContext(credentials, leaseId) {
      const game = leased(credentials, leaseId); if (!game) return unauthorized();
      const s = game.encounter.snapshot();
      return { ok: true, context: game.encounter.context(game.active.npcId),
        history: game.transcripts.history(game.active.npcId, s.loopId) };
    },
    history(credentials, { npcId, loopIndex } = {}) {
      const game = auth(credentials); if (!game) return unauthorized();
      if (npcId !== undefined && !NPC_IDS.includes(npcId)) return { ok: false, reason: 'unknown_npc' };
      if (loopIndex !== undefined && (!Number.isSafeInteger(loopIndex) || loopIndex < 1)) return { ok: false, reason: 'invalid_loop' };
      return { ok: true, history: game.transcripts.snapshot({ npcId, loopId: loopIndex === undefined ? undefined : `${game.id}:loop:${loopIndex}` }) };
    },
    appendTranscript(credentials, leaseId, event) {
      const game = leased(credentials, leaseId); if (!game) return unauthorized();
      const s = game.encounter.snapshot();
      return { ok: true, appended: game.transcripts.append(game.active.leaseId, game.active.npcId, s.loopId, event) };
    },
    appendTyped(credentials, leaseId, message) {
      const game = leased(credentials, leaseId); if (!game) return unauthorized();
      const s = game.encounter.snapshot();
      return { ok: true, appended: game.transcripts.appendTyped(game.active.leaseId, game.active.npcId, s.loopId, message) };
    },
    submitAction(credentials, leaseId, command) {
      const game = leased(credentials, leaseId); if (!game) return unauthorized();
      if (!budget(game)) return { ok: false, reason: 'state_capacity' };
      return result(game, game.encounter.submitAction(command, { npcId: game.active.npcId }));
    },
    reset(credentials, command) {
      const game = auth(credentials); if (!game) return unauthorized();
      if (!budget(game)) return { ok: false, reason: 'state_capacity' };
      const outcome = game.encounter.reset(command);
      if (outcome.accepted) game.active = null;
      return result(game, outcome);
    },
    // Trusted world adapter may observe while no voice conversation is active.
    trustedWorld(credentials, method, command) {
      const game = auth(credentials); if (!game) return unauthorized();
      if (!['confirmFact', 'observeVisibility', 'advance', 'observeStage'].includes(method) || typeof game.encounter[method] !== 'function') return { ok: false, reason: 'unsupported_mutation' };
      if (!budget(game)) return { ok: false, reason: 'state_capacity' };
      return result(game, game.encounter[method](command));
    },
    trustedFull(credentials) {
      const game = auth(credentials); return game ? { ok: true, snapshot: game.encounter.snapshot() } : unauthorized();
    },
    // Only trusted world/rule adapters may call these; lease fencing still blocks obsolete work.
    trustedMutation(credentials, leaseId, method, command) {
      const game = leased(credentials, leaseId); if (!game) return unauthorized();
      if (!['confirmFact', 'recordClaim', 'observeVisibility', 'discloseClue'].includes(method) || typeof game.encounter[method] !== 'function') return { ok: false, reason: 'unsupported_mutation' };
      if (!budget(game)) return { ok: false, reason: 'state_capacity' };
      return result(game, game.encounter[method](command));
    },
  });
}

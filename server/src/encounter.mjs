/** Transport-independent authoritative state. No IO or provider dependencies.
 * Trusted server integration owns this instance and authenticates npcId. Never expose
 * observeVisibility, confirmFact, recordClaim or reset as unvalidated model tools.
 * authorizeAction supplies authored social eligibility; missing policy fails closed.
 * Snapshot/context are detached copies. Persist snapshots externally if required.
 */
export const NPC_IDS = Object.freeze(['maya', 'ren', 'luca', 'theo']);
export const ACTOR_IDS = Object.freeze(['player', ...NPC_IDS, 'affair_partner']);
export const MOODS = Object.freeze(['Intimate', 'Aggressive']);
const copy = value => structuredClone(value);
const own = (o, k) => Object.hasOwn(o, k);
const isId = value => typeof value === 'string' && /^[a-zA-Z0-9_.:-]{1,128}$/.test(value);

export function createEncounter(definition = {}) {
  const { authorizeAction } = definition;
  const authored = copy({
    id: definition.id ?? 'club', facts: definition.facts ?? {},
    npcs: definition.npcs ?? {}, recognitionFactId: definition.recognitionFactId ?? null,
    initialMood: definition.initialMood ?? 'Aggressive',
  });
  if (!isId(authored.id) || !MOODS.includes(authored.initialMood)) throw new Error('Invalid encounter definition');
  for (const [id, fact] of Object.entries(authored.facts)) {
    if (!isId(id) || typeof fact.text !== 'string' || !fact.text.trim()) throw new Error('Invalid authored fact');
  }
  if (authored.recognitionFactId && !own(authored.facts, authored.recognitionFactId)) throw new Error('Unknown recognition fact');
  for (const id of Object.keys(authored.npcs)) if (!NPC_IDS.includes(id)) throw new Error('Unknown NPC');
  for (const npc of Object.values(authored.npcs)) {
    for (const id of npc.knownFactIds ?? []) if (!own(authored.facts, id)) throw new Error('Unknown initial fact');
  }
  let loopIndex = 1;
  let revision = 0;
  let discoveries = [];
  let state;
  let requests;
  const loopId = () => `${authored.id}:loop:${loopIndex}`;
  function initialize() {
    state = {
      encounterId: authored.id, loopId: loopId(), loopIndex, revision,
      mood: authored.initialMood, recognized: false, phase: 'exploring',
      recording: false, catastrophe: null, victory: false,
      playerDiscoveries: copy(discoveries),
      actors: Object.fromEntries(ACTOR_IDS.map(id => [id, { action: id === 'maya' ? { type: 'follow', targetId: 'player' } : { type: 'idle' } }])),
      npcs: Object.fromEntries(NPC_IDS.map(id => [id, {
        knowledge: (authored.npcs[id]?.knownFactIds ?? []).map(factId => ({ factId, source: 'authored_initial' })), claims: [],
      }])), events: [],
    };
    requests = new Map();
  }
  initialize();
  function reject(reason) { return { accepted: false, reason, loopId: state.loopId, revision }; }
  function fence(command) {
    if (!command || command.loopId !== state.loopId) return reject('stale_loop');
    if (command.revision !== revision) return reject('stale_revision');
    return null;
  }
  function commit(type, payload) {
    revision++;
    state.revision = revision;
    const event = { id: `${state.loopId}:event:${revision}`, loopId: state.loopId, revision, type, ...copy(payload) };
    state.events.push(event);
    return { accepted: true, loopId: state.loopId, revision, event: copy(event) };
  }
  function learn(recipientId, factId, source) {
    if (recipientId === 'player') {
      if (!discoveries.some(d => d.factId === factId)) discoveries.push({ factId, source, learnedInLoop: state.loopId });
      state.playerDiscoveries = copy(discoveries);
    } else if (!state.npcs[recipientId].knowledge.some(k => k.factId === factId)) {
      state.npcs[recipientId].knowledge.push({ factId, source });
    }
  }
  function context(npcId) {
    if (!NPC_IDS.includes(npcId)) throw new Error('Unknown NPC');
    const npc = authored.npcs[npcId] ?? {};
    return copy({
      npcId, loopId: state.loopId, revision, mood: state.mood,
      // Explicit allowlist: never pass an arbitrary authored record or world snapshot into a voice prompt.
      role: npc.role ?? '', objective: npc.objective ?? '', personality: npc.personality ?? '',
      beliefs: npc.beliefs ?? [], secrets: npc.secrets ?? [], permittedLies: npc.permittedLies ?? [],
      disclosureRules: npc.disclosureRules ?? [],
      action: state.actors[npcId].action,
      knownFacts: state.npcs[npcId].knowledge.map(k => ({ ...k, text: authored.facts[k.factId].text })),
      claims: state.npcs[npcId].claims,
    });
  }
  return Object.freeze({
    snapshot: () => copy(state),
    context,
    submitAction(command, principal) {
      if (!command || command.loopId !== state.loopId) return reject('stale_loop');
      if (!NPC_IDS.includes(principal?.npcId) || command.actorId !== principal.npcId) return reject('unauthorized_actor');
      if (!isId(command.requestId)) return reject('invalid_request_id');
      if (requests.has(command.requestId)) return reject('duplicate_request');
      const stale = fence(command); if (stale) return stale;
      const { type, actorId } = command;
      if (!['wait', 'follow', 'request_music'].includes(type)) return reject('unsupported_action');
      if ((type === 'wait' || type === 'follow') && actorId !== 'maya') return reject('ineligible_actor');
      if (type === 'follow' && command.targetId !== 'player') return reject('invalid_target');
      if (type === 'wait' && command.targetId != null) return reject('invalid_target');
      if (type === 'request_music' && (actorId !== 'ren' || !MOODS.includes(command.mood))) return reject('invalid_music_request');
      let allowed = false;
      try { allowed = authorizeAction?.(copy(command), copy(state)) === true; }
      catch { return reject('policy_error'); }
      if (!allowed) return reject('authored_policy_refused');
      if (type === 'request_music') state.mood = command.mood;
      else state.actors.maya.action = type === 'follow' ? { type, targetId: 'player' } : { type };
      const result = commit('action_committed', { requestId: command.requestId, actorId, action: type, ...(type === 'request_music' ? { mood: command.mood } : {}) });
      requests.set(command.requestId, true);
      return result;
    },
    // Supply one validated observation from the Unity/server world adapter, never model assertions.
    observeVisibility(observation) {
      const stale = fence(observation); if (stale) return stale;
      if (observation.observerId !== 'maya' || observation.inRecognitionArea !== true || !Array.isArray(observation.visibleActorIds)) return reject('recognition_conditions_unmet');
      if (!observation.visibleActorIds.every(id => ACTOR_IDS.includes(id))) return reject('unknown_visible_actor');
      if (!['theo', 'affair_partner'].every(id => observation.visibleActorIds.includes(id))) return reject('recognition_conditions_unmet');
      if (state.recognized) return reject('already_recognized');
      state.recognized = true;
      if (authored.recognitionFactId) learn('maya', authored.recognitionFactId, 'recognition');
      return commit('recognized', { actorId: 'maya', visibleActorIds: ['theo', 'affair_partner'] });
    },
    // Eligibility/confirmation rule is evaluated by trusted server caller. A claim is insufficient.
    confirmFact(command) {
      const stale = fence(command); if (stale) return stale;
      if (!own(authored.facts, command.factId)) return reject('unknown_fact');
      if (!Array.isArray(command.recipients) || !command.recipients.length || !command.recipients.every(id => id === 'player' || NPC_IDS.includes(id))) return reject('invalid_recipients');
      if (!isId(command.source)) return reject('invalid_confirmation_source');
      for (const recipientId of new Set(command.recipients)) learn(recipientId, command.factId, command.source);
      return commit('fact_confirmed', { factId: command.factId, recipients: [...new Set(command.recipients)], source: command.source });
    },
    recordClaim(command) {
      const stale = fence(command); if (stale) return stale;
      if (!NPC_IDS.includes(command.speakerId) || typeof command.text !== 'string' || !command.text.trim() || command.text.length > 4000) return reject('invalid_claim');
      if (!Array.isArray(command.audience) || !command.audience.every(id => id === 'player' || NPC_IDS.includes(id))) return reject('invalid_audience');
      const claim = { speakerId: command.speakerId, text: command.text, audience: [...new Set(command.audience)], knownLie: command.knownLie === true, loopId: state.loopId };
      for (const id of new Set([command.speakerId, ...command.audience.filter(id => NPC_IDS.includes(id))])) {
        // Hearing speech does not reveal the speaker's private intent to deceive.
        state.npcs[id].claims.push({ ...copy(claim), knownLie: id === command.speakerId ? claim.knownLie : null });
      }
      return commit('claim_recorded', { claim });
    },
    reset(command) {
      const stale = fence(command); if (stale) return stale;
      loopIndex++; revision++; initialize();
      return { accepted: true, loopId: state.loopId, revision };
    },
  });
}

import { createEncounter } from './encounter.mjs';

/** Newly authored demo content; this is not a transcription of the source board. */
export function createDemoDefinition(id = 'before-the-drop-demo') {
  return {
    id, initialMood: 'Aggressive', recognitionFactId: 'affair_seen',
    facts: {
      affair_seen: { text: 'Theo is having an affair with the person beside him at the VIP seating.' },
      exposure_fear: { text: 'Luca personally heard Theo demand that the affair stay out of public view.' },
      shove_seen: { text: 'Theo shoved Luca while reaching for Maya’s phone; Luca fell backward against the low VIP table.' },
      safe_exchange: { text: 'Maya and Theo completed a private exchange, kept their distance, and separated with Luca mediating.' },
    },
    npcs: {
      maya: { role: 'Player’s friend; photographer', objective: 'Understand what she saw and preserve her choice about the evidence.', personality: 'Warm, outspoken under pressure.', knownFactIds: [], disclosureRules: ['Do not pretend to know about the affair before recognition.'] },
      ren: { role: 'DJ', objective: 'Finish the set and keep people safe.', personality: 'Precise, observant, dryly amused.', knownFactIds: [], disclosureRules: ['You can see the confrontation from the booth, but cannot hear every conversation.'] },
      luca: { role: 'Bartender', objective: 'Protect patrons and prevent physical intimidation.', personality: 'Measured and guarded under aggression.', knownFactIds: ['exposure_fear'], disclosureRules: ['Share the direct observation about Theo’s fear of exposure during a calm, direct question under Intimate music.'] },
      theo: { role: 'Socialite', objective: 'Keep the affair from becoming a public spectacle.', personality: 'Charming; defensive when exposed.', knownFactIds: ['affair_seen', 'exposure_fear'], permittedLies: ['May initially deny the affair; a denial never changes the authored truth.'] },
    },
    authorizeAction(command) {
      return (command.actorId === 'maya' && ['wait', 'follow'].includes(command.type)) ||
        (command.actorId === 'ren' && command.type === 'request_music');
    },
  };
}

/** Same interface as createEncounter plus trusted advance/observeStage/discloseClue.
 * The server world adapter owns clock, pause state and staging observations. None of
 * these three methods is an unrestricted client/model RPC. submitAction is still
 * fenced by an authenticated NPC principal and authored eligibility below.
 */
export function createDemoScenario(definition = createDemoDefinition(), { durationSeconds = 180 } = {}) {
  if (!Number.isFinite(durationSeconds) || durationSeconds < 30 || durationSeconds > 900) throw new TypeError('Invalid demo duration');
  const base = createEncounter(definition);
  let revision = 0;
  let local;
  function initialize() {
    local = { elapsedSeconds: 0, recognizedAt: null, phase: 'exploring', recording: false,
      catastrophe: null, victory: false, privatePlan: false, distanceAccepted: false,
      mediated: false, separated: false, stage: { theoAtMaya: false, lucaBetween: false, renCanSee: false, allInMediation: false } };
  }
  initialize();
  const reject = reason => ({ accepted: false, reason, loopId: base.snapshot().loopId, revision });
  function fence(command) {
    if (command?.loopId !== base.snapshot().loopId) return reject('stale_loop');
    if (command.revision !== revision) return reject('stale_revision');
    return null;
  }
  const terminal = () => local.catastrophe !== null || local.victory || local.phase === 'unresolved';
  function commit(type, payload = {}) {
    revision++;
    return { accepted: true, loopId: base.snapshot().loopId, revision, event: { ...structuredClone(payload), type, revision, loopId: base.snapshot().loopId } };
  }
  function runBase(method, command, principal) {
    const stale = fence(command); if (stale) return stale;
    if (terminal() && method !== 'reset') return reject('encounter_ended');
    const outcome = base[method]({ ...command, revision: base.snapshot().revision }, principal);
    if (!outcome.accepted) return { ...outcome, revision };
    if (method === 'reset') initialize();
    if (method === 'observeVisibility') {
      local.recognizedAt = local.elapsedSeconds;
      local.recording = true;
      local.phase = 'recording';
    }
    return commit(outcome.event?.type ?? method);
  }
  function learn(factId, recipients, source) {
    const s = base.snapshot();
    return base.confirmFact({ loopId: s.loopId, revision: s.revision, factId, recipients, source });
  }
  function evaluate() {
    if (terminal()) return;
    if (local.mediated) local.phase = local.separated ? 'resolved' : 'separating';
    else if (local.recognizedAt !== null) {
      const age = local.elapsedSeconds - local.recognizedAt;
      // Movement milestones are logical requests; violence also needs trusted physical staging.
      if (age >= 4) local.phase = 'theo_approaching';
      if (age >= 10 && local.stage.theoAtMaya) local.phase = 'phone_dispute';
      if (age >= 14 && local.stage.theoAtMaya && local.recording && !local.distanceAccepted) local.phase = 'luca_intervening';
      if (age >= 20 && local.stage.theoAtMaya && local.stage.lucaBetween && local.stage.renCanSee && local.recording && !local.distanceAccepted) {
        local.catastrophe = { victimId: 'luca', responsibleId: 'theo', cause: 'shove_into_low_table' };
        local.phase = 'catastrophe';
        learn('shove_seen', ['player', 'ren', 'maya', 'theo'], 'witnessed_demo_catastrophe');
        return;
      }
    }
    if (local.elapsedSeconds >= durationSeconds) {
      local.victory = local.mediated && local.separated && !local.recording && local.distanceAccepted;
      local.phase = local.victory ? 'victory' : 'unresolved';
      if (local.victory) learn('safe_exchange', ['player', 'maya', 'theo', 'luca'], 'completed_mediation');
    }
  }
  function snapshot() {
    const s = base.snapshot();
    const actors = s.actors;
    if (['theo_approaching', 'phone_dispute', 'luca_intervening'].includes(local.phase)) actors.theo.action = { type: local.distanceAccepted ? 'keep_distance' : 'approach', targetId: 'maya' };
    if (local.phase === 'luca_intervening') actors.luca.action = { type: 'intervene', targetId: 'maya' };
    if (local.distanceAccepted) actors.theo.action = { type: 'keep_distance', targetId: 'maya' };
    if (local.distanceAccepted && !local.recording && !local.mediated) actors.luca.action = { type: 'approach', targetId: 'maya' };
    if (local.catastrophe) actors.luca.action = { type: 'fall' };
    if (local.mediated) {
      actors.theo.action = { type: 'separate', targetId: 'vip' };
      actors.maya.action = { type: 'separate', targetId: 'player' };
    }
    return { ...s, revision, actors, phase: local.phase, recording: local.recording,
      catastrophe: structuredClone(local.catastrophe), victory: local.victory,
      scenario: structuredClone({ ...local, durationSeconds }) };
  }
  const requests = new Set();
  return Object.freeze({
    snapshot,
    context(npcId) {
      const c = base.context(npcId);
      // Only this character's own behavior and accepted commitments survive a
      // conversation switch. Never attach the private scenario record here.
      const ownState = npcId === 'maya' ? { recording: local.recording, privateApproachAgreed: local.privatePlan }
        : npcId === 'theo' ? { distanceAgreed: local.distanceAccepted }
        : npcId === 'luca' ? { mediationAccepted: local.mediated } : {};
      return { ...c, revision, action: snapshot().actors[npcId].action, ownState };
    },
    reset(command) { const outcome = runBase('reset', command); if (outcome.accepted) requests.clear(); return outcome; },
    observeVisibility: command => runBase('observeVisibility', command),
    confirmFact: command => runBase('confirmFact', command),
    recordClaim: command => runBase('recordClaim', command),
    submitAction(command, principal) {
      const stale = fence(command); if (stale) return stale;
      if (terminal()) return reject('encounter_ended');
      if (command.actorId !== principal?.npcId) return reject('unauthorized_actor');
      if (typeof command.requestId !== 'string' || !/^[a-zA-Z0-9_.:-]{1,128}$/.test(command.requestId)) return reject('invalid_request_id');
      if (requests.has(command.requestId)) return reject('duplicate_request');
      if (['wait', 'follow', 'request_music'].includes(command.type)) {
        const outcome = runBase('submitAction', command, principal);
        if (outcome.accepted) requests.add(command.requestId);
        return outcome;
      }
      const s = base.snapshot();
      const clue = s.playerDiscoveries.some(d => d.factId === 'exposure_fear');
      if (command.type === 'ask_about_exposure') {
        if (command.actorId !== 'luca' || s.mood !== 'Intimate') return reject('disclosure_conditions_unmet');
        const text = base.context('luca').knownFacts.find(fact => fact.factId === 'exposure_fear')?.text;
        if (!text) return reject('unavailable_authored_fact');
        const learned = learn('exposure_fear', ['player'], 'luca_direct_observation');
        if (!learned.accepted) return reject(learned.reason);
        requests.add(command.requestId);
        return commit('clue_disclosed', { factId: 'exposure_fear', text });
      }
      if (command.type === 'agree_private_approach' && command.actorId === 'maya' && s.mood === 'Intimate' && clue && !local.mediated) local.privatePlan = true;
      else if (command.type === 'agree_distance' && command.actorId === 'theo' && s.recognized && local.privatePlan && s.mood === 'Intimate' && !local.mediated) local.distanceAccepted = true;
      else if (command.type === 'stop_recording' && command.actorId === 'maya' && local.distanceAccepted && local.recording) local.recording = false;
      else if (command.type === 'mediate' && command.actorId === 'luca' && s.recognized && local.privatePlan && local.distanceAccepted && !local.recording && local.stage.allInMediation && s.mood === 'Intimate' && !local.mediated) local.mediated = true;
      else return reject('authored_policy_refused');
      requests.add(command.requestId); evaluate(); return commit('scenario_action_committed');
    },
    discloseClue(command) {
      const stale = fence(command); if (stale) return stale;
      if (terminal()) return reject('encounter_ended');
      // Trusted dialogue adjudicator verifies the player's direct question, not a transcript substring.
      if (command.speakerId !== 'luca' || command.directQuestion !== true || base.snapshot().mood !== 'Intimate') return reject('disclosure_conditions_unmet');
      learn('exposure_fear', ['player'], 'luca_direct_observation');
      return commit('clue_disclosed');
    },
    observeStage(command) {
      const stale = fence(command); if (stale) return stale;
      if (terminal()) return reject('encounter_ended');
      const allowed = ['theoAtMaya', 'lucaBetween', 'renCanSee', 'allInMediation', 'separated'];
      if (!command.stage || Object.keys(command.stage).length === 0 || Object.entries(command.stage).some(([key, value]) => !allowed.includes(key) || typeof value !== 'boolean')) return reject('invalid_stage');
      if (command.stage.separated !== undefined) local.separated = local.mediated && command.stage.separated;
      for (const key of allowed.filter(k => k !== 'separated')) if (command.stage[key] !== undefined) local.stage[key] = command.stage[key];
      evaluate(); return commit('stage_observed');
    },
    advance(command) {
      const stale = fence(command); if (stale) return stale;
      if (terminal()) return reject('encounter_ended');
      if (!Number.isFinite(command.seconds) || command.seconds < 0 || command.seconds > durationSeconds || typeof command.paused !== 'boolean') return reject('invalid_clock_tick');
      if (!command.paused) local.elapsedSeconds = Math.min(durationSeconds, local.elapsedSeconds + command.seconds);
      evaluate(); return commit('clock_advanced');
    },
  });
}

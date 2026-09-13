import test from 'node:test';
import assert from 'node:assert/strict';
import { createDemoDefinition, createDemoScenario } from '../src/demo-scenario.mjs';

function fixture() {
  const game = createDemoScenario(createDemoDefinition()); let request = 0;
  const command = data => ({ loopId: game.snapshot().loopId, revision: game.snapshot().revision, ...data });
  const invoke = (method, data) => game[method](command(data));
  const action = (actorId, type, data = {}) => game.submitAction(command({ actorId, type, requestId: `r${++request}`, ...data }), { npcId: actorId });
  const recognize = () => invoke('observeVisibility', { observerId: 'maya', inRecognitionArea: true, visibleActorIds: ['theo', 'affair_partner'] });
  const tick = seconds => invoke('advance', { seconds, paused: false });
  return { game, command, invoke, action, recognize, tick };
}

test('zone entry alone and avoidance until set end cannot cause death or victory', () => {
  const f = fixture();
  f.invoke('observeStage', { stage: { theoAtMaya: true, lucaBetween: true, renCanSee: true } });
  f.tick(180);
  assert.equal(f.game.snapshot().catastrophe, null);
  assert.equal(f.game.snapshot().victory, false);
  assert.equal(f.game.snapshot().phase, 'unresolved');
});

test('default recognition starts recording; causal timeline requires actual staging and retains witnessed clue across reset', () => {
  const f = fixture(); f.recognize();
  assert.equal(f.game.snapshot().recording, true);
  f.tick(20);
  assert.equal(f.game.snapshot().catastrophe, null);
  f.invoke('observeStage', { stage: { theoAtMaya: true, lucaBetween: true, renCanSee: true } });
  assert.equal(f.game.snapshot().catastrophe.victimId, 'luca');
  assert.equal(f.game.snapshot().catastrophe.responsibleId, 'theo');
  const old = f.command({ seconds: 1, paused: false });
  f.invoke('reset', {});
  assert.equal(f.game.snapshot().loopIndex, 2);
  assert.equal(f.game.snapshot().recording, false);
  assert.equal(f.game.snapshot().recognized, false);
  assert.equal(f.game.snapshot().scenario.elapsedSeconds, 0);
  assert.ok(f.game.snapshot().playerDiscoveries.some(d => d.factId === 'shove_seen'));
  assert.ok(!f.game.context('ren').knownFacts.some(d => d.factId === 'shove_seen'));
  assert.equal(f.game.advance(old).reason, 'stale_loop');
});

test('pause consumes no encounter time; accepted wait visibly changes second attempt', () => {
  const f = fixture(); f.recognize();
  f.invoke('advance', { seconds: 180, paused: true });
  assert.equal(f.game.snapshot().scenario.elapsedSeconds, 0);
  f.invoke('reset', {});
  assert.equal(f.action('maya', 'wait').accepted, true);
  assert.deepEqual(f.game.snapshot().actors.maya.action, { type: 'wait' });
  f.tick(15);
  assert.equal(f.game.snapshot().recognized, false);
});

test('a claim does not unlock the private route; quiet music alone does not prevent default violence', () => {
  const f = fixture();
  f.action('ren', 'request_music', { mood: 'Intimate' });
  f.invoke('recordClaim', { speakerId: 'luca', text: 'Theo fears exposure.', audience: ['player'] });
  assert.equal(f.action('maya', 'agree_private_approach').accepted, false);
  f.recognize(); f.invoke('observeStage', { stage: { theoAtMaya: true, lucaBetween: true, renCanSee: true } }); f.tick(20);
  assert.ok(f.game.snapshot().catastrophe);
});

test('active prevention needs evidence, commitments, completed mediation, separation, and set end', () => {
  const f = fixture();
  f.action('maya', 'wait');
  f.action('ren', 'request_music', { mood: 'Intimate' });
  const clue = f.action('luca', 'ask_about_exposure');
  assert.equal(clue.accepted, true);
  assert.equal(clue.event.factId, 'exposure_fear');
  assert.match(clue.event.text, /Luca personally heard Theo/);
  assert.equal(f.action('maya', 'agree_private_approach').accepted, true);
  f.action('maya', 'follow', { targetId: 'player' }); f.recognize();
  assert.equal(f.action('luca', 'mediate').accepted, false);
  assert.equal(f.action('theo', 'agree_distance').accepted, true);
  assert.deepEqual(f.game.snapshot().actors.theo.action, { type: 'keep_distance', targetId: 'maya' });
  assert.equal(f.action('maya', 'stop_recording').accepted, true);
  assert.deepEqual(f.game.snapshot().actors.luca.action, { type: 'approach', targetId: 'maya' });
  assert.equal(f.action('luca', 'mediate').accepted, false);
  f.invoke('observeStage', { stage: { allInMediation: true } });
  assert.equal(f.action('luca', 'mediate').accepted, true);
  assert.equal(f.game.snapshot().victory, false);
  f.invoke('observeStage', { stage: { separated: true } });
  assert.equal(f.game.snapshot().phase, 'resolved');
  f.tick(179); assert.equal(f.game.snapshot().victory, false);
  f.tick(1); assert.equal(f.game.snapshot().victory, true);
});

test('exposure proposal cannot select arbitrary facts, bypass mood, replay, or impersonate Luca', () => {
  const f = fixture();
  assert.equal(f.action('luca', 'ask_about_exposure').reason, 'disclosure_conditions_unmet');
  f.action('ren', 'request_music', { mood: 'Intimate' });
  assert.equal(f.action('maya', 'ask_about_exposure').accepted, false);
  const accepted = f.action('luca', 'ask_about_exposure', { factId: 'shove_seen', directQuestion: false, requestId: 'exposure' });
  assert.equal(accepted.accepted, true);
  assert.equal(accepted.event.factId, 'exposure_fear');
  assert.ok(!f.game.snapshot().playerDiscoveries.some(d => d.factId === 'shove_seen'));
  assert.equal(f.action('luca', 'ask_about_exposure', { requestId: 'exposure' }).reason, 'duplicate_request');
});

test('actor spoofing, stale revisions, invalid clock and incomplete separation are rejected or lose', () => {
  const f = fixture(); const stale = f.command({ seconds: 1, paused: false });
  f.action('maya', 'wait');
  assert.equal(f.game.advance(stale).reason, 'stale_revision');
  assert.equal(f.invoke('advance', { seconds: NaN, paused: false }).reason, 'invalid_clock_tick');
  assert.equal(f.game.submitAction(f.command({ actorId: 'maya', type: 'agree_private_approach', requestId: 'spoof' }), { npcId: 'theo' }).reason, 'unauthorized_actor');
  f.invoke('observeStage', { stage: { separated: true } });
  assert.equal(f.game.snapshot().scenario.separated, false);
  assert.equal(f.invoke('observeStage', { stage: { invented: true } }).reason, 'invalid_stage');
});

test('own commitments persist in fresh NPC context without crossing character or loop boundaries', () => {
  const f = fixture();
  const initial = {
    maya: { recording: false, privateApproachAgreed: false },
    theo: { distanceAgreed: false }, luca: { mediationAccepted: false }, ren: {},
  };
  for (const [npcId, expected] of Object.entries(initial)) assert.deepEqual(f.game.context(npcId).ownState, expected);
  assert.equal(f.action('maya', 'agree_private_approach').accepted, false);
  assert.deepEqual(f.game.context('maya').ownState, initial.maya);
  assert.equal(f.action('ren', 'request_music', { mood: 'Intimate' }).accepted, true);
  assert.equal(f.action('luca', 'ask_about_exposure').accepted, true);
  assert.equal(f.action('maya', 'agree_private_approach').accepted, true);
  assert.equal(f.recognize().accepted, true);
  assert.deepEqual(f.game.context('maya').ownState, { recording: true, privateApproachAgreed: true });
  assert.equal(f.action('theo', 'agree_distance').accepted, true);
  assert.equal(f.action('maya', 'stop_recording').accepted, true);
  assert.equal(f.invoke('observeStage', { stage: { allInMediation: true } }).accepted, true);
  assert.equal(f.action('luca', 'mediate').accepted, true);
  const committed = {
    maya: { recording: false, privateApproachAgreed: true },
    theo: { distanceAgreed: true }, luca: { mediationAccepted: true }, ren: {},
  };
  for (const [npcId, expected] of Object.entries(committed)) {
    const context = f.game.context(npcId);
    assert.deepEqual(context.ownState, expected);
    assert.equal(Object.hasOwn(context, 'scenario'), false);
    context.ownState.injected = true;
    assert.deepEqual(f.game.context(npcId).ownState, expected);
  }
  assert.equal(f.invoke('reset', {}).accepted, true);
  for (const [npcId, expected] of Object.entries(initial)) assert.deepEqual(f.game.context(npcId).ownState, expected);
  assert.ok(f.game.snapshot().playerDiscoveries.some(d => d.factId === 'exposure_fear'));
});

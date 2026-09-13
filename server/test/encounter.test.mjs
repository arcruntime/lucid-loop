import assert from 'node:assert/strict';
import test from 'node:test';
import { createEncounter } from '../src/encounter.mjs';
const content = () => ({
  facts: { affair: { text: 'Authored affair fact' }, secret: { text: 'Luca private fact' } },
  recognitionFactId: 'affair', npcs: { luca: { knownFactIds: ['secret'], secrets: ['Private motive'] } },
  authorizeAction: () => true,
});
const current = game => { const { loopId, revision } = game.snapshot(); return { loopId, revision }; };
const action = (game, changes = {}) => game.submitAction({ ...current(game), requestId: 'req1', actorId: 'maya', type: 'wait', ...changes }, { npcId: changes.actorId ?? 'maya' });

test('NPC projection excludes other secrets, private claims and player discoveries', () => {
  const game = createEncounter(content());
  game.confirmFact({ ...current(game), factId: 'affair', recipients: ['player'], source: 'witness_event' });
  game.recordClaim({ ...current(game), speakerId: 'luca', text: 'I know nothing', knownLie: true, audience: ['player'] });
  assert.equal(game.context('theo').knownFacts.length, 0);
  assert.equal(game.context('theo').claims.length, 0);
  assert.deepEqual(game.context('theo').secrets, []);
  assert.equal(game.context('luca').claims[0].knownLie, true);
  assert.equal(game.context('luca').knownFacts[0].text, 'Luca private fact');
  assert.equal(game.context('luca').knownFacts.length, 1);
  const snapshot = game.snapshot(); snapshot.npcs.luca.knowledge.length = 0;
  const projection = game.context('luca'); projection.secrets.push('corruption');
  assert.equal(game.context('luca').knownFacts.length, 1);
  assert.equal(game.context('luca').secrets.length, 1);
});

test('claims never confirm truth and unknown fact injection leaves state unchanged', () => {
  const game = createEncounter(content());
  game.recordClaim({ ...current(game), speakerId: 'theo', text: 'Made up revelation', knownLie: true, audience: ['maya', 'player'] });
  assert.equal(game.context('maya').claims.length, 1);
  assert.equal(game.context('maya').claims[0].knownLie, null);
  assert.equal(game.context('theo').claims[0].knownLie, true);
  assert.equal(game.context('maya').knownFacts.length, 0);
  assert.deepEqual(game.snapshot().playerDiscoveries, []);
  const before = game.snapshot();
  assert.equal(game.confirmFact({ ...current(game), factId: 'invented', recipients: ['player'], source: 'model' }).reason, 'unknown_fact');
  assert.deepEqual(game.snapshot(), before);
});

test('wait/follow and music commit only legal authored decisions; copies cannot alter state', () => {
  const game = createEncounter(content());
  assert.equal(action(game).accepted, true);
  assert.equal(game.snapshot().actors.maya.action.type, 'wait');
  assert.equal(action(game, { requestId: 'follow', type: 'follow', targetId: 'player' }).accepted, true);
  assert.deepEqual(game.snapshot().actors.maya.action, { type: 'follow', targetId: 'player' });
  assert.equal(action(game, { requestId: 'music', actorId: 'ren', type: 'request_music', mood: 'Intimate' }).accepted, true);
  assert.equal(game.context('theo').mood, 'Intimate');
  const before = game.snapshot();
  assert.equal(action(game, { requestId: 'bad_music', actorId: 'ren', type: 'request_music', mood: 'Euphoric' }).accepted, false);
  assert.equal(action(game, { requestId: 'bad_actor', actorId: 'theo' }).accepted, false);
  assert.deepEqual(game.snapshot(), before);
  const closed = createEncounter(); assert.equal(action(closed).reason, 'authored_policy_refused');
});

test('authenticated actor, duplicate request and revision fences prevent repeated or stale commits', () => {
  const game = createEncounter(content()); const fence = current(game);
  assert.equal(game.submitAction({ ...fence, requestId: 'spoof', actorId: 'ren', type: 'request_music', mood: 'Intimate' }, { npcId: 'maya' }).reason, 'unauthorized_actor');
  assert.equal(action(game).accepted, true);
  assert.equal(action(game).reason, 'duplicate_request');
  const before = game.snapshot();
  assert.equal(game.submitAction({ ...fence, requestId: 'old', actorId: 'maya', type: 'wait' }, { npcId: 'maya' }).reason, 'stale_revision');
  assert.deepEqual(game.snapshot(), before);
});

test('recognition needs Maya zone and both visible participants, and triggers once per loop without death', () => {
  const game = createEncounter(content());
  for (const obs of [
    { observerId: 'player', inRecognitionArea: true, visibleActorIds: ['theo', 'affair_partner'] },
    { observerId: 'maya', inRecognitionArea: false, visibleActorIds: ['theo', 'affair_partner'] },
    { observerId: 'maya', inRecognitionArea: true, visibleActorIds: ['theo'] },
  ]) assert.equal(game.observeVisibility({ ...current(game), ...obs }).accepted, false);
  const observation = { observerId: 'maya', inRecognitionArea: true, visibleActorIds: ['theo', 'affair_partner'] };
  assert.equal(game.observeVisibility({ ...current(game), ...observation }).accepted, true);
  assert.equal(game.observeVisibility({ ...current(game), ...observation }).reason, 'already_recognized');
  assert.equal(game.context('maya').knownFacts[0].factId, 'affair');
  assert.equal(game.context('theo').knownFacts.length, 0);
  assert.equal(game.snapshot().catastrophe, null);
  assert.equal(game.snapshot().recording, false);
  assert.equal(game.snapshot().victory, false);
});

test('reset retains player learning but restores NPC memory, mood, follow and recognition; rejects late results', () => {
  const game = createEncounter(content());
  game.confirmFact({ ...current(game), factId: 'affair', recipients: ['player', 'maya'], source: 'observation' });
  game.recordClaim({ ...current(game), speakerId: 'maya', text: 'I saw them', audience: ['player'] });
  action(game);
  action(game, { requestId: 'music', actorId: 'ren', type: 'request_music', mood: 'Intimate' });
  const old = current(game); assert.equal(game.reset(old).accepted, true);
  assert.equal(game.snapshot().loopIndex, 2);
  assert.notEqual(game.snapshot().loopId, old.loopId);
  assert.equal(game.snapshot().mood, 'Aggressive');
  assert.equal(game.snapshot().recognized, false);
  assert.equal(game.snapshot().actors.maya.action.type, 'follow');
  assert.equal(game.snapshot().playerDiscoveries[0].factId, 'affair');
  assert.equal(game.context('maya').knownFacts.length, 0);
  assert.equal(game.context('maya').claims.length, 0);
  assert.equal(game.context('luca').knownFacts[0].factId, 'secret');
  assert.equal(game.submitAction({ ...old, requestId: 'late', actorId: 'maya', type: 'wait' }, { npcId: 'maya' }).reason, 'stale_loop');
  assert.equal(game.reset(old).reason, 'stale_loop');
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { createGameSessions } from '../src/game-sessions.mjs';
const definitionFactory = () => ({ facts: { hidden: { text: 'Secret author fact' }, clue: { text: 'Player fact' } }, npcs: { luca: { knownFactIds: ['hidden'], secrets: ['private motive'] } }, authorizeAction: () => true });
const fence = snapshot => ({ loopId: snapshot.loopId, revision: snapshot.revision });
const delta = { type: 'session.input_transcript.delta', delta: 'Private conversation', event_id: 'e1', start_ms: 0, end_ms: 100 };

test('world liveness checks do not renew idle games or leak undiscovered clue text', () => {
  let time = 0;
  const registry = createGameSessions({ definitionFactory, idleTtlMs: 100, now: () => time });
  const { credentials, snapshot } = registry.create();
  const revealed = registry.trustedWorld(credentials, 'confirmFact', { ...fence(snapshot), factId: 'clue', recipients: ['player'], source: 'inspection' });
  assert.equal(revealed.snapshot.playerDiscoveries[0].text, 'Player fact');
  assert.doesNotMatch(JSON.stringify(revealed.snapshot), /Secret author fact/);
  time = 80; assert.equal(registry.isAlive(credentials), true);
  time = 101; assert.equal(registry.isAlive(credentials), false);
});

test('unpredictable credentials isolate games; public projection hides secrets and private event log', () => {
  const registry = createGameSessions({ definitionFactory }); const a = registry.create(); const b = registry.create();
  assert.notEqual(a.credentials.resumeToken, b.credentials.resumeToken);
  assert.equal(a.credentials.resumeToken.length, 43);
  assert.equal(registry.resume({ gameId: b.credentials.gameId, resumeToken: a.credentials.resumeToken }).ok, false);
  const attach = registry.attach(a.credentials, 'luca');
  assert.equal(registry.npcContext(b.credentials, attach.lease.leaseId).ok, false);
  assert.match(JSON.stringify(registry.npcContext(a.credentials, attach.lease.leaseId)), /private motive/);
  const safe = JSON.stringify(registry.publicSnapshot(a.credentials));
  assert.doesNotMatch(safe, /private motive|Secret author fact|hidden|knowledge|claims|events/);
  assert.equal(registry.trustedFull(a.credentials).snapshot.npcs.luca.knowledge.length, 1);
});

test('switch, reconnect and detach fence old leases without deleting same-loop history', () => {
  const registry = createGameSessions({ definitionFactory }); const { credentials } = registry.create();
  const first = registry.attach(credentials, 'maya');
  assert.equal(registry.appendTranscript(credentials, first.lease.leaseId, delta).appended, true);
  const second = registry.attach(credentials, 'theo');
  assert.equal(registry.appendTranscript(credentials, first.lease.leaseId, { ...delta, event_id: 'late' }).ok, false);
  assert.equal(registry.detach(credentials, first.lease.leaseId).ok, false);
  assert.equal(registry.npcContext(credentials, second.lease.leaseId).history.length, 0);
  const third = registry.attach(credentials, 'maya');
  assert.equal(third.lease.generation, 2);
  assert.equal(registry.npcContext(credentials, third.lease.leaseId).history.length, 1);
  assert.equal(registry.detach(credentials, third.lease.leaseId).ok, true);
  assert.equal(registry.npcContext(credentials, third.lease.leaseId).ok, false);
  assert.equal(registry.history(credentials).history.fragments.length, 1);
});

test('reset retains player discoveries/history but clears NPC context and invalidates old lease', () => {
  const registry = createGameSessions({ definitionFactory }); const { credentials } = registry.create();
  const attached = registry.attach(credentials, 'maya'); const lease = attached.lease.leaseId;
  registry.appendTranscript(credentials, lease, delta);
  const confirmed = registry.trustedMutation(credentials, lease, 'confirmFact', { ...fence(attached.snapshot), factId: 'clue', recipients: ['player', 'maya'], source: 'witness' });
  assert.equal(confirmed.outcome.accepted, true);
  const reset = registry.reset(credentials, fence(confirmed.snapshot));
  assert.equal(reset.outcome.accepted, true);
  assert.equal(reset.snapshot.playerDiscoveries.length, 1);
  assert.equal(registry.submitAction(credentials, lease, { ...fence(reset.snapshot), requestId: 'late', type: 'wait', actorId: 'maya' }).ok, false);
  const fresh = registry.attach(credentials, 'maya'); const context = registry.npcContext(credentials, fresh.lease.leaseId);
  assert.equal(context.context.knownFacts.length, 0);
  assert.equal(context.history.length, 0);
  assert.equal(registry.history(credentials, { loopIndex: 1 }).history.fragments.length, 1);
});

test('resuming same game preserves accepted actions and lease authority rejects NPC spoof', () => {
  const registry = createGameSessions({ definitionFactory }); const { credentials } = registry.create();
  const attached = registry.attach(credentials, 'maya');
  const accepted = registry.submitAction(credentials, attached.lease.leaseId, { ...fence(attached.snapshot), requestId: 'wait', actorId: 'maya', type: 'wait' });
  assert.equal(accepted.outcome.accepted, true);
  assert.equal(registry.resume(credentials).snapshot.actors.maya.action.type, 'wait');
  const spoof = registry.submitAction(credentials, attached.lease.leaseId, { ...fence(accepted.snapshot), requestId: 'spoof', actorId: 'ren', type: 'request_music', mood: 'Intimate' });
  assert.equal(spoof.outcome.reason, 'unauthorized_actor');
});

test('capacity and explicit expiry bound registry; revision and transcript limits bound retained records', () => {
  let time = 0;
  const registry = createGameSessions({ definitionFactory, maxGames: 1, idleTtlMs: 100, maxRevisions: 1, transcriptOptions: { maxFragments: 1 }, now: () => time });
  const { credentials } = registry.create(); assert.equal(registry.create().reason, 'capacity');
  const attached = registry.attach(credentials, 'maya'); const lease = attached.lease.leaseId;
  registry.appendTranscript(credentials, lease, delta);
  assert.equal(registry.appendTranscript(credentials, lease, { ...delta, event_id: 'e2' }).appended, false);
  const changed = registry.submitAction(credentials, lease, { ...fence(attached.snapshot), requestId: 'wait', actorId: 'maya', type: 'wait' });
  assert.equal(registry.reset(credentials, fence(changed.snapshot)).reason, 'state_capacity');
  time = 101; assert.equal(registry.cleanup(), 1);
  assert.equal(registry.resume(credentials).ok, false);
  assert.equal(registry.create().ok, true);
});

test('authenticated subscriptions emit safe state and unsubscribe/expiry cleanly; world updates need no voice', () => {
  let time = 0;
  const registry = createGameSessions({ definitionFactory, idleTtlMs: 100, now: () => time });
  const a = registry.create(); const b = registry.create(); const events = [];
  assert.equal(registry.subscribe({ ...a.credentials, resumeToken: b.credentials.resumeToken }, () => {}).ok, false);
  const subscription = registry.subscribe(a.credentials, event => events.push(event));
  assert.equal(registry.authorize(a.credentials).ok, true);
  const confirmed = registry.trustedWorld(a.credentials, 'confirmFact', { ...fence(a.snapshot), factId: 'clue', recipients: ['player'], source: 'world_observed' });
  assert.equal(confirmed.outcome.accepted, true);
  assert.equal(events.length, 1);
  assert.equal(events[0].snapshot.playerDiscoveries.length, 1);
  assert.doesNotMatch(JSON.stringify(events), /private motive|Secret author fact|hidden|knowledge|claims/);
  subscription.unsubscribe();
  registry.reset(a.credentials, fence(confirmed.snapshot));
  assert.equal(events.length, 1);
  registry.subscribe(a.credentials, event => events.push(event));
  time = 101; registry.cleanup();
  assert.equal(events[1].type, 'expired');
  assert.equal(registry.publicState(a.credentials).ok, false);
});

test('typed and spoken history use non-authorizing correlation IDs distinct from active leases', () => {
  const registry = createGameSessions({ definitionFactory });
  const { credentials } = registry.create();
  const first = registry.attach(credentials, 'maya');
  const firstLease = first.lease.leaseId;
  assert.equal(registry.appendTranscript(credentials, firstLease, delta).appended, true);
  assert.equal(registry.appendTyped(credentials, firstLease, { requestId: 'typed1', text: 'Please wait.' }).appended, true);
  const firstHistory = registry.history(credentials).history.fragments;
  assert.equal(firstHistory.length, 2);
  const firstCorrelation = firstHistory[0].sessionId;
  assert.match(firstCorrelation, /^conversation_[0-9a-f-]{36}$/);
  assert.equal(firstHistory[1].sessionId, firstCorrelation);
  assert.notEqual(firstCorrelation, firstLease);
  assert.equal(JSON.stringify(firstHistory).includes(firstLease), false);
  assert.equal(registry.npcContext(credentials, firstCorrelation).ok, false);
  assert.equal(registry.appendTyped(credentials, firstCorrelation, { requestId: 'forged', text: 'No authority.' }).ok, false);
  assert.equal(registry.npcContext(credentials, firstLease).ok, true);
  assert.equal(registry.detach(credentials, firstLease).ok, true);
  const second = registry.attach(credentials, 'maya');
  const secondLease = second.lease.leaseId;
  // Provider IDs/request IDs can repeat in a new conversation without deduplicating across sessions.
  assert.equal(registry.appendTranscript(credentials, secondLease, delta).appended, true);
  assert.equal(registry.appendTyped(credentials, secondLease, { requestId: 'typed1', text: 'New conversation.' }).appended, true);
  const history = registry.history(credentials).history.fragments;
  assert.equal(history.length, 4);
  assert.notEqual(history[2].sessionId, firstCorrelation);
  assert.equal(history[2].sessionId, history[3].sessionId);
  for (const lease of [firstLease, secondLease]) assert.equal(JSON.stringify(history).includes(lease), false);
  assert.equal(registry.npcContext(credentials, history[2].sessionId).ok, false);
  assert.equal(registry.npcContext(credentials, secondLease).ok, true);
  assert.equal(registry.appendTranscript(credentials, firstLease, { ...delta, event_id: 'old' }).ok, false);
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { createGameSessions } from '../src/game-sessions.mjs';
import { createDemoDefinition, createDemoScenario } from '../src/demo-scenario.mjs';
import { CLUB_WORLD_CONFIG, createEncounterWorld } from '../src/encounter-world.mjs';

function fixture(config = CLUB_WORLD_CONFIG) {
  const registry = createGameSessions({ definitionFactory: createDemoDefinition, encounterFactory: createDemoScenario, maxRevisions: 20_000 });
  const { credentials } = registry.create();
  const world = createEncounterWorld({ registry, credentials, config });
  const state = () => registry.trustedFull(credentials).snapshot;
  let request = 0, sequence = 0;
  function action(actorId, type, extra = {}) {
    const { lease } = registry.attach(credentials, actorId);
    const s = state();
    const result = registry.submitAction(credentials, lease.leaseId, { loopId: s.loopId, revision: s.revision, requestId: `request-${++request}`, actorId, type, ...extra });
    registry.detach(credentials, lease.leaseId);
    assert.equal(result.outcome?.accepted, true, JSON.stringify(result));
    return result;
  }
  const move = (x, z) => world.input({ type: 'move_to', loopId: state().loopId, sequence: sequence++, destination: { x, z } });
  const run = seconds => { for (let i = 0; i < Math.round(seconds * 10); i++) world.tick(0.1); };
  return { registry, credentials, world, state, action, move, run };
}

test('input only accepts sequenced player destinations; collision routes and speed are bounded', () => {
  const f = fixture(); const loopId = f.state().loopId;
  for (const destination of [{ x: NaN, z: 1 }, { x: Infinity, z: 1 }, { x: 1000, z: 0 }, { x: 9, z: -3 }]) {
    assert.equal(f.world.input({ type: 'move_to', loopId, sequence: 0, destination }).accepted, false);
  }
  assert.equal(f.world.input({ type: 'move_to', loopId, sequence: 0, destination: { x: 0, z: 0 }, actorId: 'theo' }).reason, 'invalid_input');
  assert.equal(f.world.input({ type: 'observeStage', loopId, sequence: 0, stage: { lucaBetween: true } }).reason, 'invalid_input');
  assert.equal(f.move(10, -1.7).accepted, true);
  assert.equal(f.world.input({ type: 'stop', loopId, sequence: 0 }).reason, 'stale_sequence');
  let previous = f.world.snapshot().actors.player.position;
  for (let i = 0; i < 100; i++) {
    f.world.tick(0.1); const next = f.world.snapshot().actors.player.position;
    assert.ok(Math.hypot(next.x - previous.x, next.z - previous.z) <= 0.3700001);
    for (const r of CLUB_WORLD_CONFIG.obstacles) assert.ok(!(next.x >= r.minX - 0.35 && next.x <= r.maxX + 0.35 && next.z >= r.minZ - 0.35 && next.z <= r.maxZ + 0.35), r.id);
    previous = next;
  }
  assert.deepEqual(previous, { x: 10, z: -1.7 });
});

test('pauses stop movement and clock, and catch-up drops stalls', () => {
  const f = fixture(); f.move(0, 0); const before = f.world.snapshot();
  for (const reason of ['paused', 'voiceActive', 'suspended']) assert.equal(f.world.tick(60, { [reason]: true }).steps, 0);
  assert.deepEqual(f.world.snapshot(), before);
  assert.equal(f.world.tick(60).steps, 5);
  assert.equal(f.state().scenario.elapsedSeconds, 0.5);
  assert.equal(f.world.tick(NaN).ok, false);
  assert.equal(f.world.tick(0.1, { paused: 'false' }).ok, false);
});

test('default opening derives recognition, actual intervention at table, and a frozen catastrophe', () => {
  const f = fixture(); assert.equal(f.move(10, -1.7).accepted, true); f.run(40);
  assert.equal(f.state().recognized, true);
  assert.equal(f.state().catastrophe?.victimId, 'luca');
  assert.equal(f.state().scenario.stage.lucaBetween, true);
  assert.equal(f.world.snapshot().actors.luca.motion, 'fallen');
  assert.ok(f.state().scenario.elapsedSeconds >= 20);
  const frozen = f.world.snapshot(); f.run(10); assert.deepEqual(f.world.snapshot(), frozen);
  assert.equal(f.world.canConverse('luca').reason, 'encounter_ended');
});

test('recognition requires both sightlines; no entry or timer invents a catastrophe', () => {
  const config = structuredClone(CLUB_WORLD_CONFIG);
  config.spawns.maya = { x: 9, z: -1.8 };
  config.fall.anchor = { x: 7, z: -5 };
  config.obstacles.push({ id: 'vip-screen', minX: 8.4, maxX: 8.55, minZ: -1.4, maxZ: -1.3, blocksSight: true });
  const f = fixture(config);
  f.action('maya', 'wait'); f.run(180.1);
  assert.equal(f.state().recognized, false);
  assert.equal(f.state().catastrophe, null);
  assert.equal(f.state().phase, 'unresolved');
  assert.equal(f.state().victory, false);
  config.obstacles.pop();
  const visible = fixture(config); visible.action('maya', 'wait'); visible.run(0.1);
  assert.equal(visible.state().recognized, true);
});

test('an actual intervention away from the VIP table cannot invent the fatal collision', () => {
  const config = structuredClone(CLUB_WORLD_CONFIG);
  config.recognitionArea = { ...config.bounds }; config.recognitionDistance = 20;
  const f = fixture(config); f.run(45);
  assert.equal(f.state().recognized, true);
  assert.equal(f.state().scenario.stage.theoAtMaya, true);
  assert.equal(f.state().phase, 'luca_intervening');
  assert.equal(f.state().scenario.stage.lucaBetween, false);
  assert.equal(f.state().catastrophe, null);
});

test('social agreement plus physically assembled mediation and actual separation wins', () => {
  const f = fixture();
  f.action('ren', 'request_music', { mood: 'Intimate' });
  f.action('luca', 'ask_about_exposure'); f.action('maya', 'agree_private_approach');
  f.move(10, -1.7);
  for (let i = 0; i < 150 && !f.state().recognized; i++) f.world.tick(0.1);
  assert.equal(f.state().recognized, true);
  f.action('theo', 'agree_distance'); f.action('maya', 'stop_recording');
  f.run(15);
  assert.equal(f.state().scenario.stage.allInMediation, true);
  f.action('luca', 'mediate');
  assert.equal(f.state().victory, false);
  f.move(0, -8); f.run(20);
  assert.equal(f.state().scenario.separated, true);
  f.run(180);
  assert.equal(f.state().victory, true);
  assert.equal(f.state().catastrophe, null);
});

test('conversation gate covers all four NPCs, and reset restores poses and fences old movement', () => {
  const f = fixture(); f.action('maya', 'wait');
  assert.equal(f.world.canConverse('maya').reason, 'out_of_range');
  assert.equal(f.world.canConverse('affair_partner').reason, 'unknown_npc');
  for (const [id, x, z] of [['maya', -2.8, -3], ['ren', 2.5, 6.2], ['luca', -6, 2], ['theo', 6, -1]]) {
    assert.equal(f.move(x, z).accepted, true); f.run(12); assert.equal(f.world.canConverse(id).ok, true, id);
  }
  const s = f.state(); assert.equal(f.world.reset({ loopId: s.loopId, revision: s.revision }).outcome.accepted, true);
  assert.equal(f.world.input({ type: 'stop', loopId: s.loopId, sequence: 999 }).reason, 'stale_loop');
  assert.deepEqual(f.world.snapshot().actors.player.position, CLUB_WORLD_CONFIG.spawns.player);
  assert.deepEqual(f.world.snapshot().actors.maya.position, CLUB_WORLD_CONFIG.spawns.maya);
  assert.equal(f.world.snapshot().sequence, -1);
  assert.equal(f.world.snapshot().elapsedSeconds, 0);
});

test('second-loop prevention is reachable with every action spoken nearby and voice time paused', t => {
  const f = fixture();
  const trace = [];
  function travel(x, z) {
    assert.equal(f.move(x, z).accepted, true);
    for (let i = 0; i < 300; i++) {
      const p = f.world.snapshot().actors.player.position;
      if (Math.hypot(p.x - x, p.z - z) < 0.02) return;
      f.world.tick(0.1);
    }
    assert.fail(`Destination unreachable: ${x}, ${z}`);
  }
  function speak(id, type, extra = {}) {
    assert.equal(f.world.canConverse(id).ok, true, `${id} cannot hear ${type}: ${JSON.stringify(f.world.snapshot())}`);
    const before = f.world.snapshot();
    assert.equal(f.world.tick(60, { voiceActive: true }).steps, 0);
    f.action(id, type, extra);
    assert.equal(f.world.tick(60, { voiceActive: true }).steps, 0);
    const after = f.world.snapshot();
    assert.equal(after.elapsedSeconds, before.elapsedSeconds);
    for (const actor of Object.keys(before.actors)) assert.deepEqual(after.actors[actor].position, before.actors[actor].position);
    trace.push({ at: Number(after.elapsedSeconds.toFixed(1)), id, type, player: after.actors.player.position });
  }
  // A genuine first-loop observation supplies the retained clue; no trusted facts
  // or staging values are injected by this test.
  f.move(10, -1.7); f.run(40);
  assert.equal(f.state().catastrophe?.victimId, 'luca');
  const ended = f.state();
  assert.equal(f.world.reset({ loopId: ended.loopId, revision: ended.revision }).outcome.accepted, true);
  assert.ok(f.state().playerDiscoveries.some(d => d.factId === 'shove_seen'));
  travel(-2, -4); speak('maya', 'wait');
  travel(2.5, 6.2); speak('ren', 'request_music', { mood: 'Intimate' });
  travel(-6, 2); speak('luca', 'ask_about_exposure');
  travel(-2, -4); speak('maya', 'agree_private_approach'); speak('maya', 'follow', { targetId: 'player' });
  travel(9.7, -1.7);
  for (let i = 0; i < 100 && !f.state().recognized; i++) f.world.tick(0.1);
  travel(9, -1.7);
  speak('theo', 'agree_distance'); speak('maya', 'stop_recording'); speak('maya', 'wait');
  const publicMediationReady = () => {
    const { actors } = f.world.snapshot();
    const s = f.registry.publicState(f.credentials).snapshot;
    const separation = (a, b) => Math.hypot(a.position.x - b.position.x, a.position.z - b.position.z);
    return actors.luca.motion === 'idle' && actors.maya.motion === 'idle' && actors.theo.motion === 'idle' &&
      actors.luca.action.type === 'approach' && actors.maya.action.type === 'wait' && actors.theo.action.type === 'keep_distance' &&
      separation(actors.luca, actors.maya) <= 1.3 && separation(actors.theo, actors.maya) >= 2 && separation(actors.theo, actors.maya) <= 3.5 &&
      s.recording === false && s.mood === 'Intimate';
  };
  for (let i = 0; i < 150 && !publicMediationReady(); i++) f.world.tick(0.1);
  assert.equal(publicMediationReady(), true);
  assert.equal(f.state().scenario.stage.allInMediation, true);
  t.diagnostic(JSON.stringify({ mediationReadyAt: Number(f.world.snapshot().elapsedSeconds.toFixed(1)), actors: f.world.snapshot().actors }));
  travel(8, -1.7);
  speak('luca', 'mediate');
  travel(0, -8); f.run(10);
  assert.equal(f.state().scenario.separated, true, JSON.stringify(trace));
  assert.ok(f.state().scenario.elapsedSeconds < 180, JSON.stringify(trace));
  t.diagnostic(JSON.stringify({ actions: trace, safelySeparatedBy: Number(f.state().scenario.elapsedSeconds.toFixed(1)) }));
  f.run(180);
  assert.equal(f.state().victory, true, JSON.stringify(trace));
  assert.equal(f.state().catastrophe, null);
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { createRelayServer } from '../src/server.mjs';
import { createGameSessions } from '../src/game-sessions.mjs';
import { createDemoDefinition, createDemoScenario } from '../src/demo-scenario.mjs';
import { createEncounterWorld } from '../src/encounter-world.mjs';
import { RelayPlayer } from '../test-support/relay-player.mjs';

const enabled = process.env.RUN_GPT_LIVE_PREVENTION === '1' && Boolean(process.env.OPENAI_API_KEY);

test('opt-in real relay, Live and Responses prevent the second-loop catastrophe through nearby conversations',
  { skip: !enabled, timeout: 480000 }, async t => {
    const registry = createGameSessions({ definitionFactory: createDemoDefinition, encounterFactory: createDemoScenario });
    const relay = createRelayServer({ host: '127.0.0.1', port: 0, apiKey: process.env.OPENAI_API_KEY,
      intentModel: process.env.INTENT_MODEL || 'gpt-5.6-luna', gameSessions: registry, worldFactory: createEncounterWorld });
    await relay.listen();
    const player = new RelayPlayer(`ws://127.0.0.1:${relay.address().port}`, t.signal);
    try {
      await player.start();
      await player.walk(10, -1.7);
      await player.until(() => player.state.phase === 'catastrophe', 'first-loop witnessed catastrophe', 45);
      assert.ok(player.state.playerDiscoveries.some(clue => clue.factId === 'shove_seen'));
      const oldLoop = player.state.loopId;
      player.send(player.game, { type: 'game.reset', loopId: oldLoop, revision: player.state.revision });
      await player.until(() => player.state.loopId !== oldLoop && player.world.loopId === player.state.loopId, 'rewind');
      assert.equal(player.state.loopIndex, 2);
      assert.ok(player.state.playerDiscoveries.some(clue => clue.factId === 'shove_seen'));
      player.sequence = 0;

      await player.walk(-2, -4);
      await player.say('maya', 'Please wait right here while I speak to the others.');
      await player.walk(2.5, 6.2);
      await player.say('ren', 'Please switch the music to the quieter Intimate set.');
      assert.equal(player.state.mood, 'Intimate');
      await player.walk(-6, 2);
      await player.say('luca', 'What does Theo fear about being exposed? What did you personally hear him say?');
      assert.ok(player.state.playerDiscoveries.some(clue => clue.factId === 'exposure_fear'));
      await player.walk(-2, -4);
      await player.say('maya', [
        'Please agree to approach Theo privately and discreetly, rather than making a public accusation.',
        'Now follow me; let us walk over together.',
      ]);
      await player.walk(9.7, -1.7);
      await player.until(() => player.state.recording, 'Maya sees the affair and begins recording', 15);
      await player.walk(9, -1.7);
      await player.say('theo', 'Keep your distance from Maya and do not grab her phone. Please agree to that.');
      await player.say('maya', [
        'Please stop recording and lower your phone. Keep the evidence you already have.',
        'Please wait here while I ask Luca to help us.',
      ]);
      assert.equal(player.state.recording, false);
      // Luca's accepted approach remains normal world movement, not a staged RPC.
      await player.until(() => {
        const { luca, maya, theo } = player.world.actors;
        const distance = (a, b) => Math.hypot(a.position.x - b.position.x, a.position.z - b.position.z);
        return [luca, maya, theo].every(actor => actor.motion === 'idle') && distance(luca, maya) <= 1.3 &&
          distance(theo, maya) >= 2 && distance(theo, maya) <= 3.5 &&
          luca.action.type === 'approach' && maya.action.type === 'wait' && theo.action.type === 'keep_distance' &&
          player.state.recording === false && player.state.mood === 'Intimate';
      }, 'Luca arrives for private mediation', 25);
      await player.walk(8, -1.7);
      await player.say('luca', 'Please mediate this private exchange between Maya and Theo now.');
      await player.walk(0, -8);
      await player.until(() => player.state.phase === 'resolved', 'physical separation', 30);
      assert.equal(player.state.catastrophe, null);
      assert.equal(player.state.victory, false, 'Mediation alone must not end the full set early');
      t.diagnostic(`Second loop safely separated at ${player.state.elapsedSeconds.toFixed(1)} active seconds`);
      await player.until(() => player.state.phase === 'victory', 'survive to the authored 180-second set end', 185);
      assert.equal(player.state.victory, true);
      assert.equal(player.state.catastrophe, null);
      assert.ok(player.state.elapsedSeconds >= 180);
    } finally { player.close(); await relay.close(); }
  });

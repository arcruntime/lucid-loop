import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createRelayServer } from '../src/server.mjs';
import { createGameSessions } from '../src/game-sessions.mjs';
import { createDemoDefinition, createDemoScenario } from '../src/demo-scenario.mjs';
import { createEncounterWorld } from '../src/encounter-world.mjs';
import { RelayPlayer } from '../test-support/relay-player.mjs';

const enabled = process.env.RUN_GPT_LIVE_SPOKEN === '1' && Boolean(process.env.OPENAI_API_KEY);

test('opt-in spoken PCM delegates and commits Maya wait through the real production relay',
  { skip: !enabled, timeout: 100000 }, async t => {
    // Generated from an authored sentence in CI, not a recording of a person.
    // Raw signed little-endian PCM, mono, 24 kHz. No game.text is sent by this test.
    const pcm = await readFile(process.env.LIVE_SPOKEN_PCM);
    assert.ok(pcm.length >= 48000 && pcm.length <= 20 * 48000 && pcm.length % 2 === 0, 'Expected 1–20 seconds of PCM');
    assert.ok(pcm.some(byte => byte !== 0), 'Speech fixture must not be silent');
    const registry = createGameSessions({ definitionFactory: createDemoDefinition, encounterFactory: createDemoScenario });
    const relay = createRelayServer({ host: '127.0.0.1', port: 0, apiKey: process.env.OPENAI_API_KEY,
      intentModel: process.env.INTENT_MODEL || 'gpt-5.6-luna', gameSessions: registry, worldFactory: createEncounterWorld });
    await relay.listen();
    const player = new RelayPlayer(`ws://127.0.0.1:${relay.address().port}`, t.signal);
    let timer;
    try {
      await player.start();
      await player.walk(-2, -4);
      assert.equal(player.world.conversations.maya.eligible, true);
      const voice = await player.open('/live');
      let delegations = 0, inputFragments = 0, outputSamples = 0;
      voice.socket.on('message', raw => {
        const event = JSON.parse(raw);
        if (event.type === 'session.delegation.created') delegations++;
        if (event.type === 'session.input_transcript.delta') inputFragments++;
        if (event.type === 'session.output_audio.delta') outputSamples += Buffer.from(event.delta ?? '', 'base64').length / 2;
      });
      player.send(voice, { type: 'gym.start', character: 'maya', ...player.credentials });
      await player.receive(voice, 'gym.status', event => event.status === 'ready');
      const activeSeconds = player.world.elapsedSeconds;
      let offset = -48000; // One second of leading silence, then speech once, then silence.
      timer = setInterval(() => {
        const frame = Buffer.alloc(4800);
        if (offset >= 0 && offset < pcm.length) pcm.copy(frame, 0, offset, Math.min(pcm.length, offset + frame.length));
        offset += frame.length;
        if (voice.socket.readyState === 1) player.send(voice, { type: 'session.input_audio.append', audio: frame.toString('base64') });
      }, 100);
      const result = await player.receive(voice, 'game.intent_result', event => event.ok && event.committed, 55);
      assert.equal(result.outcome?.accepted, true);
      assert.ok(delegations > 0, 'A real provider client delegation must initiate the action');
      assert.ok(inputFragments > 0, 'Real spoken input must produce transcript fragments');
      await player.until(() => player.state?.actors.maya.action.type === 'wait', 'public Maya wait action', 5);
      assert.equal(player.world.elapsedSeconds, activeSeconds, 'Conversation must keep the world paused');
      await player.until(() => outputSamples > 0, 'voiced NPC response', 15);
      clearInterval(timer);
      player.send(voice, { type: 'session.close' });
      assert.ok((await player.receive(voice, 'session.closed', () => true, 20)).usage);
      t.diagnostic(JSON.stringify({ delegations, inputFragments, outputSamples, reinterpretations: result.reinterpretations ?? 0, committed: true }));
    } finally {
      clearInterval(timer); player.close(); await relay.close();
    }
  });

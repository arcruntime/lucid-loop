import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createRelayServer } from '../src/server.mjs';
import { createGameSessions } from '../src/game-sessions.mjs';
import { createDemoDefinition, createDemoScenario } from '../src/demo-scenario.mjs';
import { createEncounterWorld } from '../src/encounter-world.mjs';
import { createIntentInterpreter } from '../src/responses-client.mjs';
import { parseIntentResponse } from '../src/gameplay-intent.mjs';
import { RelayPlayer } from '../test-support/relay-player.mjs';

const enabled = process.env.RUN_GPT_LIVE_CORRECTION === '1' && Boolean(process.env.OPENAI_API_KEY);

test('opt-in spoken correction supersedes a held real wait interpretation before commit',
  { skip: !enabled, timeout: 140000 }, async t => {
    // Synthesized, timing-controlled integration fixtures, not natural timing or an
    // iPhone test. Both clips are raw signed PCM16 little-endian, mono, 24 kHz.
    // Only public audio is sent; no game.text, fabricated transcripts, or actions.
    const counts = { delegations: 0, inputFragments: 0, correctionFragments: 0,
      interpretations: 0, reinterpretations: 0, initialWait: false, laterFollowProposals: 0, waitStates: 0,
      followStates: 0, initialBytes: 0, correctionBytes: 0, outputSamples: 0,
      gateReleased: false, committed: false, finalUsage: false };
    let relay, player, voice, timer, releaseFirst;
    let firstReady = false, firstFailed = false;
    const held = new Promise(resolve => { releaseFirst = resolve; });
    try {
      const clips = await Promise.all([process.env.LIVE_SPOKEN_PCM, process.env.LIVE_CORRECTION_PCM].map(async path => {
        assert.ok(typeof path === 'string' && path.length > 0, 'Both spoken PCM fixtures are required');
        const pcm = await readFile(path);
        assert.ok(pcm.length >= 48000 && pcm.length <= 20 * 48000 && pcm.length % 2 === 0,
          'Expected 1–20 seconds of PCM');
        assert.ok(pcm.some(byte => byte !== 0), 'Speech fixture must not be silent');
        return pcm;
      }));
      const registry = createGameSessions({ definitionFactory: createDemoDefinition, encounterFactory: createDemoScenario });
      const realInterpret = createIntentInterpreter({ apiKey: process.env.OPENAI_API_KEY });
      relay = createRelayServer({ host: '127.0.0.1', port: 0, apiKey: process.env.OPENAI_API_KEY,
        intentModel: process.env.INTENT_MODEL || 'gpt-5.6-luna', gameSessions: registry,
        worldFactory: createEncounterWorld,
        intentInterpreter: async (body, options) => {
          const first = ++counts.interpretations === 1;
          try {
            const response = await realInterpret(body, options);
            const proposal = parseIntentResponse(response, { npcId: 'maya' });
            if (first) {
              counts.initialWait = proposal.kind === 'action_proposal' && proposal.action === 'wait';
              firstReady = true;
              // Hold delivery only: the response remains the real provider response.
              // Bridge disposal races this promise; finally always releases it.
              await held;
              if (!counts.gateReleased) throw new Error('test_gate_not_released');
            } else if (proposal.kind === 'action_proposal' && proposal.action === 'follow' && proposal.targetId === 'player') {
              counts.laterFollowProposals++;
            }
            return response;
          } catch (error) {
            if (first) firstFailed = true;
            throw error;
          }
        } });
      await relay.listen();
      player = new RelayPlayer(`ws://127.0.0.1:${relay.address().port}`, t.signal);
      await player.start();
      assert.notEqual(player.state?.actors.maya.action.type, 'wait', 'Initial public state must not be wait');
      await player.walk(-2, -4);
      assert.equal(player.world.conversations.maya.eligible, true);
      const observeState = raw => {
        const event = JSON.parse(raw);
        if (event.type !== 'game.state' && event.type !== 'game.ready') return;
        const action = event.snapshot?.actors?.maya?.action?.type;
        if (action === 'wait') counts.waitStates++;
        if (action === 'follow') counts.followStates++;
      };
      player.game.socket.on('message', observeState);
      voice = await player.open('/live');
      voice.socket.on('message', observeState);
      let correctionStartMs = null;
      voice.socket.on('message', raw => {
        const event = JSON.parse(raw);
        if (event.type === 'session.delegation.created') counts.delegations++;
        if (event.type === 'session.input_transcript.delta') {
          counts.inputFragments++;
          if (correctionStartMs !== null && Number.isFinite(event.end_ms) && event.end_ms > correctionStartMs)
            counts.correctionFragments++;
        }
        if (event.type === 'session.output_audio.delta')
          counts.outputSamples += Buffer.from(event.delta ?? '', 'base64').length / 2;
      });
      player.send(voice, { type: 'gym.start', character: 'maya', ...player.credentials });
      await player.receive(voice, 'gym.status', event => event.status === 'ready');
      const activeSeconds = player.world.elapsedSeconds;
      let sentBytes = 0, initialOffset = 0, correctionOffset = 0;
      let playCorrection = false, correctionFinishedAt = null;
      timer = setInterval(() => {
        if (voice.socket.readyState !== 1) return;
        const frame = Buffer.alloc(4800);
        if (sentBytes >= 48000 && initialOffset < clips[0].length) {
          const length = Math.min(frame.length, clips[0].length - initialOffset);
          clips[0].copy(frame, 0, initialOffset, initialOffset + length);
          initialOffset += length; counts.initialBytes += length;
        } else if (playCorrection && correctionOffset < clips[1].length) {
          if (correctionStartMs === null) correctionStartMs = sentBytes / 48;
          const length = Math.min(frame.length, clips[1].length - correctionOffset);
          clips[1].copy(frame, 0, correctionOffset, correctionOffset + length);
          correctionOffset += length; counts.correctionBytes += length;
          if (correctionOffset === clips[1].length) correctionFinishedAt = Date.now();
        }
        player.send(voice, { type: 'session.input_audio.append', audio: frame.toString('base64') });
        sentBytes += frame.length;
      }, 100);
      await player.until(() => firstReady || firstFailed, 'first real interpretation', 55);
      assert.equal(firstFailed, false, 'First real interpretation must complete');
      assert.equal(counts.initialWait, true, 'First actual provider proposal must be wait');
      await player.until(() => initialOffset === clips[0].length, 'initial speech fully streamed', 20);
      assert.ok(counts.delegations > 0, 'Real spoken input must create a native client delegation');
      playCorrection = true;
      // Native timed evidence must overlap the correction, not merely be a late
      // fragment of the original request. A short trailing audio window admits
      // final ASR fragments; it is a test pacing gate, never an action heuristic.
      await player.until(() => correctionFinishedAt !== null && counts.correctionFragments > 0 &&
        Date.now() - correctionFinishedAt >= 2000, 'correction streamed and transcribed', 35);
      assert.equal(counts.waitStates, 0, 'Held interpretation must not commit wait');
      counts.gateReleased = true;
      releaseFirst();
      const result = await player.receive(voice, 'game.intent_result', event => event.ok && event.committed, 40);
      counts.reinterpretations = result.reinterpretations ?? 0;
      counts.committed = result.committed === true;
      assert.ok(counts.reinterpretations >= 1, 'Fresh evidence must re-interpret the pending request');
      assert.ok(counts.interpretations >= 2, 'Correction must reach the real interpreter');
      assert.ok(counts.laterFollowProposals > 0, 'A later actual provider proposal must be follow player');
      assert.equal(result.outcome?.accepted, true);
      await player.until(() => player.state?.revision >= result.outcome.revision &&
        player.state?.actors.maya.action.type === 'follow', 'committed public Maya follow revision', 5);
      assert.equal(counts.waitStates, 0, 'No intermediate public wait action may commit');
      assert.ok(counts.followStates > 0);
      assert.equal(counts.initialBytes, clips[0].length, 'Initial speech must stream exactly once');
      assert.equal(counts.correctionBytes, clips[1].length, 'Correction must stream exactly once');
      assert.equal(player.world.elapsedSeconds, activeSeconds, 'Conversation keeps the world paused');
    } finally {
      releaseFirst();
      clearInterval(timer);
      try {
        if (voice?.socket.readyState === 1) {
          player.send(voice, { type: 'session.close' });
          const closed = await player.receive(voice, 'session.closed', () => true, 20);
          counts.finalUsage = Boolean(closed.usage);
          assert.ok(counts.finalUsage, 'Real Live session must return final usage');
          assert.equal(counts.waitStates, 0, 'No public wait action may commit through session close');
        }
      } finally {
        t.diagnostic(JSON.stringify(counts));
        player?.close();
        await relay?.close();
      }
    }
  });

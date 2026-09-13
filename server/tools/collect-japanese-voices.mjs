import { mkdir, writeFile } from 'node:fs/promises';
import { isAbsolute, join } from 'node:path';
import { performance } from 'node:perf_hooks';
import { randomUUID } from 'node:crypto';
import WebSocket from 'ws';
import { buildSessionStart, CHARACTER_IDS } from '../src/prompts.mjs';
import { createVoiceCapture, pcmWav, sha256, SAMPLE_RATE } from '../test-support/voice-capture.mjs';

const WINDOW_MS = 35000;
const requestedText = 'ふうっと息を吐いて、青い海を見る。パパもママも、ぷかぷか浮かぶ船を待った。ゆっくり切符を買って、みんなで東京へ行こう。';

async function collect(npcId, directory) {
  const captureId = randomUUID();
  const startedAtUtc = new Date().toISOString();
  const start = buildSessionStart(npcId);
  start.session.instructions += '\n日本語の音声収録です。指定された日本語だけを自然に一度読み上げ、英語や説明を追加しないでください。';
  const commentary = { type: 'session.commentary.append', event_id: `japanese_sample_${npcId}`, delegation_id: null,
    content: `次の文章を日本語でそのまま一度読み上げてください：${requestedText}` };
  const capture = createVoiceCapture();
  const origin = performance.now();
  let startedMonoMs = null, windowEndMonoMs = null, closeRequestedMonoMs = null, transportClose = null;
  let failure = null, socket, silence, windowTimer, closeTimer, startupTimer, totalTimer;
  const elapsed = () => performance.now() - origin;
  await new Promise(resolve => {
    let settled = false;
    function finish(reason = null) {
      if (settled) return;
      settled = true; failure = reason;
      for (const timer of [silence, windowTimer, closeTimer, startupTimer, totalTimer]) clearTimeout(timer);
      if (socket && socket.readyState !== WebSocket.CLOSED) socket.terminate();
      resolve();
    }
    function send(event) {
      if (socket.readyState !== WebSocket.OPEN) { finish('socket_not_open'); return; }
      if (socket.bufferedAmount > 1_000_000) { finish('outbound_buffer_limit'); return; }
      socket.send(JSON.stringify(event), error => { if (error) finish('send_failed'); });
    }
    startupTimer = setTimeout(() => finish('startup_timeout'), 15000);
    totalTimer = setTimeout(() => finish('session_total_timeout'), 65000);
    try {
      socket = new WebSocket('wss://api.openai.com/v1/live/sessions', {
        headers: { Authorization: `Bearer ${process.env.OPENAI_API_KEY}`, 'User-Agent': 'lucid-loop-japanese-collector/0.1' },
        handshakeTimeout: 12000, maxPayload: 1_000_000,
      });
      socket.on('open', () => send(start));
      socket.on('error', () => finish('websocket_error'));
      socket.on('close', (code, reason) => {
        transportClose = { code, reason: reason.toString(), receiptMonoMs: elapsed() };
        finish('transport_closed_before_session_closed');
      });
      socket.on('message', raw => {
        if (settled) return;
        try {
          const event = JSON.parse(raw.toString());
          const received = elapsed();
          capture.record(event, received, startedMonoMs !== null && received < windowEndMonoMs);
          if (event.type === 'error' || event.type === 'session.error') { finish('provider_error'); return; }
          if (event.type === 'session.started' && startedMonoMs === null) {
            clearTimeout(startupTimer);
            startedMonoMs = received; windowEndMonoMs = received + WINDOW_MS;
            send(commentary);
            const frame = Buffer.alloc(4800).toString('base64');
            let sequence = 0;
            silence = setInterval(() => send({ type: 'session.input_audio.append', event_id: `silence_${++sequence}`, audio: frame }), 100);
            windowTimer = setTimeout(() => {
              clearInterval(silence); closeRequestedMonoMs = elapsed();
              send({ type: 'session.close', event_id: `close_${npcId}` });
              closeTimer = setTimeout(() => finish('session_close_timeout'), 10000);
            }, WINDOW_MS);
          }
          if (event.type === 'session.closed') finish(closeRequestedMonoMs === null ? 'session_closed_before_window_end' : null);
        } catch (error) { finish(['pcm_limit', 'event_limit', 'invalid_audio_base64', 'unaligned_pcm'].includes(error.message) ? error.message : 'invalid_provider_event'); }
      });
    } catch { finish('connection_setup_failed'); }
  });
  const data = capture.snapshot();
  if (!failure && (!data.pcm.length || !data.outputTranscriptDeltas || !data.finalUsage)) failure = 'missing_audio_transcript_or_final_usage';
  const wav = pcmWav(data.pcm);
  const records = Buffer.from(JSON.stringify({ events: data.events, audioReceipts: data.audio }, null, 2));
  await mkdir(directory);
  await writeFile(join(directory, 'voice.wav'), wav, { flag: 'wx' });
  await writeFile(join(directory, 'events.json'), records, { flag: 'wx' });
  const manifest = { npcId, captureId, startedAtUtc, sourceSha: process.env.GITHUB_SHA ?? null,
    githubRunId: process.env.GITHUB_RUN_ID ?? null, githubRunAttempt: process.env.GITHUB_RUN_ATTEMPT ?? null,
    providerSessionStarted: data.events.find(record => record.event.type === 'session.started') ?? null,
    model: start.session.model, voice: start.session.audio.output.voice, requestedText,
    startRequest: start, commentaryRequest: commentary, status: failure ? 'partial_failure' : 'captured', failure,
    sampleRate: SAMPLE_RATE, channels: 1, bitsPerSample: 16, samples: data.pcm.length / 2,
    capture: { kind: 'fixed_window_not_completed_utterance', windowMs: WINDOW_MS, startedMonoMs, windowEndMonoMs, closeRequestedMonoMs,
      receiptClock: 'process performance.now relative to this NPC collection start; no audio-to-session timestamp alignment inferred',
      playbackClock: null, physicalOrAudibleClockMeasured: false }, transportClose,
    outputTranscriptDeltas: data.outputTranscriptDeltas, finalUsage: data.finalUsage,
    files: [{ path: 'voice.wav', bytes: wav.length, sha256: sha256(wav) }, { path: 'events.json', bytes: records.length, sha256: sha256(records) }] };
  await writeFile(join(directory, 'manifest.json'), JSON.stringify(manifest, null, 2), { flag: 'wx' });
  return { npcId, status: manifest.status, failure, manifest: `${npcId}/manifest.json` };
}

async function main() {
  if (process.env.RUN_GPT_LIVE_JAPANESE !== '1') throw new Error('Set RUN_GPT_LIVE_JAPANESE=1 to opt in to paid voice collection.');
  if (!process.env.OPENAI_API_KEY) throw new Error('OPENAI_API_KEY is required.');
  const output = process.env.LIVE_JAPANESE_OUTPUT;
  if (!output || !isAbsolute(output)) throw new Error('LIVE_JAPANESE_OUTPUT must be an absolute path to a fresh directory.');
  await mkdir(output); // Existing directories fail; previous evidence is never overwritten.
  const results = [];
  for (const npcId of CHARACTER_IDS) {
    results.push(await collect(npcId, join(output, npcId)));
    await writeFile(join(output, 'manifest.json'), JSON.stringify({ sourceSha: process.env.GITHUB_SHA ?? null,
      githubRunId: process.env.GITHUB_RUN_ID ?? null, githubRunAttempt: process.env.GITHUB_RUN_ATTEMPT ?? null,
      requestedText, results }, null, 2));
    console.log(`${npcId}: ${results.at(-1).status}`);
  }
  if (results.some(result => result.failure)) process.exitCode = 1;
}

main().catch(() => { console.error('Japanese voice collection failed. Check required opt-in, key, fresh absolute output path, and any saved partial manifests.'); process.exitCode = 1; });

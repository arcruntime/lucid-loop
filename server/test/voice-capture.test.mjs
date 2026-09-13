import test from 'node:test';
import assert from 'node:assert/strict';
import { createVoiceCapture, pcmWav, sha256 } from '../test-support/voice-capture.mjs';

test('WAV contains exact signed PCM samples with mono 24 kHz 16-bit header', () => {
  const pcm = Buffer.from([0, 128, 255, 127, 0, 0]);
  const wav = pcmWav(pcm);
  assert.equal(wav.toString('ascii', 0, 4), 'RIFF');
  assert.equal(wav.readUInt32LE(4), wav.length - 8);
  assert.equal(wav.toString('ascii', 8, 16), 'WAVEfmt ');
  assert.equal(wav.readUInt16LE(20), 1);
  assert.equal(wav.readUInt16LE(22), 1);
  assert.equal(wav.readUInt32LE(24), 24000);
  assert.equal(wav.readUInt32LE(28), 48000);
  assert.equal(wav.readUInt16LE(32), 2);
  assert.equal(wav.readUInt16LE(34), 16);
  assert.equal(wav.readUInt32LE(40), pcm.length);
  assert.deepEqual(wav.subarray(44), pcm);
  assert.equal(sha256(Buffer.from('abc')), 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
  assert.throws(() => pcmWav(Buffer.alloc(1)), /invalid_pcm/);
});

test('capture retains provider timestamps and transcript deltas without inferring alignment', () => {
  const c = createVoiceCapture();
  const audio = { type: 'session.output_audio.delta', delta: Buffer.from([1, 0, 2, 0]).toString('base64'), offset_ms: 700, event_id: 'a' };
  c.record(audio, 19.5);
  c.record({ type: 'session.output_transcript.delta', delta: 'ふう', start_ms: 901, end_ms: 999 }, 23);
  c.record(audio, 30);
  c.record(audio, 35001, false);
  c.record({ type: 'session.closed', usage: { output_tokens: 7 } }, 35010, false);
  const result = c.snapshot();
  assert.deepEqual(result.audio.map(a => [a.receiptMonoMs, a.sampleStart, a.sampleEnd]), [[19.5, 0, 2], [30, 2, 4], [35001, null, null]]);
  assert.equal(result.audio[0].providerFields.offset_ms, 700);
  assert.equal(Object.hasOwn(result.audio[0].providerFields, 'delta'), false);
  assert.deepEqual(result.events[1].event, { type: 'session.output_transcript.delta', delta: 'ふう', start_ms: 901, end_ms: 999 });
  assert.equal(result.outputTranscriptDeltas, 1);
  assert.deepEqual(result.finalUsage, { output_tokens: 7 });
  assert.equal(result.pcm.length, 8);
  result.audio[0].providerFields.offset_ms = 0;
  assert.equal(c.snapshot().audio[0].providerFields.offset_ms, 700);
});

test('limits and malformed audio reject atomically while preserving partial evidence', () => {
  const c = createVoiceCapture({ maxPcmBytes: 4, maxEvents: 3 });
  const event = { type: 'session.output_audio.delta', delta: Buffer.alloc(4).toString('base64') };
  c.record(event, 1);
  assert.throws(() => c.record(event, 2), /pcm_limit/);
  assert.throws(() => c.record({ ...event, delta: '!!!!' }, 2), /invalid_audio_base64/);
  assert.throws(() => c.record({ ...event, delta: 'AQ==' }, 2), /unaligned_pcm/);
  assert.equal(c.snapshot().events.length, 1);
  assert.equal(c.snapshot().pcm.length, 4);
  c.record({ type: 'ack' }, 3); c.record({ type: 'ack' }, 4);
  assert.throws(() => c.record({ type: 'ack' }, 5), /event_limit/);
  const tiny = createVoiceCapture({ maxEventBytes: 1 });
  assert.throws(() => tiny.record(event, 1), /event_limit/);
  assert.equal(tiny.snapshot().pcm.length, 0);
  assert.equal(tiny.snapshot().events.length, 0);
});

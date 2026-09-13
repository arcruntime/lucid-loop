import { createHash } from 'node:crypto';

export const SAMPLE_RATE = 24000;
export const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

export function pcmWav(pcm) {
  if (!Buffer.isBuffer(pcm) || pcm.length % 2 || pcm.length > 0xffffffff - 36) throw new Error('invalid_pcm');
  const header = Buffer.alloc(44);
  header.write('RIFF'); header.writeUInt32LE(36 + pcm.length, 4); header.write('WAVEfmt ', 8);
  header.writeUInt32LE(16, 16); header.writeUInt16LE(1, 20); header.writeUInt16LE(1, 22);
  header.writeUInt32LE(SAMPLE_RATE, 24); header.writeUInt32LE(SAMPLE_RATE * 2, 28);
  header.writeUInt16LE(2, 32); header.writeUInt16LE(16, 34); header.write('data', 36);
  header.writeUInt32LE(pcm.length, 40);
  return Buffer.concat([header, pcm]);
}

export function createVoiceCapture({ maxPcmBytes = 4_000_000, maxEventBytes = 2_000_000, maxEvents = 12000 } = {}) {
  const chunks = [], events = [], audio = [];
  let pcmBytes = 0, eventBytes = 0, outputTranscriptDeltas = 0, finalUsage = null;
  return {
    record(event, receiptMonoMs, captureAudio = true) {
      if (!event || typeof event.type !== 'string' || !Number.isFinite(receiptMonoMs) || receiptMonoMs < 0) throw new Error('invalid_event');
      let bytes;
      let metadata = event;
      if (event.type === 'session.output_audio.delta') {
        const { delta, ...rest } = event;
        metadata = rest;
        if (typeof delta !== 'string' || !/^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/.test(delta)) throw new Error('invalid_audio_base64');
        bytes = Buffer.from(delta, 'base64');
        if (bytes.length % 2) throw new Error('unaligned_pcm');
        if (captureAudio && pcmBytes + bytes.length > maxPcmBytes) throw new Error('pcm_limit');
      }
      const record = { receiptMonoMs, event: structuredClone(metadata) };
      const cost = Buffer.byteLength(JSON.stringify(record));
      if (events.length >= maxEvents || eventBytes + cost > maxEventBytes) throw new Error('event_limit');
      eventBytes += cost; events.push(record);
      if (bytes) {
        audio.push({ receiptMonoMs, providerFields: structuredClone(metadata), captured: captureAudio,
          receivedSamples: bytes.length / 2, sampleStart: captureAudio ? pcmBytes / 2 : null,
          sampleEnd: captureAudio ? (pcmBytes + bytes.length) / 2 : null });
        if (captureAudio) { chunks.push(bytes); pcmBytes += bytes.length; }
      }
      if (event.type === 'session.output_transcript.delta' && typeof event.delta === 'string' && event.delta.length) outputTranscriptDeltas++;
      if (event.type === 'session.closed') finalUsage = event.usage ?? null;
    },
    snapshot() {
      return { pcm: Buffer.concat(chunks), events: structuredClone(events), audio: structuredClone(audio),
        outputTranscriptDeltas, finalUsage: structuredClone(finalUsage) };
    },
  };
}

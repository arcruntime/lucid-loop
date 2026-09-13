// Offline evaluation only: complete audio and complete checked reading are available.
// No model training, provider calls, streaming or contact-accuracy claim.
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { performance } from 'node:perf_hooks';
import { loadCheckedKana } from './checked_kana.mjs';

const [sourceArg, captureArg, readingArg, outputArg, baselineArg] = process.argv.slice(2);
if (!outputArg) throw new Error('Usage: node probe_npc_alignment.mjs SOURCE CAPTURE CHECKED_READING OUTPUT [ORIGINAL_BASELINE]');
const source = resolve(sourceArg), capture = resolve(captureArg), output = resolve(outputArg);
const pin = 'acea62e125aa2200648a489900de750c3e3587fa';
const git = args => execFileSync('git', ['-C', source, ...args], { encoding: 'utf8' }).trim();
if (git(['rev-parse', 'HEAD']) !== pin) throw new Error('Unexpected upstream revision');
git(['diff', '--exit-code', pin, '--', 'ja', 'harness', 'vendor']);
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const json = async path => JSON.parse(await readFile(path, 'utf8'));
const readingBytes = await readFile(resolve(readingArg));
const reading = JSON.parse(readingBytes.toString('utf8'));
const checkedKana = await loadCheckedKana(source);
const parsed = checkedKana(reading.reading);
const { readWavMono } = await import(pathToFileURL(resolve(source, 'harness/wav.mjs')).href);
const { extractFeatures } = await import(pathToFileURL(resolve(source, 'ja/features.mjs')).href);
const { alignClip, labelAt, labelAtMasked } = await import(baselineArg
  ? new URL('./vendor/japanese_alignment/segment.mjs', import.meta.url).href
  : pathToFileURL(resolve(source, 'ja/segment.mjs')).href);
const manifest = await json(resolve(capture, 'manifest.json'));
await mkdir(output, { recursive: true });
const samples = [];
for (const npc of ['maya', 'ren', 'luca', 'theo']) {
  const folder = resolve(capture, npc), meta = await json(resolve(folder, 'manifest.json'));
  if (meta.status !== 'captured' || meta.requestedText !== reading.input) throw new Error('Capture/reading mismatch');
  for (const name of ['voice.wav', 'events.json']) {
    const expected = meta.files.find(f => f.path === name), bytes = await readFile(resolve(folder, name));
    if (!expected || bytes.length !== expected.bytes || sha(bytes) !== expected.sha256) throw new Error('Capture hash mismatch');
  }
  const wav = readWavMono(resolve(folder, 'voice.wav'));
  if (wav.sampleRate !== 24000 || wav.channels !== 1 || wav.samples.length > 24000 * 40) throw new Error('Unexpected or oversized recording');
  const started = performance.now();
  const feat = extractFeatures(wav.samples, wav.sampleRate, { hop: 32 });
  const featured = performance.now();
  const alignment = alignClip(feat, reading.reading);
  const finished = performance.now();
  let comparison;
  if (baselineArg) {
    const baseline = await json(resolve(baselineArg, `${npc}.alignment.json`));
    const baselineSummary = await json(resolve(baselineArg, 'summary.json'));
    const prior = baselineSummary.samples.find(s => s.npcId === npc);
    if (baselineSummary.upstreamCommit !== pin || prior.audioSha256 !== meta.files.find(f => f.path === 'voice.wav').sha256)
      throw new Error('Baseline provenance mismatch');
    const boundariesIdentical = JSON.stringify(alignment) === JSON.stringify(baseline);
    if (!boundariesIdentical) throw new Error(`Alignment changed for ${npc}`);
    let changedFrames = 0, maskedGapFrames = 0;
    for (let t = 16; t < wav.samples.length / wav.sampleRate * 1000; t += 16) {
      const raw = labelAt(alignment, t), masked = labelAtMasked(alignment, t);
      if (raw.label !== masked.label) changedFrames++;
      const inside = alignment.segments.some(([start, end]) => t >= start && t < end);
      if (!inside && masked.label !== 'sil') throw new Error('Silence mask failed');
      if (!inside && raw.label !== 'sil') maskedGapFrames++;
      if (inside && raw.label !== masked.label) throw new Error('Speech target changed');
    }
    comparison = { boundariesIdentical, changedFrames, maskedGapFrames,
      baselineAlignmentMs: prior.alignmentMs, desktopSpeedup: prior.alignmentMs / (finished - featured) };
  }
  const invalid = alignment.morae.flatMap((m, index) => {
    const issues = [];
    if (!Number.isFinite(m.startMs) || !Number.isFinite(m.endMs) || m.endMs <= m.startMs) issues.push('nonpositive_or_nonfinite_span');
    if (index && m.startMs < alignment.morae[index - 1].endMs) issues.push('overlap');
    for (const part of [m.onset, m.vowel].filter(Boolean)) {
      if (part.startMs < m.startMs || part.endMs > m.endMs || part.endMs <= part.startMs) issues.push('invalid_phone_span');
    }
    return issues.length ? [{ index, kana: m.kana, startMs: m.startMs, endMs: m.endMs, issues }] : [];
  });
  const moraDurations = alignment.morae.map(m => m.endMs - m.startMs);
  // A mora can span a hole in the compressed DP domain. labelAt does not
  // separately mask these silence gaps, even though its timestamps are valid.
  const gapSpans = alignment.morae.flatMap((m, index) => {
    const activeMs = alignment.segments.reduce((sum, [start, end]) =>
      sum + Math.max(0, Math.min(m.endMs, end) - Math.max(m.startMs, start)), 0);
    const outsideMs = m.endMs - m.startMs - activeMs;
    return outsideMs > feat.hopMs ? [{ index, kana: m.kana, outsideDetectedSpeechMs: outsideMs }] : [];
  });
  const record = { npcId: npc, voice: meta.voice, audioSha256: meta.files.find(f => f.path === 'voice.wav').sha256,
    pcmDurationMs: wav.samples.length / wav.sampleRate * 1000,
    featureFrames: feat.frames.length, featureMs: featured - started, alignmentMs: finished - featured,
    expectedMorae: parsed.morae.length, actualMorae: alignment.morae.length,
    expectedPhrases: parsed.phrases.length, detectedSegments: alignment.segments.length,
    grouping: parsed.phrases.length === alignment.segments.length ? 'phrase_to_segment' : 'all_morae_over_segment_union',
    segments: alignment.segments, invalidSpans: invalid, gapSpans, ...(comparison ? { comparison } : {}),
    minimumMoraMs: Math.min(...moraDurations), maximumMoraMs: Math.max(...moraDurations),
    structurallyValid: alignment.morae.length === parsed.morae.length && invalid.length === 0 };
  samples.push(record);
  await writeFile(resolve(output, `${npc}.alignment.json`), JSON.stringify(alignment, null, 2) + '\n');
  console.log(`${npc}: ${record.actualMorae} morae, ${record.detectedSegments} segments, ${invalid.length} invalid spans, ${record.alignmentMs.toFixed(1)} ms alignment`);
}
await writeFile(resolve(output, 'summary.json'), JSON.stringify({ upstreamCommit: pin,
  sourceSha: manifest.sourceSha, captureRunId: manifest.githubRunId, checkedReadingSha256: sha(readingBytes),
  mode: baselineArg ? 'offline_duration_window_comparison' : 'offline_full_audio_full_reading', causal: false, contactAccuracyMeasured: false,
  timingNote: 'Single desktop run; computation excludes file IO, reading conversion and delivery. Not an iOS latency measurement.',
  samples }, null, 2) + '\n');

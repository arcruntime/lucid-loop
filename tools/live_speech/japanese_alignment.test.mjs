import test from 'node:test';
import assert from 'node:assert/strict';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { dpAlignOnDomain, labelAtMasked } from './vendor/japanese_alignment/segment.mjs';
import { parseKana } from './vendor/japanese_alignment/kana.mjs';

test('duration-window DP preserves pinned boundaries for dense, gapped and infeasible domains', { skip: !process.env.LUCID_LIPSYNC_SOURCE }, async () => {
  const { dpAlignOnDomain: original } = await import(pathToFileURL(resolve(process.env.LUCID_LIPSYNC_SOURCE, 'ja/segment.mjs')).href);
  let seed = 1729;
  const random = () => { seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0; return seed / 4294967296; };
  const morae = parseKana('パパモママモユックリ').morae;
  for (const hop of [2, 16]) {
    for (let trial = 0; trial < 24; trial++) {
      const frames = Array.from({ length: 80 + trial * 9 }, (_, i) => ({
        tMs: 16 + i * hop, le: -5 + random() * 4,
        v: Array.from({ length: 12 }, () => random()),
      }));
      const domain = frames.flatMap((_, i) => trial % 2 && i > 30 && i < 65 ? [] : [i]);
      const expected = original(frames, morae.slice(0, 1 + trial % morae.length), domain);
      assert.deepEqual(dpAlignOnDomain(frames, morae.slice(0, 1 + trial % morae.length), domain), expected, `hop ${hop} trial ${trial}`);
    }
  }
});

test('silence masking releases a held target across gaps and at interval ends', () => {
  const alignment = { segments: [[10, 20], [40, 60]], morae: [
    { startMs: 10, endMs: 60, kana: 'ア', flags: [], vowel: { viseme: 'aa', phone: 'a', startMs: 10, endMs: 60 } },
  ] };
  for (const t of [0, 20, 30, 60, NaN, Infinity]) assert.equal(labelAtMasked(alignment, t).label, 'sil');
  for (const t of [10, 19.9, 40, 59.9]) assert.equal(labelAtMasked(alignment, t).label, 'aa');
});

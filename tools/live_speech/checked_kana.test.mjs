import test from 'node:test';
import assert from 'node:assert/strict';
import { loadCheckedKana } from './checked_kana.mjs';

const source = process.env.LUCID_LIPSYNC_SOURCE;
test('checked upstream reading preserves articulations and rejects silently skipped input', { skip: !source }, async () => {
  const parse = await loadCheckedKana(source);
  const result = parse('フウ、パパモママモ、キップ、ミンナ、トーキョー。');
  assert.equal(result.timingEstablished, false);
  assert.ok(result.morae.some(m => m.onsetViseme === 'ja_FU'));
  assert.ok(result.morae.some(m => m.vowelViseme === 'ja_U'));
  assert.ok(result.morae.some(m => m.type === 'Q' && m.onsetViseme === 'PP'));
  assert.ok(result.morae.some(m => m.type === 'N' && m.onsetViseme === 'nn'));
  assert.equal(parse('ﾏﾔ').normalized, 'マヤ');
  for (const raw of ['東京', 'Ren', '🙂', 'カ漢ャ', '123'])
    assert.throws(() => parse(raw), /unsupported_reading_characters/);
  assert.throws(() => parse('ャ'), /reading_was_not_fully_parsed/);
  for (const raw of ['ー', 'ア。ー', 'ンー']) assert.throws(() => parse(raw), /long_vowel_without_vowel/);
  for (const raw of ['', '。', 'ア'.repeat(2049)]) assert.throws(() => parse(raw));
});

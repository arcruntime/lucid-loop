// Experimental reading handoff. This does not turn raw transcripts into readings.
import { readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const HASHES = {
  'kana.mjs': '8c53099fc28c430880ff582f6305800d1d5f4111b1bb8f3719de7f43c56cf6a8',
  'kana-map.json': 'd761dd35e5f5fe39ce1b8b84ec14b0a736420327c973377730de4735dfb4a404',
};
const BREAKS = new Set([...'、。！？,.!?・…']);

export async function loadCheckedKana(source) {
  const directory = resolve(source, 'ja');
  for (const [name, expected] of Object.entries(HASHES)) {
    const text = (await readFile(resolve(directory, name), 'utf8')).replace(/\r\n/g, '\n');
    if (createHash('sha256').update(text).digest('hex') !== expected) throw new Error('unreviewed_kana_source');
  }
  const { KANA_MAP, parseKana } = await import(pathToFileURL(resolve(directory, 'kana.mjs')).href);
  const allowed = new Set([...KANA_MAP.vowelKana, ...Object.keys(KANA_MAP.specialVowelKana),
    ...Object.keys(KANA_MAP.smallKana), ...Object.values(KANA_MAP.onsets).flatMap(v => [...v.kana]), ...BREAKS]);
  return function checkedKana(reading) {
    if (typeof reading !== 'string' || !reading.trim() || reading.length > 2048) throw new Error('invalid_reading');
    const normalized = reading.normalize('NFKC').replace(/\s/g, '');
    const unknown = [...new Set([...normalized].filter(ch => !allowed.has(ch)))];
    if (unknown.length) throw new Error('unsupported_reading_characters:' + unknown.map(ch => 'U+' + ch.codePointAt(0).toString(16).toUpperCase()).join(','));
    const parsed = parseKana(normalized);
    // The upstream parser skips unknown/standalone small kana. Every spoken
    // character must survive into a mora; punctuation only separates phrases.
    if (!parsed.morae.length || parsed.morae.map(m => m.kana).join('') !== [...normalized].filter(ch => !BREAKS.has(ch)).join(''))
      throw new Error('reading_was_not_fully_parsed');
    for (const phrase of parsed.phrases) {
      for (let i = 0; i < phrase.length; i++) {
        if (phrase[i].type === 'LONG' && (!i || !phrase[i - 1].resolvedVowel || ['N', 'Q'].includes(phrase[i - 1].type)))
          throw new Error('long_vowel_without_vowel');
      }
    }
    return { reading, normalized, ...parsed, timingEstablished: false };
  };
}

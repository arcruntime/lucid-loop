// kana.mjs — kana -> mora -> viseme mapping for the Japanese analysis route.
// Zero dependencies. Data table in kana-map.json; contextual rules here.
// See ja/kana-map.md for the linguistic rationale of every choice.

import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
export const KANA_MAP = JSON.parse(
  fs.readFileSync(path.join(HERE, 'kana-map.json'), 'utf8'));

export const UPSTREAM_ORDER = KANA_MAP.upstreamOrder;          // 15 slot names
export const SLOT_TO_LABEL = KANA_MAP.slotToLabel;             // ja model semantics

// viseme label -> upstream slot id (for record emission)
export const LABEL_TO_SLOT = {};
for (const [slot, label] of Object.entries(SLOT_TO_LABEL)) {
  if (!(label in LABEL_TO_SLOT)) LABEL_TO_SLOT[label] = UPSTREAM_ORDER.indexOf(slot);
}
LABEL_TO_SLOT['en_L'] = -1; // no slot in a ja-only model

// ---- kana -> {onset, vowel} base table ------------------------------------
const KANA_BASE = new Map();   // kana char -> {onset, vowel}
for (const [onset, def] of Object.entries(KANA_MAP.onsets)) {
  const chars = [...def.kana];
  const seq = def.vowelSeq;
  chars.forEach((ch, i) =>
    KANA_BASE.set(ch, { onset, vowel: seq[i % seq.length] }));
}
const VOWEL_CHARS = { 'あ':'a','い':'i','う':'u','え':'e','お':'o',
                      'ア':'a','イ':'i','ウ':'u','エ':'e','オ':'o' };
for (const ch of KANA_MAP.vowelKana)
  KANA_BASE.set(ch, { onset: null, vowel: VOWEL_CHARS[ch] });
for (const [ch, v] of Object.entries(KANA_MAP.specialVowelKana))
  if (!KANA_BASE.has(ch)) KANA_BASE.set(ch, { onset: null, vowel: v });

const SMALL = KANA_MAP.smallKana;
const VOICELESS = new Set(KANA_MAP.voicelessOnsets);
const PHRASE_BREAKS = new Set([...'、。！？,.!?・…']);

/**
 * Parse a kana transcript into phrase groups of morae.
 * Returns { phrases: [[mora,...],...], morae: [mora,...] (flat) }
 * mora = { kana, type:'CV'|'V'|'N'|'Q'|'LONG', onset, onsetViseme, vowel,
 *          resolvedVowel, vowelViseme, flags:[], phrase }
 */
export function parseKana(text) {
  const chars = [...String(text).replace(/[\s　]+/g, '')];
  const phrases = [[]];
  for (let i = 0; i < chars.length; i++) {
    const ch = chars[i];
    if (PHRASE_BREAKS.has(ch)) { phrases.push([]); continue; }

    // digraph: base + small kana
    if (i + 1 < chars.length && SMALL[chars[i + 1]]) {
      const small = SMALL[chars[i + 1]];
      const base = KANA_BASE.get(ch) || { onset: null, vowel: null };
      const mora = { kana: ch + chars[i + 1], type: 'CV', flags: [] };
      if (small.length === 2 && small[0] === 'y') {
        mora.onset = base.onset;
        mora.vowel = { ya: 'a', yu: 'u', yo: 'o' }[small];
        mora.flags.push('palatalized');
      } else if (small === 'wa') {
        mora.onset = base.onset; mora.vowel = 'a';
        mora.flags.push('labialized');
      } else {
        mora.onset = base.onset; mora.vowel = small;
        if (base.onset == null) {
          if (base.vowel === 'u') mora.onset = 'w';
          else if (base.vowel === 'i') mora.onset = 'y';
          mora.flags.push('glide');
        } else mora.flags.push('loanword');
      }
      phrases[phrases.length - 1].push(mora);
      i++;
      continue;
    }

    const b = KANA_BASE.get(ch);
    if (!b) continue;                       // unknown char skipped
    const cur = phrases[phrases.length - 1];
    if (b.onset === 'N' || ch === 'ん' || ch === 'ン') {
      cur.push({ kana: ch, type: 'N', onset: 'N', vowel: null, flags: [] });
    } else if (ch === 'っ' || ch === 'ッ') {
      cur.push({ kana: ch, type: 'Q', onset: 'Q', vowel: null, flags: [] });
    } else if (ch === 'ー' || ch === '‐' || ch === '−') {
      cur.push({ kana: ch, type: 'LONG', onset: null, vowel: 'prev', flags: ['longVowel'] });
    } else if (b.onset == null) {
      cur.push({ kana: ch, type: 'V', onset: null, vowel: b.vowel, flags: [] });
    } else {
      cur.push({ kana: ch, type: 'CV', onset: b.onset, vowel: b.vowel, flags: [] });
    }
  }
  const morae = [];
  phrases.forEach((p, pi) => p.forEach(m => { m.phrase = pi; morae.push(m); }));

  // ---- contextual rules ----------------------------------------------------
  for (let i = 0; i < morae.length; i++) {
    const m = morae[i];
    const prev = morae[i - 1];
    const next = morae[i + 1];
    const nextSame = next && next.phrase === m.phrase;

    if (m.type === 'N') {
      if (nextSame && KANA_MAP.nAsPP.includes(next.onset)) m.flags.push('bilabial');
      else if (nextSame && KANA_MAP.nAsKk.includes(next.onset)) m.flags.push('velar');
      else m.flags.push('default');
    }
    if (m.type === 'LONG' && prev) {
      m.vowel = prev.resolvedVowel ?? prev.vowel ?? 'a';
    }
    // う after an o-vowel CV mora / い after an e-vowel CV mora inside a phrase:
    // realized as vowel length (こう=[ko:], けい=[ke:]), not a separate
    // ja_U/I articulation. Bare-vowel sequences (思う-type) are kept distinct —
    // ambiguous in kana, documented in kana-map.md.
    if (prev && prev.phrase === m.phrase && prev.type === 'CV' && m.type === 'V') {
      const pv = prev.resolvedVowel ?? prev.vowel;
      if (pv === 'o' && m.vowel === 'u') { m.vowel = 'o'; m.flags.push('longVowel'); }
      else if (pv === 'e' && m.vowel === 'i') { m.vowel = 'e'; m.flags.push('longVowel'); }
    }
    m.resolvedVowel = m.vowel === 'prev' ? (prev?.resolvedVowel ?? 'a') : m.vowel;

    // devoiced-vowel flag: i/u devoice when surrounded by voiceless context —
    // voiceless left neighbor (own onset, or the preceding mora's onset for a
    // bare vowel mora) and voiceless right neighbor (voiceless onset, sokuon,
    // or phrase end). Covers す in です/ます and medial き/く/し/つ cases.
    if ((m.resolvedVowel === 'i' || m.resolvedVowel === 'u')
        && (m.type === 'CV' || m.type === 'V')) {
      const voicelessLeft = m.onset != null ? VOICELESS.has(m.onset)
        : !!(prev && prev.phrase === m.phrase && VOICELESS.has(prev.onset));
      const voicelessRight =
        !nextSame || VOICELESS.has(next.onset) || next.type === 'Q';
      if (voicelessLeft && voicelessRight) m.flags.push('devoiced');
    }

    // viseme resolution
    if (m.type === 'N') {
      m.vowelViseme = null;
      m.onsetViseme = m.flags.includes('bilabial') ? 'PP'
        : m.flags.includes('velar') ? 'kk' : 'nn';
    } else if (m.type === 'Q') {
      m.vowelViseme = null;
      const nxt = nextSame ? next?.onset : null;
      const nxtVis = nxt && KANA_MAP.onsets[nxt] ? KANA_MAP.onsets[nxt].viseme : null;
      m.onsetViseme = nxtVis === 'CH' ? 'DD'        // held stop phase of affricate
        : nxtVis === 'ja_R' ? 'DD'                  // held tap = held alveolar contact
        : nxtVis === null ? 'sil'                   // phrase-final っ = glottal cutoff
        : nxtVis;
      if (nxtVis) m.flags.push('geminate'); else m.flags.push('glottal-final');
    } else {
      m.onsetViseme = m.onset ? KANA_MAP.onsets[m.onset].viseme : null;
      m.vowelViseme = KANA_MAP.vowels[m.resolvedVowel].viseme;
    }
  }
  return { phrases: phrases.filter(p => p.length), morae };
}

/** Slot id (0..14) a label occupies inside the ja model binary. */
export function slotOf(label) { return LABEL_TO_SLOT[label]; }

/** Map a 15-slot classifier output name to the ja semantic label. */
export function slotNameToLabel(slotName) { return SLOT_TO_LABEL[slotName] || slotName; }

// segment.mjs — transcript-driven alignment: distribute morae across the
// energy-active regions of a clip, refine boundaries by DP on the le/flux
// grid, then split CV morae into onset/vowel sub-spans at the release point.
// Pure offline DSP — no external aligner, no cloud calls.
// Local changes: feasible-duration search windows and silence-masked lookup.
// See upstream.json and LICENSE for pinned source and attribution.

import { parseKana, KANA_MAP } from './kana.mjs';

/** Otsu threshold over le values -> split silence vs speech. */
export function otsuThreshold(les, bins = 64) {
  const lo = Math.min(...les), hi = Math.max(...les);
  if (!(hi > lo)) return lo;
  const hist = new Array(bins).fill(0);
  for (const v of les) hist[Math.min(bins - 1, Math.floor((v - lo) / (hi - lo) * bins))]++;
  const total = les.length;
  let sumAll = 0, sumW = 0, wB = 0, best = -1, bestT = lo;
  const binMid = i => lo + (i + 0.5) / bins * (hi - lo);
  for (let i = 0; i < bins; i++) sumAll += hist[i] * binMid(i);
  for (let i = 0; i < bins; i++) {
    wB += hist[i];
    if (wB === 0) continue;
    const wF = total - wB;
    if (wF === 0) break;
    sumW += hist[i] * binMid(i);
    const mB = sumW / wB, mF = (sumAll - sumW) / wF;
    const between = wB * wF * (mB - mF) * (mB - mF);
    if (between > best) { best = between; bestT = binMid(i); }
  }
  return bestT;
}

/**
 * Find speech-active regions on the le grid.
 * @returns {[[startMs,endMs],...]}
 */
export function speechSegments(frames, { gapMs = 70, minMs = 50, padMs = 10 } = {}) {
  const les = frames.map(f => f.le);
  const thr = otsuThreshold(les);
  const act = frames.map(f => f.le > thr);
  const segs = [];
  let s = -1;
  for (let i = 0; i < act.length; i++) {
    if (act[i] && s < 0) s = i;
    if (!act[i] && s >= 0) { segs.push([frames[s].tMs, frames[i - 1].tMs]); s = -1; }
  }
  if (s >= 0) segs.push([frames[s].tMs, frames[frames.length - 1].tMs]);
  // merge short gaps, drop short segs, pad edges
  const merged = [];
  for (const g of segs) {
    const last = merged[merged.length - 1];
    if (last && g[0] - last[1] < gapMs) last[1] = g[1]; else merged.push([g[0], g[1]]);
  }
  return merged
    .filter(g => g[1] - g[0] >= minMs)
    .map(g => [Math.max(0, g[0] - padMs), g[1] + padMs]);
}

/**
 * DP align `morae` over a domain of frame indices (not necessarily contiguous —
 * silence-gap frames may be excluded). Durations are measured in ms.
 * Cost = normalized duration deviation - boundary salience (low le + high
 * spectral flux at the boundary).
 * @returns {number[]} boundary frame indices, length morae.length+1
 */
export function dpAlignOnDomain(frames, morae, domain) {
  const M = morae.length;
  const T = domain.length;
  const priors = morae.map(m =>
    KANA_MAP.durationPriorMs[m.type === 'CV' || m.type === 'V' ? m.type
      : m.type === 'N' ? 'N' : m.type === 'Q' ? 'Q' : 'long']);
  const gridMs = frames.length > 1 ? frames[1].tMs - frames[0].tMs : 2;
  const tAt = p => frames[domain[Math.min(p, T - 1)]].tMs;   // start time of domain pos p
  const tEnd = p => frames[domain[p - 1]].tMs + gridMs;      // end time of span ending at p

  // boundary salience per domain position (le dip + flux peak), normalized 0..1
  const sal = new Float64Array(T + 1);
  let sLo = Infinity, sHi = -Infinity;
  for (let p = 1; p < T; p++) {
    const i = domain[p], j = domain[p - 1];
    const dip = Math.min(frames[j].le, frames[i].le);
    let flux = 0;
    const a = frames[j].v, b = frames[i].v;
    if (a && b) for (let k = 0; k < 12; k++) flux += (b[k] - a[k]) ** 2;
    sal[p] = -dip + Math.sqrt(flux) * 2.0;
    if (sal[p] < sLo) sLo = sal[p]; if (sal[p] > sHi) sHi = sal[p];
  }
  for (let p = 1; p < T; p++) sal[p] = sHi > sLo ? (sal[p] - sLo) / (sHi - sLo) : 0;

  const minDms = 30, maxDms = 340;
  const BETA = 0.5;

  // Ordered frame times make both ends monotonic. Precompute only candidates
  // satisfying the unchanged duration limits; preserve ascending q/tie order.
  // This also works when domain indices omit silence gaps.
  const first = new Int32Array(T + 1), after = new Int32Array(T + 1);
  let lo = 0, hi = 0;
  for (let p = 1; p <= T; p++) {
    const end = tEnd(p);
    while (lo < p && end - tAt(lo) > maxDms) lo++;
    hi = Math.max(hi, lo);
    while (hi < p && end - tAt(hi) >= minDms) hi++;
    first[p] = lo; after[p] = hi;
  }

  const dp = Array.from({ length: M + 1 }, () => new Float64Array(T + 1).fill(Infinity));
  const back = Array.from({ length: M + 1 }, () => new Int32Array(T + 1).fill(-1));
  dp[0][0] = 0;
  for (let m = 0; m < M; m++) {
    const prior = priors[m];
    for (let p = 1; p <= T; p++) {
      let bestC = Infinity, bestQ = -1;
      for (let q = first[p]; q < after[p]; q++) {
        if (dp[m][q] === Infinity) continue;
        const dur = tEnd(p) - tAt(q);
        if (dur > maxDms) continue;   // q ascending -> dur shrinks; keep scanning
        if (dur < minDms) break;      // further q only shortens the span
        const r = dur / prior;
        const c = dp[m][q] + (r - 1) * (r - 1) - BETA * sal[p];
        if (c < bestC) { bestC = c; bestQ = q; }
      }
      dp[m + 1][p] = bestC; back[m + 1][p] = bestQ;
    }
  }
  const bounds = new Array(M + 1);
  bounds[M] = domain[T - 1];
  let p = T;
  for (let m = M; m > 0; m--) {
    const q = back[m][p];
    if (q < 0) { for (let k = m - 1; k >= 0; k--) bounds[k] = bounds[m] ?? domain[0]; break; }
    p = q;
    bounds[m - 1] = domain[p];
  }
  return bounds;
}

/** Split a CV mora span into onset/vowel at the release (max flux/energy-rise). */
export function splitCV(frames, f0, f1, cs) {
  const span = frames[f1].tMs - frames[f0].tMs;
  const head = Math.min(cs.max, Math.max(cs.min, span * cs.frac));
  const limit = f0 + Math.max(1, Math.round(head / (frames[1].tMs - frames[0].tMs)));
  let best = -Infinity, bi = f0 + 1;
  for (let i = f0 + 1; i <= Math.min(limit, f1); i++) {
    let flux = 0;
    const a = frames[i - 1].v, b = frames[i].v;
    if (a && b) for (let k = 0; k < 12; k++) flux += (b[k] - a[k]) ** 2;
    const rise = frames[i].le - frames[i - 1].le;
    const score = Math.sqrt(flux) + Math.max(0, rise) * 0.8;
    if (score > best) { best = score; bi = i; }
  }
  return bi;
}

/**
 * Align a parsed kana transcript to extracted features.
 * @param {{frames:[{tMs,le,v}]}} feat  hop-32 feature dump
 * @param {string} kanaText
 * @param {object} [opts] { within:[startMs,endMs] restrict to a sub-span
 *                        (spliced clips: align only the ja half) }
 * @returns {{morae:[...], segments:[[s,e]], phrases:[...]}}
 */
export function alignClip(feat, kanaText, opts = {}) {
  const { phrases, morae } = parseKana(kanaText);
  const frames = feat.frames;
  let segs = speechSegments(frames);
  if (opts.within) {
    const [w0, w1] = opts.within;
    segs = segs.map(s => [Math.max(s[0], w0), Math.min(s[1], w1)])
      .filter(s => s[1] - s[0] >= 50);
  }
  if (!morae.length || !segs.length) return { morae: [], segments: segs, phrases };

  // Group morae into alignment spans. When #phrases == #segments, pair them
  // 1:1 (most accurate). Otherwise align all morae over the union of the
  // segments on a compressed domain (inter-segment gap frames excluded from
  // the grid so no mora lands inside a pause).
  const nSegs = segs.length;
  let groups; // [{morae:[...], domain:[frameIndex,...]}]
  const inSeg = (t) => segs.some(s => t >= s[0] && t <= s[1]);
  if (phrases.length === nSegs) {
    groups = phrases.map((pm, p) => {
      const domain = [];
      for (let i = 0; i < frames.length; i++)
        if (frames[i].tMs >= segs[p][0] && frames[i].tMs <= segs[p][1]) domain.push(i);
      return { morae: pm, domain };
    });
  } else {
    const domain = [];
    for (let i = 0; i < frames.length; i++) if (inSeg(frames[i].tMs)) domain.push(i);
    groups = [{ morae, domain }];
  }

  // Per group: DP-align its morae over its frame domain.
  const out = [];
  for (const g of groups) {
    const pm = g.morae;
    if (!pm.length || g.domain.length < 2) continue;
    const bounds = dpAlignOnDomain(frames, pm, g.domain);
    const cs = KANA_MAP.consonantSpanMs;
    const gridMs = frames.length > 1 ? frames[1].tMs - frames[0].tMs : 2;
    for (let m = 0; m < pm.length; m++) {
      const f0 = bounds[m];                    // first frame of mora m
      const f1 = bounds[m + 1];                // first frame of mora m+1 (or last frame)
      const end = m === pm.length - 1 ? frames[f1].tMs + gridMs : frames[f1].tMs;
      const mo = pm[m];
      const rec = {
        kana: mo.kana, type: mo.type, flags: mo.flags,
        startMs: frames[f0].tMs, endMs: end,
        onset: null, vowel: null
      };
      if (mo.type === 'CV' && mo.onsetViseme && mo.vowelViseme) {
        const bi = splitCV(frames, f0, Math.max(f0, f1 - 1), cs);
        const splitMs = (bi <= f0 || bi >= frames.length)
          ? rec.startMs + Math.min(cs.min, (end - rec.startMs) * 0.4)
          : frames[bi].tMs;
        rec.onset = { viseme: mo.onsetViseme, phone: mo.onset, startMs: rec.startMs, endMs: splitMs };
        rec.vowel = { viseme: mo.vowelViseme, phone: mo.resolvedVowel, startMs: splitMs, endMs: end };
      } else if (mo.type === 'V' || mo.type === 'LONG') {
        rec.vowel = { viseme: mo.vowelViseme, phone: mo.resolvedVowel, startMs: rec.startMs, endMs: end };
      } else { // N or Q: whole span is the (contextual) consonant class
        const ph = mo.type === 'N'
          ? (mo.flags.includes('bilabial') ? 'Nm' : mo.flags.includes('velar') ? 'Ng' : 'Nn')
          : 'Q' + (mo.onsetViseme === 'DD' ? 't' : mo.onsetViseme === 'PP' ? 'p'
            : mo.onsetViseme === 'kk' ? 'k' : mo.onsetViseme === 'SS' ? 's'
            : mo.onsetViseme === 'ja_FU' ? 'f' : mo.onsetViseme === 'nn' ? 'n' : 'x');
        rec.onset = { viseme: mo.onsetViseme, phone: ph, startMs: rec.startMs, endMs: end };
      }
      out.push(rec);
    }
  }
  return { morae: out, segments: segs, phrases };
}

/** Viseme label + phone covering time tMs, or 'sil'. */
export function labelAt(alignment, tMs) {
  for (const m of alignment.morae) {
    if (tMs >= m.startMs && tMs < m.endMs) {
      if (m.onset && tMs < m.onset.endMs)
        return { label: m.onset.viseme, phone: m.onset.phone, kana: m.kana, part: 'onset', flags: m.flags };
      if (m.vowel)
        return { label: m.vowel.viseme, phone: m.vowel.phone, kana: m.kana, part: 'vowel', flags: m.flags };
      if (m.onset)
        return { label: m.onset.viseme, phone: m.onset.phone, kana: m.kana, part: 'onset', flags: m.flags };
      return { label: 'sil', phone: '', kana: m.kana, part: 'none', flags: m.flags };
    }
  }
  return { label: 'sil', phone: '', kana: '', part: 'sil', flags: [] };
}

/** Half-open speech intervals mask holes in the compressed alignment domain. */
export function labelAtMasked(alignment, tMs) {
  if (!Number.isFinite(tMs) || !alignment.segments.some(([start, end]) => tMs >= start && tMs < end))
    return { label: 'sil', phone: '', kana: '', part: 'sil', flags: [] };
  return labelAt(alignment, tMs);
}

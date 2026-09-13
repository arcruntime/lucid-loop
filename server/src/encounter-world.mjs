import { NPC_IDS, ACTOR_IDS } from './encounter.mjs';

const point = (x, z) => ({ x, z });
const distance = (a, b) => Math.hypot(a.x - b.x, a.z - b.z);
const finitePoint = p => p && Number.isFinite(p.x) && Number.isFinite(p.z);
const rect = (id, x, z, width, depth, blocksSight = false) => ({ id, minX: x - width / 2, maxX: x + width / 2, minZ: z - depth / 2, maxZ: z + depth / 2, blocksSight });
const inside = (p, r, margin = 0) => p.x >= r.minX - margin && p.x <= r.maxX + margin && p.z >= r.minZ - margin && p.z <= r.maxZ + margin;

/** Versioned planar approximation of GymBuilder's Character gym, in XZ metres.
 * Platform height and stairs are presentation geometry, not 2D walls. Furniture
 * blocks locomotion; low tables/counters do not occlude an eye-height sightline.
 */
export const CLUB_WORLD_CONFIG = {
  version: 'club-plan-1', bounds: rect('floor', 0, 0, 26, 22), radius: 0.35,
  playerSpeed: 3.7, npcSpeed: 3, followDistance: 1.2, conversationDistance: 2.2,
  approachDistance: 1.25, agreedDistance: 2.5, safeDistance: 4,
  spawns: { player: point(0, -8), maya: point(-2.8, -2), ren: point(2.5, 7.4), luca: point(-7, 2), theo: point(7, -1), affair_partner: point(8, -0.8) },
  recognitionArea: rect('recognition', 8.5, -1, 3.4, 3), recognitionDistance: 7,
  destinations: { vip: point(10.5, 4) },
  fall: { anchor: point(8.65, -1.5), tolerance: 0.9, tableId: 'table-vip-south', endpoint: point(9, -3) },
  obstacles: [
    rect('bar', -10.3, 1, 2.5, 13.3), rect('dj-booth', 0, 8.2, 4, 1.2),
    ...[-5, 5].map(x => rect(`speaker-${x}`, x, 9.4, 0.9, 0.8, true)),
    ...[-4, -2, 0, 2, 4, 6].map(z => rect(`stool-${z}`, -8.2, z, 0.64, 0.64)),
    ...Array.from({ length: 5 }, (_, i) => rect(`sofa-${i}`, 12.1, -4 + i * 1.5, 1.4, 1.4)),
    ...[[9, 1, 'vip-north'], [9, -3, 'vip-south'], [-7, -8, 'west'], [6, -8, 'east']].flatMap(([x, z, id]) => [
      rect(`table-${id}`, x, z, 1.3, 1.3), rect(`seat-${id}-left`, x - 1.3, z, 0.9, 0.9), rect(`seat-${id}-right`, x + 1.3, z, 0.9, 0.9),
    ]),
    ...[-3, 3].map(x => rect(`entrance-column-${x}`, x, -10.8, 0.35, 0.35, true)),
  ],
};

function intersects(a, b, r, margin = 0) {
  let lo = 0, hi = 1;
  for (const [axis, min, max] of [['x', r.minX - margin, r.maxX + margin], ['z', r.minZ - margin, r.maxZ + margin]]) {
    const delta = b[axis] - a[axis];
    if (Math.abs(delta) < 1e-10) { if (a[axis] < min || a[axis] > max) return false; }
    else { const t1 = (min - a[axis]) / delta, t2 = (max - a[axis]) / delta; lo = Math.max(lo, Math.min(t1, t2)); hi = Math.min(hi, Math.max(t1, t2)); if (lo > hi) return false; }
  }
  return true;
}

/** Server-only adapter. Transport must authenticate credentials before retrieving
 * this object; tick pause flags are trusted lifecycle state, never model output.
 * No timers or IO here: host calls tick with monotonic elapsed seconds.
 */
export function createEncounterWorld({ registry, credentials, config = CLUB_WORLD_CONFIG }) {
  const cfg = structuredClone(config);
  const validRect = r => r && ['minX', 'maxX', 'minZ', 'maxZ'].every(k => Number.isFinite(r[k])) && r.minX < r.maxX && r.minZ < r.maxZ;
  if (!registry || !credentials || !validRect(cfg.bounds) || !validRect(cfg.recognitionArea) ||
      !Array.isArray(cfg.obstacles) || cfg.obstacles.length > 128 || !cfg.obstacles.every(validRect) ||
      !['radius', 'playerSpeed', 'npcSpeed', 'followDistance', 'conversationDistance', 'approachDistance', 'agreedDistance', 'safeDistance', 'recognitionDistance'].every(k => Number.isFinite(cfg[k]) && cfg[k] > 0 && cfg[k] <= 20) ||
      !ACTOR_IDS.every(id => finitePoint(cfg.spawns?.[id])) || !finitePoint(cfg.destinations?.vip) ||
      !finitePoint(cfg.fall?.anchor) || !finitePoint(cfg.fall?.endpoint) || !Number.isFinite(cfg.fall?.tolerance) || cfg.fall.tolerance <= 0 ||
      !cfg.obstacles.some(o => o.id === cfg.fall.tableId && inside(cfg.fall.endpoint, o))) throw new TypeError('Invalid club world configuration');
  const walkable = p => finitePoint(p) && inside(p, cfg.bounds, -cfg.radius) && !cfg.obstacles.some(o => inside(p, o, cfg.radius));
  const clear = (a, b) => walkable(a) && walkable(b) && !cfg.obstacles.some(o => intersects(a, b, o, cfg.radius));
  const sight = (a, b) => !cfg.obstacles.some(o => o.blocksSight && intersects(a, b, o));
  const conversationSpace = (a, b) => !cfg.obstacles.some(o => intersects(a, b, o));
  if (!Object.values(cfg.spawns).every(walkable) || !walkable(cfg.destinations.vip) || !walkable(cfg.fall.anchor)) throw new TypeError('Spawn or destination is outside walkable space');
  // A visibility graph around inflated rectangle corners is intentionally a small
  // club solver, not Unity NavMesh parity or general dynamic crowd navigation.
  const corners = cfg.obstacles.flatMap(o => {
    const m = cfg.radius + 0.025;
    return [point(o.minX - m, o.minZ - m), point(o.minX - m, o.maxZ + m), point(o.maxX + m, o.minZ - m), point(o.maxX + m, o.maxZ + m)];
  }).filter(walkable);
  const edges = corners.map((a, i) => corners.flatMap((b, j) => i !== j && clear(a, b) ? [{ j, cost: distance(a, b) }] : []));
  function route(start, end) {
    if (!walkable(end)) return null;
    if (clear(start, end)) return [end];
    const nodes = [...corners, start, end], from = corners.length, to = from + 1;
    const costs = nodes.map(() => Infinity), previous = nodes.map(() => -1), visited = new Set(); costs[from] = 0;
    const extra = nodes.map((a, i) => i < from ? [...edges[i], ...(clear(a, end) ? [{ j: to, cost: distance(a, end) }] : [])] :
      i === from ? corners.flatMap((b, j) => clear(a, b) ? [{ j, cost: distance(a, b) }] : []) : []);
    while (visited.size < nodes.length) {
      let current = -1;
      for (let i = 0; i < nodes.length; i++) if (!visited.has(i) && (current < 0 || costs[i] < costs[current])) current = i;
      if (current < 0 || !Number.isFinite(costs[current])) return null;
      if (current === to) { const path = []; for (let i = to; i !== from; i = previous[i]) path.unshift(nodes[i]); return path; }
      visited.add(current);
      for (const { j, cost } of extra[current]) if (costs[current] + cost < costs[j]) { costs[j] = costs[current] + cost; previous[j] = current; }
    }
    return null;
  }
  let positions, motions, loopId, sequence, accumulator = 0, frame = 0, destination = null, lastStage = null;
  function state() { const result = registry.trustedFull(credentials); return result.ok ? result.snapshot : null; }
  function sync(s) {
    if (s.loopId === loopId) return;
    loopId = s.loopId; positions = structuredClone(cfg.spawns); motions = Object.fromEntries(ACTOR_IDS.map(id => [id, 'idle']));
    sequence = -1; accumulator = 0; destination = null; lastStage = null; frame++;
  }
  const initial = state(); if (!initial) throw new TypeError('Unauthorized world credentials'); sync(initial);
  const ended = s => Boolean(s.catastrophe || s.victory || s.phase === 'unresolved');
  function observe(method, payload) { const s = state(); if (!s) return { ok: false, reason: 'unauthorized' }; return registry.trustedWorld(credentials, method, { ...payload, loopId: s.loopId, revision: s.revision }); }
  function towards(id, target, stop = 0, speed = cfg.npcSpeed) {
    const here = positions[id], d = distance(here, target);
    if (d <= stop + 0.01) return;
    const path = route(here, target); if (!path) { motions[id] = 'blocked'; return; }
    let remaining = Math.min(speed * 0.1, d - stop);
    for (const waypoint of path) {
      const length = distance(positions[id], waypoint);
      if (length <= remaining) { positions[id] = { ...waypoint }; remaining -= length; }
      else { const ratio = remaining / length; positions[id] = point(positions[id].x + (waypoint.x - positions[id].x) * ratio, positions[id].z + (waypoint.z - positions[id].z) * ratio); break; }
      if (remaining <= 0) break;
    }
    if (distance(here, positions[id]) > 0.001) motions[id] = 'moving';
  }
  function step() {
    let s = state(); if (!s) return false; sync(s); if (ended(s)) return false;
    motions = Object.fromEntries(ACTOR_IDS.map(id => [id, 'idle']));
    if (destination) { towards('player', destination, 0, cfg.playerSpeed); if (distance(positions.player, destination) < 0.02) destination = null; }
    for (const id of NPC_IDS) {
      const action = s.actors[id].action, target = positions[action.targetId];
      if (action.type === 'follow' && target) towards(id, target, cfg.followDistance);
      else if (action.type === 'approach' && target) towards(id, target, cfg.approachDistance);
      else if (action.type === 'intervene') towards(id, point((positions.theo.x + positions.maya.x) / 2, (positions.theo.z + positions.maya.z) / 2));
      else if (action.type === 'keep_distance' && target) {
        const d = distance(positions[id], target);
        if (d < cfg.agreedDistance - 0.01) { const dx = d > 0.001 ? (positions[id].x - target.x) / d : -1, dz = d > 0.001 ? (positions[id].z - target.z) / d : 0;
          towards(id, point(target.x + dx * cfg.agreedDistance, target.z + dz * cfg.agreedDistance)); }
      } else if (action.type === 'separate') {
        if (action.targetId === 'vip') towards(id, cfg.destinations.vip);
        else if (target) towards(id, target, cfg.followDistance);
      } else if (action.type === 'fall') motions[id] = 'fallen';
    }
    if (!s.recognized && inside(positions.maya, cfg.recognitionArea) && ['theo', 'affair_partner'].every(id => distance(positions.maya, positions[id]) <= cfg.recognitionDistance && sight(positions.maya, positions[id]))) {
      const result = observe('observeVisibility', { observerId: 'maya', inRecognitionArea: true, visibleActorIds: ['theo', 'affair_partner'] });
      if (!result.ok || !result.outcome.accepted) return false;
    }
    s = state();
    const a = positions.theo, b = positions.maya, l = positions.luca;
    const lengthSquared = (b.x - a.x) ** 2 + (b.z - a.z) ** 2;
    const t = lengthSquared > 0 ? ((l.x - a.x) * (b.x - a.x) + (l.z - a.z) * (b.z - a.z)) / lengthSquared : 0;
    const segmentDistance = distance(l, point(a.x + t * (b.x - a.x), a.z + t * (b.z - a.z)));
    const stage = {
      theoAtMaya: distance(a, b) <= 1.5 && conversationSpace(a, b),
      lucaBetween: t > 0.15 && t < 0.85 && segmentDistance <= 0.45 && distance(l, cfg.fall.anchor) <= cfg.fall.tolerance &&
        !cfg.obstacles.some(o => o.id !== cfg.fall.tableId && intersects(l, cfg.fall.endpoint, o)),
      renCanSee: sight(positions.ren, l),
      allInMediation: distance(l, b) <= 2 && distance(a, b) >= 2 && distance(a, b) <= 3.5 && conversationSpace(l, b) && conversationSpace(a, b) && conversationSpace(a, l),
      separated: s.scenario?.mediated === true && distance(a, b) >= cfg.safeDistance && distance(a, cfg.destinations.vip) <= 0.2 && s.actors.theo.action.type === 'separate' && s.actors.maya.action.type === 'separate',
    };
    if (JSON.stringify(stage) !== JSON.stringify(lastStage)) {
      const result = observe('observeStage', { stage }); if (!result.ok || !result.outcome.accepted) return false; lastStage = stage;
    }
    if (ended(state())) { frame++; return true; }
    const result = observe('advance', { seconds: 0.1, paused: false });
    if (!result.ok || !result.outcome.accepted) return false;
    frame++; return true;
  }
  function snapshot() {
    const s = state(); if (!s) return { ok: false, reason: 'unauthorized' }; sync(s);
    return { ok: true, version: cfg.version, loopId, frame, sequence, elapsedSeconds: s.scenario?.elapsedSeconds ?? 0,
      actors: Object.fromEntries(ACTOR_IDS.map(id => [id, { position: { ...positions[id] }, motion: s.actors[id].action.type === 'fall' ? 'fallen' : motions[id], action: structuredClone(s.actors[id].action) }])) };
  }
  return Object.freeze({
    snapshot,
    input(command) {
      const s = state(); if (!s) return { accepted: false, reason: 'unauthorized' }; sync(s);
      if (command?.loopId !== loopId) return { accepted: false, reason: 'stale_loop' };
      if (!Number.isSafeInteger(command.sequence) || command.sequence <= sequence || command.sequence < 0) return { accepted: false, reason: 'stale_sequence' };
      if (ended(s)) return { accepted: false, reason: 'encounter_ended' };
      if (!['move_to', 'stop'].includes(command.type) || Object.keys(command).some(k => !['type', 'loopId', 'sequence', 'destination'].includes(k))) return { accepted: false, reason: 'invalid_input' };
      if (command.type === 'move_to' && (!finitePoint(command.destination) || Object.keys(command.destination).some(k => !['x', 'z'].includes(k)) || !route(positions.player, command.destination))) return { accepted: false, reason: 'unwalkable_destination' };
      sequence = command.sequence; destination = command.type === 'stop' ? null : { ...command.destination };
      return { accepted: true, loopId, sequence };
    },
    tick(seconds, { paused = false, voiceActive = false, suspended = false } = {}) {
      if (!Number.isFinite(seconds) || seconds < 0 || ![paused, voiceActive, suspended].every(v => typeof v === 'boolean')) return { ok: false, reason: 'invalid_tick' };
      if (paused || voiceActive || suspended) { accumulator = 0; return { ok: true, steps: 0, snapshot: snapshot() }; }
      // Drop excessive elapsed time: a stall never fast-forwards the encounter.
      accumulator = Math.min(0.5, accumulator + Math.min(seconds, 0.5));
      let steps = 0;
      while (accumulator >= 0.1 - 1e-9 && steps < 5) { accumulator -= 0.1; if (!step()) { accumulator = 0; break; } steps++; }
      return { ok: true, steps, snapshot: snapshot() };
    },
    canConverse(npcId) {
      const s = state(); if (!s) return { ok: false, reason: 'unauthorized' }; sync(s);
      if (!NPC_IDS.includes(npcId)) return { ok: false, reason: 'unknown_npc' };
      if (ended(s)) return { ok: false, reason: 'encounter_ended' };
      return distance(positions.player, positions[npcId]) <= cfg.conversationDistance && sight(positions.player, positions[npcId]) ? { ok: true } : { ok: false, reason: 'out_of_range' };
    },
    reset(command) { const result = registry.reset(credentials, command); if (result.ok && result.outcome.accepted) sync(state()); return result; },
  });
}

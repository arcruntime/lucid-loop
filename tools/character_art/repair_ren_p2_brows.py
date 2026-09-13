"""Localized native P2 brow relief study; run with Blender --background --python.

Only selected main-head vertices move. No topology, UV, source file, eyelid,
mouth, skull silhouette, or component replacement is performed by this tool.
Every native vertex carries source_vertex_id for later independent patch merges.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys

import numpy as np
try:
    import bpy
    from mathutils import Vector
except ImportError:
    bpy = None

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'art/generated/characters/ren/parts-workflow-v1'
AUDIT = BASE / 'audits/p2-head-native'
SOURCE = ROOT / 'art/generated/characters/ren/parts-generation-v1/tripo/head/originals/model.fbx'
EXPECTED = 'cbb8f2da55edf79d138eafadf1609a89b15792f74937160244f2bd527fda1819'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def smoothstep(a, b, x):
    t = np.clip((x-a)/(b-a), 0, 1)
    return t*t*(3-2*t)


def prepare_biharmonic():
    """Solve a fixed-boundary fair patch with system Python/SciPy."""
    from scipy.sparse import coo_matrix, diags
    from scipy.sparse.linalg import spsolve
    data = np.load(AUDIT / 'analysis-data/mesh-000.npz')
    p, edges = data['positions'], data['edges']
    labels = np.load(AUDIT / 'analysis-data/mesh-000-component-labels.npz')['vertex_component']
    selected = ((labels == 0) & (p[:, 0] > .14) & (p[:, 2] > .111) & (p[:, 2] < .245) &
                (np.abs(p[:, 1]) > .025) & (np.abs(p[:, 1]) < .265))
    a, b = edges.T
    adjacency = coo_matrix((np.ones(2*len(edges)), (np.r_[a, b], np.r_[b, a])), shape=(len(p), len(p))).tocsr()
    degree = np.asarray(adjacency.sum(axis=1)).ravel()
    laplace = diags(np.ones(len(p))) - diags(1/np.maximum(degree, 1)) @ adjacency
    free = np.flatnonzero(selected)
    fixed = np.flatnonzero(~selected)
    matrix = laplace[:, free]
    rhs = -(matrix.T @ (laplace[:, fixed] @ p[fixed]))
    solution = spsolve((matrix.T @ matrix).tocsc(), rhs)
    report = {'source_sha256': EXPECTED, 'method': 'Fixed-boundary discrete biharmonic minimum-curvature patch; graph umbrella Laplacian',
              'coordinate_frame': 'Native world coordinates',
              'vertices': [{'source_vertex_id': int(i), 'before': p[i].tolist(), 'after': q.tolist()} for i, q in zip(free, solution)]}
    out = BASE / 'brows-v1/biharmonic-proposal.json'
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    print('BROW_BIHARMONIC_PROPOSAL', len(free), out)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--id', default='relief-v1')
    parser.add_argument('--iterations', type=int, default=24)
    parser.add_argument('--factor', type=float, default=.65)
    parser.add_argument('--fit-surface', action='store_true')
    parser.add_argument('--biharmonic', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    if not args.id.replace('-', '').isalnum() or not 1 <= args.iterations <= 100 or not 0 < args.factor <= 1:
        raise ValueError('Invalid study parameters')
    if digest(SOURCE) != EXPECTED:
        raise RuntimeError('Unexpected native head source')
    out = BASE / 'brows-v1' / args.id
    out.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(AUDIT / 'audit-scene.blend'))
    metadata = json.loads((AUDIT / 'import-metadata.json').read_text())
    obj = bpy.data.objects[metadata['objects'][0]['object']]
    mesh = obj.data
    native = np.array([tuple(v.co) for v in mesh.vertices], dtype=np.float64)
    world = np.array([tuple(obj.matrix_world @ v.co) for v in mesh.vertices], dtype=np.float64)
    labels = np.load(AUDIT / 'analysis-data/mesh-000-component-labels.npz')['vertex_component']
    saved_uv = [np.array([tuple(v.uv) for v in layer.data]) for layer in mesh.uv_layers]
    saved_faces = [tuple(p.vertices) for p in mesh.polygons]
    attr = mesh.attributes.get('source_vertex_id') or mesh.attributes.new('source_vertex_id', 'INT', 'POINT')
    attr.data.foreach_set('value', np.arange(len(native), dtype=np.int32))

    # Four soft patch borders, all chosen in the inspected native source frame.
    # Protect the aperture and lid (Z <= .111), glabella, temples, and forehead.
    x, y, z = world.T
    weights = smoothstep(.111, .135, z) * (1-smoothstep(.20, .245, z))
    weights *= smoothstep(.025, .055, np.abs(y)) * (1-smoothstep(.22, .265, np.abs(y)))
    weights *= ((labels == 0) & (x > .14))
    edges = np.array([tuple(e.vertices) for e in mesh.edges], dtype=np.int32)
    deg = np.bincount(edges.ravel(), minlength=len(native))
    work = native.copy()
    # Local umbrella fairing resolves the actual folded relief, including small
    # tangential redistributions; an X-only projection would retain overlapping
    # folds where the generated ridge doubles back in its YZ projection.
    for _ in range(args.iterations):
        sums = np.zeros_like(work)
        np.add.at(sums, edges[:, 0], work[edges[:, 1]])
        np.add.at(sums, edges[:, 1], work[edges[:, 0]])
        average = sums / np.maximum(deg[:, None], 1)
        work += args.factor * weights[:, None] * (average-work)
    fit_report = None
    if args.fit_surface:
        # Fit only healthy peripheral skin around the brow, never the ridge.
        # Low-degree local fairing supplies a smooth depth field after tangential
        # relaxation has unfolded the original dense ridge strips.
        control = ((labels == 0) & (np.abs(y) < .265) & (x > .13) &
                   (((z > .23) & (z < .30)) | ((z > .111) & (z < .125))))
        def basis(points):
            v, w = points[:, 1]/.25, (points[:, 2]-.17)/.1
            return np.stack([v**i * w**j for i in range(4) for j in range(4-i)], axis=1)
        # Equalize sampling density with per-YZ-bin weights so dense lid rings do
        # not overwhelm the relatively sparse healthy forehead.
        samples = world[control]
        bins = np.floor(samples[:, 1:]/.015).astype(int)
        _, inverse, counts = np.unique(bins, axis=0, return_inverse=True, return_counts=True)
        density = 1/np.sqrt(counts[inverse])
        design = basis(samples)
        coeffs = np.linalg.lstsq(design*density[:, None], samples[:, 0]*density, rcond=None)[0]
        work_world = np.array([tuple(obj.matrix_world @ Vector(p)) for p in work])
        target_x = basis(work_world) @ coeffs
        work_world[:, 0] += weights * (target_x-work_world[:, 0])
        inv = obj.matrix_world.inverted()
        work = np.array([tuple(inv @ Vector(p)) for p in work_world])
        # Floating point matrix roundtrips must not move unselected vertices.
        work[weights == 0] = native[weights == 0]
        fit_report = {'healthy_control_vertices': np.flatnonzero(control).tolist(),
                      'degree': 3, 'coefficients': coeffs.tolist(),
                      'rms_control_residual': float(np.sqrt(np.mean((design@coeffs-samples[:, 0])**2)))}
    if args.biharmonic:
        proposal = json.loads((BASE / 'brows-v1/biharmonic-proposal.json').read_text())
        if proposal['source_sha256'] != EXPECTED:
            raise RuntimeError('Biharmonic proposal source mismatch')
        work = native.copy()
        inverse = obj.matrix_world.inverted()
        for row in proposal['vertices']:
            i = row['source_vertex_id']
            if np.linalg.norm(world[i]-row['before']) > 1e-7:
                raise RuntimeError('Biharmonic native coordinate mismatch')
            work[i] = tuple(inverse @ Vector(row['after']))
    changed = np.flatnonzero(np.linalg.norm(work-native, axis=1) > 1e-12)
    if np.any(labels[changed] != 0) or np.any(world[changed, 2] <= .111):
        raise RuntimeError('Repair escaped inspected brow region')

    scene = bpy.context.scene
    scene.render.resolution_x = scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    camera = scene.camera
    records = []
    def apply(points):
        for vertex, co in zip(mesh.vertices, points):
            vertex.co = co
        mesh.update()
        # Imported split normals describe the old ridge. Recalculate from each
        # stage's actual geometry instead of carrying stale normals through edits.
        if mesh.has_custom_normals:
            mesh.normals_split_custom_set([(0, 0, 0)] * len(mesh.loops))

    def render(stage, label, position, target, scale):
        camera.location = position
        camera.rotation_euler = (Vector(target)-camera.location).to_track_quat('-Z', 'Y').to_euler()
        camera.data.ortho_scale = scale
        path = out / f'{stage}-{label}.png'
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        records.append({'file': path.name, 'sha256': digest(path)})

    for stage, coords in [('before', native), ('after', work)]:
        apply(coords)
        render(stage, 'front', (3, 0, 0), (0, 0, 0), 1.2)
        render(stage, 'quarter', (2.5, -1.7, 0), (0, 0, 0), 1.2)
        render(stage, 'profile', (0, -3, 0), (0, 0, 0), 1.2)
        render(stage, 'brow-quarter', (2.2, -1.2, .17), (.23, 0, .15), .62)

    actual = np.array([tuple(v.co) for v in mesh.vertices])
    changed = np.flatnonzero(np.any(actual != native, axis=1))
    unchanged = np.ones(len(native), bool)
    unchanged[changed] = False
    assert np.array_equal(actual[unchanged], native[unchanged])
    assert saved_faces == [tuple(p.vertices) for p in mesh.polygons]
    for old, layer in zip(saved_uv, mesh.uv_layers):
        assert np.array_equal(old, np.array([tuple(v.uv) for v in layer.data]))
    scene['construction_status'] = 'Localized brow relief study; no character likeness acceptance.'
    bpy.ops.wm.save_as_mainfile(filepath=str(out / 'ren-p2-brow-repair.blend'))
    report = {
        'status': 'UNREVIEWED_LOCAL_BROW_REPAIR', 'source_sha256': EXPECTED,
        'blender_version': bpy.app.version_string,
        'method': ('Fixed-boundary discrete biharmonic minimum-curvature patch; graph umbrella Laplacian' if args.biharmonic else
                   'Localized umbrella fairing of inspected main-head brow relief; smooth weights; unchanged boundary. No global fit.'),
        'parameters': vars(args), 'coordinate_frame': 'Native FBX mesh-local coordinates; source world front +X, up +Z',
        'optional_peripheral_surface_fit': fit_report,
        'changed_native_vertex_count': len(changed), 'removed_native_vertex_ids': [],
        'max_displacement_source_units': float(np.linalg.norm(actual-native, axis=1).max()),
        'topology_unchanged': True, 'uv_unchanged': True,
        'normals': 'Recomputed from actual geometry in both before and after views',
        'changed_native_vertices': [{'source_vertex_id': int(i), 'before': native[i].tolist(), 'after': actual[i].tolist()} for i in changed],
        'renders': records,
    }
    (out / 'construction.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    assert digest(SOURCE) == EXPECTED
    print('REN_BROW_REPAIR_READY', len(changed), report['max_displacement_source_units'], out, flush=True)


if __name__ == '__main__':
    if '--prepare-biharmonic' in sys.argv:
        prepare_biharmonic()
    else:
        main()

"""One source-preserving Ren hair detail comparison, without provider calls.

Run through ordinary Python; Blender is isolated in background. This writes only
hair-detail-recovery-v1. The current P2 head is temporary context: the selected
Studio H face will change the final fit. No procedural locks are reconstructed.
"""
from pathlib import Path
import hashlib
import json
import os
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'art/generated/characters/ren/parts-workflow-v1'
OUT = BASE / 'hair-detail-recovery-v1'
SOURCE = BASE / 'tripo-hair-lowpoly-v1/originals/model.fbx'
NATIVE = ROOT / 'art/generated/characters/ren/parts-generation-v1/tripo/hair/originals/model.fbx'
CONTEXT = BASE / 'complete-head-v2/Ren_CompleteHead_Review.blend'
CAP = BASE / 'head-accessories-v2/band-clearance-v1/Ren_Head_Accessories.blend'
SOURCE_HASH = '236db190bd05e48750fb68d23f381538a4917fa8c1ac6255dee533a4c0bdf036'
NATIVE_HASH = '9f8c2cb79c763bb048b4c0b62c058e2357802e86012badf0ce0a15f240a083c9'
CAP_HASH = 'bff2a53430d58535e6f2aef8d271e8a8774a6c8015185a4cd5f2b74168ab805d'
BLENDER = Path('C:/Program Files/Blender Foundation/Blender 5.1/blender.exe')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def dump(path, value):
    path.write_text(json.dumps(value, indent=2, allow_nan=False) + '\n', encoding='utf-8')


def signature(obj):
    import numpy as np
    mesh = obj.data
    fields = {'vertices': np.asarray([v.co[:] for v in mesh.vertices], np.float32),
              'loops': np.asarray([loop.vertex_index for loop in mesh.loops], np.int32),
              'polygons': np.asarray([(p.loop_start, p.loop_total) for p in mesh.polygons], np.int32),
              'matrix': np.asarray(obj.matrix_world, np.float64)}
    if mesh.shape_keys:
        for key in mesh.shape_keys.key_blocks:
            fields['shape:' + key.name] = np.asarray([v.co[:] for v in key.data], np.float32)
    return {name: hashlib.sha256(value.tobytes()).hexdigest() for name, value in fields.items()}


def worker():
    import bpy
    import math
    import numpy as np
    from collections import Counter
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    assert sha(SOURCE) == SOURCE_HASH and sha(NATIVE) == NATIVE_HASH and sha(CAP) == CAP_HASH
    sources = {name: {'path': path.relative_to(ROOT).as_posix(), 'sha256': sha(path)}
               for name, path in [('returned', SOURCE), ('native', NATIVE), ('temporary_head_context', CONTEXT), ('cap', CAP)]}
    bpy.ops.wm.open_mainfile(filepath=str(CONTEXT))
    bpy.context.preferences.filepaths.save_version = 0
    head = bpy.data.objects['Ren_Head']
    head_before = signature(head)
    for obj in list(bpy.context.scene.objects):
        if obj.name.startswith('Ren_Hair_') or obj.type in {'LIGHT', 'CAMERA'}:
            bpy.data.objects.remove(obj, do_unlink=True)
        elif obj.type == 'MESH' and obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks[1:]:
                key.value = 1 if key.name == 'mouthSeal' else 0
    cap = bpy.data.objects['Ren_Cap']
    cap_before = signature(cap)
    bpy.context.view_layer.update()
    evaluated = head.evaluated_get(bpy.context.evaluated_depsgraph_get())
    evaluated_mesh = evaluated.to_mesh()
    head_bvh = BVHTree.FromPolygons([head.matrix_world @ v.co for v in evaluated_mesh.vertices],
                                  [list(p.vertices) for p in evaluated_mesh.polygons])
    evaluated.to_mesh_clear()
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(SOURCE), use_anim=False)
    raw = next(o for o in bpy.data.objects if o not in before and o.type == 'MESH')
    raw.name = 'Ren_Hair_Tripo9026_Unchanged'
    original_matrix = [list(row) for row in raw.matrix_world]
    original_signature = signature(raw)
    raw.matrix_world.translation += Vector((-.09, 0, .22))
    coordinates = np.asarray([v.co[:] for v in raw.data.vertices], np.float64)
    world = np.asarray([list(raw.matrix_world @ v.co) for v in raw.data.vertices], np.float64)
    faces = np.asarray([list(p.vertices) for p in raw.data.polygons], np.int32)
    assert faces.shape == (9026, 3)
    parents = list(range(len(coordinates)))

    def find(i):
        while parents[i] != i:
            parents[i] = parents[parents[i]]
            i = parents[i]
        return i

    edges = Counter()
    for face in faces:
        for a, b in zip(face, np.roll(face, -1)):
            edges[tuple(sorted((int(a), int(b))))] += 1
            ra, rb = find(int(a)), find(int(b))
            if ra != rb:
                parents[rb] = ra
    component_faces = Counter(find(int(face[0])) for face in faces)
    removal = []
    # Delete only suspect tiny fragments/slivers/branch faces when every vertex,
    # edge midpoint and centroid lies at least 0.004 inside temporary head skin.
    # This deliberately retains any questionable strand visible outside the head.
    for index, face in enumerate(faces):
        p = world[face]
        lengths = np.linalg.norm(p - np.roll(p, -1, axis=0), axis=1)
        area2 = float(np.linalg.norm(np.cross(p[1] - p[0], p[2] - p[0])))
        slenderness = area2 / max(float(lengths.max() ** 2), 1e-20)
        branched = any(edges[tuple(sorted((int(a), int(b))))] > 2 for a, b in zip(face, np.roll(face, -1)))
        tiny_component = component_faces[find(int(face[0]))] <= 16
        if not (branched or tiny_component or slenderness < .06):
            continue
        samples = np.vstack((p, (p + np.roll(p, -1, axis=0)) / 2, p.mean(axis=0)))
        depths = []
        for sample in samples:
            point = Vector(sample)
            closest, normal, _, distance = head_bvh.find_nearest(point)
            if closest is None or (point - closest).dot(normal) > -.004:
                break
            depths.append(float(distance))
        if len(depths) == 7:
            removal.append({'source_triangle': index, 'vertices': face.tolist(),
                            'reason': {'branched_edge': branched, 'small_component': tiny_component,
                                       'slenderness': slenderness}, 'minimum_sample_depth': min(depths)})
    removed_ids = {entry['source_triangle'] for entry in removal}
    kept_ids = [i for i in range(len(faces)) if i not in removed_ids]
    mesh = bpy.data.meshes.new('Ren_TripoHair_HiddenCleanup')
    # Retain source vertex order and coordinates, including now unused vertices,
    # so every kept vertex can be checked exactly against the imported source.
    mesh.from_pydata(coordinates.tolist(), [], faces[kept_ids].tolist())
    mesh.update()
    candidate = bpy.data.objects.new('Ren_Hair_TripoDetail_Comparison', mesh)
    bpy.context.scene.collection.objects.link(candidate)
    candidate.matrix_world = raw.matrix_world.copy()
    material = bpy.data.materials.new('Neutral_Hair_Clay_No_Texture')
    material.use_nodes = True
    shader = material.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (.49, .47, .43, 1)
    shader.inputs['Roughness'].default_value = .8
    for obj in (raw, candidate):
        obj.data.materials.clear()
        obj.data.materials.append(material)
        for face in obj.data.polygons:
            face.use_smooth = True
    # Basis has absolutely no nonrigid fit. A separate optional morph makes one
    # provisional cap preview, keeping the original cap-off detail recoverable.
    candidate.shape_key_add(name='Basis')
    key = candidate.shape_key_add(name='capOn_TEMPORARY_P2_FIT')
    cap_points = [cap.matrix_world @ v.co for v in cap.data.vertices]
    cap_faces = [list(p.vertices) for p in cap.data.polygons]
    skirt_start = len(cap_points)
    cap_points.extend(cap_points[i] - Vector((0, 0, .09)) for i in range(33))
    cap_faces.extend([i, i + 1, skirt_start + i + 1, skirt_start + i] for i in range(32))
    cap_bvh = BVHTree.FromPolygons(cap_points, cap_faces)
    origin = Vector((-.060, 0, .20))
    inverse = candidate.matrix_world.inverted()
    moved, deficits, misses = [], [], []
    for i, co in enumerate(world):
        point = Vector(co)
        theta = math.atan2(point.y, point.x + .043)
        band = .218 + .030 * math.cos(theta) + .011 * math.sin(theta)
        if point.z < band - .045:
            continue
        ray = point - origin
        direction = ray.normalized()
        skin, _, _, inner = head_bvh.ray_cast(origin, direction, 2)
        shell, _, _, outer = cap_bvh.ray_cast(origin, direction, 2)
        if shell is None or skin is None:
            misses.append(i)
            continue
        minimum, maximum = inner + .002, outer - .002
        if maximum < minimum:
            deficits.append({'vertex': i, 'clearance': outer - inner})
            continue
        target = origin + direction * max(minimum, min(ray.length, maximum))
        delta = (point - target).length
        if delta > 1e-7:
            key.data[i].co = inverse @ target
            moved.append({'vertex': i, 'distance': delta})
    key.value = 0
    assert np.array_equal(np.asarray([v.co[:] for v in mesh.vertices]), coordinates)
    report = {'status': 'BOUNDED_DETAIL_RECOVERY_COMPARISON_NOT_FINAL_FIT_OR_LIKENESS', 'blender': bpy.app.version_string,
              'sources': sources, 'input_counts': {'native_triangles': 51054, 'returned_triangles': 9026, 'returned_vertices': len(coordinates)},
              'candidate_counts': {'triangles': len(kept_ids), 'vertices_retaining_source_ids': len(coordinates), 'uv_layers': len(mesh.uv_layers)},
              'removed_triangles': removal, 'kept_source_triangle_ids': kept_ids,
              'basis_geometry': 'All source vertex coordinates and kept face winding exactly preserved; no decimation, smoothing, generated locks or silhouette deformation.',
              'original_import_matrix': original_matrix, 'temporary_review_matrix': [list(row) for row in candidate.matrix_world],
              'temporary_rigid_translation': [-.09, 0, .22], 'temporary_uniform_scale': 1,
              'cleanup_scope': 'Only suspect sliver/branch/small-component triangles whose seven samples are inside temporary P2 head by >=0.004 native units; context-dependent cleanup, not a general invisible-surface proof.',
              'cap_fit': {'control': key.name, 'moved_vertices': moved, 'insufficient_corridors': deficits, 'ray_misses': misses,
                          'method': 'One radial projection of roots above local band minus0.045 into head+0.002/cap-0.002 interval, including virtual fitting skirt. Below-threshold visible tips unchanged.',
                          'status': 'Temporary P2-only preview; final chosen Studio H head will require a fresh fit; triangle collisions are not certified absent.'},
              'retained_source_signature_before_placement': original_signature,
              'limitations': ['Returned mesh has no UVs; no texture bake or material integration performed.',
                              'External crossbars and slivers retained when they could affect silhouette.',
                              'Native reduction already blunts some crown roots and introduces angular thin tips.',
                              'Temporary P2 face is no longer selected facial baseline.']}
    dump(OUT / 'geometry-report.json', report)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x = scene.render.resolution_y = 960
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.view_transform = 'AgX'
    scene.world = bpy.data.worlds.new('Detail_Recovery_Neutral_World')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.07, .075, .09, 1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .65
    target = Vector((-.025, 0, .09))
    camera = bpy.data.objects.new('Detail_Recovery_Camera', bpy.data.cameras.new('Detail_Recovery_Camera'))
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 1.45
    for name, location, energy in [('Key', (2,-2,3), 230), ('Fill', (2,2,1), 150), ('Rim', (-2,1,2), 170)]:
        obj = bpy.data.objects.new(name, bpy.data.lights.new(name, 'AREA'))
        scene.collection.objects.link(obj)
        obj.location = location
        obj.rotation_euler = (target-obj.location).to_track_quat('-Z', 'Y').to_euler()
        obj.data.energy, obj.data.size = energy, 3
    captures = []
    for label, cap_on, active, views in [('candidate-cap-off', False, candidate, [('front',0),('quarter',-35),('profile',-90)]),
                                        ('candidate-cap-on', True, candidate, [('front',0),('quarter',-35),('profile',-90)]),
                                        ('source-cap-off', False, raw, [('front',0),('quarter',-35)])]:
        raw.hide_render = active != raw
        candidate.hide_render = active != candidate
        cap.hide_render = not cap_on
        key.value = float(cap_on)
        for view, angle in views:
            angle = math.radians(angle)
            camera.location = target + Vector((math.cos(angle), math.sin(angle), 0)) * 3
            camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
            path = OUT / (label + '-' + view + '.png')
            scene.render.filepath = str(path)
            bpy.ops.render.render(write_still=True)
            captures.append({'file': path.name, 'sha256': sha(path), 'camera_position': list(camera.location),
                             'target': list(target), 'orthographic_scale': camera.data.ortho_scale,
                             'hair_material': 'Uniform neutral clay; no strand textures or normal maps', 'cap_fit_weight': float(cap_on)})
            dump(OUT / 'captures.json', captures)
            print('RENDERED', path.name, flush=True)
    raw.hide_render = True
    raw.hide_set(True)
    candidate.hide_render = False
    cap.hide_render = True
    key.value = 0
    bpy.ops.object.select_all(action='DESELECT')
    candidate.select_set(True)
    bpy.context.view_layer.objects.active = candidate
    outputs = []
    for extension in ('fbx', 'glb'):
        path = OUT / ('Ren_TripoHair_Detail_Comparison.' + extension)
        if extension == 'fbx':
            bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH'},
                                    use_mesh_modifiers=False, use_triangles=False, bake_anim=False, add_leaf_bones=False)
        else:
            bpy.ops.export_scene.gltf(filepath=str(path), export_format='GLB', use_selection=True,
                                     export_apply=False, export_animations=False, export_materials='EXPORT')
        outputs.append({'file': path.name, 'sha256': sha(path), 'bytes': path.stat().st_size})
    assert signature(head) == head_before and signature(cap) == cap_before
    assert all(sha(ROOT / item['path']) == item['sha256'] for item in sources.values())
    for image in bpy.data.images:
        if image.size[0] and not image.packed_file:
            try:
                image.pack()
            except RuntimeError:
                pass
    path = OUT / 'Ren_TripoHair_Detail_Review.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(path))
    outputs.append({'file': path.name, 'sha256': sha(path), 'bytes': path.stat().st_size})
    report.update({'outputs': outputs, 'head_and_cap_coordinates_shapes_matrices_unchanged': True,
                   'all_input_files_unchanged': True, 'captures': captures})
    dump(OUT / 'geometry-report.json', report)
    print('DETAIL_RECOVERY_READY', len(kept_ids), str(path), flush=True)


def verify():
    import bpy
    from mathutils.kdtree import KDTree
    report = json.loads((OUT / 'geometry-report.json').read_text(encoding='utf-8'))
    hashes = {item['file']: item['sha256'] for item in report['outputs']}
    bpy.ops.wm.open_mainfile(filepath=str(OUT / 'Ren_TripoHair_Detail_Review.blend'))
    source = bpy.data.objects['Ren_Hair_TripoDetail_Comparison']
    expected = {}
    for state in ('Basis', 'capOn_TEMPORARY_P2_FIT'):
        points = [source.matrix_world @ v.co for v in source.data.shape_keys.key_blocks[state].data]
        tree = KDTree(len(points))
        for i, point in enumerate(points):
            tree.insert(point, i)
        tree.balance()
        expected[state] = tree
    records = []
    for extension in ('fbx', 'glb'):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path = OUT / ('Ren_TripoHair_Detail_Comparison.' + extension)
        assert sha(path) == hashes[path.name]
        if extension == 'fbx':
            bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
        else:
            bpy.ops.import_scene.gltf(filepath=str(path))
        meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
        assert len(meshes) == 1
        obj = meshes[0]
        obj.data.calc_loop_triangles()
        triangles = len(obj.data.loop_triangles)
        assert triangles == report['candidate_counts']['triangles']
        errors = {}
        for state, tree in expected.items():
            key = next(k for k in obj.data.shape_keys.key_blocks if k.name.endswith(state))
            distances = [tree.find(obj.matrix_world @ v.co)[2] for v in key.data]
            errors[state] = max(distances)
            assert errors[state] < 1e-6
        records.append({'file': path.name, 'sha256': sha(path), 'rendered_triangles': triangles,
                        'vertices_after_import': len(obj.data.vertices), 'uv_layers': len(obj.data.uv_layers),
                        'morph_names': [key.name for key in obj.data.shape_keys.key_blocks],
                        'maximum_state_vertex_distance_native_units': errors})
    assert all(sha(OUT / name) == value for name, value in hashes.items())
    assert all(sha(ROOT / item['path']) == item['sha256'] for item in report['sources'].values())
    dump(OUT / 'verification.json', {'status': 'PASS', 'blender': bpy.app.version_string,
                                     'export_reimports': records, 'frozen_outputs_unchanged': True,
                                     'all_source_files_unchanged': True,
                                     'scope': 'Counts and both position states checked; this does not certify cap fit, UV readiness or production likeness.'})
    print('DETAIL_RECOVERY_EXPORTS_VERIFIED', flush=True)


def main():
    if '--verify-worker' in sys.argv:
        verify()
        return
    if '--worker' in sys.argv:
        worker()
        return
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / '.gitignore').write_text('.blender-user/\n*.log\n*.blend1\n', encoding='utf-8')
    env = os.environ.copy()
    env['BLENDER_USER_RESOURCES'] = str(OUT / '.blender-user')
    checking = '--verify' in sys.argv
    command = [str(BLENDER), '--background', '--factory-startup', '--threads', '8', '--python', str(Path(__file__).resolve()), '--', '--verify-worker' if checking else '--worker']
    logfile = OUT / ('verify.log' if checking else 'build.log')
    with logfile.open('w', encoding='utf-8') as log:
        result = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL,
                                env=env, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    print('Blender exit:', result.returncode, 'log:', logfile)
    raise SystemExit(result.returncode)


if __name__ == '__main__':
    main()

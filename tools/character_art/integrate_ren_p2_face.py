"""Combine reviewed local Ren facial patches in an isolated Blender study.

Native sources are never overwritten. All retained vertex/face IDs, oral shape
keys and UVs survive filtering of obsolete generated eye/lash components.
"""
from __future__ import annotations

from collections import defaultdict
import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy
import numpy as np
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'art/generated/characters/ren/parts-workflow-v1'
OUT = BASE / 'face-integration-v1'
SHA = 'cbb8f2da55edf79d138eafadf1609a89b15792f74937160244f2bd527fda1819'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def retain_faces(obj, keep):
    """Filter faces with explicit transfer of all morphs, UVs and source IDs."""
    old = obj.data
    used = sorted({v for p in old.polygons if p.index in keep for v in p.vertices})
    remap = {old_id: new_id for new_id, old_id in enumerate(used)}
    mesh = bpy.data.meshes.new('Ren_Head_LocalRepairs')
    polygons = [p for p in old.polygons if p.index in keep]
    mesh.from_pydata([tuple(old.vertices[i].co) for i in used], [],
                     [[remap[v] for v in p.vertices] for p in polygons])
    for mat in old.materials:
        mesh.materials.append(mat)
    for new, original in zip(mesh.polygons, polygons):
        new.material_index = original.material_index
        new.use_smooth = original.use_smooth
    for layer in old.uv_layers:
        target = mesh.uv_layers.new(name=layer.name)
        values = [tuple(layer.data[i].uv) for p in polygons for i in p.loop_indices]
        target.data.foreach_set('uv', np.asarray(values, np.float32).ravel())
    for name, domain, values in [
        ('source_vertex_id', 'POINT', [old.attributes['source_vertex_id'].data[i].value for i in used]),
        ('source_face_id', 'FACE', [old.attributes['source_face_id'].data[p.index].value for p in polygons]),
    ]:
        attr = mesh.attributes.new(name, 'INT', domain)
        attr.data.foreach_set('value', np.asarray(values, np.int32))
    shape_data = []
    if old.shape_keys:
        for key in old.shape_keys.key_blocks:
            shape_data.append((key.name, [tuple(key.data[i].co) for i in used], key.value))
    obj.data = mesh
    for name, coords, value in shape_data:
        key = obj.shape_key_add(name=name)
        key.data.foreach_set('co', np.asarray(coords, np.float32).ravel())
        key.value = value
    return {'before_vertices': len(old.vertices), 'after_vertices': len(mesh.vertices),
            'before_faces': len(old.polygons), 'after_faces': len(mesh.polygons)}


def material(name, srgb, roughness=.85):
    color = [v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in srgb]
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    bsdf = result.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = roughness
    result.diffuse_color = (*color, 1)
    return result


def lip_face_ids():
    """Find lip faces inside the outer verified native vermilion loop."""
    data = np.load(BASE / 'audits/p2-head-native/analysis-data/mesh-000.npz')
    labels = np.load(BASE / 'audits/p2-head-native/analysis-data/mesh-000-component-labels.npz')['vertex_component']
    report = json.loads((BASE / 'audits/p2-head-native/mouth-construction-topology.json').read_text())
    loop = report['native_lip_edge_loop_traces']['closed_loops'][3]
    blocked = {tuple(sorted(data['edges'][i])) for i in loop['edge_ids']}
    edge_faces = defaultdict(list)
    faces = {}
    for fi, (start, size) in enumerate(zip(data['face_start'], data['face_size'])):
        f = list(data['loop_vertex'][start:start+size])
        if labels[f[0]] != 0:
            continue
        faces[fi] = f
        for a, b in zip(f, f[1:]+f[:1]):
            edge = tuple(sorted((a, b)))
            if edge not in blocked:
                edge_faces[edge].append(fi)
    adjacency = defaultdict(set)
    for group in edge_faces.values():
        for fi in group:
            adjacency[fi].update(group)
    unseen = set(faces)
    groups = []
    while unseen:
        stack = [min(unseen)]
        found = set()
        while stack:
            fi = stack.pop()
            if fi in found:
                continue
            found.add(fi)
            unseen.discard(fi)
            stack.extend(adjacency[fi]-found)
        groups.append(found)
    return set().union(*sorted(groups, key=len)[:-1])


def main():
    global OUT
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--v2', action='store_true')
    argv = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    args = parser.parse_args(argv)
    if args.v2:
        OUT = BASE / 'face-integration-v2'
    OUT.mkdir(parents=True, exist_ok=True)
    mouth_path = BASE / 'mouth-v1/Ren_P2_Oral_Reconstruction.blend'
    oral_uv_path = BASE / 'face-uv-v1/oral-integration/Ren_P2_Oral_FaceUV.blend'
    eyes_path = BASE / 'eyes-v1/Ren_P2_Eyes_Neutral.blend'
    if args.v2:
        mouth_path = BASE / 'mouth-v1/corner-clearance-v3/Ren_P2_Oral_Reconstruction.blend'
        oral_uv_path = BASE / 'face-uv-v1/oral-integration-v3/Ren_P2_Oral_FaceUV.blend'
        eyes_path = BASE / 'eyes-v1/lid-repair-v2/Ren_P2_Eyes_Neutral.blend'
    brow_path = BASE / 'brows-v1/relief-v4/construction.json'
    mouth = json.loads((mouth_path.parent / 'source-id-patch.json').read_text())
    brow = json.loads(brow_path.read_text())
    if mouth['source_sha256'] != SHA or brow['source_sha256'] != SHA:
        raise RuntimeError('Patch sources differ')
    inputs = {str(p.relative_to(ROOT)): digest(p) for p in (mouth_path, eyes_path, brow_path)}
    # Use the UV-only derivative after confirming it corresponds to the current
    # source geometry, including any final interior clearance corrections.
    uv_report = json.loads((oral_uv_path.parent / 'integration-report.json').read_text())
    if uv_report['source_blend_sha256'] != digest(mouth_path) or uv_report['derived_blend_sha256'] != digest(oral_uv_path):
        raise RuntimeError('UV derivative is stale or differs from its verified readback')
    inputs[str(oral_uv_path.relative_to(ROOT))] = digest(oral_uv_path)
    bpy.ops.wm.open_mainfile(filepath=str(oral_uv_path))
    head = bpy.data.objects[mouth['source_object_name']]
    head.name = 'Ren_Head'
    labels = np.load(BASE / 'audits/p2-head-native/analysis-data/mesh-000-component-labels.npz')['vertex_component']
    ids = np.array([v.value for v in head.data.attributes['source_vertex_id'].data])
    keep = {p.index for p in head.data.polygons if all(ids[v] < 0 or labels[ids[v]] == 0 for v in p.vertices)}
    filter_report = retain_faces(head, keep)
    id_to_vertex = {v.value: i for i, v in enumerate(head.data.attributes['source_vertex_id'].data) if v.value >= 0}
    applied = []
    for row in brow['changed_native_vertices']:
        i = id_to_vertex.get(row['source_vertex_id'])
        if i is None:
            raise RuntimeError('Brow vertex unexpectedly absent from oral head')
        vertex = head.data.vertices[i]
        if (vertex.co-Vector(row['before'])).length > 1e-7:
            raise RuntimeError('Brow and mouth native coordinates disagree')
        delta = Vector(row['after'])-vertex.co
        vertex.co += delta
        for key in head.data.shape_keys.key_blocks:
            key.data[i].co += delta
        applied.append(row['source_vertex_id'])
    head.data.update()
    if head.data.has_custom_normals:
        head.data.normals_split_custom_set([(0, 0, 0)] * len(head.data.loops))
    stitch_report = None
    if args.v2:
        sys.path.insert(0, str(ROOT / 'tools/character_art'))
        from stitch_ren_eye_patches import stitch
        contract_paths = [eyes_path.parent / f'annulus-{side}-contract.json' for side in ('L', 'R')]
        for p in contract_paths:
            inputs[str(p.relative_to(ROOT))] = digest(p)
        stitch_report = stitch(head, [json.loads(p.read_text()) for p in contract_paths], brow['changed_native_vertices'])
    with bpy.data.libraries.load(str(eyes_path), link=False) as (available, selected):
        selected.objects = [name for name in available.objects if name.startswith('Ren_Eye_') and 'SkinAnnulus' not in name]
    eye_objects = []
    for obj in selected.objects:
        if obj:
            bpy.context.scene.collection.objects.link(obj)
            eye_objects.append(obj)
    if not eye_objects:
        raise RuntimeError('No fitted eye components found')
    skin = material('Ren_Draft_Skin', (.83, .68, .61))
    lips = material('Ren_Draft_RoseLips', (.64, .36, .38), .63)
    skin_slots = [i for i, mat in enumerate(head.data.materials) if mat.name != 'Ren_Oral_Cavity']
    for i in skin_slots:
        head.data.materials[i] = skin
    lip_slot = len(head.data.materials)
    head.data.materials.append(lips)
    lip_faces = lip_face_ids()
    for poly, source in zip(head.data.polygons, head.data.attributes['source_face_id'].data):
        if source.value in lip_faces:
            poly.material_index = lip_slot
    for obj in eye_objects:
        for i, mat in enumerate(obj.data.materials):
            if 'Clay' in mat.name or 'Skin' in mat.name:
                obj.data.materials[i] = skin
    # Neutral source opening remains the basis for provenance. The review starts
    # from the authored seal; this is an explicit preset, not a hidden mesh edit.
    head.data.shape_keys.key_blocks['mouthSeal'].value = 1
    for obj in eye_objects:
        if obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks:
                if key.name != 'Basis':
                    key.value = 0
    # The mouth builder saves geometry only, so add a deterministic review setup.
    sys.path.insert(0, str(ROOT / 'tools/character_art'))
    from build_ren_p2_mouth import setup_review
    scene, camera, aim = setup_review()
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = scene.render.resolution_y = 1024
    renders = []
    poses = ['closed-rest', 'source-rest', 'open-A']
    if args.v2:
        poses += ['blink-half', 'blink-closed', 'open-A-blink']
    for pose in poses:
        for obj in scene.objects:
            if obj.type == 'MESH' and obj.data.shape_keys:
                for key in obj.data.shape_keys.key_blocks:
                    if key.name == 'mouthSeal': key.value = 1 if pose in ('closed-rest', 'blink-half', 'blink-closed') else 0
                    if key.name == 'jawOpen_A': key.value = 1 if pose in ('open-A', 'open-A-blink') else 0
                    if key.name.startswith('eyeBlink'): key.value = 1 if pose in ('blink-closed', 'open-A-blink') else .5 if pose == 'blink-half' else 0
        for view, location in [('front', (3, 0, 0)), ('quarter', (2.5, -1.7, 0)), ('profile', (0, -3, 0))]:
            camera.location = location
            aim(camera, (0, 0, 0))
            camera.data.ortho_scale = 1.12
            path = OUT / f'{pose}-{view}.png'
            scene.render.filepath = str(path)
            bpy.ops.render.render(write_still=True)
            renders.append({'file': path.name, 'sha256': digest(path)})
    for obj in scene.objects:
        if obj.type == 'MESH' and obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks:
                if key.name != 'Basis': key.value = 1 if key.name == 'mouthSeal' else 0
    scene['construction_status'] = 'Integrated local repair and flat-color study. Painted likeness and full blink still pending.'
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Ren_P2_Face_Integrated.blend'))
    report = {'status': 'UNREVIEWED_INTEGRATED_FACE', 'input_sha256': inputs,
              'native_source_sha256': SHA, 'oral_head_filter': filter_report,
              'brow_source_vertices_applied': applied, 'eye_objects': [o.name for o in eye_objects],
              'eyelid_stitch': stitch_report,
              'default_pose': {'mouthSeal': 1, 'jawOpen_A': 0},
              'materials': 'Flat temporary skin and rose lip colors; no artist texture projection yet.',
              'renders': renders, 'blend_sha256': digest(OUT / 'Ren_P2_Face_Integrated.blend')}
    (OUT / 'integration.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    for name, sha in inputs.items():
        if digest(ROOT / name) != sha:
            raise RuntimeError('Input changed during integration; rebuild from a consistent checkpoint')
    print('REN_FACE_INTEGRATED', OUT, flush=True)


if __name__ == '__main__':
    main()

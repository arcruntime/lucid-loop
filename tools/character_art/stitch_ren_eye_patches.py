"""Stitch Ren-specific eyelid annuli into the source-ID-carrying oral head."""
from pathlib import Path
import json

import bpy
import numpy as np
from mathutils import Vector


def stitch(head, contracts, brow_rows):
    from integrate_ren_p2_face import retain_faces
    removed = set().union(*(set(c['removed_source_face_ids']) for c in contracts))
    face_ids = head.data.attributes['source_face_id'].data
    retain_faces(head, {p.index for p in head.data.polygons if face_ids[p.index].value not in removed})
    old = head.data
    positions = [tuple(v.co) for v in old.vertices]
    source_ids = [v.value for v in old.attributes['source_vertex_id'].data]
    native = {sid: i for i, sid in enumerate(source_ids) if sid >= 0}
    faces = [list(p.vertices) for p in old.polygons]
    source_faces = [v.value for v in old.attributes['source_face_id'].data]
    face_parts = [0 if sid >= 0 else 1 for sid in source_faces]
    vertex_parts = [0 if sid >= 0 else 1 for sid in source_ids]
    patch_indices = [-1] * len(positions)
    slots = [p.material_index for p in old.polygons]
    uv = {layer.name: [tuple(v.uv) for v in layer.data] for layer in old.uv_layers}
    shapes = {key.name: [tuple(v.co) for v in key.data] for key in old.shape_keys.key_blocks}
    values = {key.name: key.value for key in old.shape_keys.key_blocks}
    matrix = head.matrix_world
    inverse = matrix.inverted()
    brow = {r['source_vertex_id']: matrix.to_3x3() @ (Vector(r['after'])-Vector(r['before'])) for r in brow_rows}
    reports = []
    for part, contract in enumerate(contracts, start=2):
        name = 'eyeBlink' + contract['side']
        if name not in shapes:
            shapes[name] = positions.copy()
            values[name] = 0
        remap = {}
        weights = {r['vertex_index']: r['boundary_weights'] for r in contract['generated_vertex_boundary_weights']}
        max_boundary_error = 0
        for index, (sid, point, blink_point) in enumerate(zip(contract['source_vertex_ids'], contract['neutral_positions'], contract['blink_positions'])):
            if sid >= 0:
                if sid not in native:
                    raise RuntimeError(f'Missing healthy eye boundary source vertex {sid}')
                dest = native[sid]
                expected = Vector(point) + brow.get(sid, Vector((0, 0, 0)))
                error = (matrix @ Vector(positions[dest])-expected).length
                max_boundary_error = max(max_boundary_error, error)
                if error > 2e-7:
                    raise RuntimeError(f'Eye boundary patch differs from corrected head at {sid}: {error}')
                remap[index] = dest
                continue
            offset = Vector((0, 0, 0))
            for row in weights.get(index, []):
                offset += brow.get(row['source_vertex_id'], Vector((0, 0, 0))) * row['weight']
            neutral = tuple(inverse @ (Vector(point)+offset))
            closed = tuple(inverse @ (Vector(blink_point)+offset))
            remap[index] = len(positions)
            positions.append(neutral)
            source_ids.append(-1)
            vertex_parts.append(part)
            patch_indices.append(index)
            for shape_name, data in shapes.items():
                data.append(closed if shape_name == name else neutral)
        added_faces = []
        for patch_face in contract['faces']:
            added_faces.append(len(faces))
            faces.append([remap[i] for i in patch_face])
            slots.append(0)
            source_faces.append(-1)
            face_parts.append(part)
            for data in uv.values():
                data.extend([(0, 0)] * len(patch_face))
        reports.append({'side': contract['side'], 'boundary_max_error': max_boundary_error,
                        'patch_vertex_to_head_vertex': {str(k): v for k, v in remap.items()},
                        'new_head_face_ids': added_faces})
    mesh = bpy.data.meshes.new('Ren_Head_StitchedLids')
    mesh.from_pydata(positions, [], faces)
    for mat in old.materials:
        mesh.materials.append(mat)
    for p, slot in zip(mesh.polygons, slots):
        p.material_index = slot
        p.use_smooth = True
    for name, data in uv.items():
        layer = mesh.uv_layers.new(name=name)
        layer.data.foreach_set('uv', np.asarray(data, np.float32).ravel())
    for name, domain, data in [('source_vertex_id','POINT',source_ids), ('source_face_id','FACE',source_faces),
                               ('face_part','FACE',face_parts), ('vertex_part','POINT',vertex_parts),
                               ('patch_vertex_id','POINT',patch_indices)]:
        attr = mesh.attributes.new(name, 'INT', domain)
        attr.data.foreach_set('value', np.asarray(data, np.int32))
    head.data = mesh
    for name, data in shapes.items():
        if len(data) != len(positions):
            raise RuntimeError('Shape key lost new patch vertices')
        key = head.shape_key_add(name=name)
        key.data.foreach_set('co', np.asarray(data, np.float32).ravel())
        key.value = values[name]
    if len(set(v for v in source_ids if v >= 0)) != sum(v >= 0 for v in source_ids):
        raise RuntimeError('Stitch duplicated native boundary vertices')
    return {'removed_native_face_ids': sorted(removed), 'patches': reports,
            'new_skin_uv_status': 'UNASSIGNED; new eye faces deliberately zero until dedicated UV integration',
            'part_ids': {'native': 0, 'new_cavity': 1, 'new_eye_L': 2, 'new_eye_R': 3}}

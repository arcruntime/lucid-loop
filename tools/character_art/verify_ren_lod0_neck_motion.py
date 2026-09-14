"""Measure the actual final assembly's neck seam under rig and facial motion."""
from pathlib import Path
import hashlib
import json
import math
import bpy
from mathutils import kdtree

ROOT = Path('B:/lucid-loop/art/generated/characters/ren/lod0-final-v1')
source = ROOT / 'Ren_LOD0.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
join = bpy.data.objects['RenNeckJoin']
sources = [bpy.data.objects[n] for n in ('RenNeckChest_LOD0', 'RenHeadSkin')]
rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
rig.data.pose_position = 'POSE'
for bone in rig.pose.bones:
    bone.matrix_basis.identity()
for obj in bpy.context.scene.objects:
    if obj.type == 'MESH' and obj.data.shape_keys:
        for key in obj.data.shape_keys.key_blocks:
            key.value = 0
bpy.context.view_layer.update()
points = []
for obj in sources:
    for vertex in obj.data.vertices:
        points.append((obj, vertex.index, obj.matrix_world @ vertex.co))
tree = kdtree.KDTree(len(points))
for index, (_, _, co) in enumerate(points):
    tree.insert(co, index)
tree.balance()
pairs = []
for vertex in join.data.vertices:
    _, index, distance = tree.find(join.matrix_world @ vertex.co)
    if distance > 1e-5:
        raise RuntimeError(f'Join vertex {vertex.index} has no matching edge endpoint: {distance}')
    obj, other, _ = points[index]
    pairs.append((vertex.index, obj.name, other))

def evaluated_points(obj):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    try:
        return [evaluated.matrix_world @ vertex.co for vertex in mesh.vertices]
    finally:
        evaluated.to_mesh_clear()

records = []
neutral_head = evaluated_points(bpy.data.objects['RenHeadSkin'])
max_head_movement = 0.0
for turn, tilt in [(0, 0), (-45, 0), (45, 0), (0, -20), (0, 20), (-30, 15), (30, -15)]:
    # Blender head bone local axes; these are bounded rig stress poses,
    # not claimed to be a recovered performance or Unity controller angles.
    head = rig.pose.bones['Head']
    head.rotation_mode = 'XYZ'
    head.rotation_euler = (math.radians(tilt), math.radians(turn), 0)
    for expression in ['Basis', 'speech_A', 'speech_MBP', 'speech_O', 'emotion_Amused', 'emotion_Guarded', 'eyeBlinkL', 'eyeBlinkR']:
        for obj in sources:
            if obj.data.shape_keys:
                for key in obj.data.shape_keys.key_blocks:
                    key.value = int(key.name == expression and expression != 'Basis')
        bpy.context.view_layer.update()
        evaluated = {obj.name: evaluated_points(obj) for obj in [join] + sources}
        max_head_movement = max(max_head_movement, max((a-b).length for a,b in zip(neutral_head, evaluated['RenHeadSkin'])))
        gap = max((evaluated[join.name][a] - evaluated[name][b]).length for a, name, b in pairs)
        records.append(dict(turn=turn, tilt=tilt, expression=expression, max_gap_metres=gap))
report = dict(source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
              paired_vertices=len(pairs), cases=len(records),
              max_gap_metres=max(case['max_gap_metres'] for case in records),
              max_head_vertex_movement_metres=max_head_movement,
              cases_detail=records,
              scope='Evaluated Blender skinning at joined endpoints. Does not establish headphone clearance, cloth contacts, normals, or visual likeness.')
report['passed'] = report['max_gap_metres'] < 1e-5 and max_head_movement > .01
(ROOT / 'neck-motion-validation.json').write_text(json.dumps(report, indent=2))
print(json.dumps({k:v for k,v in report.items() if k != 'cases_detail'}))
if not report['passed']:
    raise RuntimeError('Neck seam separates under motion')

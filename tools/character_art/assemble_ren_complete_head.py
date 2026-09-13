"""Assemble reviewed Ren parts without rewriting their construction sources.

Run in Blender. The resulting asset remains a visual review candidate. Gaze uses
the normalized iris hierarchy; export keeps calibration nodes for each engine.
"""
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


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def dump(path, value):
    path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def signature(obj):
    mesh = obj.data
    fields = {'vertices': np.asarray([v.co[:] for v in mesh.vertices], np.float32),
              'loops': np.asarray([loop.vertex_index for loop in mesh.loops], np.int32),
              'polygons': np.asarray([(p.loop_start, p.loop_total) for p in mesh.polygons], np.int32)}
    for uv in mesh.uv_layers:
        fields['uv:' + uv.name] = np.asarray([v.uv[:] for v in uv.data], np.float32)
    if mesh.shape_keys:
        for key in mesh.shape_keys.key_blocks:
            fields['shape:' + key.name] = np.asarray([v.co[:] for v in key.data], np.float32)
    return {name: hashlib.sha256(array.tobytes()).hexdigest() for name, array in fields.items()}


def append_parts(path, predicate):
    with bpy.data.libraries.load(str(path), link=False) as (available, loaded):
        loaded.objects = [name for name in available.objects if predicate(name)]
    assert loaded.objects, f'No selected parts in {path}'
    for obj in loaded.objects:
        bpy.context.scene.collection.objects.link(obj)
        obj.hide_render = False
        obj.hide_set(False)
    return loaded.objects


def pose(meshes, mouth=0, blink_l=0, blink_r=0, gaze=(0, 0), cap=True):
    values = {'mouthSeal': 1 - mouth, 'jawOpen_A': mouth,
              'eyeBlinkL': blink_l, 'eyeBlinkR': blink_r, 'capOn': float(cap)}
    for obj in meshes:
        if obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks[1:]:
                key.value = values.get(key.name, 0)
    for side in 'LR':
        rot = bpy.data.objects['Ren_GazeRotate_' + side]
        rot['gazeX'], rot['gazeY'] = gaze
    bpy.context.view_layer.update()


def render_views(output, meshes):
    scene = bpy.context.scene
    for obj in list(scene.objects):
        if obj.type in {'LIGHT', 'CAMERA'}:
            bpy.data.objects.remove(obj, do_unlink=True)
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 40
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 1100
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.film_transparent = False
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.view_settings.exposure = 0
    scene.view_settings.gamma = 1
    scene.world = bpy.data.worlds.new('RenCompleteHead_ReviewWorld')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.055, .063, .075, 1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .65
    camera = bpy.data.objects.new('RenCompleteHead_Camera', bpy.data.cameras.new('RenCompleteHead_Camera'))
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 1.35
    target = Vector((.03, 0, .01))
    for name, location, energy, size in [
        ('Key', (2, -2, 3), 210, 3), ('Fill', (2, 2, 1), 120, 3), ('Rim', (-2, 1, 2), 180, 2)]:
        light = bpy.data.objects.new('RenCompleteHead_' + name, bpy.data.lights.new(name, 'AREA'))
        scene.collection.objects.link(light)
        light.location = location
        light.rotation_euler = (target - light.location).to_track_quat('-Z', 'Y').to_euler()
        light.data.energy = energy
        light.data.shape = 'DISK'
        light.data.size = size
    captures = []
    for name, location, controls in [
        ('front', (3, 0, .05), {}), ('quarter', (2.5, -1.7, .05), {}),
        ('profile', (0, -3, .05), {}),
        ('combined-half', (3, 0, .05), {'blink_l': .5, 'blink_r': .5, 'gaze': (-.5, .5)}),
        ('blink', (3, 0, .05), {'blink_l': 1, 'blink_r': 1}),
        ('open-a', (3, 0, .05), {'mouth': 1}),
    ]:
        pose(meshes, **controls)
        camera.location = location
        camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
        path = output / (name + '.png')
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        captures.append({'file': path.name, 'sha256': sha(path), 'controls': controls})
    pose(meshes)
    return captures


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--hair', required=True, type=Path)
    parser.add_argument('--paint', type=Path, default=BASE / 'anime-paint-v2/Ren_AnimePaint_v2.blend')
    parser.add_argument('--accessories', type=Path, default=BASE / 'head-accessories-v1/Ren_Head_Accessories.blend')
    parser.add_argument('--output', type=Path, default=BASE / 'complete-head-v1')
    parser.add_argument('--render', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    source = BASE / 'gaze-repair-v1/Ren_P2_GazeRotation_LowSclera.blend'
    inputs = {name: path.resolve() for name, path in
              [('gaze', source), ('paint', args.paint), ('hair', args.hair), ('accessories', args.accessories)]}
    before = {name: sha(path) for name, path in inputs.items()}
    output = args.output.resolve()
    assert all(output != path.parent for path in inputs.values())
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(source))
    bpy.context.preferences.filepaths.save_version = 0
    head = bpy.data.objects['Ren_Head']
    head_before = signature(head)
    native_meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH' and not obj.hide_render]
    source_signatures = {obj.name: signature(obj) for obj in native_meshes}
    with bpy.data.libraries.load(str(inputs['paint']), link=False) as (available, loaded):
        assert 'Ren_AnimePaint_v2_BakedSkin' in available.materials
        loaded.materials = ['Ren_AnimePaint_v2_BakedSkin']
    skin = loaded.materials[0]
    head.data.materials[0] = skin
    head.data.materials[2] = skin
    hair = append_parts(inputs['hair'], lambda name: name.startswith('Ren_Hair_'))
    accessories = append_parts(inputs['accessories'], lambda name: name == 'Ren_Cap' or name.startswith('Ren_RightEar_'))
    meshes = native_meshes + hair + accessories
    for side in 'LR':
        rotate = bpy.data.objects['Ren_GazeRotate_' + side]
        for axis, position in zip('XYZ', [(1, 0, 0), (0, 1, 0), (0, 0, 1)]):
            marker = bpy.data.objects.new(f'Ren_GazeAxis_{side}_{axis}', None)
            bpy.context.scene.collection.objects.link(marker)
            marker.parent = rotate
            marker.location = position
            marker.empty_display_size = .02
    pose(meshes)
    assert signature(head) == head_before
    assert all(signature(bpy.data.objects[name]) == sig for name, sig in source_signatures.items())
    records = []
    for obj in meshes:
        obj.data.calc_loop_triangles()
        records.append({'name': obj.name, 'vertices': len(obj.data.vertices),
                        'triangles': len(obj.data.loop_triangles),
                        'materials': [mat.name if mat else None for mat in obj.data.materials],
                        'uv_layers': [uv.name for uv in obj.data.uv_layers],
                        'shape_keys': [key.name for key in obj.data.shape_keys.key_blocks] if obj.data.shape_keys else []})
    report = {'status': 'ASSEMBLED_REVIEW_CANDIDATE_NOT_ARTIST_ACCEPTANCE',
              'inputs': {name: {'path': str(path.relative_to(ROOT)), 'sha256': before[name]} for name, path in inputs.items()},
              'all_native_mesh_geometry_uv_shapes_unchanged': True,
              'source_frame': 'front +X, up +Z; native units not established meters',
              'objects': records, 'triangles': sum(obj['triangles'] for obj in records),
              'gaze_contract': json.loads((BASE / 'gaze-repair-v1/gaze-controller-contract.json').read_text(encoding='utf-8')),
              'default_controls': {'mouthSeal': 1, 'jawOpen_A': 0, 'gazeX': 0, 'gazeY': 0, 'capOn': 1},
              'limitations': ['Hair and paint remain review candidates', 'Only seal/open-A speech prototype',
                             'No full expressions, shared-body skinning, or device performance claim']}
    if args.render:
        report['captures'] = render_views(output, meshes)
    blend = output / 'Ren_CompleteHead_Review.blend'
    for im in bpy.data.images:
        if im.source == 'FILE' and im.has_data and not im.packed_file:
            im.pack()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    report['blend_sha256'] = sha(blend)
    # Blender's diffuse/emission review mix is not a portable glTF shader.
    # Keep the atlas identical; runtime viewers supply their own diffuse lighting.
    atlas = next(node.image for node in skin.node_tree.nodes if node.type == 'TEX_IMAGE' and node.image)
    skin.node_tree.nodes.clear()
    material_output = skin.node_tree.nodes.new('ShaderNodeOutputMaterial')
    principled = skin.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
    principled.inputs['Roughness'].default_value = 1
    principled.inputs['Specular IOR Level'].default_value = 0
    texture_node = skin.node_tree.nodes.new('ShaderNodeTexImage')
    texture_node.image = atlas
    skin.node_tree.links.new(texture_node.outputs['Color'], principled.inputs['Base Color'])
    skin.node_tree.links.new(principled.outputs[0], material_output.inputs['Surface'])
    report['export_skin_shader'] = 'Portable rough diffuse atlas carrier; viewers implement ambient/dynamic-light response. Source Blender review mix preserved in saved blend.'
    # Named UV coordinates stay exact; runtime exporters receive FaceUV as UV0.
    export_uvs = {}
    for obj in meshes:
        preferred = obj.data.uv_layers.get('FaceUV_v1')
        if preferred:
            coordinates = np.asarray([uv.uv[:] for uv in preferred.data], np.float32)
            for uv in list(obj.data.uv_layers):
                obj.data.uv_layers.remove(uv)
            layer = obj.data.uv_layers.new(name='FaceUV_v1')
            layer.data.foreach_set('uv', coordinates.reshape(-1))
            layer.active_render = True
            export_uvs[obj.name] = {'channel': 0, 'source_layer': 'FaceUV_v1'}
        # glTF multiplies exported COLOR_0 into base color. Keep it only where
        # the authored material actually uses a vertex-color node (the irises).
        uses_color = any(mat and mat.use_nodes and any(node.type in {'VERTEX_COLOR', 'ATTRIBUTE'}
                         for node in mat.node_tree.nodes) for mat in obj.data.materials)
        if not uses_color:
            for attribute in list(obj.data.color_attributes):
                obj.data.color_attributes.remove(attribute)
    bpy.ops.object.select_all(action='DESELECT')
    selected = meshes + [obj for obj in bpy.context.scene.objects if obj.type == 'EMPTY' and obj.name.startswith('Ren_Gaze')]
    for obj in selected:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = head
    report['native_nodes'] = [{'name': obj.name, 'parent': obj.parent.name if obj.parent else None,
                               'local_matrix': [list(row) for row in obj.matrix_local],
                               'world_matrix': [list(row) for row in obj.matrix_world]} for obj in selected]
    glb = output / 'Ren_CompleteHead_Review.glb'
    bpy.ops.export_scene.gltf(filepath=str(glb), export_format='GLB', use_selection=True,
                             export_apply=False, export_yup=True, export_animations=False,
                             export_morph=True, export_extras=True)
    # FBX baseline is unposed; Unity controller applies the declared closed rest.
    for obj in meshes:
        if obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks[1:]:
                key.value = 0
    fbx = output / 'Ren_CompleteHead_Review.fbx'
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={'MESH', 'EMPTY'},
                            use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False,
                            axis_forward='-Z', axis_up='Y', mesh_smooth_type='OFF', path_mode='AUTO', colors_type='LINEAR')
    report['export_uvs'] = export_uvs
    materials = []
    texture_dir = output / 'textures'
    texture_dir.mkdir(exist_ok=True)
    used_materials = {mat for obj in meshes for mat in obj.data.materials if mat}
    for mat in sorted(used_materials, key=lambda value: value.name):
        nodes = mat.node_tree.nodes if mat.use_nodes else []
        bsdf = next((node for node in nodes if node.type == 'BSDF_PRINCIPLED'), None)
        texture = next((node.image for node in nodes if node.type == 'TEX_IMAGE' and node.image), None)
        uses_color = any(node.type in {'VERTEX_COLOR', 'ATTRIBUTE'} for node in nodes)
        record = {'sourceName': mat.name, 'useVertexColor': uses_color,
                  'vertexColorEncoding': 'linear', 'baseColorSrgb': True,
                  'baseColorLinearRgba': list(bsdf.inputs['Base Color'].default_value if bsdf else mat.diffuse_color),
                  'roughness': float(bsdf.inputs['Roughness'].default_value) if bsdf else .8,
                  'metallic': float(bsdf.inputs['Metallic'].default_value) if bsdf else 0,
                  'doubleSided': not mat.use_backface_culling,
                  'previewNote': 'Current browser and Unity review shaders use diffuse painted response; GLB retains authored metal/roughness for later lighting integration.'}
        if texture:
            raw = bytes(texture.packed_file.data) if texture.packed_file else Path(bpy.path.abspath(texture.filepath)).read_bytes()
            extension = '.png' if raw.startswith(b'\x89PNG') else '.jpg' if raw.startswith(b'\xff\xd8') else None
            assert extension, f'Unsupported packed texture bytes: {texture.name}'
            destination = texture_dir / (mat.name + extension)
            destination.write_bytes(raw)
            record.update(baseColorFile=str(destination.relative_to(output)).replace('\\', '/'),
                          baseColorSha256=sha(destination), baseColorSrgb=texture.colorspace_settings.name == 'sRGB')
        materials.append(record)
    report['materials'] = materials
    report['exports'] = [{'file': path.name, 'sha256': sha(path), 'bytes': path.stat().st_size} for path in [glb, fbx]]
    assert all(sha(path) == before[name] for name, path in inputs.items())
    dump(output / 'assembly.json', report)
    print('REN_COMPLETE_HEAD_READY', output, report['triangles'], flush=True)


if __name__ == '__main__':
    main()

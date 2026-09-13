"""Import a provider's original GLB into Blender and export a faithful Unity copy.

Run with Blender --background --python this_file.py -- --source model.glb ...
Original geometry, UVs, and texture bytes are retained. No decimation or facial
repair is performed. The viewer handles presentation scale and orientation.
"""

import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import sys

import bpy


ROOT = Path(__file__).resolve().parents[2]
UNITY = ROOT / 'Unity'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def asset_path(path):
    return path.resolve().relative_to(UNITY.resolve()).as_posix()


def safe_name(value):
    return re.sub(r'[^A-Za-z0-9_.-]+', '-', value).strip('-.') or 'asset'


def image_extension(data):
    if data.startswith(b'\x89PNG\r\n\x1a\n'):
        return '.png'
    if data.startswith(b'\xff\xd8\xff'):
        return '.jpg'
    raise ValueError('Source texture is not PNG/JPEG; obtain a compatible original before export')


def linked_image(socket, visited=None):
    """Follow material math/normal nodes to the source image without altering it."""
    visited = set() if visited is None else visited
    for link in socket.links:
        node = link.from_node
        if node.as_pointer() in visited:
            continue
        visited.add(node.as_pointer())
        if node.type == 'TEX_IMAGE' and node.image:
            return node.image
        for upstream in node.inputs:
            found = linked_image(upstream, visited)
            if found:
                return found
    return None


def original_image_bytes(image):
    if image.packed_file:
        return bytes(image.packed_file.data)
    path = Path(bpy.path.abspath(image.filepath))
    if path.is_file():
        return path.read_bytes()
    raise ValueError(f'No original file bytes for texture {image.name}')


def read_source_materials(path):
    with path.open('rb') as stream:
        magic, version, length = struct.unpack('<4sII', stream.read(12))
        if magic != b'glTF' or version != 2 or length != path.stat().st_size:
            raise ValueError('Expected an intact glTF 2.0 binary source')
        chunk_length, chunk_type = struct.unpack('<II', stream.read(8))
        if chunk_type != 0x4E4F534A:
            raise ValueError('GLB must begin with its JSON chunk')
        return json.loads(stream.read(chunk_length)).get('materials', [])


def main():
    global UNITY
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--id', required=True)
    parser.add_argument('--label', required=True)
    parser.add_argument('--provider', required=True)
    parser.add_argument('--model-label', required=True)
    parser.add_argument('--pose', required=True)
    parser.add_argument('--front-yaw', type=float, default=0)
    parser.add_argument('--base-color', type=Path,
                        help='Original external base-color image, only for a single-material model')
    parser.add_argument('--material-overrides', type=Path,
                        help='JSON: material name -> baseColor/normal/metallic/roughness original file paths')
    parser.add_argument('--unity-project', type=Path, default=UNITY,
                        help='Destination Unity project; override for isolated conversion validation')
    parser.add_argument('--output-root', type=Path)
    parser.add_argument('--report-root', type=Path,
                        default=ROOT / 'art/generated/characters/ren/bust-comparison-v1/unity-import')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    if safe_name(args.id) != args.id:
        parser.error('--id must be a simple filesystem-safe identifier')
    UNITY = args.unity_project.resolve()
    source = args.source.resolve()
    output = (args.output_root.resolve() if args.output_root else
              UNITY / 'Assets/CharacterArt/Generated/BustComparison/Models') / args.id
    report_output = args.report_root.resolve() / args.id
    asset_path(output)  # Fail before writing if this is outside the Unity project.
    source_hash = digest(source.read_bytes())
    source_materials = read_source_materials(source)
    material_by_name = {material.get('name', f'Material_{i}'): material
                        for i, material in enumerate(source_materials)}
    output.mkdir(parents=True, exist_ok=True)
    report_output.mkdir(parents=True, exist_ok=True)
    texture_output = output / 'Textures'
    texture_output.mkdir(exist_ok=True)
    overrides = json.loads(args.material_overrides.resolve().read_text(encoding='utf-8')) \
        if args.material_overrides else {}

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    if not meshes:
        raise ValueError('Provider asset contains no mesh objects')
    materials = {material.name: material for obj in meshes for material in obj.data.materials if material}
    if args.base_color:
        if len(materials) != 1:
            raise ValueError('--base-color is ambiguous for a multi-material model; use --material-overrides')
        overrides.setdefault(next(iter(materials)), {})['baseColor'] = str(args.base_color.resolve())
    unknown = set(overrides) - set(materials)
    if unknown:
        raise ValueError(f'Material overrides do not match imported materials: {sorted(unknown)}')

    texture_records = []
    material_records = []
    texture_files = {}
    channel_sockets = {'baseColor': 'Base Color', 'normal': 'Normal',
                       'metallic': 'Metallic', 'roughness': 'Roughness'}
    for material in materials.values():
        if not material.use_nodes:
            raise ValueError(f'Material has no imported node tree: {material.name}')
        shader = next((node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED'), None)
        if shader is None:
            raise ValueError(f'No Principled material mapping: {material.name}')
        source_material = material_by_name.get(material.name)
        if source_material is None and len(source_materials) == 1:
            source_material = source_materials[0]
        if source_material is None:
            raise ValueError(f'Cannot resolve original glTF material factors: {material.name}')
        # A linked Blender socket's unused default is commonly 0.8; multiplying
        # that into Unity would darken the texture even when glTF's factor is 1.
        color = source_material.get('pbrMetallicRoughness', {}).get('baseColorFactor', [1, 1, 1, 1])
        record = {'sourceName': material.name,
                  'baseColorFactor': dict(zip(('r', 'g', 'b', 'a'), color))}
        for channel, socket_name in channel_sockets.items():
            texture = linked_image(shader.inputs[socket_name])
            override = overrides.get(material.name, {}).get(channel)
            origin = 'Original packed/provider GLB texture bytes'
            if override:
                texture_path = Path(override).resolve()
                data = texture_path.read_bytes()
                texture = bpy.data.images.load(str(texture_path), check_existing=False)
                origin = str(texture_path)
            elif texture:
                data = original_image_bytes(texture)
            else:
                continue
            file_hash = digest(data)
            if file_hash not in texture_files:
                name = safe_name(material.name) + '-' + channel + '-' + file_hash[:10] + image_extension(data)
                destination = texture_output / name
                destination.write_bytes(data)
                texture_files[file_hash] = destination
                texture_records.append({'path': asset_path(destination), 'sha256': file_hash,
                                        'width': int(texture.size[0]), 'height': int(texture.size[1]),
                                        'bytes': len(data), 'source': origin,
                                        'transfer': 'byte-identical; no image re-encoding'})
            destination = texture_files[file_hash]
            record[channel + 'Asset'] = asset_path(destination)
            if channel == 'baseColor':
                node = material.node_tree.nodes.new('ShaderNodeTexImage')
                node.image = bpy.data.images.load(str(destination), check_existing=True)
                node.image.colorspace_settings.name = 'sRGB'
                material.node_tree.links.new(node.outputs['Color'], shader.inputs['Base Color'])
            if channel == 'normal':
                links = shader.inputs['Normal'].links
                if links and links[0].from_node.type == 'NORMAL_MAP':
                    record['normalScale'] = float(links[0].from_node.inputs['Strength'].default_value)
        material_records.append(record)

    triangle_count = 0
    mesh_records = []
    for obj in meshes:
        obj.data.calc_loop_triangles()
        count = len(obj.data.loop_triangles)
        triangle_count += count
        mesh_records.append({'object': obj.name, 'vertices': len(obj.data.vertices),
                             'triangles': count, 'polygons': len(obj.data.polygons),
                             'uvLayers': [uv.name for uv in obj.data.uv_layers],
                             'materials': [m.name if m else None for m in obj.data.materials],
                             'matrixWorld': [list(row) for row in obj.matrix_world],
                             'blendshapes': len(obj.data.shape_keys.key_blocks) - 1
                             if obj.data.shape_keys else 0})
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    fbx = output / (args.id + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={'MESH'},
                             use_mesh_modifiers=False, bake_anim=False, add_leaf_bones=False,
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, path_mode='AUTO')
    blend = report_output / (args.id + '.blend')
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    sizes = sorted({f"{t['width']}x{t['height']}" for t in texture_records})
    entry = {'id': args.id, 'label': args.label, 'provider': args.provider,
             'modelLabel': args.model_label, 'pose': args.pose, 'fbxAsset': asset_path(fbx),
             'materials': material_records, 'triangleCount': triangle_count,
             'textureSummary': ', '.join(sizes), 'frontYaw': args.front_yaw,
             'notes': 'Native provider surface; no retopology, facial rig or mobile optimization.'}
    report = {'schemaVersion': 1, 'source': str(source), 'sourceSha256': source_hash,
              'blenderVersion': bpy.app.version_string,
              'geometryPolicy': 'No decimation, topology edits, modifiers, or vertex fitting; native transforms exported',
              'fbx': str(fbx), 'fbxSha256': digest(fbx.read_bytes()),
              'blend': str(blend), 'blendSha256': digest(blend.read_bytes()),
              'meshes': mesh_records, 'textures': texture_records, 'entry': entry}
    if digest(source.read_bytes()) != source_hash:
        raise RuntimeError('Original source changed during export')
    (report_output / 'export-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    (output / 'entry.json').write_text(json.dumps(entry, indent=2), encoding='utf-8')
    print('REN_BUST_EXPORTED', args.id, triangle_count, 'triangles', len(texture_records), 'textures')


if __name__ == '__main__':
    main()

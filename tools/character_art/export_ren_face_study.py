"""Export the integrated Ren construction study without baking its shape keys."""
import hashlib
import json
import argparse
from pathlib import Path
import sys

import bpy
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'art/generated/characters/ren/parts-workflow-v1/face-integration-v1'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, default=BASE / 'Ren_P2_Face_Integrated.blend')
    parser.add_argument('--output', type=Path, default=BASE)
    parser.add_argument('--verified-blink', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    source = args.input.resolve()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    before = hashlib.sha256(source.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(source))
    bpy.ops.object.select_all(action='DESELECT')
    objects = [o for o in bpy.context.scene.objects if o.type == 'MESH' and not o.hide_render]
    records = []
    for obj in objects:
        obj.select_set(True)
        shapes = []
        if obj.data.shape_keys:
            basis = np.array([v.co[:] for v in obj.data.shape_keys.key_blocks[0].data])
            for key in obj.data.shape_keys.key_blocks[1:]:
                delta = np.array([v.co[:] for v in key.data])-basis
                shapes.append({'name': key.name, 'changed_vertices': int((np.linalg.norm(delta, axis=1)>1e-8).sum()),
                               'max_delta': float(np.linalg.norm(delta, axis=1).max())})
                key.value = 0
        obj.data.calc_loop_triangles()
        records.append({'object': obj.name, 'vertices': len(obj.data.vertices),
                        'triangles': len(obj.data.loop_triangles), 'shapes': shapes,
                        'materials': [m.name for m in obj.data.materials],
                        'world_matrix': [list(row) for row in obj.matrix_world]})
    materials = []
    used = {m for o in objects for m in o.data.materials}
    for mat in sorted(used, key=lambda m: m.name):
        bsdf = mat.node_tree.nodes.get('Principled BSDF') if mat.use_nodes else None
        materials.append({'name': mat.name,
                          'base_color_linear_rgba': list(bsdf.inputs['Base Color'].default_value if bsdf else mat.diffuse_color),
                          'roughness': float(bsdf.inputs['Roughness'].default_value if bsdf else .8),
                          'uses_vertex_color': bool(mat.use_nodes and any(n.type=='VERTEX_COLOR' for n in mat.node_tree.nodes))})
    out = output / 'Ren_P2_Face_Study.fbx'
    bpy.ops.export_scene.fbx(filepath=str(out), use_selection=True, object_types={'MESH'},
                             use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False,
                             axis_forward='-Z', axis_up='Y', mesh_smooth_type='OFF', path_mode='AUTO',
                             colors_type='LINEAR')
    report = {'source_blend': str(source), 'source_blend_sha256': before, 'fbx_sha256': hashlib.sha256(out.read_bytes()).hexdigest(),
              'status': 'CONSTRUCTION_STUDY_EXPORT', 'blender_version': bpy.app.version_string,
              'source_frame': 'Blender front +X, up +Z; FBX export forward -Z, up +Y',
              'vertex_color_encoding': 'LINEAR; authored IrisColor directly replaces Base Color, not multiplied by it',
              'default_viewer_pose': {'mouthSeal': 100, 'jawOpen_A': 0},
              'objects': records, 'materials': materials,
              'blink_ready_for_unity_trial': args.verified_blink,
              'limitations': ['Temporary unpainted materials', ('Stitched blink verified in Blender; Unity import and composition review still required' if args.verified_blink else 'Old separate blink prototype is not visually accepted; disable viewer blink controls'),
                              'No bilingual speech, expressions, idle or device performance acceptance']}
    (output / 'fbx-export.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    assert hashlib.sha256(source.read_bytes()).hexdigest() == before
    print('REN_FACE_FBX_READY', out, flush=True)


if __name__ == '__main__':
    main()

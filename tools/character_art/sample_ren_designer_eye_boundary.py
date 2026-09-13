"""Extract immutable H boundary UVs in Blender; sample original sRGB outside it.

Blender: --background --python this_file -- --extract
Python: this_file (requires numpy and Pillow)
Outputs are provisional pigment data, never a texture/mesh mutation.
"""
from pathlib import Path
import hashlib
import json
import sys

ROOT = Path(__file__).resolve().parents[2]
PARTS = ROOT / 'art/generated/characters/ren/parts-workflow-v1'
OUT = PARTS / 'h-designer-eyes-v1/boundary-pigment-v1'
SOURCE = PARTS / 'h-complete-head-v1/Ren_H_CompleteHead_Portable.blend'
BOUNDARY = PARTS / 'h-eye-controls-v1/seam-contract-v1/native-study-cut-boundaries.json'
TEXTURE = ROOT / 'art/generated/characters/ren/bust-comparison-v1/tripo/studio-h3.1-a-open/embedded-original-textures/image-0.jpg'
SOURCE_SHA = '317b8f7379dcf8d2fe34501385e1da7cd91b3d057008d488db0c65ed3727b8f5'
TEXTURE_SHA = '9750e636f595a01e2c1bfd00699b5bcd083e8f99a39a43adf8b1da68436e09ce'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(name, value):
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / name).write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def extract():
    import bpy
    from mathutils.kdtree import KDTree
    sys.path.insert(0, str(Path(__file__).parent))
    from audit_ren_tripo_texture import Glb
    assert sha(SOURCE) == SOURCE_SHA
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    mesh = bpy.data.objects['Ren_H_Head'].data
    master = TEXTURE.parent.parent / 'ren-tripo-studio-h3.1-a-open-original-8k.glb'
    assert sha(master) == '9158e7e90ee22bce64154e2c2fe6d8880e9ab66de6f1a6c437c816f077049406'
    glb = Glb(master)
    primitive = glb.json['meshes'][0]['primitives'][0]
    master_uv = glb.accessor(primitive['attributes']['TEXCOORD_0'])
    boundary = json.loads(BOUNDARY.read_text(encoding='utf-8-sig'))
    # UV-split source IDs may be represented by a different coincident source ID
    # after extraction. Verify the exact geometric boundary, but sample the
    # original source-ID UV instead of substituting the representative's UV.
    tree = KDTree(len(mesh.vertices))
    for v in mesh.vertices:
        tree.insert(v.co, v.index)
    tree.balance()
    eyes = []
    for eye in boundary['eyes']:
        path = eye['geometric_paths'][0]
        rows = []
        for source_id, point in zip(path['source_h_vertex_ids'], path['native_points']):
            _, _, distance = tree.find(point)
            assert distance < 1e-8, (source_id, distance)
            entries = [{'native_position': point, 'uv': master_uv[int(source_id)].tolist()}]
            rows.append({'source_h_vertex_id': int(source_id), 'native_position': point, 'corners': entries})
        eyes.append({'side': eye['side'], 'closed': path['closed'], 'samples': rows})
    assert sha(SOURCE) == SOURCE_SHA
    write('boundary-uv.json', {'sourceSha256': SOURCE_SHA, 'boundarySha256': sha(BOUNDARY),
                             'uvSource': 'Original H GLB TEXCOORD_0 indexed by source_h_vertex_id; glTF top-left image convention',
                             'originalGlbSha256': sha(master), 'eyes': eyes})


def sample():
    import numpy as np
    from PIL import Image
    assert sha(TEXTURE) == TEXTURE_SHA
    data = json.loads((OUT / 'boundary-uv.json').read_text(encoding='utf-8-sig'))
    pixels = np.asarray(Image.open(TEXTURE).convert('RGB'), dtype=np.float64) / 255
    height, width = pixels.shape[:2]

    def bilinear(uv):
        x = np.clip(uv[0]*width-.5, 0, width-1)
        y = np.clip(uv[1]*height-.5, 0, height-1)
        ix, iy = int(x), int(y)
        fx, fy = x-ix, y-iy
        return ((1-fy)*((1-fx)*pixels[iy, ix]+fx*pixels[iy, min(ix+1,width-1)])
                + fy*((1-fx)*pixels[min(iy+1,height-1),ix]+fx*pixels[min(iy+1,height-1),min(ix+1,width-1)]))

    arrays = {}
    for eye in data['eyes']:
        rows = eye['samples']
        raw = np.asarray([np.median([bilinear(c['uv']) for c in r['corners']], axis=0) for r in rows])
        # Conservative warm/pale skin eligibility. Retain raw samples and flags:
        # this is not semantic segmentation or proof every eligible pixel is skin.
        r, g, b = raw.T
        eligible = (raw.mean(1) > .48) & (r-b > .055) & (r > g) & (g > b)
        reasons = [['low brightness'] if c.mean() <= .48 else [] for c in raw]
        for i in range(len(raw)):
            if r[i]-b[i] <= .055: reasons[i].append('insufficient warm chroma')
            if not r[i] > g[i] > b[i]: reasons[i].append('not warm ordered RGB')
        assert eligible.sum() >= 24, 'Insufficient source skin evidence; do not invent fallback color'
        outlier = np.linalg.norm(raw-np.median(raw[eligible],axis=0),axis=1) >= .18
        for i in np.flatnonzero(outlier): reasons[i].append('distance from eligible median >= .18')
        eligible &= ~outlier
        assert eligible.sum() >= 24
        positions = np.asarray([row['native_position'] for row in rows])
        filtered = raw.copy()
        for i, row in enumerate(rows):
            n = len(rows)
            nearest = [i]
            fraction = 0.
            if not eligible[i]:
                prev = next((i-j)%n for j in range(1,n) if eligible[(i-j)%n])
                nxt = next((i+j)%n for j in range(1,n) if eligible[(i+j)%n])
                nearest = [prev,nxt]
                fraction = ((i-prev)%n)/((nxt-prev)%n)
                filtered[i] = raw[prev]*(1-fraction)+raw[nxt]*fraction
            row.update(raw_srgb=raw[i].tolist(), filtered_srgb=filtered[i].tolist(),
                       eligible_skin_candidate=bool(eligible[i]),
                       filter_reasons=reasons[i], interpolation_fraction=fraction,
                       filter_source_ids=[rows[j]['source_h_vertex_id'] for j in nearest],
                       maximum_filter_distance_native=float(np.linalg.norm(positions[nearest]-positions[i],axis=1).max()))
        eye['eligible_count'] = int(eligible.sum())
        arrays[eye['side']+'_source_h_vertex_ids'] = np.asarray([r['source_h_vertex_id'] for r in rows], np.int64)
        arrays[eye['side']+'_raw_srgb'] = raw.astype(np.float32)
        arrays[eye['side']+'_filtered_srgb'] = filtered.astype(np.float32)
        arrays[eye['side']+'_eligible'] = eligible
    data.update(texturePath=str(TEXTURE.relative_to(ROOT)), textureSha256=TEXTURE_SHA,
                colorSpace='sRGB RGB in [0,1]; convert to linear when assigning Blender color attributes',
                classification='Provisional filtered skin pigment samples; not finished paint or semantic segmentation',
                filtering='Eligible source samples retained; rejected samples interpolated between nearest valid neighbors along cyclic boundary order, independently per eye. Raw values and reasons preserved.',
                sourceGeometryModified=False, sourceTextureModified=False)
    np.savez_compressed(OUT / 'boundary-pigment.npz', **arrays)
    data['npzSha256'] = sha(OUT / 'boundary-pigment.npz')
    write('boundary-pigment.json', data)
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    fig, axes = plt.subplots(2, 2, figsize=(12, 6))
    for row_index, eye in enumerate(data['eyes']):
        side = eye['side']
        colors = arrays[side+'_filtered_srgb']
        pos = np.asarray([r['native_position'] for r in eye['samples']])
        axes[row_index, 0].scatter(pos[:,0], pos[:,2], c=colors, s=24, edgecolors='#666666', linewidths=.25)
        axes[row_index, 0].set_aspect('equal')
        axes[row_index, 0].set_title(side+' boundary in native X/Z; filtered sRGB')
        axes[row_index, 1].imshow(np.stack([arrays[side+'_raw_srgb'], colors]), aspect='auto', interpolation='nearest')
        axes[row_index, 1].set_yticks([0,1], ['raw', 'filtered'])
        axes[row_index, 1].set_xlabel('Cyclic boundary index')
        axes[row_index, 1].set_title(str(eye['eligible_count'])+'/'+str(len(colors))+' eligible source samples')
    fig.suptitle('Original H skin boundary pigment — provisional input, not finished eyelid paint')
    fig.tight_layout()
    fig.savefig(OUT / 'boundary-pigment-review.png', dpi=140)
    plt.close(fig)
    print([(e['side'],len(e['samples']),e['eligible_count']) for e in data['eyes']])


if __name__ == '__main__':
    extract() if '--extract' in sys.argv else sample()

# Ren LOD0 headless body

Assembly input: `RenBody_LOD0.blend` or `RenBody_LOD0.fbx`. The immutable source is `approved-body-v4-no-headphones/RenBodyV4-review.blend`.

- Body/clothes: 10,799 rendered triangles.
- Neck/chest repair: 1,199 rendered triangles.
- Total: 11,998 triangles, 54 female_base_v3 bones, no unweighted vertices.
- Old head/hair/cap removed across the existing gap between the V4 chest and head surfaces. Approved torso proportions and rig joint placement are unchanged.
- `neck-attachment.json` contains the exact final 233-vertex ordered neck ring, normals, weights, and relevant bone transforms. Coordinates are Blender world metres, +Z up, front -Y.
- `unity-materials.json` maps both mesh materials to the two 2048px baked albedo PNGs in `textures/`.

The constrained reduction retains boundary/crease and hand priorities. Source colors are baked onto fresh low-poly UVs to prevent original atlas interpolation stretching. Approved source split normals were transferred by nearest-face interpolation after reduction. Rounded arms and shoulders receive explicit reduction protection. A localized arm-only color bake corrects underarm projection contamination. Original hand detail remains limited, the new strap material remains flat, and source cloth fold detail remains limited by the triangle budget. Inspect the complete assembled character in Unity before final art approval.

Neutral front/right/back renders are `body-front.png`, `body-right.png`, `body-back.png`. Counts and import verification are in `body-qa.json` and `fbx-import-check.json`. No animation is included. Rebuild script: `tools/character_art/finalize_ren_lod0_body.py`.


Fresh FBX import contains 11,996 triangles (10,797 body + 1,199 neck): two degenerate body triangles are omitted by FBX export. Blender count is 11,998. Both meshes remain fully weighted; 54 bones and zero animation actions verified.

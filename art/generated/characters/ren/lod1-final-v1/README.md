# Ren LOD1 correction — 13,418 triangles, budget unfinished

Ren_LOD1.blend and Ren_LOD1.fbx contain the targeted boundary-preserving correction derived solely from final lod0-final-v1/Ren_LOD0.blend. The source is unchanged. This corrects the first pass's hair destruction but does not meet the 10–12k target.

All 3,917 protected hair boundary/tip vertices survive at exactly their source positions. The 35 body, 371 neck-chest and 400 head protected neck-boundary vertices also have zero measured displacement. Split normals were transferred from the approved source onto head, neck and hair. Female-v3 plus hair-bone rest matrices remain unchanged; named morph controls, UVs, skin groups and separate cap/headphones remain present.

| Component | Triangles |
|---|---:|
| Body | 1,999 |
| Neck/chest | 400 |
| Head skin | 2,849 |
| Hair | 5,651 |
| Cap | 450 |
| Headphones | 549 |
| Eyes | 398 |
| Ear jewelry | 179 |
| Brows/lashes | 250 |
| Upper/lower teeth | 140 |
| Upper/lower gums | 120 |
| Tongue | 140 |
| Neck join | 293 |
| Total | 13,418 |

A read-only zero-ratio decimator audit reaches 5,619 hair triangles with these protected vertices: only 32 additional triangles can disappear under the current hair restriction. The resulting total floor with all other current components fixed is 13,386; this is not proof of a global minimum for the character. The required 4,233-triangle hair allocation is not attainable with the current protected boundaries.

Matched source-full.png/lod1-full.png, source-face.png/lod1-face.png and native 240-pixel-high source-gameplay240.png/lod1-gameplay240.png are included. Visual inspection shows recognizable layered hair again, without the first pass's large temple wedges. Close-up neck/chest and headphone shading remain coarse; gameplay-size views reduce their prominence but do not excuse visible defects.

manifest.json records source hash, component counts, exact rig matrices, protected-vertex errors and shape displacement maxima. Reduced speech_A remains nonzero (21.6 mm maximum head delta); hair retains capOn. These checks establish surviving controls, not final lip contacts or collision approval. FBX import and final Unity LOD-switch review remain outstanding. No Unity edits or paid provider jobs occurred. No further head/body reductions were made after measuring the protected hair floor.

## FBX reimport proof

Independent fresh FBX import measures **13,404 rendered triangles**, versus **13,418 polygon-derived triangles in the saved blend**. The export/import path drops 14 triangles: body 2, ear jewelry 3, hair 9. This discrepancy is recorded rather than claimed as exact preservation. All named shape counts match per component; all 67 skeleton bone names and hierarchy match (54 female-v3 plus 13 hair bones), and the sets of actually weighted rig bones match. All material names are unchanged from LOD0, so existing material mappings can be reused. Detailed per-part names/counts are in fbx-validation.json. This verification does not certify animation endpoint contact quality or unchanged skin weight magnitudes after export.

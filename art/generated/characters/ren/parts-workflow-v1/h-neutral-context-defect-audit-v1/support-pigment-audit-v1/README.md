# Support pigment donor audit

**Existing pigment is correct for the corrected assembly placement. No pigment-only derivative is needed.** This is established by replaying the actual sampler against the exact retained H face material source, not inferred from the transform bug.

The original `paint_ren_h_support_boundary.py` uses `obj.matrix_world @ vertex.co`, nearest face triangles, barycentric HNativeUV interpolation, bilinear pigment sampling, a 0.05 distance guard and explicit median-skin fallback. The saved pigment library later contains a displaced support matrix, but that saved matrix does not reproduce its stored colors.

| Replay | Maximum stored-vs-recomputed linear RGB difference | Support fallback count |
|---|---:|---:|
| Correct assembly support world identity | 2.971e-8 | 362 / 864 |
| Saved displaced support world (+0.06,0,+0.20) | 0.221189 | 859 / 864 |

Correct placement reproduces **all 864 support and all 72 bridge colors** within float32 precision. Its 502 valid support donors / 362 fallback vertices exactly agree with the original material contract. All72 bridge samples are valid. Therefore the sampling used the intended assembly placement; the incorrect standalone matrix is a later parenting/persistence issue. Correcting the export placement does not invalidate those colors.

[donor-audit.json](donor-audit.json) binds original pigment, retained face and sampler hashes. [per-vertex-donors.json](per-vertex-donors.json) records triangle ID, source-H triangle ID, barycentric weights, UVs, distance, fallback decision and linear pigment for both replay placements at every vertex.

Specific seam provenance: bridge vertex1 lands exactly on the H skin donor (distance0), original source face491211, UV(0.532958984375,0.09423828125), RGB(0.77582235,0.54572458,0.43415366). Adjacent bridge vertex0 uses face674847 at distance0.0361461, within the explicit0.05 guard. Valid support donors extend to distance0.0495675. Rear/far/dark fallback remains intentionally approximate; this audit does not assert every backing seam is visually invisible.

No blend, geometry, pigment, shader or viewer asset was changed. The first hypothesis check failed when trying to reproduce colors from the saved displaced matrix; that rejected assumption was investigated, not used to repaint. The v2 geometry-only export remains the appropriate next comparison. Actual matched v2 views belong to the viewer task; there is no new pigment change to render.

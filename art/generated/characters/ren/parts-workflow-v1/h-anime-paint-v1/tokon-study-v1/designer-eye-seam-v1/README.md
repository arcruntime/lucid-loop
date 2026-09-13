# Designer eye boundary-lighting diagnosis

Read-only source: designer eye pigment blend SHA715617c4... and neutral-export-v2 contract; accepted head context SHA317b8f73.... Original and derived geometry, UVs, normals, sampled pigment and source files remain unchanged.

The actual v6 unlit front has much less of the hard under-eye mask band seen in Tokon. Head and shutters share threshold, softness, palette, face frame and FaceMode0.90. Head uses spatial R and shadow albedo; shutters use constant R1 and base times shade multiplier. Linear vertex pigment is consumed once.

`retained-boundary-uv.json` extracts the actual surviving head corner UVs at coincident native boundary positions. `boundary-response-audit.json` samples those UVs with sRGB decoding before bilinear interpolation at the base mip. This is an offline material-input audit, not GPU readback.

| Boundary | R min / median / max | Median linear shadow/base ratio |
|---|---|---|
| L, 285 corners | 0.57647 / 1 / 1 | 0.72041, 0.53023, 0.50037 |
| R, 314 corners | 0.43627 / 1 / 1 | 0.72055, 0.53043, 0.50029 |

Shutter fallback is R1 and ratio(0.72,0.53,0.50). Ratio absolute-error P95 is approximately 0.006–0.011. Outer-boundary R differs locally; palette mismatch is not the dominant measured discrepancy. This provides no justification for globally darkening/lightening shutter pigment.

The separate actual received-shadow-off pair under `.local/ren-designer-shadow-v1/live-review/` largely removes the pronounced hard mask bands while retaining painted smoke. Subtle boundary differences remain; this is not an overall seam/likeness acceptance. The diagnostic zeroes only received-shadow modulation strengths on head/shutter materials, leaving authored diffuse response intact.

Actual frozen renderer inspection corrects the initial caster hypothesis: all 20 graphic-eye renderers already have casting Off; only 2 SkinShutters cast. A graphic-off test would be a no-op. The next authorized comparison disables casting only on those 2 shutters, restores receiver strengths to baseline and preserves head/cap/hair casting. This tests self/overlap shadow without throwing away useful external shadows. Results are pending.

The broad dark support region has a separate matched-pigment bridge owner; it is not being repainted or geometrically changed here. Hair, eye silhouette and source pigment approval are also separate tasks.

# V2 cap — minimal band clearance correction

Use `Ren_Head_Accessories.blend`, `.fbx` or `.glb` here for the current cap/ear
assembly. The Blender file contains `Ren_Cap`, `Ren_RightEar_HelixCuffs` and
`Ren_RightEar_DropPiercing` only. Total: **1,466 triangles**.

The hair fit measured eight lower cap/scalp corridors narrower than the required
0.003 outside skin plus 0.003 inside cloth. A local outward adjustment opens
those gaps without changing the head, hair or ear jewelry. The original V2
exports remain intact in the parent directory.

- 112 cap vertices adjusted with a smooth falloff.
- Requested displacement limit: **0.002 native units**. Maximum stored float
  coordinate displacement is 0.002000012, within the recorded 1e-7 tolerance.
- Minimum of the eight corridor clearances: **0.004475 → 0.006469**.
- Crown peak remains **z=0.5235**; cap topology/UVs and 1,110 triangles remain.
- All head, ear and hair mesh signatures remain unchanged.

[band-clearance-report.json](band-clearance-report.json) contains all eight
before/after probes and every changed vertex. [export-verification.json](export-verification.json)
contains the actual FBX/GLB roundtrip counts, placement checks, material slots,
embedded texture verification and hashes. The texture is unchanged from V2.

Standalone Blender SHA256:
`bff2a53430d58535e6f2aef8d271e8a8774a6c8015185a4cd5f2b74168ab805d`.

The matching hair cap-on state is frozen at
`../../ren-shag-v1/selected-v2/Ren_Shag_CapState.blend`, SHA256
`f160e66c8b611796c284c396508ba46f3c052b3ea8a219dceb603d18694565cc`.
Final painted front, quarter and profile captures and
[combined-review.json](../combined-review.json) are in the parent directory.
The large crown protrusions are resolved in these views, but two tiny pale
specks remain on the ear-side black band. This is a documented working-viewer
fit candidate; sampled corridor clearance is not a complete intersection test.
No Unity asset, shared rig, skin weights, source geometry, commit or push changed.

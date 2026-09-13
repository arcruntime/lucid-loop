# Continuous iris gaze repair

The white scallops in the live Unity combined-half capture came from linearly adding four endpoint-projected iris shapes. Intermediate vertices followed chords through the curved sclera. This derivative replaces those shapes with an actual ellipsoid-space rotation hierarchy. Grey-blue iris artwork, pupil, catchlight, UVs and topology are retained.

## Deliverables

| Derivative | SHA-256 | Purpose |
| --- | --- | --- |
| `Ren_P2_GazeRotation.blend` | `98597b2bbed0c98e1f2124199f04974eaf83c7fadb11003b98ee1f1e87e9ac1e` | Original sclera resolution; isolated gaze correction. |
| `Ren_P2_GazeRotation_LowSclera.blend` | `a52a8473438e8c48d859eb90023950c8bc1c1283e80b46d0504c27bbe6d5a3ee` | Same correction, lower-resolution inscribed sclera; **1,702 total separate eye/liner triangles**. |

Source is the immutable `face-uv-v1/eye-integration-v2/Ren_P2_Face_EyeUV.blend`, SHA `6f388b7c0ec135c77288a39d329e9e459c51f4121adb5629270ab42f90fa6959`. The original source and frozen construction tools were not changed.

The first derivative preserves every non-iris mesh's geometry, topology, UV and shape coordinates exactly. The iris's neutral world positions differ by at most `7.45e-9` source units because of transform round-trip precision. The unsafe four iris shape keys are deliberately removed. Existing eyelid, liner, lash and mouth controls remain intact.

The budget variant replaces only the two sclera meshes with smooth-shaded 16-segment, 10-ring ellipsoids, 288 triangles each. The irises retain 472 triangles each, and all liners/lashes retain their original topology. This saves **7,360 triangles**, bringing the complete current face assembly from 24,420 to **17,060 triangles**. The skin, lid and oral meshes have not been simplified. The macro captures show comparable visible eye contours; actual Unity LOD rendering still needs review.

## Actual hierarchy and controls

For each side:

```text
Ren_GazeEllipsoid_L/R       translation = center, scale = radii
  Ren_GazeRotate_L/R        local rotation only
    Ren_Eye_L/R_Iris        normalized mesh coordinates, identity local transform
```

In Blender source coordinates, the center is `(0.185, ±0.119, 0.085)` and radii are `(0.060, 0.062, 0.060)`. These are source units, not established meters. `gazeX` and `gazeY` custom properties on each rotation object drive the actual rotation and clamp to `[-1,1]`.

The operation is `p' = center + D R D^-1 (p - center)` with `D = diag(radii)` and `R = Rz(gazeX × 0.16199795457112545) Ry(-gazeY × 0.11693296146237843)`. Thus positive X gaze moves toward native positive Y, and positive Y gaze moves upward. Remap UI signs if its convention differs.

Unity should retain the translation/nonuniform-scale parent, normalized child mesh and rotation child. Updating only the rotation Transform requires no per-frame mesh upload or allocation. When retaining the native local axes, use quaternion multiplication in the specified order:

```csharp
rotation.localRotation =
    Quaternion.AngleAxis(gazeX * 0.16199795457112545f * Mathf.Rad2Deg, Vector3.forward)
  * Quaternion.AngleAxis(-gazeY * 0.11693296146237843f * Mathf.Rad2Deg, Vector3.up);
```

Here `forward` and `up` denote native rotation-node local Z and Y axes, not the character's conceptual forward/up. If FBX axis conversion remaps that node's local basis, conjugate the rotation by the imported basis conversion, or reconstruct this three-level hierarchy with the converted basis explicitly. Verify known gaze directions after import. Do not collapse the nonuniform scale into an ordinary world-space rotation: the normalization is what keeps the iris on the ellipsoid. Do not continue applying the old additive gaze shapes. Blender drivers are authoring controls and are not assumed to export as Unity runtime code.

`gaze-controller-contract.json` supplies exact object names, centers, radii, rotation order and preservation results. Material-only work can be transferred independently; iris material slots and color attributes remain present. No Unity files or external services were changed in this task.

## Verification

The clearance proof covers the entire continuous gaze domain, rather than only a grid of good-looking endpoint poses. Every point on every normalized iris triangle lies at radius at least `1.00123346`; every sclera triangle lies inside radius `1.00000066`, by convexity of the sphere and its inscribed mesh. Orthogonal rotation preserves every triangle point's radius. Multiplication by D therefore preserves a conservative iris-to-sclera gap of approximately **`7.397e-5` source units for every rotation**. The lower-resolution sclera satisfies the same bound. This proof concerns iris/sclera penetration; it does not certify eyelid contact or whole-head intersections.

`reopen-verification.json` records fresh-file checks for both derivatives:

- 441 gaze inputs per derivative, both eyes, agree with the independent analytic rotation within `2.56e-7` normalized units. Actual maximum iris travel is `0.01245` source units, so the test also confirms that gaze moved.
- Maximum radial drift after actual Blender transforms is `2.41e-7`, much smaller than the clearance margin.
- Full blink exposes zero sampled iris/sclera hits in front and quarter views at neutral, all four extreme diagonals and the problematic half diagonal.
- Source hash remains unchanged.

Actual `neutral-*`, `partial-diagonal-*`, `extreme-*-*`, `combined-half-*` and `extreme-half-*` macro renders show front and quarter views. `low-*` repeats those states for the reduced sclera. The combined-half view uses gaze `(-0.5,+0.5)` and 50% blink; the pupil remains cleanly occluded by the moving lid without white sclera scallops cutting through the iris. Existing inner-corner shading and overall likeness/material limitations remain visible and are outside this correction.

Reproduce with Blender 5.1.1, two threads, `--python tools/character_art/repair_ren_p2_gaze.py`. Add `-- --verify` for the fresh-file verification. Unity export, imported hierarchy validation and live-controller captures remain integration work; these Blender results do not claim Unity or iPhone performance acceptance.

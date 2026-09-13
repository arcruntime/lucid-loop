# Selected H face-shape audit

The exact user-selected H A-open asset is the authoritative shape master. The current P2 head is a different face: its lower face extends farther below the eye line, with a longer cheek-to-chin taper. This is not a small isolated chin defect. P2 is already broad through the upper cheeks; simply widening the cheeks would not recover H.

## Evidence and registration

Source: [immutable H GLB](../../bust-comparison-v1/tripo/studio-h3.1-a-open/ren-tripo-studio-h3.1-a-open-original-8k.glb), SHA256 `9158e7e90ee22bce64154e2c2fe6d8880e9ab66de6f1a6c437c816f077049406`. Blender imports 1,031,818 vertices / 1,934,041 triangles, one whole-bust mesh. Native frame is face -Y, Z up. Source geometry was not edited.

P2 comparison: `../complete-head-v2/Ren_CompleteHead_Review.blend`, Ren_Head only. Separate eyes/accessories are excluded so they cannot conceal the facial surface. All comparison images use identical orthographic camera placement, 0.85 scale, 700-square output and clay material override.

`registration.json` records the one rigid frame change, uniform scale 1.0465116, and translation applied to H as an object transform only. Visible approximate H outer canthi X -0.179/+0.165, Z0.548 map to P2 Y +/-0.180, Z0.085. Hair partly covers the H eyelids. Depth placement uses an approximate eyelid plane, not a solved surface registration. Allow approximately 0.01–0.02 P2 units of landmark uncertainty; this is a review-grade comparison, not a final transfer correspondence. No anisotropic scaling, sculpting, or warping was applied.

## Measured findings

- Prior native-ID audit: 314 retained P2 jaw vertices (X>0.10, Z<-0.20) differ from immutable P2 FBX by at most 1.325e-8. The sharp/tall lower-face shape was inherited from P2, rather than created by the local eye/mouth/brow repairs.
- P2 front chin is approximately (0.2600, 0.00854, -0.30298); eye-line-to-chin height is about 0.388.
- H front-chin-region sample, restricted to |X+0.007|<0.04 and Y<-0.25, reaches Z0.213654. Registered height is Z-0.264897: approximately 0.038 higher than P2, or roughly 10% shorter eye-to-chin distance. This threshold sample is a conservative visible-front chin-region measure, not an anatomical menton label; registration uncertainty prevents treating 10% as an exact sculpt target.
- Both P2 closed rest (mouthSeal=1, jawOpen_A=0) and P2 A-open (mouthSeal=0, jawOpen_A=1) are shown. Switching P2 mouth pose opens the lips but does not remove its longer lower-face silhouette. H has a substantially wider vertical mouth opening; lip and immediate perioral differences therefore remain pose-confounded.
- Matched front and quarter views show a different cheek-to-jaw transition and shorter H lower face. They do not justify a blanket cheek-volume increase. H hair obscures portions of the cheek perimeter, so no unreliable whole-mesh cheek-width/volume measurement is reported.
- Profile views confirm different nose/lip/chin relationships and mandibular contour, but absolute forward-depth differences are not quantified because depth registration is approximate.

## Transfer implication

Use the exact H surface for coordinated cheek, jaw, chin and overall facial proportion fitting. Establish exposed-skin landmarks and segment skin away from crossing hair before closest-surface fitting. Preserve the eye/mouth/blink architecture as reusable topology/controls, then fit and validate it against H. Do not fit to hair or use the P2 shape as the new master. Treat H's A-open mouth pose separately when deriving neutral closed rest; an automatic global shrinkwrap would conflate expression with identity.

Open `comparison.html` for the nine matched views, or inspect `matched-comparison.blend`. The scene is audit-only and not an integration candidate. No source meshes, UVs, shape keys, Unity files, or selected assets were overwritten. No jaw-softening derivative was created.

## Portable reproduction and local conveniences

`render-comparison.py` imports the immutable H GLB directly and appends Ren_Head from the frozen V2 blend. It no longer requires `source-audit.blend`. Run with Blender 5.1.1 in background mode; optional `-- --output-dir PATH` selects output. The default is a new `reproduced/` subfolder, preserving finalized evidence.

Read-only inspection of the original setup confirmed AuditClay base/diffuse RGB 0.55, metallic 0, roughness 0.5, IOR 1.5, alpha 1; world background strength 1 (initial RGB 0.12, overridden to 0.15 for matched views); Cycles 12 samples; 100% resolution; opaque film; PNG RGBA 8-bit; AgX / None look, exposure 0, gamma 1, sRGB display. These settings are now explicit in the script. Matched cameras/lights remain unchanged. This portability edit was syntax-checked only; finalized PNGs and registration were not rerendered or overwritten.

`source-audit.blend`, `matched-comparison.blend`, and `source-positions.npz` are **local-only convenience files**, not required portable audit inputs. The nine matched PNGs, comparison.html, registration.json, this report, and self-contained script are the portable audit evidence; the immutable H GLB and frozen V2 blend are the source dependencies. Initial whole-bust minusY/plusX previews are also local inspection conveniences.

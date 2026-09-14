# Isolated iris pigment study

Root selected the matched before/after source-camera direction for an actual Unity comparison only. The blue-grey lower crescent and upper dark tone are clearer; this remains a simple four-ring graphic treatment, not final designer-detail approval.

Only `DesignerIrisColor` on the two v6 iris objects changes. All geometry, UVs, shape keys, matrices, pupil/highlight shapes, ink fans, shutter smoke and other vertex pigments remain exact. The diagnostic blend saves an unobscured cropped camera view; it is not a replacement assembled scene.

`pigment-contract.json` binds source and candidate hashes. `iris-pigment-arrays.npz` records native positions and linear RGBA. `iris-color-transfer.json` provides 257 unique native positions per iris, registered positions, source UVs and old/new linear colors for matching against the existing frozen runtime eye mesh. Imported split vertices may map to the same unique source record; unmatched/ambiguous positions must fail.

Runtime integration must clone only iris mesh color storage, preserve all other attributes/indices/bounds/shapes/transforms, validate old colors against actual UNorm8 quantization, and consume new linear color once with white tint and sRGB decoding disabled. The viewer intends a stricter half-byte UNorm8 tolerance than the transfer contract's conservative one-byte ceiling. No replacement FBX is needed.

Palette/sector choices are artist-guided vertex-pigment authoring, not exact pixel tracing or photographic iris fibers. No new highlights were added. The next acceptance step is a matched actual GPU color-only comparison after the independent forehead/receiver tests.

# Neutral iris, lower return and hair comparison

Actual Unity 6000.3.24f1 Windows-player captures of the same H construction candidate. Root selected all three changes as the next provisional review baseline after inspecting front, quarter and source portrait. This is not final likeness or style acceptance.

Five states are captured under matching front, quarter, both profiles, club-front, unlit-front and the fixed source camera: baseline, iris-pigment, lower-return, directional-hair and combined. Each source-camera state also has an equal-scale untouched portrait crop. Forty original GPU images and three explicitly enlarged pixel crops are included.

The baseline front is pixel-identical to the previously selected local forehead result. All 40 actual camera, color hash, map, visibility and shader-property readbacks pass. The lower return fills all 273 previously exact-background pixels in the disclosed lower-iris ROI; zero remain. Its full front difference is 492 pixels. Small existing skin slivers and ink irregularities remain. Iris pigment changes 2,007 front pixels within the visible iris area. Hair is now paler and more directional, with a cool/silver cast under this lighting.

The support/skin boundaries, far outer fan silhouette and large source-portrait style gap remain visible. These neutral eyes and the lower return have no demonstrated blink/gaze controls. No mouth, phone performance, final female rig attachment or final art claim follows from these captures.

## Exact changes and fixed context

- Two existing 257-point iris meshes are cloned and only their UNorm8 COLOR arrays change, using designer-iris-pigment-v1/iris-color-transfer.json (da81e1d8...). Non-color mesh hashes remain identical. Colors are linear, quantized once, with white tint, vertexColorSrgb=0 and no atlas/specular.
- The separate lower-return FBX eab8459a... contributes 118 triangles, with 133 imported split vertices covering all 120 authored points. It uses the matched graphic Backing material and casts no shadows. Canonical 354-corner positions/UV/colors and absence of shapes are verified.
- Existing hair meshes receive only the paired coherent-v2 maps 5bc80c29... and 3f4553af.... Both are sRGB; the independent shadow pigment is not multiplied by an older brown shade. Geometry/UV/normals/capOn and shader controls remain fixed.
- H073e source, neutral eye v6, support-export-v2, temporal patch, selected receiver classification/.05 and the local 189-triangle forehead material partition stay fixed. Outlines stay off. The source camera and crop are unchanged; the existing approximately 18 source-pixel mouth/chin fitting residual remains.

## Import diagnostic and reproduction

The first import stopped on a new 1e-6 coordinate gate. A bounded second run showed the existing Unity registration already has 1.55-1.67e-6 native error, with exact UVs. Raw frozen FBX corners and the transfer agree exactly in positions, UVs and old colors. The final helper retains the original 2e-6 actual Unity-import gate and separately applies 1e-6 transfer-to-raw provenance. No source coordinates or calibration changed. Both failed logs remain under the local player folder; final source snapshots correspond to the successful build only.

This scratch-only build requires the existing isolated review dependencies; this checkpoint does not promote a main-project H scene. Copy the source snapshots to their corresponding scratch Runtime/Editor directories, then invoke `LucidLoop.CharacterArt.Editor.RenDesignerEyeHairReviewBuilder.BuildWindowsViewer` with `-renDesignerIrisTransfer` pointing to the frozen transfer JSON, `-renDesignerReturnSource` to lower-band-v2/return-export-v1, `-renDesignerHairSource` to h-hair-appearance-v2/coherent-v2, and an explicit new `-renDesignerPlayerOutput`. The builder opens RenDesignerForeheadReview and saves a separate RenDesignerEyeHairReview scene.

The tested player is `.local/ren-designer-eye-hair-v1/RenDesignerEyeHairReview.exe`. Capture with `-batchmode -renDesignerEyeHairCapture <fresh-directory> -logFile <fresh-log>`. The player advances live frames, validates actual bindings, writes images and exits. No visible launch occurred. `verify_capture.py` is the exact local verification script, with its documented local predecessor-image dependency. Full source and asset hashes are in the audits and BuildProvenance.json.

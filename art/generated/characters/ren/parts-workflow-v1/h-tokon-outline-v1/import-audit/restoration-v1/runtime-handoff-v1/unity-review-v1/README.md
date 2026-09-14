# Actual Unity A/seal rendering with current designer eyes

Eighteen actual Windows-player images show A-open weights0,25,50,75,100 and return0, all with semantic seal1, from front, quarter and profile. The four saved canonical-position mesh assets are attached beneath their unchanged source nodes at local scale.0001. The current designer-eye v6, blue iris pigment, lower return, directional hair and graded forehead response remain fixed.

The live renderer visibly opens the oral aperture through intermediate states and returns to the exact same closed-rest image in all three views. Actual blendshape weights, child/source transforms, frame number, FaceFrame matrix, material/shader/map names, closed-map weight and original-renderer disabled state are recorded per image. This is GPU camera rendering after16 live frames per state, not replacement BakeMesh snapshots.

The import gate compares646,488 ordered submesh corners across the four meshes. UVs/base normals/tangents match exactly; maximum native Basis error9.0946e-8 and actual child compensation error5.9721e-8. All110 prior transforms,52 unrelated renderer states/material references and the FaceFrame remain preserved. The helper uses only Head/InnerLipWall/OralCavity/Tongue, excluding all historical saved eye/hair meshes.

A weights are100*a on all four. Head/wall seal is100*s*(1-a); there is no a+seal normalization. The existing closed-lip map weight is1-smoothstep(clamp(a/.75)). The selected four saved meshes contain99 tiny zeroed canonical rows, all on Head and below2e-6 native tolerance; this is not bit-exact canonical fidelity. Imported normal/tangent deltas remain explicitly diagnostic.

Reset images are pixel-identical. The restored neutral is not pixel-identical to the historical import:252 front,113 quarter and24 profile edge pixels change; mean absolute front difference is.000834 byte across the frame. Eye-region changes during A are at most one byte; no structural eye or attachment change is claimed. PixelVerification.json records exact comparisons.

Lip pigment transitions and simple oral surfaces remain visibly provisional, especially the teeth/tongue at full A. These images verify basic A/seal motion only: no full English/Japanese speech set, new-eye blink/gaze, expressions, final likeness,40k-triangle budget, canonical female rig or phone performance has been accepted.

## Reproduction

Use the existing isolated scratch project and first reproduce the saved scale10000 assets with the independently verified persistence plan. The source handoff JSON in this folder's parent lists the exact four assets/hashes and prerequisite report. Copy the exact executed source snapshots to scratch Runtime/Editor, invoke LucidLoop.CharacterArt.Editor.RenDesignerMouthReviewBuilder.BuildWindowsViewer with -renDesignerMouthHandoff pointing to runtime-handoff.json and -renDesignerPlayerOutput to a new executable. It opens RenDesignerForeheadFieldReview and saves a separate RenDesignerMouthReview scene.

Run that player with -batchmode -renDesignerMouthCapture <fresh-directory> -logFile <fresh-log>. The tested local executable is .local/ren-designer-mouth-v1/RenDesignerMouthReview.exe. No visible launch or main-project scene promotion occurred. BuildProvenance.json binds the actual scene/helper/mesh hashes. The executed helper3317 wrote its audit successfully; a later reviewer found an audit-I/O failure rollback edge being hardened separately. This package preserves the tested bytes without relabeling them as a later unexecuted helper.

from pathlib import Path
import json,hashlib,shutil
repo=Path(r'B:\lucid-loop');project=Path(r'C:\Users\jetha\AppData\Local\LucidLoopScratch\ren-eye-import-verification')
run=repo/'.local/ren-designer-eye-v6';generated=project/'Assets/CharacterArt/Generated/RenDesignerEyeReview'
out=repo/'art/generated/characters/ren/parts-workflow-v1/h-designer-eyes-v1/trial-v6/unity-review-v1';out.mkdir(exist_ok=True)
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for p in (run/'live-review').glob('*'):shutil.copy2(p,out/p.name)
for name in ['Manifest.json','ImportAudit.json','SourceContract.json']:shutil.copy2(generated/name,out/name)
for folder in ['tested-source','materials','failed-attempts']:(out/folder).mkdir(exist_ok=True)
for rel in ['Runtime/RenDesignerEyeReview.cs','Runtime/RenDesignerEyeReview.cs.meta','Editor/RenDesignerEyeReviewBuilder.cs','Editor/RenDesignerEyeReviewBuilder.cs.meta']:
 p=project/'Assets/CharacterArt'/rel;shutil.copy2(p,out/'tested-source'/p.name)
for p in (generated/'Materials').glob('*.mat'):shutil.copy2(p,out/'materials'/p.name)
for name in ['build-color-mismatch-v2.log','build-duplicate-source-root.log','raw-fbx-color-layers-v2.json']:shutil.copy2(run/name,out/'failed-attempts'/name)
for name in ['Ren_DesignerEye_L_UpperInk.json','Ren_DesignerEye_L_VisibleFan_0.json']:
 p=run/'imported-colors-v2'/name
 if p.exists():shutil.copy2(p,out/'failed-attempts'/name)
shutil.copy2(generated/'Ren_DesignerEyes_Neutral.fbx.meta',out/'importer.meta.txt')
reference=project/'Assets/CharacterArt/Generated/RenHReferenceAnimation/References/OriginalArtist.png'
shutil.copy2(reference.with_suffix('.png.meta'),out/'inherited-reference-importer.meta.txt')
for name in ['prepare_designer_eye_review.py','audit_designer_fbx_colors.py','curate_designer_eye_v6.py']:shutil.copy2(repo/'.local/ren-h-reference-preparation'/name,out/'tested-source'/name)
audit=json.loads((out/'ImportAudit.json').read_text());live=json.loads((out/'LiveEvidence.json').read_text())
report={'status':'ACTUAL_NEUTRAL_V6_UNITY_CAPTURE_NOT_VISUAL_ACCEPTANCE','captureCount':len(live['captures']),'sourceEyeSha256':audit['sourceSha256'],'sourceHeadSha256':audit['headSha256'],'sourceEyesImmutable':True,'outlineOff':True,'noCanonicalMorphRestoration':True,'closedLipPaintCandidate':True,'newEyeControls':'NONE','actualRenderedCornerColorAudit':'Every triangle-referenced imported vertex matched raw FBX native XYZ/UV then final layer0 linearRGBA; maximum linear color error <= halfUNorm8 step +2e-5.','maximumLinearColorError':max(e['maximumReferencedColorError'] for e in audit['eyes']),'unusedImportedVertices':sum(e['unusedVertices'] for e in audit['eyes']),'markerRegistrationError':audit['markerError'],'sourceReferenceSha256':h(reference),'referencePresentationLimitation':'Full-sheet source-camera pairs are diagnostic only and too small for likeness judgment. Inherited reference importer scales non-power-of-two source; a new reference-only import/crop presentation is queued, preserving original image bytes and source-pixel scale.','observedLimitations':['Pale/taupe patches around both eyes remain visible; shutter fallback shadow response differs from mapped head pigment.','Far outer eyelash is fragmented/jagged in quarter view.','Forehead and jaw/support patches, existing profile jaw gap, extraction boundaries and striped hair persist.','Eyes are shallow/elongated but not artist 1:1 accepted.','New separate support/bridge derivative has not been integrated.','No phone performance, canonical female rig, motion, blink, gaze or speech claim.'],'failedAttempts':[{'file':'failed-attempts/build-duplicate-source-root.log','cause':'Ambiguous Head name from historical outline; fixed lookup to explicit original Source subtree.'},{'file':'failed-attempts/build-color-mismatch-v2.log','cause':'Whole-vertex color range included unused white vertices. Actual read-only dump isolated them; no rendered color or tolerance changed. Gate now checks referenced corners.'}],'playerLocal':'.local/ren-designer-eye-v6/RenDesignerEyeReview.exe','sceneScratch':'Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerEyeReview.unity','visiblePlayerLaunched':False}
(out/'review-evidence.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8',newline='\n')
(out/'README.md').write_text('''# Neutral designer-eye v6: actual Unity review

These 18 images were rendered by the isolated Windows Unity 6000.3.24f1 D3D11 player. The exact v2 eye FBX is combined with unchanged historical H073e geometry, selected Tokon refined-face/placed-cap maps, and the explicitly experimental closed-lip paint. Old eye meshes and outline shells are disabled. The new eye package has no blink, gaze or speech controls.

Start with `tokon--front.png`, `tokon--quarter.png`, both profiles and their `unlit--` counterparts. Cap-off and hair-hidden profile are geometry diagnostics. Full source-camera pairs retain the supplied eye-region camera fit, but their full-sheet layout is too small for useful likeness review; a corrected reference import and equal-source-pixel portrait crop are queued separately. The supplied camera itself has roughly 18 source-pixel mouth/chin residual and a different mouth pose from the sketch.

All 22 meshes passed actual imported rendered-corner position/UV/color checks against the immutable binary FBX. Unity uses UNorm8 linear vertex colors: maximum color error 0.001960663. Four unused vertices have no rendered color meaning; they are separately recorded. The first audit stopped on unused white vertices; the second audit verified every drawn corner without widening pigment tolerance or changing source data. `ImportAudit.json` and the preserved failed-attempt records show this distinction.

The result is not visual acceptance. Pale/taupe patches around the eyes, fragmented far eyelash edge, support/forehead marks, profile jaw gap and striped hair remain visible. Skin shutters use the selected Skin preset with fallback R1/G0/B0/A1 and FaceMode .90, linear vertex color once, no texture/control/shadow atlas. The head uses authored base/shadow/control maps. This response difference is a separate material question. No support bridge or canonical morph restoration is present.

The player remains local at `.local/ren-designer-eye-v6/RenDesignerEyeReview.exe`; it was run hidden for capture and exited normally. Build with the tested scratch Editor entry `LucidLoop.CharacterArt.Editor.RenDesignerEyeReviewBuilder.BuildWindowsViewer`, providing a new `-renDesignerPlayerOutput` path. Capture with `-renDesignerCapture <new folder>`. Main Unity was not launched or modified. This package retains source snapshots/evidence rather than promoting a production scene or making a phone performance claim.
''',encoding='utf-8',newline='\n')
files=[{'file':p.relative_to(out).as_posix(),'sha256':h(p),'bytes':p.stat().st_size} for p in out.rglob('*') if p.is_file() and p.name!='files.json']
(out/'files.json').write_text(json.dumps({'files':files},indent=2)+'\n',encoding='utf-8',newline='\n');print(json.dumps({'output':str(out),'files':len(files)+1,'captures':len(live['captures'])}))

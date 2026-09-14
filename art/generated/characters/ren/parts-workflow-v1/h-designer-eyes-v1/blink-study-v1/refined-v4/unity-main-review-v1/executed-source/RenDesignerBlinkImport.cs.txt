using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LucidLoop.CharacterArt.Editor
{
    // Editor helper only. The caller owns scratch launch, shader assignment and runtime capture.
    public static class RenDesignerBlinkImport
    {
        public const string Root = "Assets/CharacterArt/Generated/RenDesignerBlinkReview";
        public const string AuditPath = Root + "/BlinkImportAudit.json";
        const string FramePath = "Assets/CharacterArt/Generated/RenDesignerBlinkReview/NeutralFrameAudit.json";
        const string FbxName = "Ren_DesignerEyes_Blink.fbx";
        const string FbxHash = "e201b5afede1c136356609b00dbf997f4702de80982c009170d5baf6c6f11149";
        const string ContractHash = "a31bcd6c92a51b80ba7de7d0d378bc9e57de3be05c864261d5639bc1139e9d21";
        const string NeutralEyeFbxHash = "ee7fbae3066f0576328f32fd635583da0d419d8dcfd6033ed102d2695f6338fc";
        const float PositionTolerance = 2e-6f, UvTolerance = 2e-5f, ColorTolerance = .5f / 255f + .00002f;
        const float CanonicalPositionScale = 10000f;
        [Serializable] sealed class Contract { public Row[] objects; public int total_triangles; }
        [Serializable] sealed class Row
        {
            public string @object, side, key, corner_file, corner_sha256, endpoint_file, endpoint_sha256;
            public int vertices, triangles, authored_referenced_vertices, unused_authored_vertices, corner_count;
        }
        [Serializable] sealed class Frame { public string sourceSha256; public float[] nativeToUnity; }
        [Serializable] sealed class Failure { public int importedVertex; public Vector3 expectedNativeDelta, actualNativeDelta; public float error; }
        [Serializable] sealed class MeshAudit
        {
            public string renderer, key, colorFormat, oldRendererType, cornerSha256, endpointSha256;
            public int authoredVertices, authoredReferencedVertices, importedVertices, drawnImportedVertices, unusedImportedVertices, triangles, movingDrawnVertices, lostDrawnMotionsOverTolerance;
            public float neutralNativeError, previousNeutralNativeError, uv0Error, openLinearColorError, closedLinearColorError, actualNativeEndpointError, actualNeutralBakeError;
            public float[] bakedQuarterWeightErrors;
            public string[] shapeNames;
            public Failure[] endpointFailures;
            public bool originalDisabled, shaderAssignmentLeftToCaller;
            public bool promotedFromMeshRenderer;
            public RestorationAudit canonicalPositionRestoration;
        }
        [Serializable] sealed class RestorationAudit
        {
            public string meshAsset, savedAssetSha256;
            public float positionScale = CanonicalPositionScale, childInverseScale = 1f / CanonicalPositionScale, rawNeutralNativeError, rawEndpointNativeError, savedEndpointNativeError;
            public int rawLostMotionsOverTolerance, authoredRowsCovered, importedRowsMatched, coincidentEquivalentAuthoredRows;
            public bool allAuthoredRowsCovered, savedAndReloaded, topologyChannelsBaseNormalsTangentsExact, importedShapeNormalsTangentsExact, parentTransformUnchanged;
            public Failure[] rawEndpointFailures;
        }
        [Serializable] sealed class Audit
        {
            public string status = "PREPARING", unityVersion, fbxSha256 = FbxHash, contractSha256 = ContractHash, frameAuditSha256;
            public float existingMarkerError, importedMarkerError, positionTolerance = PositionTolerance, uvTolerance = UvTolerance, colorTolerance = ColorTolerance;
            public float[] nativeToUnity;
            public MeshAudit[] meshes;
            public bool sourceUnchanged, unchangedStaticRendererBindings, returnReferenceRebound, newEyesRebound, materialListsRebound;
            public bool canonicalPositionRestorationExplicitlyEnabled;
            public string limitation = "Scratch candidate only. No scale compensation or recreated morphs. Full native endpoints and quarter-weight BakeMesh positions are audited; names alone are insufficient. One pale return sample and tiny terminal overlap remain GPU review limitations. Static iris/backing and working mouth assets are untouched. Runtime must bind matching per-eye _EyeBlinkWeight and verify neutral/clip appearance.";
        }
        sealed class Corner { public Vector3 position, delta; public Vector2 uv; public Color open, closed; }
        sealed class StaticBinding { public Renderer renderer; public int mesh; public int[] materials; public Matrix4x4 matrix; }
        sealed class MaterialChange { public Material target, backup; public string path; public bool created; }

        public static SkinnedMeshRenderer[] Add(RenDesignerEyeReview review, string exportFolder, bool restoreCanonicalPositions = false)
        {
            if (!Path.GetFullPath(Application.dataPath).Replace('\\', '/').Equals("B:/lucid-loop/Unity/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Blink import is restricted to the existing B:/lucid-loop/Unity main project.");
            if (!review || !review.ModelRoot) throw new ArgumentNullException(nameof(review));
            Directory.CreateDirectory(Root); Directory.CreateDirectory(Root + "/Canonical");
            var audit = new Audit { unityVersion = Application.unityVersion, canonicalPositionRestorationExplicitlyEnabled = restoreCanonicalPositions }; var audits = new List<MeshAudit>(); GameObject instance = null; Transform wrapper = null;
            var materialChanges = new List<MaterialChange>(); Action rollbackBindings = null;
            var createdMeshAssets = new List<string>();
            if (restoreCanonicalPositions) audit.limitation = "Explicit opt-in private position restoration: canonical Basis and position deltas x10000 with an inverse-scale child; imported base and shape normals/tangents retained and checked, not canonically rederived. Frozen art remains unchanged. Every saved/reloaded endpoint and quarter-weight BakeMesh must pass 2e-6 native tolerance. Neutral visual parity, blink clip, one pale return sample and terminal flicker still require GPU review; no likeness approval.";
            var sourceFbx = Path.Combine(exportFolder, FbxName); var sourceContract = Path.Combine(exportFolder, "contract.json");
            try
            {
                Verify(sourceFbx, FbxHash); Verify(sourceContract, ContractHash);
                var contract = JsonUtility.FromJson<Contract>(File.ReadAllText(sourceContract));
                if (contract.objects == null || contract.objects.Length != 13 || contract.total_triangles != 3947) throw new InvalidDataException("Expected13 controlled layers/3947 triangles.");
                var rows = contract.objects.ToDictionary(r => r.@object);
                if (rows.Count != 13 || rows.Values.Any(r => r.key != "eyeBlink" + r.side || (r.side != "L" && r.side != "R"))) throw new InvalidDataException("Unexpected blink contract names/sides.");
                var original = rows.Keys.ToDictionary(name => name, name => review.ModelRoot.GetComponentsInChildren<Renderer>(true).Single(r => r.name == name));
                var oldSet = original.Values.ToHashSet();
                var staticBindings = review.ModelRoot.GetComponentsInChildren<Renderer>(true).Where(r => !oldSet.Contains(r)).Select(r => new StaticBinding { renderer = r, mesh = Mesh(r).GetInstanceID(), materials = r.sharedMaterials.Select(m => m ? m.GetInstanceID() : 0).ToArray(), matrix = r.localToWorldMatrix }).ToArray();
                var returnContext = review.GetComponent<RenDesignerEyeHairReview>();
                if (!returnContext || returnContext.ApertureReturn != original["Ren_DesignerEye_L_ApertureReturn"]) throw new InvalidDataException("Selected return runtime reference is not the original controlled return.");
                var previousNewEyes = review.NewEyes; var previousMaterials = review.Materials; var previousGraphics = review.GraphicMaterials; var previousReturn = returnContext.ApertureReturn; var enabledBefore = original.Values.ToDictionary(r => r, r => r.enabled);
                rollbackBindings = () => { foreach (var pair in enabledBefore) if (pair.Key) pair.Key.enabled = pair.Value; review.NewEyes = previousNewEyes; review.Materials = previousMaterials; review.GraphicMaterials = previousGraphics; if (returnContext) returnContext.ApertureReturn = previousReturn; };
                audit.frameAuditSha256 = Hash(FramePath);
                if (!string.IsNullOrEmpty(review.ImportAuditSha256) && review.ImportAuditSha256 != audit.frameAuditSha256) throw new InvalidDataException("Loaded review native-frame audit changed.");
                var frame = JsonUtility.FromJson<Frame>(File.ReadAllText(FramePath));
                if (frame.sourceSha256 != NeutralEyeFbxHash || frame.nativeToUnity == null || frame.nativeToUnity.Length != 16) throw new InvalidDataException("Frozen native-eye frame absent.");
                var nativeToUnity = Matrix(frame.nativeToUnity); audit.nativeToUnity = frame.nativeToUnity;
                audit.existingMarkerError = MarkerError(Find(review.ModelRoot, "NeutralDesignerEyes"), nativeToUnity);
                if (audit.existingMarkerError > 2e-5f) throw new InvalidDataException("Existing neutral eye registration differs from frozen audit.");
                var corners = new Dictionary<string, Corner[]>();
                var allEndpoints = new Dictionary<string, Corner[]>();
                foreach (var row in rows.Values)
                {
                    foreach (var file in new[] { row.corner_file, row.endpoint_file }) if (Path.GetFileName(file) != file) throw new InvalidDataException("Canonical input must be a simple filename.");
                    var cornerPath = Path.Combine(exportFolder, row.corner_file); var endpointPath = Path.Combine(exportFolder, row.endpoint_file); Verify(cornerPath, row.corner_sha256); Verify(endpointPath, row.endpoint_sha256);
                    var data = ReadCorners(File.ReadAllBytes(cornerPath), row.corner_count); var endpoints = ReadEndpoints(File.ReadAllBytes(endpointPath), row.vertices);
                    // Validate canonical source identity before using either sidecar as an import oracle.
                    foreach (var c in data) if (!endpoints.Any(e => e.position.Equals(c.position) && e.delta.Equals(c.delta))) throw new InvalidDataException("Corner/endpoint source disagreement: " + row.@object);
                    if (data.Select(c => c.position).Distinct().Count() > row.authored_referenced_vertices) throw new InvalidDataException("Canonical reference count is invalid.");
                    corners.Add(row.@object, data); allEndpoints.Add(row.@object, endpoints); File.Copy(cornerPath, Root + "/Canonical/" + row.corner_file, true); File.Copy(endpointPath, Root + "/Canonical/" + row.endpoint_file, true);
                }
                File.Copy(sourceFbx, Root + "/" + FbxName, true); File.Copy(sourceContract, Root + "/SourceContract.json", true); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var asset = Root + "/" + FbxName; var importer = AssetImporter.GetAtPath(asset) as ModelImporter;
                if (!importer) throw new InvalidDataException("Blink model importer absent.");
                importer.isReadable = true; importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None; importer.importBlendShapes = true;
                importer.importNormals = ModelImporterNormals.Import; importer.importBlendShapeNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.Import;
                importer.meshCompression = ModelImporterMeshCompression.Off; importer.optimizeMeshVertices = false; importer.optimizeMeshPolygons = false; importer.weldVertices = false; importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport(); Verify(asset, FbxHash);
                instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(asset)); instance.name = "ControlledDesignerBlink";
                var importedFrame = MarkerMatrix(instance.transform); wrapper = new GameObject("DesignerBlinkRegistration").transform; wrapper.SetParent(review.ModelRoot, false); instance.transform.SetParent(wrapper, false);
                SetMatrix(wrapper, review.ModelRoot.worldToLocalMatrix * nativeToUnity * importedFrame.inverse); audit.importedMarkerError = MarkerError(instance.transform, nativeToUnity);
                if (audit.importedMarkerError > 2e-5f) throw new InvalidDataException("Imported blink native-marker calibration failed.");
                var imported = instance.GetComponentsInChildren<Renderer>(true);
                if (imported.Length != 13 || !imported.Select(r => r.name).ToHashSet().SetEquals(rows.Keys)) throw new InvalidDataException("Exactly the13 requested renderer names are required.");
                var promoted = new HashSet<string>();
                foreach (var renderer in imported)
                {
                    if (renderer is SkinnedMeshRenderer) continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (!(renderer is MeshRenderer) || !filter || !filter.sharedMesh) throw new InvalidDataException("Unsupported imported blink renderer type: " + renderer.name);
                    var sourceMesh = filter.sharedMesh; var sourceMatrix = renderer.localToWorldMatrix; var sourceMaterials = renderer.sharedMaterials; var wasEnabled = renderer.enabled; var gameObject = renderer.gameObject; var rendererName = renderer.name;
                    var casting = renderer.shadowCastingMode; var receiving = renderer.receiveShadows; var lightProbes = renderer.lightProbeUsage; var reflections = renderer.reflectionProbeUsage; var anchor = renderer.probeAnchor; var vectors = renderer.motionVectorGenerationMode;
                    Object.DestroyImmediate(renderer);
                    var skin = gameObject.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = sourceMesh; skin.sharedMaterials = sourceMaterials; skin.localBounds = sourceMesh.bounds; skin.bones = Array.Empty<Transform>(); skin.rootBone = null; skin.updateWhenOffscreen = true; skin.enabled = wasEnabled;
                    skin.shadowCastingMode = casting; skin.receiveShadows = receiving; skin.lightProbeUsage = lightProbes; skin.reflectionProbeUsage = reflections; skin.probeAnchor = anchor; skin.motionVectorGenerationMode = vectors;
                    if (skin.sharedMesh != sourceMesh || Enumerable.Range(0, 16).Any(i => skin.localToWorldMatrix[i] != sourceMatrix[i])) throw new InvalidDataException("MeshRenderer promotion changed mesh or transform.");
                    promoted.Add(rendererName); Object.DestroyImmediate(filter);
                }
                var result = contract.objects.Select(row => instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == row.@object)).ToArray();
                for (int rendererIndex = 0; rendererIndex < result.Length; rendererIndex++)
                {
                    var renderer = result[rendererIndex]; var row = rows[renderer.name]; var old = original[renderer.name]; var data = corners[renderer.name]; var ma = new MeshAudit { renderer = renderer.name, key = row.key, authoredVertices = row.vertices, authoredReferencedVertices = row.authored_referenced_vertices, oldRendererType = old.GetType().Name, cornerSha256 = row.corner_sha256, endpointSha256 = row.endpoint_sha256, promotedFromMeshRenderer = promoted.Contains(renderer.name) }; audits.Add(ma);
                    if (restoreCanonicalPositions) { ma.canonicalPositionRestoration = new RestorationAudit(); renderer = RestoreCanonicalPositions(renderer, row, data, allEndpoints[row.@object], nativeToUnity, ma.canonicalPositionRestoration, createdMeshAssets); result[rendererIndex] = renderer; }
                    var mesh = renderer.sharedMesh;
                    var used = Enumerable.Range(0, mesh.subMeshCount).SelectMany(i => mesh.GetIndices(i)).Distinct().ToArray(); var p = mesh.vertices; var uv0 = mesh.uv; var color = mesh.colors; var rg = new List<Vector2>(); var ba = new List<Vector2>(); mesh.GetUVs(1, rg); mesh.GetUVs(2, ba);
                    ma.importedVertices = mesh.vertexCount; ma.drawnImportedVertices = used.Length; ma.unusedImportedVertices = mesh.vertexCount - used.Length; ma.triangles = Enumerable.Range(0, mesh.subMeshCount).Sum(i => (int)mesh.GetIndexCount(i)) / 3; ma.colorFormat = mesh.GetVertexAttributeFormat(VertexAttribute.Color).ToString();
                    if (ma.triangles != row.triangles || used.Length == 0 || new[] { uv0.Length, color.Length, rg.Count, ba.Count }.Any(n => n != mesh.vertexCount)) throw new InvalidDataException("Blink triangle/color/UV channel count mismatch: " + renderer.name);
                    ma.shapeNames = Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToArray();
                    if (mesh.blendShapeCount != 1 || ma.shapeNames[0] != row.key || mesh.GetBlendShapeFrameCount(0) != 1 || mesh.GetBlendShapeFrameWeight(0, 0) != 100) throw new InvalidDataException("Unexpected actual imported blink key/frame: " + renderer.name);
                    var toNative = nativeToUnity.inverse * renderer.localToWorldMatrix; var delta = new Vector3[mesh.vertexCount]; mesh.GetBlendShapeFrameVertices(0, 0, delta, null, null); var matched = new Dictionary<int, Corner>(); var failures = new List<Failure>();
                    foreach (var i in used)
                    {
                        var native = toNative.MultiplyPoint3x4(p[i]); var candidates = data.Where(c => Vector3.Distance(c.position, native) <= PositionTolerance && Vector2.Distance(c.uv, uv0[i]) <= UvTolerance).ToArray();
                        if (candidates.Length == 0) throw new InvalidDataException("No frozen neutral position/UV match: " + renderer.name + " vertex" + i);
                        var c = candidates.OrderBy(corner => Vector3.Distance(corner.position, native) + Vector2.Distance(corner.uv, uv0[i])).First(); matched[i] = c;
                        ma.neutralNativeError = Mathf.Max(ma.neutralNativeError, Vector3.Distance(c.position, native)); ma.uv0Error = Mathf.Max(ma.uv0Error, Vector2.Distance(c.uv, uv0[i])); ma.openLinearColorError = Mathf.Max(ma.openLinearColorError, ColorError(c.open, color[i]));
                        var closed = new Color(rg[i].x, rg[i].y, ba[i].x, ba[i].y); ma.closedLinearColorError = Mathf.Max(ma.closedLinearColorError, ColorError(c.closed, closed));
                        if (ColorError(c.open, color[i]) > ColorTolerance || ColorError(c.closed, closed) > UvTolerance || color[i].a != 1 || closed.a != 1) throw new InvalidDataException("Blink linear color channel mismatch: " + renderer.name + " vertex" + i);
                        var actual = toNative.MultiplyVector(delta[i]); var error = Vector3.Distance(actual, c.delta); ma.actualNativeEndpointError = Mathf.Max(ma.actualNativeEndpointError, error);
                        if (c.delta.magnitude > PositionTolerance) { ma.movingDrawnVertices++; if (actual.magnitude <= 1e-12f) ma.lostDrawnMotionsOverTolerance++; }
                        if (error > PositionTolerance && failures.Count < 16) failures.Add(new Failure { importedVertex = i, expectedNativeDelta = c.delta, actualNativeDelta = actual, error = error });
                    }
                    ma.endpointFailures = failures.ToArray();
                    if (ma.actualNativeEndpointError > PositionTolerance || ma.lostDrawnMotionsOverTolerance > 0) throw new InvalidDataException("Actual Unity blink endpoints differ from canonical source: " + renderer.name + ", max native error=" + ma.actualNativeEndpointError.ToString("R") + ", lost motions=" + ma.lostDrawnMotionsOverTolerance + ". No automatic scale workaround was applied.");
                    // Bidirectional reference coverage prevents a shortened import from passing by checking only survivors.
                    var importedNative = used.Select(i => toNative.MultiplyPoint3x4(p[i])).ToArray();
                    foreach (var c in data) if (!importedNative.Any(v => Vector3.Distance(v, c.position) <= PositionTolerance)) throw new InvalidDataException("A referenced canonical blink point was lost: " + renderer.name);
                    var priorMesh = Mesh(old); var priorPositions = priorMesh.vertices; var priorUv = priorMesh.uv; var priorToNative = nativeToUnity.inverse * old.localToWorldMatrix;
                    foreach (var i in Enumerable.Range(0, priorMesh.subMeshCount).SelectMany(k => priorMesh.GetIndices(k)).Distinct())
                    {
                        var point = priorToNative.MultiplyPoint3x4(priorPositions[i]); var matches = data.Where(c => Vector2.Distance(c.uv, priorUv[i]) <= UvTolerance).ToArray(); if (matches.Length == 0) throw new InvalidDataException("Prior neutral UV absent from blink source: " + renderer.name);
                        var error = matches.Min(c => Vector3.Distance(c.position, point)); ma.previousNeutralNativeError = Mathf.Max(ma.previousNeutralNativeError, error); if (error > PositionTolerance) throw new InvalidDataException("Current neutral renderer differs from blink Basis: " + renderer.name);
                    }
                    ma.bakedQuarterWeightErrors = new float[5];
                    for (int state = 0; state <= 4; state++)
                    {
                        float weight = state * .25f; renderer.SetBlendShapeWeight(0, weight * 100); var baked = new Mesh(); renderer.BakeMesh(baked, false);
                        try { if (baked.vertexCount != mesh.vertexCount) throw new InvalidDataException("Blink BakeMesh topology changed."); var values = baked.vertices; foreach (var i in used) { var expected = matched[i].position + matched[i].delta * weight; ma.bakedQuarterWeightErrors[state] = Mathf.Max(ma.bakedQuarterWeightErrors[state], Vector3.Distance(toNative.MultiplyPoint3x4(values[i]), expected)); } }
                        finally { Object.DestroyImmediate(baked); }
                        if (ma.bakedQuarterWeightErrors[state] > PositionTolerance) throw new InvalidDataException("Actual baked blink state differs from canonical endpoint interpolation: " + renderer.name + " at" + weight + ", error=" + ma.bakedQuarterWeightErrors[state].ToString("R"));
                    }
                    ma.actualNeutralBakeError = ma.bakedQuarterWeightErrors[0]; renderer.SetBlendShapeWeight(0, 0); renderer.updateWhenOffscreen = true;
                    var material = new Material(old.sharedMaterial) { name = renderer.name + "_BlinkReview" }; material.SetVector("_BaseColor", Vector4.one); material.SetFloat("_UseVertexColor", 1); material.SetFloat("_VertexColorSrgb", 0); material.SetFloat("_UseBaseMap", 0);
                    if (material.HasProperty("_EyeBlinkWeight")) material.SetFloat("_EyeBlinkWeight", 0); else ma.shaderAssignmentLeftToCaller = true;
                    bool skin = renderer.name.EndsWith("_SkinShutter", StringComparison.Ordinal); if (!skin) { material.SetFloat("_Unlit", 1); material.SetFloat("_HighlightStrength", 0); material.SetFloat("_RimStrength", 0); }
                    var materialPath = Root + "/" + renderer.name + ".mat"; var saved = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (saved) { materialChanges.Add(new MaterialChange { target = saved, backup = new Material(saved), path = materialPath }); saved.shader = material.shader; saved.CopyPropertiesFromMaterial(material); Object.DestroyImmediate(material); material = saved; }
                    else { AssetDatabase.CreateAsset(material, materialPath); materialChanges.Add(new MaterialChange { target = material, path = materialPath, created = true }); }
                    renderer.sharedMaterials = Enumerable.Repeat(material, mesh.subMeshCount).ToArray(); renderer.shadowCastingMode = skin ? old.shadowCastingMode : ShadowCastingMode.Off; renderer.receiveShadows = skin && old.receiveShadows;
                }
                // Prepare the complete replacement before changing the old scene bindings.
                var byName = result.ToDictionary(r => r.name); var newEyes = review.NewEyes.Select(r => byName.TryGetValue(r.name, out var replacement) ? (Renderer)replacement : r).Concat(result.Where(r => !review.NewEyes.Any(prior => prior.name == r.name))).Distinct().ToArray();
                if (rows.Keys.Any(name => newEyes.Count(r => r.name == name) != 1)) throw new InvalidDataException("Controlled NewEyes entries were not rebound exactly once.");
                var bound = review.ModelRoot.GetComponentsInChildren<Renderer>(true).Where(r => !oldSet.Contains(r) && r.enabled).SelectMany(r => r.sharedMaterials).ToHashSet();
                var materials = review.Materials.Where(bound.Contains).Concat(result.SelectMany(r => r.sharedMaterials)).Distinct().ToArray(); var graphicMaterials = review.GraphicMaterials.Where(bound.Contains).Concat(result.Where(r => !r.name.EndsWith("_SkinShutter", StringComparison.Ordinal)).SelectMany(r => r.sharedMaterials)).Distinct().ToArray();
                foreach (var s in staticBindings) if (Mesh(s.renderer).GetInstanceID() != s.mesh || !s.renderer.sharedMaterials.Select(m => m ? m.GetInstanceID() : 0).SequenceEqual(s.materials) || Enumerable.Range(0, 16).Any(i => s.renderer.localToWorldMatrix[i] != s.matrix[i])) throw new InvalidDataException("An unrelated static/iris/mouth binding changed during blink import.");
                audit.sourceUnchanged = Hash(sourceFbx) == FbxHash && Hash(sourceContract) == ContractHash; if (!audit.sourceUnchanged) throw new InvalidDataException("Frozen blink handoff changed during import.");
                // Commit only after every endpoint, channel, neutral and baked state passed.
                foreach (var old in original.Values) old.enabled = false;
                review.NewEyes = newEyes; audit.newEyesRebound = true; returnContext.ApertureReturn = byName["Ren_DesignerEye_L_ApertureReturn"]; audit.returnReferenceRebound = true;
                review.Materials = materials; review.GraphicMaterials = graphicMaterials; audit.materialListsRebound = true;
                audit.unchangedStaticRendererBindings = true; foreach (var ma in audits) ma.originalDisabled = !original[ma.renderer].enabled;
                audit.status = "PASS_ACTUAL_UNITY_NEUTRAL_COLORS_NATIVE_ENDPOINTS_AND_QUARTER_WEIGHT_BAKES"; audit.meshes = audits.ToArray();
                foreach (var change in materialChanges) AssetDatabase.SaveAssetIfDirty(change.target);
                // Audit persistence is inside the transaction: a write/import failure rolls back bindings too.
                File.WriteAllText(AuditPath, JsonUtility.ToJson(audit, true)); AssetDatabase.ImportAsset(AuditPath, ImportAssetOptions.ForceSynchronousImport); return result;
            }
            catch (Exception e)
            {
                audit.status = "FAIL: " + e.Message; audit.meshes = audits.ToArray(); rollbackBindings?.Invoke();
                foreach (var change in materialChanges)
                {
                    try { if (change.created) AssetDatabase.DeleteAsset(change.path); else if (change.target && change.backup) { change.target.shader = change.backup.shader; change.target.CopyPropertiesFromMaterial(change.backup); AssetDatabase.SaveAssetIfDirty(change.target); } }
                    catch (Exception restoreError) { Debug.LogError("Blink material rollback failed: " + restoreError); }
                }
                if (wrapper) Object.DestroyImmediate(wrapper.gameObject); else if (instance) Object.DestroyImmediate(instance);
                foreach (var path in createdMeshAssets) { try { AssetDatabase.DeleteAsset(path); } catch (Exception restoreError) { Debug.LogError("Blink private mesh rollback failed: " + restoreError); } }
                try { File.WriteAllText(AuditPath, JsonUtility.ToJson(audit, true)); AssetDatabase.ImportAsset(AuditPath, ImportAssetOptions.ForceSynchronousImport); } catch (Exception auditError) { Debug.LogError("Blink failure-audit write failed: " + auditError); }
                throw;
            }
            finally { foreach (var change in materialChanges) if (change.backup) Object.DestroyImmediate(change.backup); }
        }
        static SkinnedMeshRenderer RestoreCanonicalPositions(SkinnedMeshRenderer source, Row row, Corner[] corners, Corner[] endpoints, Matrix4x4 nativeToUnity, RestorationAudit audit, List<string> createdAssets)
        {
            var mesh = source.sharedMesh; var p = mesh.vertices; var uv = mesh.uv; var colors = mesh.colors;
            var rg = new List<Vector2>(); var ba = new List<Vector2>(); mesh.GetUVs(1, rg); mesh.GetUVs(2, ba);
            var used = Enumerable.Range(0, mesh.subMeshCount).SelectMany(s => mesh.GetIndices(s)).ToHashSet();
            if (new[] { uv.Length, colors.Length, rg.Count, ba.Count }.Any(n => n != p.Length) || source.bones.Length != 0 || source.rootBone || mesh.bindposes.Length != 0) throw new InvalidDataException("Restoration requires complete channels and a bone-free imported mesh: " + row.@object);
            if (mesh.blendShapeCount != 1 || mesh.GetBlendShapeName(0) != row.key || mesh.GetBlendShapeFrameCount(0) != 1 || mesh.GetBlendShapeFrameWeight(0, 0) != 100) throw new InvalidDataException("Restoration requires the exact imported key/frame: " + row.@object);
            var rawDelta = new Vector3[p.Length]; var normalDelta = new Vector3[p.Length]; var tangentDelta = new Vector3[p.Length]; mesh.GetBlendShapeFrameVertices(0, 0, rawDelta, normalDelta, tangentDelta);
            var originalMatrix = source.localToWorldMatrix; var toNative = nativeToUnity.inverse * originalMatrix; var toLocal = toNative.inverse;
            var mapped = new Corner[p.Length]; var failures = new List<Failure>();
            for (int i = 0; i < p.Length; i++)
            {
                var native = toNative.MultiplyPoint3x4(p[i]);
                var candidates = (used.Contains(i) ? corners.Where(c => Vector2.Distance(c.uv, uv[i]) <= UvTolerance) : endpoints.AsEnumerable()).Where(c => Vector3.Distance(c.position, native) <= PositionTolerance).ToArray();
                if (used.Contains(i)) candidates = candidates.Where(c => ColorError(c.open, colors[i]) <= ColorTolerance && ColorError(c.closed, new Color(rg[i].x, rg[i].y, ba[i].x, ba[i].y)) <= UvTolerance).ToArray();
                if (candidates.Length == 0) throw new InvalidDataException("No canonical row match before restoration: " + row.@object + " vertex " + i);
                var selected = candidates.OrderBy(c => Vector3.Distance(c.position, native) + (used.Contains(i) ? Vector2.Distance(c.uv, uv[i]) : 0)).First();
                // A coincident identity is equivalent only if its canonical motion is also identical.
                if (candidates.Any(c => c.position.Equals(selected.position) && (!used.Contains(i) || c.uv.Equals(selected.uv)) && !c.delta.Equals(selected.delta))) throw new InvalidDataException("Ambiguous coincident canonical motion: " + row.@object + " vertex " + i);
                mapped[i] = selected; audit.rawNeutralNativeError = Mathf.Max(audit.rawNeutralNativeError, Vector3.Distance(native, selected.position));
                var actual = toNative.MultiplyVector(rawDelta[i]); var error = Vector3.Distance(actual, selected.delta); audit.rawEndpointNativeError = Mathf.Max(audit.rawEndpointNativeError, error);
                if (used.Contains(i) && selected.delta.magnitude > PositionTolerance && actual.magnitude <= 1e-12f) audit.rawLostMotionsOverTolerance++;
                if (error > PositionTolerance && failures.Count < 16) failures.Add(new Failure { importedVertex = i, expectedNativeDelta = selected.delta, actualNativeDelta = actual, error = error });
            }
            audit.rawEndpointFailures = failures.ToArray(); audit.importedRowsMatched = mapped.Length;
            foreach (var e in endpoints) { if (!mapped.Any(c => c.position.Equals(e.position) && c.delta.Equals(e.delta))) throw new InvalidDataException("An authored canonical row is absent before restoration: " + row.@object + " position " + e.position.ToString("R")); audit.authoredRowsCovered++; }
            audit.coincidentEquivalentAuthoredRows = endpoints.Length - endpoints.Select(e => (e.position, e.delta)).Distinct().Count(); audit.allAuthoredRowsCovered = audit.authoredRowsCovered == row.vertices;
            foreach (var c in corners) if (!used.Any(i => mapped[i].position.Equals(c.position) && mapped[i].delta.Equals(c.delta) && Vector2.Distance(uv[i], c.uv) <= UvTolerance && ColorError(colors[i], c.open) <= ColorTolerance && ColorError(new Color(rg[i].x, rg[i].y, ba[i].x, ba[i].y), c.closed) <= UvTolerance)) throw new InvalidDataException("A full canonical drawn corner has no imported equivalent: " + row.@object);
            var positions = mapped.Select(c => toLocal.MultiplyPoint3x4(c.position) * CanonicalPositionScale).ToArray();
            var deltas = mapped.Select(c => toLocal.MultiplyVector(c.delta) * CanonicalPositionScale).ToArray();
            var clone = Object.Instantiate(mesh); clone.name = row.@object + "_CanonicalPositionScale10000";
            try
            {
                clone.vertices = positions; clone.ClearBlendShapes(); clone.AddBlendShapeFrame(row.key, 100, deltas, normalDelta, tangentDelta);
                var bounds = new Bounds(positions[0], Vector3.zero); for (int i = 0; i < positions.Length; i++) { bounds.Encapsulate(positions[i]); bounds.Encapsulate(positions[i] + deltas[i]); } clone.bounds = bounds;
                AssertPreservedChannels(mesh, clone);
                Directory.CreateDirectory(Root + "/CanonicalPositionMeshes"); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var path = AssetDatabase.GenerateUniqueAssetPath(Root + "/CanonicalPositionMeshes/" + row.@object + ".asset");
                AssetDatabase.CreateAsset(clone, path); createdAssets.Add(path); AssetDatabase.SaveAssetIfDirty(clone);
                // Drop the working instance before loading: subsequent gates inspect persisted asset data.
                Resources.UnloadAsset(clone); clone = null; AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (!saved) throw new InvalidDataException("Saved canonical mesh did not reload: " + row.@object);
                AssertPreservedChannels(mesh, saved); audit.topologyChannelsBaseNormalsTangentsExact = true;
                if (!saved.vertices.SequenceEqual(positions)) throw new InvalidDataException("Saved canonical Basis positions changed: " + row.@object);
                if (saved.blendShapeCount != 1 || saved.GetBlendShapeName(0) != row.key || saved.GetBlendShapeFrameCount(0) != 1 || saved.GetBlendShapeFrameWeight(0, 0) != 100) throw new InvalidDataException("Saved canonical shape/frame changed: " + row.@object);
                var savedDelta = new Vector3[p.Length]; var savedNormals = new Vector3[p.Length]; var savedTangents = new Vector3[p.Length]; saved.GetBlendShapeFrameVertices(0, 0, savedDelta, savedNormals, savedTangents);
                if (!normalDelta.SequenceEqual(savedNormals) || !tangentDelta.SequenceEqual(savedTangents)) throw new InvalidDataException("Imported shape normal/tangent deltas changed during position restoration: " + row.@object);
                audit.importedShapeNormalsTangentsExact = true;
                for (int i = 0; i < p.Length; i++) audit.savedEndpointNativeError = Mathf.Max(audit.savedEndpointNativeError, Vector3.Distance(toNative.MultiplyVector(savedDelta[i] / CanonicalPositionScale), mapped[i].delta));
                if (audit.savedEndpointNativeError > PositionTolerance) throw new InvalidDataException("Saved canonical delta restoration exceeds unchanged native tolerance: " + row.@object);
                audit.meshAsset = path; audit.savedAssetSha256 = Hash(path); audit.savedAndReloaded = true;
                var child = new GameObject(row.@object); child.transform.SetParent(source.transform, false); child.transform.localPosition = Vector3.zero; child.transform.localRotation = Quaternion.identity; child.transform.localScale = Vector3.one / CanonicalPositionScale;
                var skin = child.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = saved; skin.sharedMaterials = source.sharedMaterials; skin.bones = Array.Empty<Transform>(); skin.rootBone = null; skin.updateWhenOffscreen = true; skin.localBounds = saved.bounds;
                skin.shadowCastingMode = source.shadowCastingMode; skin.receiveShadows = source.receiveShadows; skin.lightProbeUsage = source.lightProbeUsage; skin.reflectionProbeUsage = source.reflectionProbeUsage; skin.probeAnchor = source.probeAnchor; skin.motionVectorGenerationMode = source.motionVectorGenerationMode;
                if (Enumerable.Range(0, 16).Any(i => source.localToWorldMatrix[i] != originalMatrix[i])) throw new InvalidDataException("Restoration changed imported parent TRS.");
                audit.parentTransformUnchanged = true; source.enabled = false; source.name = row.@object + "_RawImportedDisabled"; skin.SetBlendShapeWeight(0, 0); return skin;
            }
            catch { if (clone && !EditorUtility.IsPersistent(clone)) Object.DestroyImmediate(clone); throw; }
        }
        static void AssertPreservedChannels(Mesh before, Mesh after)
        {
            if (before.vertexCount != after.vertexCount || before.subMeshCount != after.subMeshCount || before.indexFormat != after.indexFormat || !before.normals.SequenceEqual(after.normals) || !before.tangents.SequenceEqual(after.tangents) || !before.colors.SequenceEqual(after.colors) || !before.colors32.SequenceEqual(after.colors32) || !before.bindposes.SequenceEqual(after.bindposes)) throw new InvalidDataException("Private position clone changed topology or base normal/tangent/color data.");
            var a = before.GetVertexAttributes(); var b = after.GetVertexAttributes();
            if (a.Length != b.Length || Enumerable.Range(0, a.Length).Any(i => a[i].attribute != b[i].attribute || a[i].dimension != b[i].dimension || a[i].format != b[i].format || a[i].stream != b[i].stream)) throw new InvalidDataException("Private position clone changed a vertex channel descriptor.");
            for (int s = 0; s < before.subMeshCount; s++) if (before.GetTopology(s) != after.GetTopology(s) || before.GetBaseVertex(s) != after.GetBaseVertex(s) || !before.GetIndices(s, false).SequenceEqual(after.GetIndices(s, false))) throw new InvalidDataException("Private position clone changed ordered submesh indices.");
            for (int channel = 0; channel < 8; channel++) { var u = new List<Vector4>(); var v = new List<Vector4>(); before.GetUVs(channel, u); after.GetUVs(channel, v); if (!u.SequenceEqual(v)) throw new InvalidDataException("Private position clone changed UV channel " + channel); }
        }
        static Mesh Mesh(Renderer r) { var skin = r as SkinnedMeshRenderer; var filter = r.GetComponent<MeshFilter>(); var mesh = skin ? skin.sharedMesh : filter ? filter.sharedMesh : null; if (!mesh) throw new InvalidDataException("Renderer mesh absent: " + r.name); return mesh; }
        static Corner[] ReadCorners(byte[] bytes, int count) { if (bytes.Length != count * 16 * 4) throw new InvalidDataException("Canonical corner size mismatch."); var result = new Corner[count]; using (var r = new BinaryReader(new MemoryStream(bytes))) for (int i = 0; i < count; i++) result[i] = new Corner { position = V3(r), uv = new Vector2(r.ReadSingle(), r.ReadSingle()), open = C4(r), closed = C4(r), delta = V3(r) }; return result; }
        static Corner[] ReadEndpoints(byte[] bytes, int count) { if (bytes.Length != count * 6 * 4) throw new InvalidDataException("Canonical endpoint size mismatch."); var result = new Corner[count]; using (var r = new BinaryReader(new MemoryStream(bytes))) for (int i = 0; i < count; i++) result[i] = new Corner { position = V3(r), delta = V3(r) }; return result; }
        static Vector3 V3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        static Color C4(BinaryReader r) => new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        static float ColorError(Color a, Color b) => Enumerable.Range(0, 4).Max(i => Mathf.Abs(a[i] - b[i]));
        static Matrix4x4 Matrix(float[] values) { var m = Matrix4x4.identity; for (int i = 0; i < 16; i++) m[i / 4, i % 4] = values[i]; return m; }
        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        static Matrix4x4 MarkerMatrix(Transform root) { var origin = Find(root, "Ren_H_NativeAxis_Origin"); var m = Matrix4x4.identity; for (int i = 0; i < 3; i++) m.SetColumn(i, (Vector4)((Find(root, "Ren_H_NativeAxis_" + "XYZ"[i]).position - origin.position) / .01f)); m.SetColumn(3, new Vector4(origin.position.x, origin.position.y, origin.position.z, 1)); return m; }
        static float MarkerError(Transform root, Matrix4x4 matrix) { float error = 0; foreach (var p in new[] { ("Origin", Vector3.zero), ("X", Vector3.right * .01f), ("Y", Vector3.up * .01f), ("Z", Vector3.forward * .01f) }) error = Mathf.Max(error, Vector3.Distance(Find(root, "Ren_H_NativeAxis_" + p.Item1).position, matrix.MultiplyPoint3x4(p.Item2))); return error; }
        static void SetMatrix(Transform t, Matrix4x4 m) { var x = (Vector3)m.GetColumn(0); var y = (Vector3)m.GetColumn(1); var z = (Vector3)m.GetColumn(2); float sx = x.magnitude; if (Vector3.Dot(Vector3.Cross(x, y), z) < 0) sx = -sx; t.localPosition = m.GetColumn(3); t.localRotation = Quaternion.LookRotation(z, y); t.localScale = new Vector3(sx, y.magnitude, z.magnitude); var actual = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale); if (Enumerable.Range(0, 16).Any(i => Mathf.Abs(actual[i] - m[i]) > 1e-4f)) throw new InvalidDataException("Unsupported blink registration shear."); }
        static string Hash(string path) { using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        static void Verify(string path, string expected) { if (Hash(path) != expected) throw new InvalidDataException("Frozen blink source hash mismatch: " + path); }
    }
}

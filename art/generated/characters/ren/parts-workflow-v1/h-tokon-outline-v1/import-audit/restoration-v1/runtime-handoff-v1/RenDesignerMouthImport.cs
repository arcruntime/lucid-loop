using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LucidLoop.CharacterArt.Editor
{
    // Explicit scratch-scene binding only. Never imports, edits or saves a source asset.
    public static class RenDesignerMouthImport
    {
        const string Root = "Assets/CharacterArt/Generated/RenDesignerMouthReview";
        const string HandoffHash = "9fa7b89b259e1305fe9d555f210110819bc5e5f1ec0f6c4c695c407a1fd09b86";
        const float PositionTolerance = 2e-6f, UvTolerance = 1e-6f, AttributeTolerance = 2e-6f;
        static readonly string[] Names = { "Ren_H_Head", "Ren_H_InnerLipWall", "Ren_H_OralCavity", "Ren_H_Tongue" };
        public static string AuditPath { get; private set; }

        [Serializable] sealed class Identity { public string guid; public long fileID; public int type; }
        [Serializable] sealed class Shape { public string name, deltaSha256; public float frameWeight; }
        [Serializable] sealed class Spec
        {
            public string name, currentRendererPath, verifiedMeshAsset, verifiedMeshSha256, newChildName, savedBaseDataSha256;
            public Identity currentMeshIdentity; public Identity[] currentMaterialSlots;
            public int vertices, triangles; public float[] newChildLocalPosition, newChildLocalRotationQuaternion, newChildLocalScale, defaultWeights;
            public Shape[] orderedShapes;
        }
        [Serializable] sealed class Handoff
        {
            public string sourceFbx, sourceSha256, currentBindingSnapshot, currentBindingSnapshotSha256, persistenceReport, persistenceReportSha256;
            public float coordinateScale; public Spec[] meshes;
        }
        [Serializable] sealed class Binding
        {
            public string name, path; public Identity mesh; public Identity[] materials;
            public Vector3 localPosition, localScale; public Quaternion localRotation; public float[] weights; public string[] bones;
        }
        [Serializable] sealed class Snapshot { public string sourceFbx, sourceSha256; public bool sourceTopologyUnmodified, newEyesSupportPatchSeparate; public Binding[] rendererBindings; }
        [Serializable] sealed class PersistFrame { public string shape, positionDeltaSha256, preservedNormalDeltaSha256, preservedTangentDeltaSha256; public float frameWeight; }
        [Serializable] sealed class PersistMesh { public string name, asset, assetSha256, baseDataSha256; public int vertices, triangles; public PersistFrame[] frames; }
        [Serializable] sealed class Persistence { public string status; public float coordinateScale; public PersistMesh[] meshes; }
        [Serializable] sealed class MaterialRow
        {
            public int slot, instanceId; public string name, asset, assetSha256, serializedSha256, shaderName;
            public string guid; public long fileID; public bool matchesEarlierSnapshot;
        }
        [Serializable] sealed class MeshRow
        {
            public string name, sourcePath, sourceMeshGuid, savedMeshAsset, savedMeshSha256, childPath;
            public long sourceMeshFileID;
            public int sourceVertices, savedVertices, triangles, subMeshes, comparedCorners, cyclicallyRotatedTriangles;
            public float maximumBasisErrorNative, maximumUvError, maximumNormalError, maximumTangentError, maximumActualChildBasisErrorNative;
            public float[] originalWeights, assignedWeights, sourceLocalToNative, childLocalToNative;
            public Vector3 sourceLocalPosition, sourceLocalScale, childLocalScale;
            public Quaternion sourceLocalRotation;
            public MaterialRow[] materials;
        }
        [Serializable] sealed class Audit
        {
            public string status = "PREFLIGHT", sourceScene, unityVersion, handoffSha256, bindingSnapshotSha256, persistenceReportSha256, sourceFbxSha256, sourceMetaSha256;
            public string exception;
            public string limitation = "Four unbound scratch renderers only, preserved selected materials and attachments. GPU mixtures pending; no production/shared-rig or historical-eye promotion. The selected meshes retain99 tiny zeroed head rows below2e-6 native-unit tolerance.";
            public bool originalFilesUnchanged, existingTransformsUnchanged, unrelatedRendererReferencesAndStatesUnchanged, originalMaterialsUnchanged, faceFrameUnchanged;
            public float maximumFaceBoundsError;
            public int existingTransformCount, unrelatedRendererCount;
            public MeshRow[] meshes;
        }
        sealed class Work
        {
            public Spec Spec; public Binding Binding; public SkinnedMeshRenderer Source, Added;
            public Mesh SourceMesh, Saved; public Material[] Materials; public string[] MaterialHashes; public float[] Weights;
            public Matrix4x4 ToNative; public bool WasEnabled; public MeshRow Audit;
            public Vector3[] SourceVertices, SavedVertices;
        }
        sealed class TransformState
        {
            public Transform Node, Parent; public Vector3 Position, Scale; public Quaternion Rotation; public bool Active;
        }
        sealed class RendererState
        {
            public Renderer Renderer; public Mesh Mesh; public Material[] Materials; public bool Enabled;
            public int Layer; public ShadowCastingMode Cast; public bool Receive;
        }
        sealed class Corners
        {
            public Vector3[] Positions, Normals; public Vector4[] Tangents; public readonly List<Vector4[]> Uvs = new List<Vector4[]>();
            public Corners(Mesh mesh, Matrix4x4 toNative)
            {
                Positions = mesh.vertices.Select(toNative.MultiplyPoint3x4).ToArray(); Normals = mesh.normals; Tangents = mesh.tangents;
                for (var channel = 0; channel < 8; channel++) { var list = new List<Vector4>(); mesh.GetUVs(channel, list); Uvs.Add(list.ToArray()); }
            }
        }

        public static SkinnedMeshRenderer[] Add(RenDesignerEyeReview review, string handoffPath)
        {
            if (!Application.isBatchMode || Application.isPlaying || !Application.dataPath.Replace('\\', '/').EndsWith("LucidLoopScratch/ren-eye-import-verification/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Mouth binding is restricted to the recorded scratch batch Editor, outside Play mode.");
            if (!review || !review.ModelRoot || !review.Head || !review.FaceFrame) throw new InvalidDataException("Current designer review references are incomplete.");
            Directory.CreateDirectory(Root);
            AuditPath = Root + "/ImportAudit-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json";
            var audit = new Audit { sourceScene = review.gameObject.scene.path, unityVersion = Application.unityVersion };
            var work = new List<Work>(); var created = new List<GameObject>(); var originalHead = review.Head; var faceFrame = review.FaceFrame;
            var transforms = review.ModelRoot.GetComponentsInChildren<Transform>(true).Select(t => new TransformState { Node = t, Parent = t.parent, Position = t.localPosition, Rotation = t.localRotation, Scale = t.localScale, Active = t.gameObject.activeSelf }).ToArray();
            var states = review.ModelRoot.GetComponentsInChildren<Renderer>(true).Select(r => new RendererState { Renderer = r, Mesh = MeshOf(r), Materials = r.sharedMaterials, Enabled = r.enabled, Layer = r.gameObject.layer, Cast = r.shadowCastingMode, Receive = r.receiveShadows }).ToArray();
            var oldFaceBounds = RenNprReviewController.CalculateFaceLocalBounds(faceFrame, originalHead);
            string sourceAsset = null, sourceHash = null, metaHash = null;
            try
            {
                Verify(handoffPath, HandoffHash); audit.handoffSha256 = HandoffHash;
                var root = Path.GetDirectoryName(Path.GetFullPath(handoffPath));
                var handoff = JsonUtility.FromJson<Handoff>(File.ReadAllText(handoffPath));
                if (handoff.coordinateScale != 10000f || handoff.meshes == null || !handoff.meshes.Select(m => m.name).SequenceEqual(Names)) throw new InvalidDataException("Exactly the four ordered head/oral meshes are required.");
                var bindingPath = Path.GetFullPath(Path.Combine(root, handoff.currentBindingSnapshot)); Verify(bindingPath, handoff.currentBindingSnapshotSha256);
                var snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(bindingPath)); audit.bindingSnapshotSha256 = handoff.currentBindingSnapshotSha256;
                if (!snapshot.sourceTopologyUnmodified || !snapshot.newEyesSupportPatchSeparate || snapshot.sourceSha256 != handoff.sourceSha256 || snapshot.sourceFbx != handoff.sourceFbx)
                    throw new InvalidDataException("Current-source compatibility snapshot does not support this swap.");
                var persistencePath = Path.GetFullPath(Path.Combine(root, handoff.persistenceReport)); Verify(persistencePath, handoff.persistenceReportSha256);
                var persisted = JsonUtility.FromJson<Persistence>(File.ReadAllText(persistencePath)); audit.persistenceReportSha256 = handoff.persistenceReportSha256;
                if (persisted.coordinateScale != 10000f || persisted.status != "CANONICAL_POSITIONS_WITHIN_TOLERANCE_SAVE_RELOAD_CPU_CHECKS_PASS_NORMALS_UNVERIFIED") throw new InvalidDataException("Saved CPU-verified prerequisite is missing.");
                sourceAsset = handoff.sourceFbx; Verify(sourceAsset, handoff.sourceSha256); sourceHash = handoff.sourceSha256; metaHash = HashFile(sourceAsset + ".meta");
                audit.sourceFbxSha256 = sourceHash; audit.sourceMetaSha256 = metaHash;
                var nativeToWorld = NativeToWorld(review.ModelRoot); var worldToNative = nativeToWorld.inverse;
                foreach (var spec in handoff.meshes)
                {
                    var binding = snapshot.rendererBindings.Single(b => b.name == spec.name);
                    if (binding.path != spec.currentRendererPath || !SameId(binding.mesh, spec.currentMeshIdentity)) throw new InvalidDataException("Binding identity mismatch: " + spec.name);
                    var node = review.ModelRoot.GetComponentsInChildren<Transform>(true).Single(t => ScenePath(t) == spec.currentRendererPath);
                    if (node.name != spec.name || node.Find(spec.newChildName)) throw new InvalidDataException("Wrong source node or a prior binding already exists: " + spec.name);
                    var renderer = node.GetComponent<SkinnedMeshRenderer>();
                    if (!renderer || node.GetComponents<Renderer>().Length != 1 || !renderer.sharedMesh || !renderer.enabled) throw new InvalidDataException("Expected one enabled current morph renderer: " + spec.name);
                    var mesh = renderer.sharedMesh;
                    if (AssetDatabase.GetAssetPath(mesh) != sourceAsset || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long fileId) || guid != spec.currentMeshIdentity.guid || fileId != spec.currentMeshIdentity.fileID)
                        throw new InvalidDataException("Current mesh is not the frozen FBX subasset: " + spec.name);
                    RequireUnbound(renderer, mesh); RequireLocalTrs(node, binding);
                    if (renderer.sharedMaterials.Length != spec.currentMaterialSlots.Length || renderer.sharedMaterials.Length != mesh.subMeshCount || renderer.sharedMaterials.Any(m => !m || !m.shader)) throw new InvalidDataException("Current selected material slots are incomplete: " + spec.name);
                    Verify(spec.verifiedMeshAsset, spec.verifiedMeshSha256);
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(spec.verifiedMeshAsset);
                    if (!saved || !saved.isReadable || saved.vertexCount != spec.vertices || TriangleCount(saved) != spec.triangles || saved.subMeshCount != mesh.subMeshCount) throw new InvalidDataException("Saved mesh topology/count mismatch: " + spec.name);
                    RequireUnbound(null, saved); RequireShapes(mesh, spec.orderedShapes, false); RequireShapes(saved, spec.orderedShapes, true);
                    var proof = persisted.meshes.Single(m => m.name == spec.name);
                    if (proof.asset != spec.verifiedMeshAsset || proof.assetSha256 != spec.verifiedMeshSha256 || proof.baseDataSha256 != spec.savedBaseDataSha256) throw new InvalidDataException("Saved mesh persistence identity changed.");
                    VerifyFrameNormals(saved, proof.frames);
                    var weights = Enumerable.Range(0, mesh.blendShapeCount).Select(renderer.GetBlendShapeWeight).ToArray();
                    if (!weights.SequenceEqual(binding.weights) || !weights.SequenceEqual(spec.defaultWeights)) throw new InvalidDataException("This staged test expects the recorded closed-rest source weights: " + spec.name);
                    if (!spec.newChildLocalPosition.SequenceEqual(new[] { 0f, 0f, 0f }) || !spec.newChildLocalRotationQuaternion.SequenceEqual(new[] { 0f, 0f, 0f, 1f }) || !spec.newChildLocalScale.SequenceEqual(new[] { .0001f, .0001f, .0001f })) throw new InvalidDataException("Unapproved child compensation.");
                    var materials = renderer.sharedMaterials; var toNative = worldToNative * node.localToWorldMatrix;
                    var row = new MeshRow { name = spec.name, sourcePath = spec.currentRendererPath, sourceMeshGuid = guid, sourceMeshFileID = fileId, savedMeshAsset = spec.verifiedMeshAsset, savedMeshSha256 = spec.verifiedMeshSha256,
                        sourceVertices = mesh.vertexCount, savedVertices = saved.vertexCount, triangles = spec.triangles, subMeshes = mesh.subMeshCount, sourceLocalPosition = node.localPosition, sourceLocalRotation = node.localRotation, sourceLocalScale = node.localScale,
                        originalWeights = weights, sourceLocalToNative = ArrayOf(toNative), materials = materials.Select((m, i) => MaterialAudit(m, i, spec.currentMaterialSlots[i])).ToArray() };
                    CompareCorners(mesh, saved, toNative, toNative * Matrix4x4.Scale(Vector3.one * .0001f), row);
                    work.Add(new Work { Spec = spec, Binding = binding, Source = renderer, SourceMesh = mesh, Saved = saved, Materials = materials, MaterialHashes = materials.Select(MaterialHash).ToArray(), Weights = weights,
                        WasEnabled = renderer.enabled, ToNative = toNative, Audit = row, SourceVertices = mesh.vertices, SavedVertices = saved.vertices });
                }
                if (work[0].Source != originalHead) throw new InvalidDataException("The current review head does not match the explicit source binding.");
                audit.meshes = work.Select(w => w.Audit).ToArray();
                // Every source, saved mesh and rendered corner passes before scene binding.
                foreach (var w in work)
                {
                    var child = new GameObject(w.Spec.newChildName); created.Add(child); child.layer = w.Source.gameObject.layer; child.tag = w.Source.gameObject.tag;
                    child.transform.SetParent(w.Source.transform, false); child.transform.localPosition = Vector3.zero; child.transform.localRotation = Quaternion.identity; child.transform.localScale = Vector3.one * .0001f;
                    var added = child.AddComponent<SkinnedMeshRenderer>(); w.Added = added; added.enabled = false; added.sharedMesh = w.Saved; added.sharedMaterials = w.Materials;
                    added.bones = Array.Empty<Transform>(); added.rootBone = null; added.updateWhenOffscreen = true; added.quality = w.Source.quality;
                    var scaledBounds = new Bounds(w.Source.localBounds.center * 10000f, w.Source.localBounds.size * 10000f); scaledBounds.Encapsulate(w.Saved.bounds.min); scaledBounds.Encapsulate(w.Saved.bounds.max); added.localBounds = scaledBounds;
                    CopyRenderSettings(w.Source, added);
                    for (var i = 0; i < w.Weights.Length; i++) added.SetBlendShapeWeight(i, w.Weights[i]);
                    var actualToNative = worldToNative * child.transform.localToWorldMatrix;
                    var expectedToNative = w.ToNative * Matrix4x4.Scale(Vector3.one * .0001f);
                    var maxError = 0f;
                    foreach (var v in w.SavedVertices) maxError = Mathf.Max(maxError, Vector3.Distance(actualToNative.MultiplyPoint3x4(v), expectedToNative.MultiplyPoint3x4(v)));
                    if (maxError > PositionTolerance) throw new InvalidDataException("Actual child transform does not preserve native Basis: " + w.Spec.name + "/" + maxError.ToString("R"));
                    if (!added.sharedMaterials.SequenceEqual(w.Materials)) throw new InvalidDataException("Selected material references changed during binding.");
                    w.Audit.childPath = ScenePath(child.transform); w.Audit.childLocalScale = child.transform.localScale; w.Audit.childLocalToNative = ArrayOf(actualToNative); w.Audit.maximumActualChildBasisErrorNative = maxError;
                    w.Audit.assignedWeights = Enumerable.Range(0, w.Saved.blendShapeCount).Select(added.GetBlendShapeWeight).ToArray();
                }
                foreach (var w in work) { w.Source.enabled = false; w.Added.enabled = w.WasEnabled; }
                review.Head = work[0].Added;
                var newFaceBounds = RenNprReviewController.CalculateFaceLocalBounds(faceFrame, review.Head);
                audit.maximumFaceBoundsError = Mathf.Max(Vector3.Distance(oldFaceBounds.min, newFaceBounds.min), Vector3.Distance(oldFaceBounds.max, newFaceBounds.max));
                if (audit.maximumFaceBoundsError > PositionTolerance || review.FaceFrame != faceFrame) throw new InvalidDataException("Face-frame lighting calibration changed.");
                foreach (var t in transforms) if (!t.Node || t.Node.parent != t.Parent || !Exact(t.Node.localPosition, t.Position) || !Exact(t.Node.localScale, t.Scale) || !Exact(t.Node.localRotation, t.Rotation) || t.Node.gameObject.activeSelf != t.Active) throw new InvalidDataException("An existing transform or active state changed.");
                foreach (var state in states)
                {
                    var expectedEnabled = work.Any(w => w.Source == state.Renderer) ? false : state.Enabled;
                    if (!state.Renderer || state.Renderer.enabled != expectedEnabled || MeshOf(state.Renderer) != state.Mesh || !state.Renderer.sharedMaterials.SequenceEqual(state.Materials) || state.Renderer.gameObject.layer != state.Layer || state.Renderer.shadowCastingMode != state.Cast || state.Renderer.receiveShadows != state.Receive)
                        throw new InvalidDataException("An existing renderer changed beyond the four enabled flags.");
                }
                foreach (var w in work)
                {
                    if (!w.Materials.Select(MaterialHash).SequenceEqual(w.MaterialHashes) || !Enumerable.Range(0, w.SourceMesh.blendShapeCount).Select(w.Source.GetBlendShapeWeight).SequenceEqual(w.Weights)) throw new InvalidDataException("Original material values or source pose changed.");
                    Verify(w.Spec.verifiedMeshAsset, w.Spec.verifiedMeshSha256);
                }
                Verify(sourceAsset, sourceHash); Verify(sourceAsset + ".meta", metaHash);
                audit.originalFilesUnchanged = audit.existingTransformsUnchanged = audit.unrelatedRendererReferencesAndStatesUnchanged = audit.originalMaterialsUnchanged = audit.faceFrameUnchanged = true;
                audit.existingTransformCount = transforms.Length; audit.unrelatedRendererCount = states.Length - 4; audit.status = "PASS_FOUR_VERIFIED_MESH_BINDINGS_CURRENT_ATTACHMENTS_PRESERVED_GPU_PENDING";
                // A failed success-record write is still a failed transaction: roll back below.
                File.WriteAllText(AuditPath, JsonUtility.ToJson(audit, true));
                Debug.Log("REN_DESIGNER_MOUTH_IMPORT_AUDIT: " + AuditPath + " " + audit.status);
                return work.Select(w => w.Added).ToArray();
            }
            catch (Exception error)
            {
                review.Head = originalHead; foreach (var w in work) if (w.Source) w.Source.enabled = w.WasEnabled;
                foreach (var child in created) if (child) Object.DestroyImmediate(child);
                audit.status = "FAIL_BINDING_ROLLED_BACK"; audit.exception = error.ToString(); audit.meshes = work.Select(w => w.Audit).ToArray();
                // Logging must not replace the original exception or prevent rollback.
                try { File.WriteAllText(AuditPath, JsonUtility.ToJson(audit, true)); Debug.Log("REN_DESIGNER_MOUTH_IMPORT_AUDIT: " + AuditPath + " " + audit.status); }
                catch (Exception loggingError) { try { Debug.LogWarning("Mouth binding rolled back; failure audit unavailable: " + loggingError.Message); } catch { } }
                throw;
            }
        }

        static void CompareCorners(Mesh oldMesh, Mesh saved, Matrix4x4 oldToNative, Matrix4x4 savedToNative, MeshRow audit)
        {
            var old = new Corners(oldMesh, oldToNative); var fresh = new Corners(saved, savedToNative);
            if ((old.Normals.Length == 0) != (fresh.Normals.Length == 0) || (old.Tangents.Length == 0) != (fresh.Tangents.Length == 0)) throw new InvalidDataException("Basis attribute presence differs.");
            for (var channel = 0; channel < 8; channel++) if ((old.Uvs[channel].Length == 0) != (fresh.Uvs[channel].Length == 0)) throw new InvalidDataException("UV channel presence differs.");
            for (var sub = 0; sub < oldMesh.subMeshCount; sub++)
            {
                if (oldMesh.GetTopology(sub) != MeshTopology.Triangles || saved.GetTopology(sub) != MeshTopology.Triangles) throw new InvalidDataException("Only triangle submeshes are supported.");
                var a = oldMesh.GetIndices(sub); var b = saved.GetIndices(sub); if (a.Length != b.Length || a.Length % 3 != 0) throw new InvalidDataException("Rendered submesh corner count differs.");
                for (var triangle = 0; triangle < a.Length; triangle += 3)
                {
                    var accepted = -1;
                    for (var rotation = 0; rotation < 3 && accepted < 0; rotation++)
                    {
                        var valid = true;
                        for (var c = 0; c < 3 && valid; c++) valid = CornerMatches(old, fresh, a[triangle + c], b[triangle + (c + rotation) % 3]);
                        if (valid) accepted = rotation;
                    }
                    if (accepted < 0) throw new InvalidDataException("Ordered rendered Basis/UV/normal/winding mismatch: " + audit.name + " submesh=" + sub + " triangle=" + triangle / 3 + " old=" + old.Positions[a[triangle]].ToString("R") + " saved=" + fresh.Positions[b[triangle]].ToString("R") + ". No nearest remap or reversed winding accepted.");
                    if (accepted != 0) audit.cyclicallyRotatedTriangles++;
                    for (var c = 0; c < 3; c++)
                    {
                        var i = a[triangle + c]; var j = b[triangle + (c + accepted) % 3]; audit.comparedCorners++;
                        audit.maximumBasisErrorNative = Mathf.Max(audit.maximumBasisErrorNative, Vector3.Distance(old.Positions[i], fresh.Positions[j]));
                        for (var uv = 0; uv < 8; uv++) if (old.Uvs[uv].Length != 0) audit.maximumUvError = Mathf.Max(audit.maximumUvError, MaxDifference(old.Uvs[uv][i], fresh.Uvs[uv][j]));
                        if (old.Normals.Length != 0) audit.maximumNormalError = Mathf.Max(audit.maximumNormalError, Vector3.Distance(old.Normals[i], fresh.Normals[j]));
                        if (old.Tangents.Length != 0) audit.maximumTangentError = Mathf.Max(audit.maximumTangentError, MaxDifference(old.Tangents[i], fresh.Tangents[j]));
                    }
                }
            }
        }
        static bool CornerMatches(Corners a, Corners b, int i, int j)
        {
            if (Vector3.Distance(a.Positions[i], b.Positions[j]) > PositionTolerance) return false;
            for (var uv = 0; uv < 8; uv++) if (a.Uvs[uv].Length != 0 && MaxDifference(a.Uvs[uv][i], b.Uvs[uv][j]) > UvTolerance) return false;
            return (a.Normals.Length == 0 || Vector3.Distance(a.Normals[i], b.Normals[j]) <= AttributeTolerance) && (a.Tangents.Length == 0 || MaxDifference(a.Tangents[i], b.Tangents[j]) <= AttributeTolerance);
        }
        static void RequireUnbound(SkinnedMeshRenderer renderer, Mesh mesh)
        {
            if (mesh.bindposes.Length != 0 || mesh.GetAllBoneWeights().Length != 0 || (renderer && (renderer.bones.Length != 0 || renderer.rootBone))) throw new InvalidDataException("Shared-rig/bound meshes are outside this diagnostic.");
        }
        static void RequireShapes(Mesh mesh, Shape[] shapes, bool verifyDeltas)
        {
            if (mesh.blendShapeCount != shapes.Length) throw new InvalidDataException("Shape count differs.");
            for (var i = 0; i < shapes.Length; i++)
            {
                if (mesh.GetBlendShapeName(i) != shapes[i].name || mesh.GetBlendShapeFrameCount(i) != 1 || mesh.GetBlendShapeFrameWeight(i, 0) != shapes[i].frameWeight) throw new InvalidDataException("Ordered shape name/frame weight differs.");
                if (verifyDeltas) { var dp = new Vector3[mesh.vertexCount]; mesh.GetBlendShapeFrameVertices(i, 0, dp, null, null); if (HashVectors(dp) != shapes[i].deltaSha256) throw new InvalidDataException("Saved position delta bytes changed."); }
            }
        }
        static void VerifyFrameNormals(Mesh mesh, PersistFrame[] frames)
        {
            if (frames.Length != mesh.blendShapeCount) throw new InvalidDataException("Persistence frame count differs.");
            for (var i = 0; i < frames.Length; i++)
            {
                var dp = new Vector3[mesh.vertexCount]; var dn = new Vector3[dp.Length]; var dt = new Vector3[dp.Length]; mesh.GetBlendShapeFrameVertices(i, 0, dp, dn, dt);
                if (mesh.GetBlendShapeName(i) != frames[i].shape || HashVectors(dp) != frames[i].positionDeltaSha256 || HashVectors(dn) != frames[i].preservedNormalDeltaSha256 || HashVectors(dt) != frames[i].preservedTangentDeltaSha256) throw new InvalidDataException("Saved normal/tangent/position frame differs from persistence proof.");
            }
        }
        static void RequireLocalTrs(Transform node, Binding expected)
        {
            if (Vector3.Distance(node.localPosition, expected.localPosition) > 1e-9f || Vector3.Distance(node.localScale, expected.localScale) > 1e-6f || MaxDifference(new Vector4(node.localRotation.x, node.localRotation.y, node.localRotation.z, node.localRotation.w), new Vector4(expected.localRotation.x, expected.localRotation.y, expected.localRotation.z, expected.localRotation.w)) > 2e-7f) throw new InvalidDataException("Original source-node local TRS changed: " + node.name);
        }
        static void CopyRenderSettings(Renderer source, Renderer target)
        {
            target.shadowCastingMode = source.shadowCastingMode; target.receiveShadows = source.receiveShadows; target.renderingLayerMask = source.renderingLayerMask;
            target.lightProbeUsage = source.lightProbeUsage; target.reflectionProbeUsage = source.reflectionProbeUsage; target.probeAnchor = source.probeAnchor; target.lightProbeProxyVolumeOverride = source.lightProbeProxyVolumeOverride;
            target.motionVectorGenerationMode = source.motionVectorGenerationMode; target.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            target.sortingLayerID = source.sortingLayerID; target.sortingOrder = source.sortingOrder; target.lightmapIndex = source.lightmapIndex; target.realtimeLightmapIndex = source.realtimeLightmapIndex;
            target.lightmapScaleOffset = source.lightmapScaleOffset; target.realtimeLightmapScaleOffset = source.realtimeLightmapScaleOffset;
            var block = new MaterialPropertyBlock(); source.GetPropertyBlock(block); target.SetPropertyBlock(block);
            for (var i = 0; i < source.sharedMaterials.Length; i++) { block.Clear(); source.GetPropertyBlock(block, i); target.SetPropertyBlock(block, i); }
        }
        static MaterialRow MaterialAudit(Material m, int slot, Identity earlier)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out string guid, out long id); var path = AssetDatabase.GetAssetPath(m);
            return new MaterialRow { slot = slot, instanceId = m.GetInstanceID(), name = m.name, asset = path, assetSha256 = File.Exists(path) ? HashFile(path) : null, serializedSha256 = MaterialHash(m), shaderName = m.shader.name,
                guid = guid, fileID = id, matchesEarlierSnapshot = guid == earlier.guid && id == earlier.fileID };
        }
        static Matrix4x4 NativeToWorld(Transform model)
        {
            var source = model.Find("Source"); if (!source) throw new InvalidDataException("Current H Source frame is absent.");
            var all = source.GetComponentsInChildren<Transform>(true); var head = all.Single(t => t.name == "Head"); var matrix = Matrix4x4.identity;
            for (var i = 0; i < 3; i++) matrix.SetColumn(i, (Vector4)(all.Single(t => t.name == "Ren_H_HeadAxis_" + "XYZ"[i]).position - head.position));
            matrix.SetColumn(3, new Vector4(head.position.x, head.position.y, head.position.z, 1)); matrix *= Matrix4x4.Translate(new Vector3(.05999999865889549f, 0, .20000000298023224f));
            if (Mathf.Abs(matrix.determinant) < 1e-8f) throw new InvalidDataException("Native marker frame is singular."); return matrix;
        }
        static Mesh MeshOf(Renderer r) => r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
        static string ScenePath(Transform t) { var names = new List<string>(); for (; t; t = t.parent) names.Add(t.name); names.Reverse(); return string.Join("/", names); }
        static int TriangleCount(Mesh m) => Enumerable.Range(0, m.subMeshCount).Sum(s => (int)m.GetIndexCount(s)) / 3;
        static float[] ArrayOf(Matrix4x4 m) => Enumerable.Range(0, 16).Select(i => m[i / 4, i % 4]).ToArray();
        static bool SameId(Identity a, Identity b) => a.guid == b.guid && a.fileID == b.fileID;
        static bool Exact(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
        static bool Exact(Quaternion a, Quaternion b) => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
        static float MaxDifference(Vector4 a, Vector4 b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y), Mathf.Abs(a.z - b.z), Mathf.Abs(a.w - b.w));
        static string MaterialHash(Material m) => HashBytes(System.Text.Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(m)));
        static string HashVectors(Vector3[] a) { using (var s = new MemoryStream()) using (var w = new BinaryWriter(s)) { foreach (var v in a) { w.Write(v.x); w.Write(v.y); w.Write(v.z); } return HashBytes(s.ToArray()); } }
        static string HashBytes(byte[] data) { using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(data)).Replace("-", "").ToLowerInvariant(); }
        static string HashFile(string path) { using (var s = File.OpenRead(path)) using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(s)).Replace("-", "").ToLowerInvariant(); }
        static void Verify(string path, string hash) { if (HashFile(path) != hash) throw new InvalidDataException("Frozen file hash changed: " + path); }
    }
}

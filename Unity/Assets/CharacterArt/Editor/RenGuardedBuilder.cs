using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LucidLoop.CharacterArt.Editor
{
    public static class RenGuardedBuilder
    {
        const string Root = "Assets/CharacterArt/Generated/RenLOD0";
        const string Neutral = Root + "/Models/RenGuardedNeutral.fbx";
        const string Acting = Root + "/Models/RenGuardedActing.fbx";
        const string IdleSource = "Assets/CharacterArt/Generated/RenBodyV3/Animations/RenObserverIdleFemaleV3.anim";
        const string IdlePath = Root + "/Animations/RenObserverIdleCorrectedDigits.anim";
        const string PosePath = Root + "/Animations/RenGuardedPose.asset";

        [MenuItem("Lucid Loop/Ren LOD0/Install guarded acting")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Exit Play and save the scene first");
            Directory.CreateDirectory(Root + "/Animations");
            string source = Path.GetFullPath("../art/generated/characters/ren/guarded-final-v1");
            File.Copy(source + "/RenGuarded_WeightAndDigitCorrection.fbx", Neutral, true);
            File.Copy(source + "/RenGuarded_Animation.fbx", Acting, true);
            AssetDatabase.Refresh();
            foreach (var path in new[] { Neutral, Acting })
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.importAnimation = path == Acting;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.importBlendShapes = true;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importBlendShapeNormals = ModelImporterNormals.Calculate;
                importer.importCameras = importer.importLights = false;
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            const string prefabPath = Root + "/Prefabs/RenLOD0.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try { Install(prefab.GetComponent<RenLOD0Controller>(), true); PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var scene = EditorSceneManager.OpenScene(RenLOD0Builder.ScenePath);
            Install(UnityEngine.Object.FindFirstObjectByType<RenLOD0Controller>(), false);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("REN_GUARDED_INSTALLED");
        }

        static void Install(RenLOD0Controller controller, bool writeAssets)
        {
            var donor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Neutral), controller.transform);
            var acting = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Acting), controller.transform);
            try
            {
                foreach (var model in new[] { donor, acting })
                {
                    model.transform.localPosition = controller.LOD0.transform.localPosition;
                    model.transform.localRotation = controller.LOD0.transform.localRotation;
                    model.transform.localScale = controller.LOD0.transform.localScale;
                    foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                }
                var targetBones = controller.LOD0.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                var donorBones = donor.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                var sourceBody = donor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "RenBody_LOD0");
                var body = controller.LOD0.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "RenBody_LOD0");
                if (body.sharedMesh.vertexCount != sourceBody.sharedMesh.vertexCount || !body.sharedMesh.triangles.SequenceEqual(sourceBody.sharedMesh.triangles))
                    throw new InvalidOperationException("Guarded body topology changed during import");
                float maximum = body.sharedMesh.vertices.Zip(sourceBody.sharedMesh.vertices, (a,b) => (a-b).magnitude).Max();
                if (maximum > .00001f) throw new InvalidOperationException("Guarded neutral body differs: " + maximum);
                var idle = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleSource));
                var bindings = AnimationUtility.GetCurveBindings(idle);
                foreach (var pair in donorBones.Where(p => p.Key.StartsWith("LeftHand") && p.Key != "LeftHand"))
                {
                    var target = targetBones[pair.Key]; var rest = pair.Value;
                    // Original FBX defines the old clip's rest, even on a repeat install.
                    var oldModel = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/Ren_LOD0.fbx");
                    var old = oldModel.GetComponentsInChildren<Transform>(true).Single(t => t.name == pair.Key);
                    var path = AnimationUtility.CalculateTransformPath(target, controller.BodyIdleRoot.transform);
                    var rotation = new AnimationCurve[4]; var position = new AnimationCurve[3];
                    for (int i = 0; i < 4; i++) rotation[i] = AnimationUtility.GetEditorCurve(idle, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[i]));
                    for (int i = 0; i < 3; i++) position[i] = AnimationUtility.GetEditorCurve(idle, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition." + "xyz"[i]));
                    if (rotation.Any(c => c == null) || position.Any(c => c == null)) throw new InvalidOperationException("Missing finger idle tracks: " + path);
                    // Resample on the clip's frame grid: imported position/rotation times
                    // differ by float roundoff and must not create near-duplicate keys.
                    var times = Enumerable.Range(0, Mathf.RoundToInt(idle.length * idle.frameRate) + 1).Select(i => i / idle.frameRate).ToArray();
                    var newRot = Enumerable.Range(0,4).Select(_ => new AnimationCurve()).ToArray();
                    var newPos = Enumerable.Range(0,3).Select(_ => new AnimationCurve()).ToArray();
                    Quaternion previous = rest.localRotation;
                    foreach (float time in times)
                    {
                        var q = rest.localRotation * Quaternion.Inverse(old.localRotation) * new Quaternion(rotation[0].Evaluate(time), rotation[1].Evaluate(time), rotation[2].Evaluate(time), rotation[3].Evaluate(time));
                        if (Quaternion.Dot(q, previous) < 0) q = new Quaternion(-q.x,-q.y,-q.z,-q.w);
                        previous = q;
                        var p = rest.localPosition + new Vector3(position[0].Evaluate(time),position[1].Evaluate(time),position[2].Evaluate(time)) - old.localPosition;
                        for (int i=0;i<4;i++) newRot[i].AddKey(time,q[i]);
                        for (int i=0;i<3;i++) newPos[i].AddKey(time,p[i]);
                    }
                    for (int i=0;i<4;i++) AnimationUtility.SetEditorCurve(idle,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[i]),newRot[i]);
                    for (int i=0;i<3;i++) AnimationUtility.SetEditorCurve(idle,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalPosition."+"xyz"[i]),newPos[i]);
                    target.localPosition=rest.localPosition;target.localRotation=rest.localRotation;target.localScale=rest.localScale;
                }
                body.sharedMesh=sourceBody.sharedMesh;
                body.bones=sourceBody.bones.Select(b=>targetBones[b.name]).ToArray();
                body.rootBone=targetBones[sourceBody.rootBone.name];
                var clip=AssetDatabase.LoadAllAssetsAtPath(Acting).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
                clip.SampleAnimation(acting,1.8f);
                var posed=acting.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                var pose=ScriptableObject.CreateInstance<RenGuardedPose>();
                pose.Bones=donorBones.Where(p=>targetBones.ContainsKey(p.Key)&&posed.ContainsKey(p.Key)&&
                    (Quaternion.Angle(p.Value.localRotation,posed[p.Key].localRotation)>.05f||(p.Value.localPosition-posed[p.Key].localPosition).magnitude>.00001f))
                    .Where(p=>p.Key.StartsWith("Left")||p.Key=="RightArm")
                    .Select(p=>new RenGuardedPose.Bone{Path=AnimationUtility.CalculateTransformPath(targetBones[p.Key],controller.BodyIdleRoot.transform),RestPosition=p.Value.localPosition,RestRotation=p.Value.localRotation,Position=posed[p.Key].localPosition,Rotation=posed[p.Key].localRotation}).ToArray();
                if(pose.Bones.Length<3)throw new InvalidOperationException("Guarded animation did not import expected arm motion");
                if(writeAssets){Save(idle,IdlePath);Save(pose,PosePath);}
                UnityEngine.Object.DestroyImmediate(idle);UnityEngine.Object.DestroyImmediate(pose);
                controller.BodyIdle=AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
                controller.GuardedPose=AssetDatabase.LoadAssetAtPath<RenGuardedPose>(PosePath);
                File.WriteAllText(Root+"/Evidence/guarded-import.txt",$"PASS: body topology and neutral retained (max error {maximum}); corrected 15 shared female left digits, retargeted idle, imported {controller.GuardedPose.Bones.Length} guarded pose bones.\n");
                EditorUtility.SetDirty(controller);
            }
            finally{UnityEngine.Object.DestroyImmediate(donor);UnityEngine.Object.DestroyImmediate(acting);}
        }
        static void Save(UnityEngine.Object source,string path)
        {
            var old=AssetDatabase.LoadMainAssetAtPath(path);
            if(old)EditorUtility.CopySerialized(source,old);
            else AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(source),path);
        }
    }
}

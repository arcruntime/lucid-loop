using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LucidLoop.CharacterArt.Editor
{
    public static class RenLOD1Builder
    {
        const string Root = "Assets/CharacterArt/Generated/RenLOD0";
        const string ModelPath = Root + "/Models/Ren_LOD1.fbx";
        const string PrefabPath = Root + "/Prefabs/RenLOD0.prefab";

        [MenuItem("Lucid Loop/Ren LOD0/Install LOD1")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Exit Play and save the scene first");
            File.Copy(Path.GetFullPath("../art/generated/characters/ren/lod1-final-v1/Ren_LOD1.fbx"), ModelPath, true);
            AssetDatabase.Refresh();
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.importBlendShapes = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importBlendShapeNormals = ModelImporterNormals.Calculate;
            importer.isReadable = true;
            importer.importCameras = importer.importLights = false;
            importer.SaveAndReimport();
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Install(prefab.GetComponent<RenLOD0Controller>());
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var scene = EditorSceneManager.OpenScene(RenLOD0Builder.ScenePath);
            Install(UnityEngine.Object.FindFirstObjectByType<RenLOD0Controller>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("REN_LOD1_INSTALLED");
        }

        static void Install(RenLOD0Controller controller)
        {
            if (controller.LOD1Cap) UnityEngine.Object.DestroyImmediate(controller.LOD1Cap);
            if (controller.LOD1Headphones) UnityEngine.Object.DestroyImmediate(controller.LOD1Headphones);
            if (controller.LOD1) UnityEngine.Object.DestroyImmediate(controller.LOD1);
            var originals = controller.LOD0.GetComponentsInChildren<Renderer>(true);
            var bones = controller.LOD0.GetComponentsInChildren<Transform>(true)
                .GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), controller.transform);
            // These renderer overrides are owned by the character prefab, not the vendor model.
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Ren_LOD1";
            model.transform.localPosition = controller.LOD0.transform.localPosition;
            model.transform.localRotation = controller.LOD0.transform.localRotation;
            model.transform.localScale = controller.LOD0.transform.localScale;
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
            var reduced = model.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in reduced)
            {
                var source = originals.Single(r => r.name == renderer.name);
                renderer.sharedMaterials = source.sharedMaterials;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    foreach (var bone in skin.bones)
                    {
                        if (!bone || !bones.TryGetValue(bone.name, out var target))
                            throw new InvalidOperationException("Missing LOD0 bone on " + renderer.name);
                        var relative = target.worldToLocalMatrix * bone.localToWorldMatrix;
                        for (int i = 0; i < 16; i++)
                            if (Mathf.Abs(relative[i] - Matrix4x4.identity[i]) > .0002f)
                                throw new InvalidOperationException("LOD rest transform differs: " + bone.name + " relative=" + relative.ToString("F6") + " source=" + bone.localToWorldMatrix.ToString("F6") + " target=" + target.localToWorldMatrix.ToString("F6"));
                    }
                    skin.bones = skin.bones.Select(b => bones[b.name]).ToArray();
                    if (skin.rootBone) skin.rootBone = bones[skin.rootBone.name];
                    skin.updateWhenOffscreen = false;
                }
                else if (renderer.name == "RenCap_Static" || renderer.name == "RenHeadphones_Static")
                    renderer.transform.SetParent(bones[renderer.name == "RenCap_Static" ? "Head" : "Spine"], true);
            }
            // Only the LOD0 skeleton is animated. Rigid accessories now attach to it too.
            var duplicateHips = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Hips");
            if (duplicateHips)
            {
                if (duplicateHips.GetComponentsInChildren<Renderer>(true).Length != 0)
                    throw new InvalidOperationException("Unmapped renderer under duplicate skeleton");
                UnityEngine.Object.DestroyImmediate(duplicateHips.gameObject);
            }
            long triangles = reduced.Sum(r => (long)(r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>().sharedMesh).triangles.Length / 3);
            if (triangles > 12000 || triangles < 10000) throw new InvalidOperationException("LOD1 outside 10–12k budget: " + triangles);
            var group = controller.GetComponent<LODGroup>();
            if (!group) group = controller.gameObject.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(new[] { new LOD(.27f, originals), new LOD(.01f, reduced) });
            group.RecalculateBounds();
            controller.LOD1 = model;
            controller.CharacterLODs = group;
            controller.LOD1Cap = reduced.Single(r => r.name == "RenCap_Static").gameObject;
            controller.LOD1Headphones = reduced.Single(r => r.name == "RenHeadphones_Static").gameObject;
            File.WriteAllText(Root + "/Evidence/lod1-import.txt", $"PASS: {triangles} LOD1 triangles; shared LOD0 bones with matching rest transforms; retained material instances; rigid cap/headphones remapped to Head/Spine.\n");
            EditorUtility.SetDirty(controller);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LucidLoop.Gyms.Editor
{
    public static class RenReviewMigrationCheck
    {
        const string ScenePath = "Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerMouthReview.unity";
        [MenuItem("Lucid Loop/Capture Ren main review")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode in the Ren review first.");
            foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (component.GetType().FullName != "LucidLoop.CharacterArt.RenDesignerMouthReview") continue;
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                component.GetType().GetMethod("Validate", flags).Invoke(component, null);
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.local/ren-main-review/main-render.png"));
                component.GetType().GetMethod("Render", flags).Invoke(component, new object[] { path });
                Debug.Log("Ren main-project render captured: " + path);
                return;
            }
            throw new InvalidOperationException("Ren mouth review component not found in the active scene.");
        }
        [Serializable] sealed class Report
        {
            public string scene;
            public string project;
            public int missingScripts;
            public int missingMeshes;
            public int missingMaterials;
            public int unsupportedShaders;
            public long activeRendererTriangles;
            public string[] problems;
        }

        [MenuItem("Lucid Loop/Review Ren in main project")]
        public static void OpenAndCheck()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before opening the review.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the current scene before opening the review.");
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Ren review migration has not supplied the scene yet.", ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var report = new Report { scene = ScenePath, project = Path.GetFullPath(".") };
            var problems = new List<string>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    report.missingScripts += missing;
                    if (missing > 0) problems.Add("Missing script: " + transform.name);
                }
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if ((renderer is SkinnedMeshRenderer || renderer is MeshRenderer) && mesh == null)
                    { report.missingMeshes++; problems.Add("Missing mesh: " + renderer.name); }
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null) { report.missingMaterials++; problems.Add("Missing material: " + renderer.name); }
                        else if (material.shader == null || !material.shader.isSupported)
                        { report.unsupportedShaders++; problems.Add("Unsupported shader: " + material.name); }
                    }
                    if (mesh != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                            if (mesh.GetTopology(sub) == MeshTopology.Triangles) report.activeRendererTriangles += mesh.GetIndexCount(sub) / 3;
                }
            }
            report.problems = problems.ToArray();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.local/ren-main-review"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "import-check.json"), JsonUtility.ToJson(report, true));
            Debug.Log("Ren main-project review import check: " + JsonUtility.ToJson(report));
            if (problems.Count > 0) throw new InvalidOperationException("Ren review has unresolved import problems; see .local/ren-main-review/import-check.json.");
            // Leave the requested scene open for the user's review. Counts include
            // active scene geometry, not only Ren, and do not include extra passes.
        }
    }
}

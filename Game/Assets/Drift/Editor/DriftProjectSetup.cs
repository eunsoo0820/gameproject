using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Drift.Editor
{
    public static class DriftProjectSetup
    {
        public const string ScenePath = "Assets/Drift/Scenes/Drift.unity";
        private static bool waitingForResources;

        [InitializeOnLoadMethod]
        private static void ScheduleInitialSetup()
        {
            // One-time creation only. Existing scenes are never regenerated during reload.
            if (!File.Exists(ScenePath)) EditorApplication.delayCall += EnsureCreated;
        }
        [MenuItem("Drift/Create or Open Game")]
        public static void CreateOrOpen()
        {
            EnsureCreated();
            if (File.Exists(ScenePath) && !EditorApplication.isPlaying && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }
        public static void EnsureCreated()
        {
            if (File.Exists(ScenePath) || EditorApplication.isPlayingOrWillChangePlaymode || waitingForResources) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += EnsureCreated; return; }
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                waitingForResources = true;
                AssetDatabase.importPackageCompleted += ResourcesImported;
                AssetDatabase.importPackageFailed += ResourcesFailed;
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                return;
            }
            Directory.CreateDirectory("Assets/Drift/Scenes"); Directory.CreateDirectory("Assets/Drift/Art");
            AssetDatabase.Refresh();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Drift/Art/Drift Korean.asset");
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 48);
                if (font == null) throw new InvalidOperationException("Windows Malgun Gothic font is required for the Korean prototype UI.");
                font.name = "Drift Korean";
                AssetDatabase.CreateAsset(font, "Assets/Drift/Art/Drift Korean.asset");
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
                EditorUtility.SetDirty(font);
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Drift/Art/World.mat");
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
                material = new Material(shader); material.SetFloat("_Smoothness", .22f);
                AssetDatabase.CreateAsset(material, "Assets/Drift/Art/World.mat");
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var root = new GameObject("Drift Application");
                SceneManager.MoveGameObjectToScene(root, scene);
                var application = root.AddComponent<DriftApplication>();
                using (var serialized = new SerializedObject(application))
                {
                    serialized.FindProperty("worldMaterial").objectReferenceValue = material;
                    serialized.FindProperty("uiFont").objectReferenceValue = font;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save Drift scene.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "drift";
            AssetDatabase.SaveAssets();
            if (!previous.IsValid() || !previous.isDirty) EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("DRIFT_SETUP_OK: " + ScenePath);
        }
        private static void ResourcesImported(string package)
        {
            AssetDatabase.importPackageCompleted -= ResourcesImported; AssetDatabase.importPackageFailed -= ResourcesFailed;
            waitingForResources = false; EditorApplication.delayCall += EnsureCreated;
        }
        private static void ResourcesFailed(string package, string error)
        {
            AssetDatabase.importPackageCompleted -= ResourcesImported; AssetDatabase.importPackageFailed -= ResourcesFailed;
            waitingForResources = false; Debug.LogError("Drift TMP resources: " + error);
        }
    }
}

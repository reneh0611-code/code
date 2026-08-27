using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static class PlayableCharacterAutoBuilder
    {
        private const string Source01 = "Assets/Models/Characters/Playable/Charakter01";
        private const string Source02 = "Assets/Models/Characters/Playable/Charakter02";
        private const string ResourceFolder = "Assets/Resources/PlayableCharacters";
        private const string Out01 = ResourceFolder + "/Character01.prefab";
        private const string Out02 = ResourceFolder + "/Character02.prefab";

        private static double nextTry;
        private static bool done;

        static PlayableCharacterAutoBuilder()
        {
            EditorApplication.update += Tick;
            EditorApplication.delayCall += Force;
        }

        [DidReloadScripts]
        private static void Reloaded()
        {
            done = false;
            EditorApplication.delayCall += Force;
        }

        private static void Force() => nextTry = 0;

        private static void Tick()
        {
            if (done || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup < nextTry) return;
            nextTry = EditorApplication.timeSinceStartup + 1.0;
            done = BuildAll();
        }

        private static bool BuildAll()
        {
            string fbx01 = FindFbx(Source01);
            string fbx02 = FindFbx(Source02);
            if (string.IsNullOrEmpty(fbx01) || string.IsNullOrEmpty(fbx02))
            {
                Debug.Log("[CYDOY PLAYABLE] Waiting for both playable FBX models to finish importing.");
                return false;
            }

            EnsureFolder("Assets/Resources");
            EnsureFolder(ResourceFolder);

            bool importReady = NormalizeImporter(fbx01) & NormalizeImporter(fbx02);
            if (!importReady) return false;

            GameObject p1 = BuildPrefab(fbx01, Out01, "Character01");
            GameObject p2 = BuildPrefab(fbx02, Out02, "Character02");
            if (p1 == null || p2 == null) return false;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CYDOY PLAYABLE] READY: {Out01} + {Out02}. Start hub can now use both characters.");
            return true;
        }

        private static string FindFbx(string folder)
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return path;
            }
            return null;
        }

        private static bool NormalizeImporter(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return false;

            bool changed = false;
            if (!importer.importAnimation) { importer.importAnimation = true; changed = true; }

            // The current locomotion/combat controller is Generic, matching the existing player pipeline.
            // Keep the imported skeleton hierarchy intact instead of trying to remap it to a Humanoid avatar.
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }

            if (importer.optimizeGameObjects)
            {
                importer.optimizeGameObjects = false;
                changed = true;
            }

            if (changed)
            {
                Debug.Log($"[CYDOY PLAYABLE] Normalizing rig import for {Path.GetFileName(path)} (Generic, hierarchy preserved).");
                importer.SaveAndReimport();
                return false;
            }
            return true;
        }

        private static GameObject BuildPrefab(string sourcePath, string outputPath, string prefabName)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) return null;

            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) instance = UnityEngine.Object.Instantiate(source);
            instance.name = prefabName;
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            // Do not leave cameras/lights imported by external tools inside character visuals.
            foreach (Camera c in instance.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(c.gameObject);
            foreach (Light l in instance.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(l.gameObject);

            Animator animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return saved;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}

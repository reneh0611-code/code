using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static class PoliceCharacterAutoBuilder
    {
        private const string OutputFolder = "Assets/Resources/Police";
        private const string ControllerPath = "Assets/Resources/Tripo_Locomotion_ExactGeneric.controller";

        private readonly struct PoliceModel
        {
            public readonly string source;
            public readonly string output;
            public readonly string name;

            public PoliceModel(string source, string output, string name)
            {
                this.source = source;
                this.output = output;
                this.name = name;
            }
        }

        private static readonly PoliceModel[] Models =
        {
            new(
                "Assets/Models/Characters/Police/chief/tripo_convert_397a538b-f23c-4b3a-bb2a-b1612ebfeaf6.fbx",
                OutputFolder + "/Chief.prefab",
                "Chief"),
            new(
                "Assets/Models/Characters/Police/policeman1/tripo_convert_032ce7c4-ef28-42cb-9639-0cea8ae14897.fbx",
                OutputFolder + "/Policeman1.prefab",
                "Policeman1"),
            new(
                "Assets/Models/Characters/Police/policemen2/tripo_convert_5a03cf57-df92-45fc-8c9e-13aaba5f48dc.fbx",
                OutputFolder + "/Policeman2.prefab",
                "Policeman2"),
            new(
                "Assets/Models/Characters/Police/policewoman/tripo_convert_d794bd92-0a71-43f8-97fd-5036b8353463.fbx",
                OutputFolder + "/Policewoman.prefab",
                "Policewoman")
        };

        private static double _nextAttempt;
        private static bool _finished;
        private static bool _building;
        private static bool _rebuildAfterImport;

        static PoliceCharacterAutoBuilder()
        {
            EditorApplication.update += Tick;
            EditorApplication.delayCall += ForceCheck;
        }

        private static void ForceCheck()
        {
            _finished = false;
            _nextAttempt = 0;
        }

        private static void Tick()
        {
            if (_finished || _building || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup < _nextAttempt) return;
            _nextAttempt = EditorApplication.timeSinceStartup + 1.5;

            try
            {
                _building = true;
                _finished = BuildAll(false);
            }
            catch (Exception exception)
            {
                _finished = true;
                Debug.LogError($"[CYDOY POLICE] Police character build stopped.\n{exception}");
            }
            finally
            {
                _building = false;
            }
        }

        [MenuItem("Tools/CYDOY/Police/Rebuild Police Characters")]
        private static void RebuildFromMenu()
        {
            _finished = false;
            _building = true;
            try
            {
                BuildAll(true);
            }
            finally
            {
                _building = false;
            }
        }

        private static bool BuildAll(bool force)
        {
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller == null) return false;

            EnsureFolder("Assets/Resources");
            EnsureFolder(OutputFolder);

            foreach (PoliceModel model in Models)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(model.source) == null) return false;
                if (NormalizeImporter(model.source)) return false;

                bool outputMissing = AssetDatabase.LoadAssetAtPath<GameObject>(model.output) == null;
                bool sourceNewer = outputMissing || File.GetLastWriteTimeUtc(model.source) > File.GetLastWriteTimeUtc(model.output);
                bool containsOldBindingWarning = !outputMissing &&
                                                 File.ReadAllText(model.output).Contains("Binding warning");
                if (force || _rebuildAfterImport || sourceNewer || containsOldBindingWarning)
                    BuildPrefab(model, controller);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _rebuildAfterImport = false;
            Debug.Log("[CYDOY POLICE] READY: Chief, Policeman 1, Policeman 2 and Policewoman are available for patrols.");
            return true;
        }

        private static bool NormalizeImporter(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return false;
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                changed = true;
            }
            if (!changed) return false;
            _rebuildAfterImport = true;
            importer.SaveAndReimport();
            return true;
        }

        private static void BuildPrefab(PoliceModel model, RuntimeAnimatorController controller)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(model.source);
            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) throw new InvalidOperationException($"Could not instantiate {model.source}");

            try
            {
                instance.name = model.name;
                foreach (Camera camera in instance.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(camera.gameObject);
                foreach (Light light in instance.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(light.gameObject);
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                PrefabUtility.SaveAsPrefabAsset(instance, model.output);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}

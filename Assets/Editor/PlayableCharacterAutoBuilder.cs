using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
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
        private const string ControllerSource = "Assets/Resources/Tripo_Locomotion_ExactGeneric.controller";
        private const string Controller01 = ResourceFolder + "/Character01.controller";
        private const string Controller02 = ResourceFolder + "/Character02.controller";
        private const string Anim01Folder = ResourceFolder + "/Character01_Animations";
        private const string Anim02Folder = ResourceFolder + "/Character02_Animations";

        private static double nextTry;
        private static bool done;
        private static bool reimporting;
        private static bool forceRebuild;

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
            if (done || reimporting || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup < nextTry) return;
            nextTry = EditorApplication.timeSinceStartup + 1.0;
            try
            {
                done = BuildAll();
            }
            catch (Exception exception)
            {
                // A broken source clip must never turn into an EditorApplication.update log loop.
                done = true;
                Debug.LogError($"[CYDOY PLAYABLE] Automatic character build stopped after one error. Use Tools/CYDOY/Playable Characters/Rebuild after fixing the source.\n{exception}");
            }
        }

        private static bool BuildAll()
        {
            CleanupLeakedBuildInstances();

            // Script reloads are common while iterating. Once the generated character set is
            // complete, leave it alone unless the developer explicitly requests a rebuild.
            if (!forceRebuild && OutputsReady()) return true;

            string fbx01 = FindFbx(Source01);
            string fbx02 = FindFbx(Source02);
            if (string.IsNullOrEmpty(fbx01) || string.IsNullOrEmpty(fbx02))
            {
                Debug.Log("[CYDOY PLAYABLE] Waiting for both playable FBX models to finish importing.");
                return false;
            }

            EnsureFolder("Assets/Resources");
            EnsureFolder(ResourceFolder);
            EnsureFolder(Anim01Folder);
            EnsureFolder(Anim02Folder);

            bool importReady = NormalizeImporter(fbx01) & NormalizeImporter(fbx02);
            if (!importReady) return false;

            GameObject p1 = BuildPrefab(fbx01, Out01, "Character01");
            GameObject p2 = BuildPrefab(fbx02, Out02, "Character02");
            if (p1 == null || p2 == null) return false;

            bool c1 = BuildRetargetedController(p1, Controller01, Anim01Folder, "Character01");
            bool c2 = BuildRetargetedController(p2, Controller02, Anim02Folder, "Character02");
            if (!c1 || !c2) return false;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            forceRebuild = false;
            Debug.Log("[CYDOY PLAYABLE] READY: both playable characters + retargeted Idle/Walk/Run/Jump/Punch animations are built.");
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
                reimporting = true;
                Debug.Log($"[CYDOY PLAYABLE] Preparing skeleton hierarchy for {Path.GetFileName(path)}.");
                importer.SaveAndReimport();
                reimporting = false;
                nextTry = EditorApplication.timeSinceStartup + .6;
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

        private static bool BuildRetargetedController(GameObject targetPrefab, string outputController, string animFolder, string label)
        {
            AnimatorController sourceController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerSource);
            if (sourceController == null)
            {
                Debug.LogError("[CYDOY PLAYABLE] Source player controller missing: " + ControllerSource);
                return false;
            }

            // Always start from the clean source controller while generating. A partially built
            // output can otherwise retain references to an earlier, incomplete clip set.
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(outputController) != null)
                AssetDatabase.DeleteAsset(outputController);
            if (!AssetDatabase.CopyAsset(ControllerSource, outputController)) return false;
            AssetDatabase.ImportAsset(outputController);

            AnimatorController targetController = AssetDatabase.LoadAssetAtPath<AnimatorController>(outputController);
            if (targetController == null) return false;

            GameObject temp = PrefabUtility.InstantiatePrefab(targetPrefab) as GameObject;
            if (temp == null) temp = UnityEngine.Object.Instantiate(targetPrefab);
            temp.name = $"__CYDOY_RETARGET_TEMP_{label}";
            temp.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                Animator animator = temp.GetComponentInChildren<Animator>(true);
                Transform animRoot = animator != null ? animator.transform : temp.transform;

                Dictionary<string, string> targetPaths = BuildBonePathMap(animRoot);
                if (targetPaths.Count < 10)
                {
                    Debug.LogError($"[CYDOY PLAYABLE] {label}: no usable rig hierarchy found. The FBX must contain the Mixamo rig, not only the mesh.");
                    return false;
                }

                Dictionary<AnimationClip, AnimationClip> clipMap = new();
                foreach (AnimatorControllerLayer layer in sourceController.layers)
                    CollectAndRetargetMotions(layer.stateMachine, targetPaths, animFolder, label, clipMap);

                foreach (AnimatorControllerLayer layer in targetController.layers)
                    ReplaceStateMachineMotions(layer.stateMachine, clipMap);

                EditorUtility.SetDirty(targetController);
                AssetDatabase.SaveAssets();

                int converted = clipMap.Values.Count(v => v != null);
                Debug.Log($"[CYDOY PLAYABLE] {label}: retargeted {converted} animation clips to its own skeleton.");
                return converted > 0;
            }
            finally
            {
                if (temp != null) UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        private static Dictionary<string, string> BuildBonePathMap(Transform root)
        {
            Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                string key = NormalizeBoneName(t.name);
                if (string.IsNullOrEmpty(key) || map.ContainsKey(key)) continue;
                map[key] = AnimationUtility.CalculateTransformPath(t, root);
            }
            return map;
        }

        private static void CollectAndRetargetMotions(AnimatorStateMachine sm, Dictionary<string, string> targetPaths, string animFolder, string label, Dictionary<AnimationClip, AnimationClip> map)
        {
            foreach (ChildAnimatorState child in sm.states)
                CollectMotion(child.state.motion, targetPaths, animFolder, label, map);
            foreach (ChildAnimatorStateMachine child in sm.stateMachines)
                CollectAndRetargetMotions(child.stateMachine, targetPaths, animFolder, label, map);
        }

        private static void CollectMotion(Motion motion, Dictionary<string, string> targetPaths, string animFolder, string label, Dictionary<AnimationClip, AnimationClip> map)
        {
            if (motion == null) return;
            if (motion is AnimationClip clip)
            {
                if (!map.ContainsKey(clip)) map[clip] = CreateRetargetedClip(clip, targetPaths, animFolder, label);
                return;
            }
            if (motion is BlendTree tree)
                foreach (ChildMotion child in tree.children) CollectMotion(child.motion, targetPaths, animFolder, label, map);
        }

        private static AnimationClip CreateRetargetedClip(AnimationClip source, Dictionary<string, string> targetPaths, string animFolder, string label)
        {
            // Mixamo commonly names every embedded take "mixamo.com". Include the FBX name and
            // local file id so unrelated states never overwrite the same generated .anim file.
            string sourceAssetName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(source));
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string _, out long localId);
            string safe = Sanitize($"{sourceAssetName}_{source.name}_{localId}");
            string path = $"{animFolder}/{safe}.anim";
            AnimationClip target = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (target == null)
            {
                target = new AnimationClip { name = source.name, frameRate = source.frameRate };
                AssetDatabase.CreateAsset(target, path);
            }
            else
            {
                target.ClearCurves();
                target.frameRate = source.frameRate;
            }

            int mapped = 0;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                EditorCurveBinding dst = binding;
                if (binding.type == typeof(Transform) && !string.IsNullOrEmpty(binding.path))
                {
                    string leaf = binding.path.Split('/').Last();
                    string key = NormalizeBoneName(leaf);
                    if (!targetPaths.TryGetValue(key, out string newPath)) continue;
                    dst.path = newPath;
                    mapped++;
                }
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                AnimationUtility.SetEditorCurve(target, dst, curve);
            }

            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
            {
                EditorCurveBinding dst = binding;
                if (!string.IsNullOrEmpty(binding.path))
                {
                    string leaf = binding.path.Split('/').Last();
                    if (targetPaths.TryGetValue(NormalizeBoneName(leaf), out string newPath)) dst.path = newPath;
                }
                AnimationUtility.SetObjectReferenceCurve(target, dst, AnimationUtility.GetObjectReferenceCurve(source, binding));
            }

            AnimationUtility.SetAnimationEvents(target, AnimationUtility.GetAnimationEvents(source));
            CopyClipSettings(source, target);
            EditorUtility.SetDirty(target);

            if (mapped == 0)
                Debug.LogWarning($"[CYDOY PLAYABLE] {label}/{source.name}: no Transform curves could be mapped.");
            return target;
        }

        private static void CopyClipSettings(AnimationClip source, AnimationClip target)
        {
            SerializedObject src = new SerializedObject(source);
            SerializedObject dst = new SerializedObject(target);
            SerializedProperty srcSettings = src.FindProperty("m_AnimationClipSettings");
            SerializedProperty dstSettings = dst.FindProperty("m_AnimationClipSettings");
            if (srcSettings != null && dstSettings != null)
            {
                CopyProperty(srcSettings, dstSettings);
                dst.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void CopyProperty(SerializedProperty src, SerializedProperty dst)
        {
            SerializedProperty iterator = src.Copy();
            SerializedProperty end = src.GetEndProperty();
            string prefix = src.propertyPath + ".";
            bool enter = true;
            while (iterator.Next(enter) && !SerializedProperty.EqualContents(iterator, end))
            {
                enter = false;
                string path = iterator.propertyPath;
                if (!path.StartsWith(prefix, StringComparison.Ordinal)) break;
                string relative = path.Substring(prefix.Length);
                SerializedProperty d = string.IsNullOrEmpty(relative) ? dst : dst.FindPropertyRelative(relative);
                if (d == null) continue;
                switch (iterator.propertyType)
                {
                    case SerializedPropertyType.Boolean: d.boolValue = iterator.boolValue; break;
                    case SerializedPropertyType.Integer: d.intValue = iterator.intValue; break;
                    case SerializedPropertyType.Float: d.floatValue = iterator.floatValue; break;
                    case SerializedPropertyType.String: d.stringValue = iterator.stringValue; break;
                    case SerializedPropertyType.Enum: d.enumValueIndex = iterator.enumValueIndex; break;
                    case SerializedPropertyType.Vector2: d.vector2Value = iterator.vector2Value; break;
                    case SerializedPropertyType.Vector3: d.vector3Value = iterator.vector3Value; break;
                    case SerializedPropertyType.Vector4: d.vector4Value = iterator.vector4Value; break;
                }
            }
        }

        private static void CleanupLeakedBuildInstances()
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid() || go.transform.parent != null) continue;
                bool leakedTemp = go.name.StartsWith("__CYDOY_RETARGET_TEMP_", StringComparison.Ordinal)
                    || go.name == "Character01"
                    || go.name == "Character02";
                if (!leakedTemp) continue;
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [MenuItem("Tools/CYDOY/Playable Characters/Rebuild")]
        private static void RebuildFromMenu()
        {
            forceRebuild = true;
            done = false;
            nextTry = 0;
            CleanupLeakedBuildInstances();
        }

        private static bool OutputsReady()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(Out01) != null
                && AssetDatabase.LoadAssetAtPath<GameObject>(Out02) != null
                && AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller01) != null
                && AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller02) != null
                && AssetDatabase.FindAssets("t:AnimationClip", new[] { Anim01Folder }).Length > 0
                && AssetDatabase.FindAssets("t:AnimationClip", new[] { Anim02Folder }).Length > 0;
        }

        private static void ReplaceStateMachineMotions(AnimatorStateMachine sm, Dictionary<AnimationClip, AnimationClip> map)
        {
            foreach (ChildAnimatorState child in sm.states)
                child.state.motion = ReplaceMotion(child.state.motion, map);
            foreach (ChildAnimatorStateMachine child in sm.stateMachines)
                ReplaceStateMachineMotions(child.stateMachine, map);
        }

        private static Motion ReplaceMotion(Motion motion, Dictionary<AnimationClip, AnimationClip> map)
        {
            if (motion is AnimationClip clip)
                return map.TryGetValue(clip, out AnimationClip replacement) && replacement != null ? replacement : motion;
            if (motion is BlendTree tree)
            {
                ChildMotion[] children = tree.children;
                for (int i = 0; i < children.Length; i++) children[i].motion = ReplaceMotion(children[i].motion, map);
                tree.children = children;
                EditorUtility.SetDirty(tree);
            }
            return motion;
        }

        private static string NormalizeBoneName(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            int colon = value.LastIndexOf(':');
            if (colon >= 0 && colon < value.Length - 1) value = value.Substring(colon + 1);
            value = value.Replace("mixamorig", "", StringComparison.OrdinalIgnoreCase);
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private static string Sanitize(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Replace('/', '_').Replace('\\', '_');
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

using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.World;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class TripoExactGenericInstaller
    {
        private const string CharacterPath = "Assets/Models/Characters/TripoTest/TripoCharacter.fbx";
        private const string IdlePath = "Assets/Models/Animations/Idle.fbx";
        private const string WalkPath = "Assets/Models/Animations/Walk.fbx";
        private const string RunPath = "Assets/Models/Animations/Run.fbx";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string ControllerPath = "Assets/Resources/Tripo_Locomotion_ExactGeneric.controller";

        [MenuItem("Tools/CYDOY/Tripo Test/Use Exact Generic Animations + Ground All")]
        public static void Install()
        {
            if (!SetGeneric(CharacterPath, false) || !SetGeneric(IdlePath, true) || !SetGeneric(WalkPath, true) || !SetGeneric(RunPath, true)) return;
            AnimationClip idle = FindClip(IdlePath), walk = FindClip(WalkPath), run = FindClip(RunPath);
            if (idle == null || walk == null || run == null) { EditorUtility.DisplayDialog("CYDOY", "Missing animation clip.", "OK"); return; }

            EnsureResourcesFolder();
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState i = sm.AddState("Idle"), w = sm.AddState("Walk"), r = sm.AddState("Run");
            i.motion = idle; w.motion = walk; r.motion = run; i.speed = 1f; w.speed = .82f; r.speed = 1f; sm.defaultState = i;
            InstallOnPlayer(controller);
            InstallOnExistingNpcs(controller);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
            EditorUtility.DisplayDialog("CYDOY", "Exact Generic locomotion installed. NPC grounding now targets the actual animated model child instead of moving the NPC controller/root.", "Test it");
        }

        private static void InstallOnPlayer(RuntimeAnimatorController controller)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform visual = root.transform.Find("CharacterVisual");
                Animator animator = visual != null ? visual.GetComponentInChildren<Animator>(true) : root.GetComponentInChildren<Animator>(true);
                if (animator == null) return;
                animator.runtimeAnimatorController = controller; animator.avatar = null; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.enabled = true;
                CharacterAnimationDriver driver = root.GetComponent<CharacterAnimationDriver>();
                if (driver != null)
                {
                    SerializedObject so = new(driver); so.FindProperty("animator").objectReferenceValue = animator; so.FindProperty("fallbackController").objectReferenceValue = controller; so.ApplyModifiedPropertiesWithoutUndo(); driver.enabled = true;
                }
                MixamoRuntimePoseAndGrounder old = root.GetComponent<MixamoRuntimePoseAndGrounder>(); if (old != null) Object.DestroyImmediate(old);
                FixedWorldVisualGrounder g = root.GetComponent<FixedWorldVisualGrounder>(); if (g == null) g = root.AddComponent<FixedWorldVisualGrounder>();
                Transform model = visual != null && visual.childCount > 0 ? visual.GetChild(0) : animator.transform;
                SerializedObject gs = new(g); gs.FindProperty("modelRoot").objectReferenceValue = model; gs.FindProperty("settleFrames").intValue = 2; gs.FindProperty("soleOffset").floatValue = 0f; gs.ApplyModifiedPropertiesWithoutUndo(); g.enabled = true;
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void InstallOnExistingNpcs(RuntimeAnimatorController controller)
        {
            NPCWanderer[] npcs = Object.FindObjectsByType<NPCWanderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int fixedCount = 0;
            foreach (NPCWanderer wanderer in npcs)
            {
                GameObject npc = wanderer.gameObject;
                Animator animator = npc.GetComponentInChildren<Animator>(true);
                if (animator == null) continue;
                animator.runtimeAnimatorController = controller; animator.avatar = null; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.enabled = true;

                // Critical fix: never use the NPC root as modelRoot. The root owns CharacterController/movement.
                // Move only the animated visual hierarchy so the soles can meet the street independently.
                Transform modelRoot = animator.transform;
                if (modelRoot == npc.transform)
                {
                    SkinnedMeshRenderer smr = npc.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (smr != null && smr.transform != npc.transform) modelRoot = FindHighestVisualChild(npc.transform, smr.transform);
                }

                FixedWorldVisualGrounder oldRootGrounder = npc.GetComponent<FixedWorldVisualGrounder>();
                if (oldRootGrounder != null) Object.DestroyImmediate(oldRootGrounder);
                FixedWorldVisualGrounder grounder = modelRoot.GetComponent<FixedWorldVisualGrounder>(); if (grounder == null) grounder = modelRoot.gameObject.AddComponent<FixedWorldVisualGrounder>();
                SerializedObject so = new(grounder); so.FindProperty("modelRoot").objectReferenceValue = modelRoot; so.FindProperty("settleFrames").intValue = 2; so.FindProperty("rayStartHeight").floatValue = 2f; so.FindProperty("rayDistance").floatValue = 6f; so.FindProperty("soleOffset").floatValue = 0f; so.ApplyModifiedPropertiesWithoutUndo(); grounder.enabled = true;
                wanderer.enabled = true; fixedCount++;
            }
            Debug.Log($"[CYDOY] Prepared {fixedCount} existing NPCs for visual-only grounding.");
        }

        private static Transform FindHighestVisualChild(Transform npcRoot, Transform leaf)
        {
            Transform current = leaf;
            while (current.parent != null && current.parent != npcRoot) current = current.parent;
            return current;
        }

        private static bool SetGeneric(string path, bool loop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter; if (importer == null) return false;
            importer.animationType = ModelImporterAnimationType.Generic; importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; importer.importAnimation = true;
            if (loop) { ModelImporterClipAnimation[] clips = importer.defaultClipAnimations; if (clips != null) { foreach (var c in clips) c.loopTime = true; importer.clipAnimations = clips; } }
            importer.SaveAndReimport(); return true;
        }
        private static AnimationClip FindClip(string path) { foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c; return null; }
        private static void EnsureResourcesFolder() { if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources"); }
    }
}

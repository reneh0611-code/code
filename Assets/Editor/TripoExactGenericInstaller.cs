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
        private const string JumpPath = "Assets/Models/Animations/Jump.fbx";
        private const string FallPath = "Assets/Models/Animations/Fall.fbx";
        private const string GettingUpPath = "Assets/Models/Animations/GettingUp.fbx";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string ControllerPath = "Assets/Resources/Tripo_Locomotion_ExactGeneric.controller";

        [MenuItem("Tools/CYDOY/Tripo Test/Use Exact Generic Animations + Ground All")]
        public static void Install()
        {
            if (!SetGeneric(CharacterPath, false) || !SetGeneric(IdlePath, true) || !SetGeneric(WalkPath, true) || !SetGeneric(RunPath, true)) return;

            bool hasJump = AssetDatabase.LoadMainAssetAtPath(JumpPath) != null;
            bool hasFall = AssetDatabase.LoadMainAssetAtPath(FallPath) != null;
            bool hasGettingUp = AssetDatabase.LoadMainAssetAtPath(GettingUpPath) != null;
            if (hasJump) SetGeneric(JumpPath, false);
            if (hasFall) SetGeneric(FallPath, false);
            if (hasGettingUp) SetGeneric(GettingUpPath, false);

            AnimationClip idle = FindClip(IdlePath), walk = FindClip(WalkPath), run = FindClip(RunPath);
            AnimationClip jump = hasJump ? FindClip(JumpPath) : null;
            AnimationClip fall = hasFall ? FindClip(FallPath) : null;
            AnimationClip gettingUp = hasGettingUp ? FindClip(GettingUpPath) : null;
            if (idle == null || walk == null || run == null) { EditorUtility.DisplayDialog("CYDOY", "Missing Idle, Walk or Run animation clip.", "OK"); return; }

            EnsureResourcesFolder();
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState i = sm.AddState("Idle"), w = sm.AddState("Walk"), r = sm.AddState("Run");
            i.motion = idle; w.motion = walk; r.motion = run; i.speed = 1f; w.speed = .82f; r.speed = 1f; sm.defaultState = i;
            if (jump != null) { AnimatorState j = sm.AddState("Jump"); j.motion = jump; j.speed = 1f; }
            if (fall != null) { AnimatorState f = sm.AddState("Fall"); f.motion = fall; f.speed = 1f; }
            if (gettingUp != null) { AnimatorState g = sm.AddState("GettingUp"); g.motion = gettingUp; g.speed = 1f; }

            InstallOnPlayer(controller);
            InstallOnExistingNpcs(controller);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();

            string status = $"Jump: {(jump != null ? "OK" : "missing")}\nFall: {(fall != null ? "OK" : "missing")}\nGettingUp: {(gettingUp != null ? "OK" : "missing")}";
            EditorUtility.DisplayDialog("CYDOY", "Exact Generic animations installed.\n\n" + status, "Test it");
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
                    SerializedObject so = new(driver);
                    so.FindProperty("animator").objectReferenceValue = animator;
                    so.FindProperty("fallbackController").objectReferenceValue = controller;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    driver.enabled = true;
                }
                foreach (FixedWorldVisualGrounder grounder in root.GetComponentsInChildren<FixedWorldVisualGrounder>(true)) Object.DestroyImmediate(grounder);
                foreach (MixamoRuntimePoseAndGrounder old in root.GetComponentsInChildren<MixamoRuntimePoseAndGrounder>(true)) Object.DestroyImmediate(old);
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
                Animator animator = wanderer.GetComponentInChildren<Animator>(true);
                if (animator == null) continue;
                animator.runtimeAnimatorController = controller; animator.avatar = null; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.enabled = true; wanderer.enabled = true; fixedCount++;
            }
            Debug.Log($"[CYDOY] Prepared {fixedCount} existing NPCs with Fall/GettingUp support.");
        }

        private static bool SetGeneric(string path, bool loop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter; if (importer == null) return false;
            importer.animationType = ModelImporterAnimationType.Generic; importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; importer.importAnimation = true;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0) { foreach (var c in clips) c.loopTime = loop; importer.clipAnimations = clips; }
            importer.SaveAndReimport(); return true;
        }
        private static AnimationClip FindClip(string path) { foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c; return null; }
        private static void EnsureResourcesFolder() { if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources"); }
    }
}

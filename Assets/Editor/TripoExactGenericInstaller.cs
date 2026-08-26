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
            if (!SetGeneric(CharacterPath, false) || !SetGeneric(IdlePath, true) || !SetGeneric(WalkPath, true) || !SetGeneric(RunPath, true))
                return;

            AnimationClip idle = FindClip(IdlePath);
            AnimationClip walk = FindClip(WalkPath);
            AnimationClip run = FindClip(RunPath);
            if (idle == null || walk == null || run == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Exact Generic", "Could not load all three clips after Generic reimport.", "OK");
                return;
            }

            EnsureResourcesFolder();
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idleState = sm.AddState("Idle");
            AnimatorState walkState = sm.AddState("Walk");
            AnimatorState runState = sm.AddState("Run");
            idleState.motion = idle;
            walkState.motion = walk;
            runState.motion = run;
            idleState.speed = 1f;
            walkState.speed = 0.82f;
            runState.speed = 1f;
            sm.defaultState = idleState;

            InstallOnPlayer(controller);
            InstallOnExistingNpcs(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            EditorUtility.DisplayDialog(
                "CYDOY · Exact Generic",
                "Done.\n\n" +
                "Character + Idle + Walk + Run now use Generic skeleton playback.\n" +
                "Unity Humanoid retargeting is bypassed, so feet/legs are not muscle-retargeted.\n\n" +
                "Idle 1.00x\nWalk 0.82x\nRun 1.00x\nCrossfades only.\n\n" +
                "Player and existing Tripo NPC visuals are grounded once against the actual world surface; animation bones are untouched.",
                "Test it");
        }

        private static void InstallOnPlayer(RuntimeAnimatorController controller)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform visual = root.transform.Find("CharacterVisual");
                Animator animator = visual != null ? visual.GetComponentInChildren<Animator>(true) : root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    EditorUtility.DisplayDialog("CYDOY · Exact Generic", "No Player Animator found.", "OK");
                    return;
                }

                animator.runtimeAnimatorController = controller;
                animator.avatar = null;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;

                CharacterAnimationDriver driver = root.GetComponent<CharacterAnimationDriver>();
                if (driver != null)
                {
                    SerializedObject so = new(driver);
                    so.FindProperty("animator").objectReferenceValue = animator;
                    so.FindProperty("fallbackController").objectReferenceValue = controller;
                    so.FindProperty("idleWalkBlend").floatValue = 0.10f;
                    so.FindProperty("walkRunBlend").floatValue = 0.08f;
                    so.FindProperty("idleRunBlend").floatValue = 0.10f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    driver.enabled = true;
                }

                MixamoRuntimePoseAndGrounder old = root.GetComponent<MixamoRuntimePoseAndGrounder>();
                if (old != null) Object.DestroyImmediate(old);

                FixedWorldVisualGrounder fixedGrounder = root.GetComponent<FixedWorldVisualGrounder>();
                if (fixedGrounder == null) fixedGrounder = root.AddComponent<FixedWorldVisualGrounder>();

                SerializedObject gso = new(fixedGrounder);
                Transform modelRoot = visual != null && visual.childCount > 0 ? visual.GetChild(0) : animator.transform;
                gso.FindProperty("modelRoot").objectReferenceValue = modelRoot;
                gso.FindProperty("settleFrames").intValue = 2;
                gso.FindProperty("soleOffset").floatValue = 0f;
                gso.ApplyModifiedPropertiesWithoutUndo();
                fixedGrounder.enabled = true;

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void InstallOnExistingNpcs(RuntimeAnimatorController controller)
        {
            GameObject npcRoot = GameObject.Find("Generated_Tripo_NPCs");
            if (npcRoot == null) return;

            foreach (Transform child in npcRoot.transform)
            {
                GameObject npc = child.gameObject;
                Animator animator = npc.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    animator.runtimeAnimatorController = controller;
                    animator.avatar = null;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.enabled = true;
                }

                FixedWorldVisualGrounder grounder = npc.GetComponent<FixedWorldVisualGrounder>();
                if (grounder == null) grounder = npc.AddComponent<FixedWorldVisualGrounder>();
                SerializedObject so = new(grounder);
                so.FindProperty("modelRoot").objectReferenceValue = npc.transform;
                so.FindProperty("settleFrames").intValue = 2;
                so.FindProperty("soleOffset").floatValue = 0f;
                so.ApplyModifiedPropertiesWithoutUndo();
                grounder.enabled = true;

                NPCWanderer wanderer = npc.GetComponent<NPCWanderer>();
                if (wanderer != null) wanderer.enabled = true;
            }
        }

        private static bool SetGeneric(string path, bool loop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Exact Generic", "Missing file:\n\n" + path, "OK");
                return false;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;

            if (loop)
            {
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    for (int i = 0; i < clips.Length; i++) clips[i].loopTime = true;
                    importer.clipAnimations = clips;
                }
            }

            importer.SaveAndReimport();
            return true;
        }

        private static AnimationClip FindClip(string path)
        {
            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            return null;
        }

        private static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }
}

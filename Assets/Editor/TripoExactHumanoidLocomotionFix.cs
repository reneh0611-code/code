using CheatOnYourDayOnes.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class TripoExactHumanoidLocomotionFix
    {
        private const string IdlePath = "Assets/Models/Animations/Idle.fbx";
        private const string WalkPath = "Assets/Models/Animations/Walk.fbx";
        private const string RunPath  = "Assets/Models/Animations/Run.fbx";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string ControllerPath = "Assets/Resources/Tripo_Locomotion_Final.controller";

        [MenuItem("Tools/CYDOY/Tripo Test/Fix New Animations + Install")]
        public static void FixAndInstall()
        {
            if (!FixAnimationFbx(IdlePath, "Idle") ||
                !FixAnimationFbx(WalkPath, "Walk") ||
                !FixAnimationFbx(RunPath, "Run"))
                return;

            AssetDatabase.Refresh();

            AnimationClip idle = FindMainClip(IdlePath);
            AnimationClip walk = FindMainClip(WalkPath);
            AnimationClip run = FindMainClip(RunPath);

            if (idle == null || walk == null || run == null)
            {
                EditorUtility.DisplayDialog(
                    "CYDOY · Locomotion",
                    "The three FBX files were converted to Humanoid, but at least one AnimationClip could not be loaded.\n\n" +
                    $"Idle: {(idle ? idle.name : "MISSING")}\n" +
                    $"Walk: {(walk ? walk.name : "MISSING")}\n" +
                    $"Run: {(run ? run.name : "MISSING")}",
                    "OK");
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
            idleState.speed = 1.0f;
            walkState.speed = 0.82f;
            runState.speed = 1.0f;
            sm.defaultState = idleState;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform visual = prefabRoot.transform.Find("CharacterVisual");
                Animator animator = visual != null ? visual.GetComponentInChildren<Animator>(true) : null;
                if (animator == null) animator = prefabRoot.GetComponentInChildren<Animator>(true);

                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                {
                    EditorUtility.DisplayDialog(
                        "CYDOY · Locomotion",
                        "The current Player character does not have a valid Humanoid Avatar. Nothing was changed on the Player.",
                        "OK");
                    return;
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;

                CharacterAnimationDriver driver = prefabRoot.GetComponent<CharacterAnimationDriver>();
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

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    "[CYDOY] FINAL locomotion installed.\n" +
                    $"Idle: {idle.name} ({IdlePath}) 1.00x\n" +
                    $"Walk: {walk.name} ({WalkPath}) 0.82x\n" +
                    $"Run: {run.name} ({RunPath}) 1.00x\n" +
                    "All 3 FBX imports: Humanoid. Clip pose/keyframe data untouched; only looping enabled."
                );

                EditorUtility.DisplayDialog(
                    "CYDOY · Locomotion",
                    "Done.\n\n" +
                    "Idle, Walk and Run are now imported as Humanoid and assigned to the current Tripo Player.\n\n" +
                    "Idle: original speed\n" +
                    "Walk: 0.82x playback\n" +
                    "Run: original speed\n" +
                    "Transitions: short clean crossfades\n\n" +
                    "The downloaded animation curves/poses were not edited.",
                    "Test it");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool FixAnimationFbx(string path, string label)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                EditorUtility.DisplayDialog(
                    "CYDOY · Locomotion",
                    $"{label}.fbx was not found here:\n\n{path}\n\nRename/move the file to this path or tell me its exact name.",
                    "OK");
                return false;
            }

            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                    clips[i].loopTime = true;
                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();
            return true;
        }

        private static AnimationClip FindMainClip(string path)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object obj in assets)
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }
            return null;
        }

        private static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }
}

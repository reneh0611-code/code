using CheatOnYourDayOnes.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static class SelectedWindow2SlidingDoorInstaller
    {
        private const string WindowPrefabPath = "Assets/Models/Buildings/ModularModernHousePack/Prefabs/Windows/Window2.prefab";
        private const string AssemblyName = "SlidingDoorAssembly";
        private static readonly Vector3 ExpectedWorldPosition = new(268.67844f, 0.047467396f, 673.929f);

        static SelectedWindow2SlidingDoorInstaller()
        {
            EditorApplication.delayCall += TryAutomaticInstall;
        }

        [MenuItem("Tools/CYDOY/Buildings/Convert Selected Window2 To Sliding Door")]
        private static void ConvertSelected()
        {
            GameObject selected = Selection.activeGameObject;
            GameObject root = selected != null ? PrefabUtility.GetNearestPrefabInstanceRoot(selected) : null;
            root ??= selected;

            if (!IsWindow2(root))
            {
                Debug.LogWarning("[Sliding Door] Select the Window2 prefab instance that should become the entrance.");
                return;
            }

            Install(root, true);
        }

        private static void TryAutomaticInstall()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutomaticInstall;
                return;
            }

            GameObject target = FindTargetWindow();
            if (target != null && target.transform.Find(AssemblyName) == null)
                Install(target, false);
        }

        private static GameObject FindTargetWindow()
        {
            GameObject selected = Selection.activeGameObject;
            GameObject selectedRoot = selected != null ? PrefabUtility.GetNearestPrefabInstanceRoot(selected) : null;
            if (IsWindow2(selectedRoot) && Vector3.Distance(selectedRoot.transform.position, ExpectedWorldPosition) < 1.5f)
                return selectedRoot;

            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject closest = null;
            float closestDistance = 1.5f;

            foreach (Transform candidate in transforms)
            {
                if (!candidate.gameObject.scene.IsValid() || candidate.parent != null || !IsWindow2(candidate.gameObject))
                    continue;

                float distance = Vector3.Distance(candidate.position, ExpectedWorldPosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = candidate.gameObject;
                }
            }

            return closest;
        }

        private static bool IsWindow2(GameObject candidate)
        {
            if (candidate == null || candidate.name != "Window2")
                return false;

            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate);
            return prefabPath == WindowPrefabPath;
        }

        private static void Install(GameObject root, bool selectAfterwards)
        {
            if (root == null || root.transform.Find(AssemblyName) != null)
                return;

            Transform originalModel = root.transform.Find("Model");
            if (originalModel == null)
            {
                Debug.LogError("[Sliding Door] Window2 has no Model child; conversion was cancelled.");
                return;
            }

            BoxCollider sourceCollider = originalModel.GetComponent<BoxCollider>();
            MeshRenderer sourceRenderer = originalModel.GetComponent<MeshRenderer>();
            if (sourceCollider == null || sourceRenderer == null)
            {
                Debug.LogError("[Sliding Door] Window2 source renderer/collider is missing; conversion was cancelled.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(root, "Convert Window2 to sliding door");
            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            Bounds localBounds = CalculateLocalBounds(root.transform, sourceCollider);
            Material[] materials = sourceRenderer.sharedMaterials;
            Material frameMaterial = materials.Length > 0 ? materials[0] : null;
            Material glassMaterial = materials.Length > 1 ? materials[1] : frameMaterial;

            originalModel.name = "OriginalWindow2_Disabled";
            originalModel.gameObject.SetActive(false);
            GameObjectUtility.SetStaticEditorFlags(root, 0);

            GameObject assembly = new(AssemblyName);
            Undo.RegisterCreatedObjectUndo(assembly, "Create sliding door assembly");
            assembly.transform.SetParent(root.transform, false);

            float depth = Mathf.Clamp(localBounds.size.x, 0.075f, 0.16f);
            float height = Mathf.Max(1.8f, localBounds.size.y);
            float width = Mathf.Max(2.4f, localBounds.size.z);
            Vector3 center = localBounds.center;

            float frame = Mathf.Clamp(width * 0.022f, 0.055f, 0.075f);
            float openingWidth = Mathf.Clamp(width * 0.54f, 1.4f, 1.65f);
            float sideWidth = Mathf.Max(0.25f, (width - openingWidth) * 0.5f);
            float clearHeight = height - frame * 2f;
            float glassDepth = Mathf.Max(0.018f, depth * 0.28f);

            Transform fixedParts = NewChild(assembly.transform, "FixedFrameAndSideGlass");
            CreatePart(fixedParts, "Frame_Top", center + Vector3.up * (height * 0.5f - frame * 0.5f), new Vector3(depth, frame, width), frameMaterial, false, false);
            CreatePart(fixedParts, "Frame_Bottom", center - Vector3.up * (height * 0.5f - frame * 0.5f), new Vector3(depth, frame, width), frameMaterial, false, false);

            float leftEdge = center.z - width * 0.5f;
            float rightEdge = center.z + width * 0.5f;
            float openingLeft = center.z - openingWidth * 0.5f;
            float openingRight = center.z + openingWidth * 0.5f;

            CreateVerticalFrame(fixedParts, "Frame_OuterLeft", center, leftEdge + frame * 0.5f, depth, clearHeight, frame, frameMaterial);
            CreateVerticalFrame(fixedParts, "Frame_DoorLeft", center, openingLeft - frame * 0.5f, depth, clearHeight, frame, frameMaterial);
            CreateVerticalFrame(fixedParts, "Frame_DoorRight", center, openingRight + frame * 0.5f, depth, clearHeight, frame, frameMaterial);
            CreateVerticalFrame(fixedParts, "Frame_OuterRight", center, rightEdge - frame * 0.5f, depth, clearHeight, frame, frameMaterial);

            float fixedGlassWidth = Mathf.Max(0.08f, sideWidth - frame * 1.5f);
            float fixedLeftZ = center.z - openingWidth * 0.5f - sideWidth * 0.5f;
            float fixedRightZ = center.z + openingWidth * 0.5f + sideWidth * 0.5f;
            GameObject leftGlass = CreatePart(fixedParts, "FixedGlass_Left", new Vector3(center.x, center.y, fixedLeftZ), new Vector3(glassDepth, clearHeight - frame, fixedGlassWidth), glassMaterial, true, false);
            GameObject rightGlass = CreatePart(fixedParts, "FixedGlass_Right", new Vector3(center.x, center.y, fixedRightZ), new Vector3(glassDepth, clearHeight - frame, fixedGlassWidth), glassMaterial, true, false);
            SetGlassRendering(leftGlass);
            SetGlassRendering(rightGlass);

            float panelWidth = openingWidth * 0.5f;
            Transform leftPanel = CreateDoorPanel(assembly.transform, "SlidingDoor_Left", new Vector3(center.x, center.y, center.z - panelWidth * 0.5f), panelWidth, clearHeight, depth, glassDepth, frame, frameMaterial, glassMaterial);
            Transform rightPanel = CreateDoorPanel(assembly.transform, "SlidingDoor_Right", new Vector3(center.x, center.y, center.z + panelWidth * 0.5f), panelWidth, clearHeight, depth, glassDepth, frame, frameMaterial, glassMaterial);

            GameObject blockerObject = new("DoorwayBlocker_ClosedOnly");
            Undo.RegisterCreatedObjectUndo(blockerObject, "Create doorway blocker");
            blockerObject.transform.SetParent(assembly.transform, false);
            blockerObject.transform.localPosition = center;
            BoxCollider blocker = blockerObject.AddComponent<BoxCollider>();
            blocker.size = new Vector3(Mathf.Max(0.18f, depth * 1.8f), clearHeight, openingWidth - frame);

            GameObject interactionZone = new("DoorInteractionRaycastZone");
            Undo.RegisterCreatedObjectUndo(interactionZone, "Create door interaction zone");
            interactionZone.transform.SetParent(assembly.transform, false);
            interactionZone.transform.localPosition = center;
            BoxCollider interactionCollider = interactionZone.AddComponent<BoxCollider>();
            interactionCollider.size = new Vector3(Mathf.Max(0.45f, depth * 3f), clearHeight, openingWidth + frame * 2f);
            interactionCollider.isTrigger = true;

            SlidingGlassDoor door = Undo.AddComponent<SlidingGlassDoor>(root);
            door.Configure(leftPanel, rightPanel, blocker, Vector3.forward, panelWidth * 0.92f, 0.72f);
            EditorUtility.SetDirty(door);
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);

            if (selectAfterwards)
                Selection.activeGameObject = root;

            Debug.Log($"[Sliding Door] Converted only '{root.name}' at {root.transform.position}. Width {width:F2}m, opening {openingWidth:F2}m. Use E in play mode.", root);
        }

        private static Bounds CalculateLocalBounds(Transform root, BoxCollider box)
        {
            Vector3 half = box.size * 0.5f;
            Bounds result = new(root.InverseTransformPoint(box.transform.TransformPoint(box.center)), Vector3.zero);

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = box.center + new Vector3(half.x * x, half.y * y, half.z * z);
                result.Encapsulate(root.InverseTransformPoint(box.transform.TransformPoint(corner)));
            }

            return result;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            GameObject child = new(name);
            Undo.RegisterCreatedObjectUndo(child, "Create sliding door part");
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void CreateVerticalFrame(Transform parent, string name, Vector3 center, float z, float depth, float height, float frame, Material material)
        {
            CreatePart(parent, name, new Vector3(center.x, center.y, z), new Vector3(depth, height, frame), material, false, false);
        }

        private static Transform CreateDoorPanel(
            Transform parent,
            string name,
            Vector3 position,
            float width,
            float height,
            float depth,
            float glassDepth,
            float frame,
            Material frameMaterial,
            Material glassMaterial)
        {
            Transform panel = NewChild(parent, name);
            panel.localPosition = position;

            GameObject glass = CreatePart(panel, "Glass", Vector3.zero, new Vector3(glassDepth, height - frame, width - frame * 1.5f), glassMaterial, false, false);
            SetGlassRendering(glass);
            CreatePart(panel, "Frame_Left", new Vector3(0f, 0f, -width * 0.5f + frame * 0.5f), new Vector3(depth, height, frame), frameMaterial, false, false);
            CreatePart(panel, "Frame_Right", new Vector3(0f, 0f, width * 0.5f - frame * 0.5f), new Vector3(depth, height, frame), frameMaterial, false, false);
            CreatePart(panel, "Frame_Top", new Vector3(0f, height * 0.5f - frame * 0.5f, 0f), new Vector3(depth, frame, width), frameMaterial, false, false);
            CreatePart(panel, "Frame_Bottom", new Vector3(0f, -height * 0.5f + frame * 0.5f, 0f), new Vector3(depth, frame, width), frameMaterial, false, false);
            return panel;
        }

        private static GameObject CreatePart(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool addCollider,
            bool trigger)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(part, "Create sliding door mesh");
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;

            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            BoxCollider collider = part.GetComponent<BoxCollider>();
            if (!addCollider)
                Object.DestroyImmediate(collider);
            else
                collider.isTrigger = trigger;

            return part;
        }

        private static void SetGlassRendering(GameObject glass)
        {
            MeshRenderer renderer = glass.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}

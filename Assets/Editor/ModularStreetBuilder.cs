using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class ModularStreetBuilder
    {
        private const float RoadWidth = 10f;
        private const float SegmentLength = 8f;
        private const float TotalLength = 64f;
        private const float SidewalkWidth = 3.25f;
        private const float SidewalkHeight = 0.18f;
        private const float CurbWidth = 0.28f;
        private const float CurbHeight = 0.16f;
        private const float GutterWidth = 0.32f;

        [MenuItem("Tools/CYDOY/Build Premium Modular Street")]
        public static void BuildPremiumStreet()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("CYDOY", "No active scene found.", "OK");
                return;
            }

            GameObject visualRoot = GameObject.Find("VisualPrototype");
            if (visualRoot == null)
            {
                visualRoot = new GameObject("VisualPrototype");
            }

            RemoveLegacyStreet(visualRoot.transform);

            Transform old = visualRoot.transform.Find("ModularStreet");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            GameObject streetRoot = new("ModularStreet");
            streetRoot.transform.SetParent(visualRoot.transform, false);

            Material asphaltA = Mat("Street_Asphalt_A", new Color(0.105f, 0.112f, 0.118f), 0.82f);
            Material asphaltB = Mat("Street_Asphalt_B", new Color(0.095f, 0.102f, 0.109f), 0.84f);
            Material asphaltEdge = Mat("Street_Asphalt_Edge", new Color(0.082f, 0.087f, 0.092f), 0.88f);
            Material curb = Mat("Street_Curb", new Color(0.49f, 0.50f, 0.49f), 0.68f);
            Material curbTop = Mat("Street_Curb_Top", new Color(0.59f, 0.60f, 0.58f), 0.62f);
            Material pavingA = Mat("Street_Paving_A", new Color(0.43f, 0.44f, 0.43f), 0.76f);
            Material pavingB = Mat("Street_Paving_B", new Color(0.38f, 0.39f, 0.39f), 0.78f);
            Material joint = Mat("Street_Paving_Joint", new Color(0.19f, 0.20f, 0.20f), 0.90f);
            Material marking = Mat("Street_Marking", new Color(0.86f, 0.85f, 0.80f), 0.58f);
            Material drain = Mat("Street_Drain", new Color(0.075f, 0.08f, 0.085f), 0.92f);

            int segmentCount = Mathf.CeilToInt(TotalLength / SegmentLength);
            float startX = -segmentCount * SegmentLength * 0.5f + SegmentLength * 0.5f;

            for (int i = 0; i < segmentCount; i++)
            {
                float centerX = startX + i * SegmentLength;
                BuildStraightSegment(streetRoot.transform, i, centerX, asphaltA, asphaltB, asphaltEdge, curb, curbTop, pavingA, pavingB, joint, marking, drain);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrWhiteSpace(scene.path))
                EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = streetRoot;
            EditorGUIUtility.PingObject(streetRoot);
            EditorUtility.DisplayDialog("CYDOY", "Premium modular street installed.\n\n64 m road, raised curbs, wide sidewalks, paving seams, gutters and drains are now in the scene.", "Nice");
        }

        private static void BuildStraightSegment(
            Transform parent,
            int index,
            float centerX,
            Material asphaltA,
            Material asphaltB,
            Material asphaltEdge,
            Material curb,
            Material curbTop,
            Material pavingA,
            Material pavingB,
            Material joint,
            Material marking,
            Material drain)
        {
            GameObject module = new($"StreetModule_{index:00}");
            module.transform.SetParent(parent, false);
            module.transform.position = new Vector3(centerX, 0f, 0f);

            Material asphalt = index % 2 == 0 ? asphaltA : asphaltB;

            // Road body: top is around y=0.03, giving the car a clean physical surface.
            Box(module.transform, "Asphalt", new Vector3(0f, -0.045f, 0f), new Vector3(SegmentLength + 0.025f, 0.15f, RoadWidth), asphalt);

            // Slightly darker wheel-edge zones stop the road from looking like one flat black slab.
            Box(module.transform, "AsphaltEdge_N", new Vector3(0f, 0.033f, -RoadWidth * 0.5f + GutterWidth * 0.5f), new Vector3(SegmentLength, 0.012f, GutterWidth), asphaltEdge, false);
            Box(module.transform, "AsphaltEdge_S", new Vector3(0f, 0.033f, RoadWidth * 0.5f - GutterWidth * 0.5f), new Vector3(SegmentLength, 0.012f, GutterWidth), asphaltEdge, false);

            BuildSide(module.transform, -1f, index, curb, curbTop, pavingA, pavingB, joint, drain);
            BuildSide(module.transform, 1f, index, curb, curbTop, pavingA, pavingB, joint, drain);

            // German-style broken centre line. Every 8 m module contains one 3 m dash.
            Box(module.transform, "CenterDash", new Vector3(0f, 0.043f, 0f), new Vector3(3.0f, 0.018f, 0.13f), marking, false);

            // Extremely subtle longitudinal seam, gives the asphalt a constructed road feel.
            if (index > 0)
                Box(module.transform, "ModuleJoint", new Vector3(-SegmentLength * 0.5f, 0.041f, 0f), new Vector3(0.022f, 0.008f, RoadWidth - 0.5f), asphaltEdge, false);
        }

        private static void BuildSide(
            Transform module,
            float side,
            int segmentIndex,
            Material curb,
            Material curbTop,
            Material pavingA,
            Material pavingB,
            Material joint,
            Material drain)
        {
            float roadEdge = RoadWidth * 0.5f;
            float curbCenterZ = side * (roadEdge + CurbWidth * 0.5f);
            float sidewalkCenterZ = side * (roadEdge + CurbWidth + SidewalkWidth * 0.5f);

            // Vertical curb body and a slightly lighter top cap.
            Box(module, side < 0 ? "Curb_N" : "Curb_S", new Vector3(0f, CurbHeight * 0.5f, curbCenterZ), new Vector3(SegmentLength, CurbHeight, CurbWidth), curb);
            Box(module, side < 0 ? "CurbTop_N" : "CurbTop_S", new Vector3(0f, CurbHeight + 0.008f, curbCenterZ), new Vector3(SegmentLength, 0.016f, CurbWidth + 0.02f), curbTop, false);

            // Wide pedestrian zone: >3 m, enough for two characters side by side.
            Box(module, side < 0 ? "SidewalkBase_N" : "SidewalkBase_S", new Vector3(0f, SidewalkHeight * 0.5f, sidewalkCenterZ), new Vector3(SegmentLength, SidewalkHeight, SidewalkWidth), pavingA);

            // Paving strips: subtle 1 m slabs with alternating grey values.
            const int slabCount = 8;
            float slabLength = SegmentLength / slabCount;
            for (int i = 0; i < slabCount; i++)
            {
                float x = -SegmentLength * 0.5f + slabLength * 0.5f + i * slabLength;
                Material slabMat = ((i + segmentIndex) & 1) == 0 ? pavingA : pavingB;
                Box(module, $"Paver_{(side < 0 ? "N" : "S")}_{i:00}", new Vector3(x, SidewalkHeight + 0.012f, sidewalkCenterZ), new Vector3(slabLength - 0.025f, 0.024f, SidewalkWidth - 0.06f), slabMat, false);

                if (i > 0)
                    Box(module, $"PaverJoint_{(side < 0 ? "N" : "S")}_{i:00}", new Vector3(x - slabLength * 0.5f, SidewalkHeight + 0.026f, sidewalkCenterZ), new Vector3(0.018f, 0.006f, SidewalkWidth - 0.10f), joint, false);
            }

            // Longitudinal inner paving seam near curb.
            float innerJointZ = side * (roadEdge + CurbWidth + 0.72f);
            Box(module, side < 0 ? "InnerJoint_N" : "InnerJoint_S", new Vector3(0f, SidewalkHeight + 0.027f, innerJointZ), new Vector3(SegmentLength, 0.006f, 0.018f), joint, false);

            // One storm drain every second module, aligned to gutter and intentionally flat.
            if (segmentIndex % 2 == 1)
            {
                float drainZ = side * (roadEdge - 0.12f);
                Box(module, side < 0 ? "Drain_N" : "Drain_S", new Vector3(1.7f, 0.044f, drainZ), new Vector3(0.70f, 0.02f, 0.30f), drain, false);
                for (int slot = -2; slot <= 2; slot++)
                {
                    float sx = 1.7f + slot * 0.11f;
                    Box(module, $"DrainSlot_{(side < 0 ? "N" : "S")}_{slot + 2}", new Vector3(sx, 0.056f, drainZ), new Vector3(0.035f, 0.008f, 0.23f), joint, false);
                }
            }
        }

        private static void RemoveLegacyStreet(Transform visualRoot)
        {
            for (int i = visualRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = visualRoot.GetChild(i);
                string n = child.name;
                if (n == "Road" || n == "SidewalkNorth" || n == "SidewalkSouth" || n.StartsWith("LaneMark"))
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        private static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, bool collider = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            Collider c = go.GetComponent<Collider>();
            if (!collider && c != null)
                Object.DestroyImmediate(c);

            return go;
        }

        private static Material Mat(string name, Color color, float smoothness)
        {
            string folder = "Assets/Materials/Prototype";
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Materials", "Prototype");

            string path = $"{folder}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}

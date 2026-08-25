using System.Text;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class AJMeshAnalyzer
    {
        private const string CharacterPath = "Assets/Models/Characters/Aj.fbx";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";

        [MenuItem("Tools/CYDOY/Analyze AJ Mesh & Materials")]
        public static void Analyze()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject rawAj = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);

            if (playerPrefab == null || rawAj == null)
            {
                EditorUtility.DisplayDialog(
                    "CYDOY · AJ Analyzer",
                    "Player.prefab or Aj.fbx is missing.",
                    "OK");
                return;
            }

            Transform playerAj = FindRecursive(playerPrefab.transform, "Mixamo_AJ");
            if (playerAj == null)
            {
                EditorUtility.DisplayDialog(
                    "CYDOY · AJ Analyzer",
                    "Mixamo_AJ was not found inside Player.prefab.",
                    "OK");
                return;
            }

            StringBuilder report = new();
            report.AppendLine("================ CYDOY AJ ANALYZER ================");
            report.AppendLine("PLAYER AJ: " + GetPath(playerAj));
            report.AppendLine();

            int rendererCount = 0;
            int skinnedCount = 0;
            int materialSlots = 0;
            int texturedSlots = 0;
            int backpackNamedObjects = 0;
            bool likelySingleAtlas = false;

            Renderer[] renderers = playerAj.GetComponentsInChildren<Renderer>(true);
            report.AppendLine($"RENDERERS: {renderers.Length}");
            report.AppendLine();

            foreach (Renderer renderer in renderers)
            {
                rendererCount++;
                bool skinned = renderer is SkinnedMeshRenderer;
                if (skinned) skinnedCount++;

                report.AppendLine("--------------------------------------------------");
                report.AppendLine($"Renderer: {GetPath(renderer.transform)}");
                report.AppendLine($"Type: {renderer.GetType().Name}");

                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer smr)
                    mesh = smr.sharedMesh;
                else if (renderer.TryGetComponent<MeshFilter>(out MeshFilter filter))
                    mesh = filter.sharedMesh;

                if (mesh != null)
                {
                    report.AppendLine($"Mesh: {mesh.name}");
                    report.AppendLine($"Vertices: {mesh.vertexCount}");
                    report.AppendLine($"SubMeshes: {mesh.subMeshCount}");
                }
                else
                {
                    report.AppendLine("Mesh: <none>");
                }

                Material[] mats = renderer.sharedMaterials;
                report.AppendLine($"Material Slots: {mats.Length}");

                for (int i = 0; i < mats.Length; i++)
                {
                    materialSlots++;
                    Material mat = mats[i];
                    if (mat == null)
                    {
                        report.AppendLine($"  [{i}] <NULL>");
                        continue;
                    }

                    Texture baseTex = GetBaseTexture(mat);
                    if (baseTex != null) texturedSlots++;

                    report.AppendLine($"  [{i}] Material: {mat.name}");
                    report.AppendLine($"      Shader: {(mat.shader != null ? mat.shader.name : "<none>")}");
                    report.AppendLine($"      Base Texture: {(baseTex != null ? baseTex.name : "<none>")}");

                    string key = (renderer.name + " " + mat.name).ToLowerInvariant();
                    report.AppendLine($"      Guess: {GuessSlotType(key)}");
                }
            }

            report.AppendLine();
            report.AppendLine("================ HIERARCHY / BACKPACK CHECK ================");
            foreach (Transform t in playerAj.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (IsBackpackName(n))
                {
                    backpackNamedObjects++;
                    report.AppendLine($"Backpack candidate: {GetPath(t)} | active={t.gameObject.activeSelf}");
                }
            }

            if (backpackNamedObjects == 0)
            {
                report.AppendLine("No Transform named Backpack/Bag/Rucksack was found.");
                report.AppendLine("=> The backpack is likely fused into a SkinnedMeshRenderer or uses an unrelated object name.");
            }

            if (skinnedCount == 1 && materialSlots <= 2)
                likelySingleAtlas = true;

            report.AppendLine();
            report.AppendLine("================ SUMMARY ================");
            report.AppendLine($"Renderers: {rendererCount}");
            report.AppendLine($"SkinnedMeshRenderers: {skinnedCount}");
            report.AppendLine($"Material slots total: {materialSlots}");
            report.AppendLine($"Slots with textures: {texturedSlots}");
            report.AppendLine($"Backpack-named objects: {backpackNamedObjects}");
            report.AppendLine($"Likely single combined character atlas: {likelySingleAtlas}");

            if (likelySingleAtlas)
            {
                report.AppendLine();
                report.AppendLine("IMPORTANT: AJ appears to use one combined mesh/material atlas.");
                report.AppendLine("In that case hoodie/pants/skin cannot be recolored independently by MaterialPropertyBlock alone.");
                report.AppendLine("We would need either UV-region recoloring, a mask texture, or separate clothing meshes/materials.");
            }

            report.AppendLine();
            report.AppendLine("RAW FBX ROOT: " + rawAj.name);
            report.AppendLine("============================================================");

            string text = report.ToString();
            Debug.Log(text);
            EditorGUIUtility.systemCopyBuffer = text;

            EditorUtility.DisplayDialog(
                "CYDOY · AJ Analyzer",
                "Analysis complete.\n\nThe full report is in the Console and has also been copied to your clipboard.\n\nSend me the SUMMARY plus the renderer/material section and I can wire the clothing colors and backpack removal precisely.",
                "OK");
        }

        private static Texture GetBaseTexture(Material mat)
        {
            if (mat.HasProperty("_BaseMap"))
            {
                Texture t = mat.GetTexture("_BaseMap");
                if (t != null) return t;
            }

            if (mat.HasProperty("_MainTex"))
            {
                Texture t = mat.GetTexture("_MainTex");
                if (t != null) return t;
            }

            return null;
        }

        private static string GuessSlotType(string key)
        {
            if (key.Contains("skin") || key.Contains("face") || key.Contains("head") || key.Contains("hand") || key.Contains("hair"))
                return "skin/face/hair";
            if (key.Contains("shirt") || key.Contains("hoodie") || key.Contains("sweater") || key.Contains("jacket") || key.Contains("top") || key.Contains("torso"))
                return "upper clothing";
            if (key.Contains("pants") || key.Contains("trouser") || key.Contains("jeans") || key.Contains("shorts") || key.Contains("lower"))
                return "lower clothing";
            if (key.Contains("shoe") || key.Contains("sneaker") || key.Contains("boot"))
                return "shoes";
            if (IsBackpackName(key))
                return "backpack/bag";
            return "unknown / possibly combined atlas";
        }

        private static bool IsBackpackName(string n)
        {
            return n.Contains("backpack") || n.Contains("back_pack") || n.Contains("rucksack") ||
                   n == "bag" || n.Contains("shoulderbag") || n.Contains("back bag");
        }

        private static Transform FindRecursive(Transform root, string targetName)
        {
            if (root.name == targetName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindRecursive(root.GetChild(i), targetName);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            Transform current = t.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
    }
}

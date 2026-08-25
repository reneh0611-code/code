using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class AJBackpackUtility
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";

        [MenuItem("Tools/CYDOY/Remove AJ Backpack")]
        public static void RemoveBackpack()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                EditorUtility.DisplayDialog("CYDOY · AJ Backpack", "Player.prefab not found.", "OK");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            int hiddenBones = 0;
            int hiddenRenderers = 0;

            try
            {
                Transform aj = FindRecursive(root.transform, "Mixamo_AJ");
                if (aj == null)
                {
                    EditorUtility.DisplayDialog("CYDOY · AJ Backpack", "Mixamo_AJ was not found inside Player.prefab.", "OK");
                    return;
                }

                // Hide the obvious backpack bones/objects first.
                foreach (Transform t in aj.GetComponentsInChildren<Transform>(true))
                {
                    if (t == aj)
                        continue;

                    if (IsBackpackToken(t.name))
                    {
                        t.gameObject.SetActive(false);
                        hiddenBones++;
                    }
                }

                // More important: disable the SkinnedMeshRenderer that actually draws the backpack.
                SkinnedMeshRenderer[] renderers = aj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (SkinnedMeshRenderer renderer in renderers)
                {
                    if (renderer == null)
                        continue;

                    if (IsBackpackRenderer(renderer))
                    {
                        renderer.enabled = false;
                        hiddenRenderers++;
                        Debug.Log($"[CYDOY] Disabled backpack renderer: {GetPath(renderer.transform)} | mesh={renderer.sharedMesh?.name}");
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "CYDOY · AJ Backpack",
                $"Backpack cleanup complete.\n\nBackpack bones/objects hidden: {hiddenBones}\nBackpack renderers disabled: {hiddenRenderers}\n\nIf renderer count is 0, send me the four renderer names from the AJ Analyzer and I can target the exact mesh by name.",
                "OK");
        }

        public static bool IsBackpackRenderer(SkinnedMeshRenderer renderer)
        {
            if (renderer == null)
                return false;

            string rendererName = renderer.name ?? string.Empty;
            string meshName = renderer.sharedMesh != null ? renderer.sharedMesh.name : string.Empty;
            string materialNames = string.Join(" ", renderer.sharedMaterials.Where(m => m != null).Select(m => m.name));
            string key = (rendererName + " " + meshName + " " + materialNames).ToLowerInvariant();

            if (IsBackpackToken(key))
                return true;

            // Some FBXs give the visible backpack mesh a generic name but bind it to backpack bones.
            Transform[] bones = renderer.bones;
            if (bones == null || bones.Length == 0)
                return false;

            int backpackBones = 0;
            foreach (Transform bone in bones)
            {
                if (bone != null && IsBackpackToken(bone.name))
                    backpackBones++;
            }

            // A dedicated accessory renderer usually has a meaningful share of its bone list on the backpack.
            return backpackBones >= 2 && backpackBones >= Mathf.CeilToInt(bones.Length * 0.12f);
        }

        private static bool IsBackpackToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string n = value.ToLowerInvariant();
            return n.Contains("backpack") || n.Contains("back_pack") || n.Contains("rucksack") ||
                   n.Contains("shoulderbag") || n.Contains("back bag") || n == "bag";
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
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}

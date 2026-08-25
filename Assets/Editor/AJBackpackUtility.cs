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
            int hidden = 0;

            try
            {
                Transform aj = FindRecursive(root.transform, "Mixamo_AJ");
                if (aj == null)
                {
                    EditorUtility.DisplayDialog("CYDOY · AJ Backpack", "Mixamo_AJ was not found inside Player.prefab.", "OK");
                    return;
                }

                foreach (Transform t in aj.GetComponentsInChildren<Transform>(true))
                {
                    if (t == aj)
                        continue;

                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("backpack") || n.Contains("back_pack") || n.Contains("rucksack") || n == "bag" || n.Contains("shoulderbag"))
                    {
                        t.gameObject.SetActive(false);
                        hidden++;
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
                hidden > 0
                    ? $"Backpack removed from AJ. Hidden objects: {hidden}."
                    : "No object with Backpack/Bag/Rucksack in its name was found. If the backpack is fused into the body mesh, it cannot be removed cleanly by hierarchy name alone.",
                "OK");
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
    }
}

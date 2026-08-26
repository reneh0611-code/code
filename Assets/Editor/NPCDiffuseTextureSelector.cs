using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class NPCDiffuseTextureSelector
    {
        [MenuItem("Tools/CYDOY/Select Selected NPC Diffuse Texture")]
        public static void SelectDiffuseTexture()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("CYDOY · NPC Texture", "Select one NPC in the Hierarchy first.", "OK");
                return;
            }

            SkinnedMeshRenderer renderer = selected.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer == null)
            {
                EditorUtility.DisplayDialog("CYDOY · NPC Texture", "No SkinnedMeshRenderer found on the selected NPC.", "OK");
                return;
            }

            Material material = renderer.sharedMaterial;
            if (material == null)
            {
                EditorUtility.DisplayDialog("CYDOY · NPC Texture", "The NPC renderer has no material.", "OK");
                return;
            }

            Texture texture = null;
            if (material.HasProperty("_BaseMap"))
                texture = material.GetTexture("_BaseMap");
            if (texture == null && material.HasProperty("_MainTex"))
                texture = material.GetTexture("_MainTex");

            if (texture == null)
            {
                EditorUtility.DisplayDialog("CYDOY · NPC Texture", "No BaseMap/MainTex texture was found on the NPC material.", "OK");
                return;
            }

            Selection.activeObject = texture;
            EditorGUIUtility.PingObject(texture);

            string path = AssetDatabase.GetAssetPath(texture);
            Debug.Log($"[CYDOY] NPC diffuse texture selected: {texture.name} | {path}");

            EditorUtility.DisplayDialog(
                "CYDOY · NPC Texture",
                $"Selected texture: {texture.name}\n\nPath: {path}\n\nOpen it in the Inspector and send me a screenshot of the full texture atlas. Then I can build clothing-only color masks while preserving skin exactly.",
                "OK");
        }
    }
}

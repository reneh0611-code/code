using System.Text;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class NPCClothingRendererAnalyzer
    {
        [MenuItem("Tools/CYDOY/Analyze Selected NPC Clothing Renderers")]
        public static void AnalyzeSelected()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("CYDOY · NPC Renderer Analyzer", "Select one NPC in the Hierarchy first.", "OK");
                return;
            }

            SkinnedMeshRenderer[] renderers = selected.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0)
            {
                EditorUtility.DisplayDialog("CYDOY · NPC Renderer Analyzer", "No SkinnedMeshRenderer found on selected object.", "OK");
                return;
            }

            StringBuilder sb = new();
            sb.AppendLine("========== CYDOY NPC RENDERERS ==========");
            sb.AppendLine("NPC: " + selected.name);
            sb.AppendLine("Renderer count: " + renderers.Length);
            sb.AppendLine();

            for (int i = 0; i < renderers.Length; i++)
            {
                SkinnedMeshRenderer r = renderers[i];
                sb.AppendLine($"RENDERER {i + 1}");
                sb.AppendLine("Name: " + r.name);
                sb.AppendLine("Mesh: " + (r.sharedMesh != null ? r.sharedMesh.name : "<none>"));
                sb.AppendLine("Bounds center: " + r.bounds.center.ToString("F3"));
                sb.AppendLine("Bounds size: " + r.bounds.size.ToString("F3"));

                Material[] mats = r.sharedMaterials;
                sb.AppendLine("Material slots: " + mats.Length);
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    sb.AppendLine($"  Material {m + 1}: {(mat != null ? mat.name : "<null>")}");
                    if (mat != null)
                    {
                        Texture tex = null;
                        if (mat.HasProperty("_BaseMap")) tex = mat.GetTexture("_BaseMap");
                        if (tex == null && mat.HasProperty("_MainTex")) tex = mat.GetTexture("_MainTex");
                        sb.AppendLine("  Texture: " + (tex != null ? tex.name : "<none>"));
                    }
                }
                sb.AppendLine();
            }

            sb.AppendLine("=========================================");

            string report = sb.ToString();
            Debug.Log(report);
            EditorGUIUtility.systemCopyBuffer = report;

            EditorUtility.DisplayDialog(
                "CYDOY · NPC Renderer Analyzer",
                "Done. The report is in the Console and copied to your clipboard.\n\nSend me the block from RENDERER 1 through RENDERER 4.",
                "OK");
        }
    }
}

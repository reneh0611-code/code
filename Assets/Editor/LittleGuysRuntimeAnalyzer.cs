using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class LittleGuysRuntimeAnalyzer
    {
        [MenuItem("Tools/CYDOY/Analyze Selected Little Guy Runtime")]
        public static void AnalyzeSelected()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Little Guys", "Select one spawned Little Guys NPC in the Hierarchy first.", "OK");
                return;
            }

            StringBuilder sb = new();
            sb.AppendLine("========== CYDOY LITTLE GUY RUNTIME ==========");
            sb.AppendLine("Object: " + selected.name);
            sb.AppendLine("Root localScale: " + selected.transform.localScale.ToString("F4"));
            sb.AppendLine("Root lossyScale: " + selected.transform.lossyScale.ToString("F4"));
            sb.AppendLine("Root position: " + selected.transform.position.ToString("F4"));
            sb.AppendLine();

            Animator animator = selected.GetComponentInChildren<Animator>(true);
            sb.AppendLine("Animator: " + (animator != null ? "YES" : "NO"));
            if (animator != null)
            {
                sb.AppendLine("Animator enabled: " + animator.enabled);
                sb.AppendLine("Avatar: " + (animator.avatar != null ? animator.avatar.name : "<none>"));
                sb.AppendLine("Avatar valid: " + (animator.avatar != null && animator.avatar.isValid));
                sb.AppendLine("Avatar humanoid: " + (animator.avatar != null && animator.avatar.isHuman));
                sb.AppendLine("Controller: " + (animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "<none>"));
            }
            sb.AppendLine();

            SkinnedMeshRenderer[] renderers = selected.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            sb.AppendLine("SkinnedMeshRenderers: " + renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                sb.AppendLine($"RENDERER {i + 1}: {r.name}");
                sb.AppendLine("  enabled: " + r.enabled);
                sb.AppendLine("  activeInHierarchy: " + r.gameObject.activeInHierarchy);
                sb.AppendLine("  mesh: " + (r.sharedMesh != null ? r.sharedMesh.name : "<none>"));
                sb.AppendLine("  bounds center: " + r.bounds.center.ToString("F4"));
                sb.AppendLine("  bounds size: " + r.bounds.size.ToString("F4"));
                sb.AppendLine("  localScale: " + r.transform.localScale.ToString("F4"));
                sb.AppendLine("  lossyScale: " + r.transform.lossyScale.ToString("F4"));

                Material[] mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    sb.AppendLine($"  MATERIAL {m + 1}: " + (mat != null ? mat.name : "<null>"));
                    if (mat == null) continue;
                    sb.AppendLine("    shader: " + (mat.shader != null ? mat.shader.name : "<none>"));
                    sb.AppendLine("    renderQueue: " + mat.renderQueue);
                    if (mat.HasProperty("_Surface")) sb.AppendLine("    _Surface: " + mat.GetFloat("_Surface"));
                    if (mat.HasProperty("_Mode")) sb.AppendLine("    _Mode: " + mat.GetFloat("_Mode"));
                    if (mat.HasProperty("_BaseColor")) sb.AppendLine("    _BaseColor: " + mat.GetColor("_BaseColor"));
                    if (mat.HasProperty("_Color")) sb.AppendLine("    _Color: " + mat.GetColor("_Color"));
                    Texture tex = null;
                    if (mat.HasProperty("_BaseMap")) tex = mat.GetTexture("_BaseMap");
                    if (tex == null && mat.HasProperty("_MainTex")) tex = mat.GetTexture("_MainTex");
                    sb.AppendLine("    texture: " + (tex != null ? tex.name : "<none>"));
                }
            }

            sb.AppendLine();
            sb.AppendLine("--- Animation clips inside Little Guys sample ---");
            string[] clipGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/LuceedStudio/Character Lab/Little Guys" });
            var clips = clipGuids
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Distinct()
                .Take(80)
                .ToArray();
            sb.AppendLine("Animation asset paths found: " + clips.Length);
            foreach (string path in clips)
                sb.AppendLine("  " + path);

            sb.AppendLine("==============================================");

            string report = sb.ToString();
            Debug.Log(report);
            EditorGUIUtility.systemCopyBuffer = report;
            EditorUtility.DisplayDialog("CYDOY · Little Guys", "Runtime report copied to clipboard. Send me the report.", "OK");
        }
    }
}

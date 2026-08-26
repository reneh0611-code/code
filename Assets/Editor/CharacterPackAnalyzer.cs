using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class CharacterPackAnalyzer
    {
        [MenuItem("Tools/CYDOY/Analyze Imported Character Pack")]
        public static void Analyze()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" });
            var candidates = guids
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Where(p => p.EndsWith(".prefab") || p.EndsWith(".fbx"))
                .Select(p => new { path = p, go = AssetDatabase.LoadAssetAtPath<GameObject>(p) })
                .Where(x => x.go != null)
                .Where(x => x.go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0)
                .OrderByDescending(x => System.IO.File.GetLastWriteTimeUtc(System.IO.Path.GetFullPath(x.path)))
                .Take(40)
                .ToArray();

            StringBuilder sb = new();
            sb.AppendLine("========== CYDOY CHARACTER PACK ANALYZER ==========");
            sb.AppendLine($"Humanoid-looking assets found: {candidates.Length}");
            sb.AppendLine();

            for (int i = 0; i < candidates.Length; i++)
            {
                var c = candidates[i];
                SkinnedMeshRenderer[] renderers = c.go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Animator animator = c.go.GetComponentInChildren<Animator>(true);

                sb.AppendLine($"ASSET {i + 1}");
                sb.AppendLine("Path: " + c.path);
                sb.AppendLine("Name: " + c.go.name);
                sb.AppendLine("SkinnedMeshRenderers: " + renderers.Length);
                sb.AppendLine("Animator: " + (animator != null ? "YES" : "NO"));
                if (animator != null)
                    sb.AppendLine("Avatar: " + (animator.avatar != null ? animator.avatar.name : "<none>"));

                for (int r = 0; r < renderers.Length; r++)
                {
                    var smr = renderers[r];
                    sb.AppendLine($"  Renderer {r + 1}: {smr.name}");
                    sb.AppendLine("    Mesh: " + (smr.sharedMesh != null ? smr.sharedMesh.name : "<none>"));
                    sb.AppendLine("    Materials: " + string.Join(", ", smr.sharedMaterials.Where(m => m != null).Select(m => m.name)));
                }

                sb.AppendLine();
            }

            sb.AppendLine("====================================================");
            string report = sb.ToString();
            Debug.Log(report);
            EditorGUIUtility.systemCopyBuffer = report;

            EditorUtility.DisplayDialog(
                "CYDOY · Character Pack",
                "Character pack scan complete. The report is in the Console and copied to your clipboard.\n\nSend me the first relevant character entries and I can wire the pack into the NPC system without touching your working player.",
                "OK");
        }
    }
}

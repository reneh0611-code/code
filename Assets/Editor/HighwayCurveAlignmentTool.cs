using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using CheatOnYourDayOnes.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class HighwayCurveAlignmentTool
{
    private const string ModulePrefix = "PIVOT_323_modular_kit318";
    private const float FlatTiltTolerance = 0.11f;
    private const float NeighborRadius = 18f;
    private const float MaximumHeightCorrection = 0.065f;

    [MenuItem("Tools/Roads/Preview Clean All Road Transitions")]
    private static void PreviewAllRoadTransitions()
    {
        List<RoadAdjustment> adjustments = CalculateConservativeAdjustments();
        WriteAdjustmentPreview(adjustments);
        Debug.Log($"[Road Cleanup Preview] {adjustments.Count} conservative adjustments. No scene objects were changed.");
    }

    [MenuItem("Tools/Roads/Apply Clean All Road Transitions")]
    private static void ApplyAllRoadTransitions()
    {
        List<RoadAdjustment> adjustments = CalculateConservativeAdjustments();
        if (adjustments.Count == 0)
        {
            Debug.Log("[Road Cleanup] No safe centimeter-scale adjustments were necessary.");
            return;
        }

        Transform[] transforms = adjustments.Select(a => a.Transform).Distinct().ToArray();
        Undo.RecordObjects(transforms, "Clean all road transitions");

        foreach (RoadAdjustment adjustment in adjustments)
        {
            Transform transform = adjustment.Transform;
            Vector3 position = transform.position;
            position.y = adjustment.TargetY;
            transform.position = position;

            Vector3 euler = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
            EditorUtility.SetDirty(transform);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.objects = transforms.Select(t => t.gameObject).ToArray();
        Debug.Log($"[Road Cleanup] Applied {adjustments.Count} conservative adjustments. Layout X/Z and intentional slopes were preserved. Undo is available.");
    }

    [MenuItem("Tools/Roads/Audit All Road Modules")]
    private static void AuditAllRoadModules()
    {
        RoadModulePivot[] modules = UnityEngine.Object.FindObjectsByType<RoadModulePivot>(FindObjectsSortMode.None);
        StringBuilder report = new StringBuilder();
        report.AppendLine("Name\tX\tY\tZ\tRotX\tRotY\tRotZ\tSurfaceMinY\tSurfaceMaxY\tColliderCount");

        foreach (RoadModulePivot module in modules.OrderBy(m => m.transform.position.x).ThenBy(m => m.transform.position.z))
        {
            Transform transform = module.transform;
            Renderer[] renderers = module.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = module.GetComponentsInChildren<Collider>(true);
            float minY = renderers.Length == 0 ? transform.position.y : renderers.Min(r => r.bounds.min.y);
            float maxY = renderers.Length == 0 ? transform.position.y : renderers.Max(r => r.bounds.max.y);
            Vector3 euler = NormalizeEuler(transform.eulerAngles);

            report.AppendLine(string.Join("\t",
                transform.name,
                transform.position.x.ToString("F4"),
                transform.position.y.ToString("F4"),
                transform.position.z.ToString("F4"),
                euler.x.ToString("F3"),
                euler.y.ToString("F3"),
                euler.z.ToString("F3"),
                minY.ToString("F4"),
                maxY.ToString("F4"),
                colliders.Length));
        }

        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/RoadAlignmentAudit.tsv"));
        File.WriteAllText(path, report.ToString());
        Debug.Log($"[Road Audit] Wrote {modules.Length} road modules to {path}");
    }

    [MenuItem("Tools/Roads/Audit 90 Degree Connections")]
    private static void AuditRightAngleConnections()
    {
        RoadModulePivot[] modules = UnityEngine.Object.FindObjectsByType<RoadModulePivot>(FindObjectsSortMode.None)
            .Where(m => m.gameObject.scene == EditorSceneManager.GetActiveScene())
            .Where(m => m.transform.parent == null)
            .ToArray();
        StringBuilder report = new StringBuilder();
        report.AppendLine("A\tB\tAX\tAZ\tBX\tBZ\tAY\tBY\tYawDifference\tHorizontalGap");
        int count = 0;

        for (int i = 0; i < modules.Length; i++)
        {
            Bounds a = GetRendererBounds(modules[i].transform);
            for (int j = i + 1; j < modules.Length; j++)
            {
                Bounds b = GetRendererBounds(modules[j].transform);
                float yawDifference = Mathf.Abs(Mathf.DeltaAngle(modules[i].transform.eulerAngles.y, modules[j].transform.eulerAngles.y));
                if (yawDifference < 75f || yawDifference > 105f) continue;
                if (Mathf.Abs(modules[i].transform.position.y - modules[j].transform.position.y) > 0.15f) continue;

                float gapX = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
                float gapZ = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
                float gap = Mathf.Sqrt(gapX * gapX + gapZ * gapZ);
                if (gap > 0.35f) continue;

                report.AppendLine(string.Join("\t", modules[i].name, modules[j].name,
                    modules[i].transform.position.x.ToString("F3"), modules[i].transform.position.z.ToString("F3"),
                    modules[j].transform.position.x.ToString("F3"), modules[j].transform.position.z.ToString("F3"),
                    modules[i].transform.position.y.ToString("F3"), modules[j].transform.position.y.ToString("F3"),
                    yawDifference.ToString("F2"), gap.ToString("F4")));
                count++;
            }
        }

        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/RoadRightAngleAudit.tsv"));
        File.WriteAllText(path, report.ToString());
        Debug.Log($"[Road 90 Audit] Wrote {count} candidate connections to {path}");
    }

    private static Bounds GetRendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static Vector3 NormalizeEuler(Vector3 euler)
    {
        return new Vector3(NormalizeAngle(euler.x), NormalizeAngle(euler.y), NormalizeAngle(euler.z));
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }

    private static List<RoadAdjustment> CalculateConservativeAdjustments()
    {
        RoadModulePivot[] modules = UnityEngine.Object.FindObjectsByType<RoadModulePivot>(FindObjectsSortMode.None)
            .Where(m => m.gameObject.scene == EditorSceneManager.GetActiveScene())
            .Where(m => m.transform.parent == null)
            .ToArray();

        List<RoadAdjustment> result = new List<RoadAdjustment>();
        foreach (RoadModulePivot module in modules)
        {
            Transform transform = module.transform;
            Vector3 tilt = NormalizeEuler(transform.eulerAngles);
            if (Mathf.Abs(tilt.x) > FlatTiltTolerance || Mathf.Abs(tilt.z) > FlatTiltTolerance)
                continue;

            List<float> neighborHeights = modules
                .Where(other => other != module)
                .Where(other => other.transform.parent == null)
                .Where(other =>
                {
                    Vector3 otherTilt = NormalizeEuler(other.transform.eulerAngles);
                    return Mathf.Abs(otherTilt.x) <= FlatTiltTolerance && Mathf.Abs(otherTilt.z) <= FlatTiltTolerance;
                })
                .Where(other => HorizontalDistance(transform.position, other.transform.position) <= NeighborRadius)
                .Where(other => Mathf.Abs(other.transform.position.y - transform.position.y) <= MaximumHeightCorrection)
                .Select(other => other.transform.position.y)
                .ToList();

            if (neighborHeights.Count == 0)
                continue;

            neighborHeights.Add(transform.position.y);
            neighborHeights.Sort();
            float targetY = neighborHeights.Count % 2 == 0
                ? (neighborHeights[neighborHeights.Count / 2 - 1] + neighborHeights[neighborHeights.Count / 2]) * 0.5f
                : neighborHeights[neighborHeights.Count / 2];

            float correction = targetY - transform.position.y;
            bool heightNeedsChange = Mathf.Abs(correction) >= 0.002f && Mathf.Abs(correction) <= MaximumHeightCorrection;
            bool tiltNeedsChange = Mathf.Abs(tilt.x) >= 0.01f || Mathf.Abs(tilt.z) >= 0.01f;
            if (heightNeedsChange || tiltNeedsChange)
                result.Add(new RoadAdjustment(transform, targetY, correction, tilt));
        }

        return result;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    private static void WriteAdjustmentPreview(IEnumerable<RoadAdjustment> adjustments)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("Name\tX\tOldY\tZ\tTargetY\tCorrection\tRotX\tRotZ");
        foreach (RoadAdjustment adjustment in adjustments.OrderBy(a => a.Transform.position.x).ThenBy(a => a.Transform.position.z))
        {
            Vector3 position = adjustment.Transform.position;
            report.AppendLine(string.Join("\t", adjustment.Transform.name, position.x.ToString("F4"), position.y.ToString("F4"),
                position.z.ToString("F4"), adjustment.TargetY.ToString("F4"), adjustment.Correction.ToString("F4"),
                adjustment.Tilt.x.ToString("F3"), adjustment.Tilt.z.ToString("F3")));
        }

        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/RoadCleanupPreview.tsv"));
        File.WriteAllText(path, report.ToString());
    }

    private sealed class RoadAdjustment
    {
        internal readonly Transform Transform;
        internal readonly float TargetY;
        internal readonly float Correction;
        internal readonly Vector3 Tilt;

        internal RoadAdjustment(Transform transform, float targetY, float correction, Vector3 tilt)
        {
            Transform = transform;
            TargetY = targetY;
            Correction = correction;
            Tilt = tilt;
        }
    }

    [MenuItem("Tools/Roads/Clean Selected Highway Curve")]
    private static void CleanSelectedHighwayCurve()
    {
        Transform selected = Selection.activeTransform;
        if (selected == null || !selected.name.StartsWith(ModulePrefix, StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog(
                "Highway Curve",
                "Select one of the yellow-line highway curve pieces first.",
                "OK");
            return;
        }

        Vector3 anchor = selected.position;
        List<Transform> pieces = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t => t.parent == null)
            .Where(t => t.name.StartsWith(ModulePrefix, StringComparison.Ordinal))
            .Where(t => Mathf.Abs(t.position.x - anchor.x) <= 30f)
            .Where(t => Mathf.Abs(t.position.z - anchor.z) <= 45f)
            .OrderBy(t => t.position.z)
            .ToList();

        if (pieces.Count < 2)
        {
            EditorUtility.DisplayDialog(
                "Highway Curve",
                "No connected highway curve chain was found near the selected piece.",
                "OK");
            return;
        }

        float[] heights = pieces.Select(t => t.position.y).OrderBy(y => y).ToArray();
        float roadHeight = heights.Length % 2 == 0
            ? (heights[heights.Length / 2 - 1] + heights[heights.Length / 2]) * 0.5f
            : heights[heights.Length / 2];

        Undo.RecordObjects(pieces.Cast<UnityEngine.Object>().ToArray(), "Clean highway curve alignment");

        foreach (Transform piece in pieces)
        {
            Vector3 position = piece.position;
            position.y = roadHeight;
            piece.position = position;

            Vector3 rotation = piece.eulerAngles;
            piece.rotation = Quaternion.Euler(0f, rotation.y, 0f);
            EditorUtility.SetDirty(piece);
        }

        Selection.objects = pieces.Select(t => t.gameObject).ToArray();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"[Highway Curve] Aligned {pieces.Count} pieces at Y={roadHeight:F3}. Undo is available.");
    }
}

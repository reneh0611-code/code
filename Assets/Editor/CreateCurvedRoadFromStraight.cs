using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;

public static class CreateCurvedRoadFromStraight
{
    [MenuItem("Tools/ProBuilder/Create 90 Degree Road From Selected Straight", true)]
    static bool ValidateCreate() => Selection.activeGameObject != null &&
        Selection.activeGameObject.GetComponentInChildren<MeshFilter>() != null;

    [MenuItem("Tools/ProBuilder/Create 90 Degree Road From Selected Straight")]
    static void Create()
    {
        var selected = Selection.activeGameObject;
        var existingCurve = selected.GetComponent<ProBuilderMesh>();
        var source = selected;
        if (existingCurve != null && selected.name.EndsWith("_ProBuilder_Curve90"))
        {
            string originalName = selected.name.Substring(0, selected.name.Length - "_ProBuilder_Curve90".Length);
            source = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(go => go.scene == selected.scene && go.name == originalName && go != selected) ?? selected;
        }

        var renderer = source.GetComponent<MeshRenderer>() ?? source.GetComponentInChildren<MeshRenderer>();
        var sourceMaterials = renderer != null ? renderer.sharedMaterials.Where(m => m != null).ToHashSet() : null;
        var filters = source.GetComponentsInChildren<MeshFilter>()
            .Where(f =>
            {
                var r = f.GetComponent<MeshRenderer>();
                return r != null && sourceMaterials != null && r.sharedMaterials.Any(sourceMaterials.Contains);
            })
            .ToArray();
        if (filters.Length == 0 || renderer == null)
            return;

        var rootToWorld = source.transform.localToWorldMatrix;
        var worldToRoot = source.transform.worldToLocalMatrix;
        var all = new List<Vector3>();
        foreach (var filter in filters)
        {
            if (filter.sharedMesh == null) continue;
            var toRoot = worldToRoot * filter.transform.localToWorldMatrix;
            all.AddRange(filter.sharedMesh.vertices.Select(toRoot.MultiplyPoint3x4));
        }
        if (all.Count == 0) return;

        var min = all[0];
        var max = all[0];
        foreach (var p in all) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        var size = max - min;
        bool alongZ = size.z >= size.x;
        float length = alongZ ? size.z : size.x;
        float totalWidth = alongZ ? size.x : size.z;

        // Derive the asphalt width from material slot 1, which is the Roads
        // material on the source Synty road module.
        float asphaltMin = float.PositiveInfinity;
        float asphaltMax = float.NegativeInfinity;
        foreach (var filter in filters)
        {
            var mesh = filter.sharedMesh;
            if (mesh == null || mesh.subMeshCount < 2) continue;
            var toRoot = worldToRoot * filter.transform.localToWorldMatrix;
            foreach (int index in mesh.GetIndices(1))
            {
                var p = toRoot.MultiplyPoint3x4(mesh.vertices[index]);
                float lateral = alongZ ? p.x : p.z;
                asphaltMin = Mathf.Min(asphaltMin, lateral);
                asphaltMax = Mathf.Max(asphaltMax, lateral);
            }
        }
        float roadWidth = asphaltMax > asphaltMin ? asphaltMax - asphaltMin : totalWidth * 0.65f;
        roadWidth = Mathf.Clamp(roadWidth, totalWidth * 0.35f, totalWidth * 0.9f);

        float halfTotal = totalWidth * 0.5f;
        float halfRoad = roadWidth * 0.5f;
        float radius = Mathf.Max(length / (Mathf.PI * 0.5f), halfTotal * 1.25f);
        const int segments = 16;
        var vertices = new List<Vertex>();
        var faces = new List<Face>();

        AddStrip(-halfTotal, -halfRoad, 0);
        AddStrip(-halfRoad, halfRoad, Mathf.Min(1, renderer.sharedMaterials.Length - 1));
        AddStrip(halfRoad, halfTotal, 0);

        void AddStrip(float lateralA, float lateralB, int materialIndex)
        {
            for (int i = 0; i < segments; i++)
            {
                float t0 = (Mathf.PI * 0.5f) * i / segments;
                float t1 = (Mathf.PI * 0.5f) * (i + 1) / segments;
                int start = vertices.Count;
                AddVertex(t0, lateralA, 0f, i / (float)segments);
                AddVertex(t0, lateralB, 1f, i / (float)segments);
                AddVertex(t1, lateralA, 0f, (i + 1f) / segments);
                AddVertex(t1, lateralB, 1f, (i + 1f) / segments);
                var face = new Face(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
                face.submeshIndex = materialIndex;
                face.manualUV = true;
                faces.Add(face);
            }
        }

        void AddVertex(float angle, float lateral, float u, float v)
        {
            float x = radius * (1f - Mathf.Cos(angle)) + lateral * Mathf.Cos(angle) - radius * 0.5f;
            float z = radius * Mathf.Sin(angle) - lateral * Mathf.Sin(angle) - radius * 0.5f;
            var p = alongZ ? new Vector3(x, max.y, z) : new Vector3(z, max.y, x);
            vertices.Add(new Vertex { position = p, normal = Vector3.up, uv0 = new Vector2(u, v) });
        }

        var shared = SharedVertex.GetSharedVerticesWithPositions(vertices.Select(v => v.position).ToList());
        var curved = existingCurve ?? ProBuilderMesh.Create(vertices, faces, shared, null, renderer.sharedMaterials);
        if (existingCurve != null)
        {
            curved.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            curved.SetVertices(vertices);
            curved.faces = faces;
            curved.sharedVertices = shared;
        }
        curved.name = source.name + "_ProBuilder_Curve90";
        curved.transform.SetParent(source.transform.parent, false);
        curved.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        curved.transform.localScale = source.transform.localScale;
        curved.ToMesh();
        curved.Refresh();
        EditorMeshUtility.Optimize(curved);
        if (existingCurve == null)
            Undo.RegisterCreatedObjectUndo(curved.gameObject, "Create ProBuilder road curve");
        else
            Undo.RegisterCompleteObjectUndo(curved, "Rebuild ProBuilder road curve");
        Selection.activeGameObject = curved.gameObject;
        EditorGUIUtility.PingObject(curved.gameObject);
        EditorSceneManager.MarkSceneDirty(curved.gameObject.scene);
    }
}

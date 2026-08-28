#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class NeubeckumRoadNetworkBuilder
{
    private const string RootName = "Neubeckum_Road_Network";
    private const string SourcePath = "Assets/Editor/NeubeckumRoads.osm";
    private const string OutputFolder = "Assets/Generated/NeubeckumRoads";
    private const double CenterLatitude = 51.798;
    private const double CenterLongitude = 8.0265;
    private const float TerrainMargin = 45f;
    private static float mapScale = 1f;
    private static Vector2 mapOffset;

    private sealed class Node
    {
        public long Id;
        public double Latitude;
        public double Longitude;
    }

    private sealed class Road
    {
        public string Name;
        public string Highway;
        public readonly List<long> NodeIds = new List<long>();
        public float Width;
        public bool HasCenterLine;
    }

    private sealed class MeshBuffers
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector2> UVs = new List<Vector2>();
        public readonly List<int> SidewalkTriangles = new List<int>();
        public readonly List<int> AsphaltTriangles = new List<int>();
        public readonly List<int> MarkingTriangles = new List<int>();
    }

    [MenuItem("Day Ones/City/Build Neubeckum Road Network %#n")]
    public static void Build()
    {
        string absoluteSource = Path.Combine(Directory.GetCurrentDirectory(), SourcePath);
        if (!File.Exists(absoluteSource))
        {
            Debug.LogError("[Neubeckum Roads] Missing source file: " + SourcePath);
            return;
        }

        XDocument document = XDocument.Load(absoluteSource);
        Dictionary<long, Node> nodes = ReadNodes(document);
        List<Road> roads = ReadRoads(document);
        if (roads.Count == 0)
        {
            Debug.LogError("[Neubeckum Roads] No driveable roads found in the OSM source.");
            return;
        }

        ConfigureMapPlacement(nodes, roads);

        MeshBuffers buffers = new MeshBuffers();
        Dictionary<long, int> junctionUse = CountJunctionUse(roads);
        Dictionary<long, float> junctionRadius = new Dictionary<long, float>();
        Dictionary<long, float> endpointRadius = new Dictionary<long, float>();

        foreach (Road road in roads)
        {
            List<Vector3> control = road.NodeIds
                .Where(nodes.ContainsKey)
                .Select(id => ToWorld(nodes[id]))
                .ToList();
            if (control.Count < 2) continue;

            List<Vector3> smooth = Smooth(control, 4);
            AddRibbon(buffers, smooth, road.Width + 3.2f, 0.025f, buffers.SidewalkTriangles, 0f);
            AddRibbon(buffers, smooth, road.Width, 0.065f, buffers.AsphaltTriangles, 0f);
            if (road.HasCenterLine)
                AddDashedCenterLine(buffers, smooth, buffers.MarkingTriangles);

            RegisterLargestRadius(endpointRadius, road.NodeIds[0], road.Width * 0.5f);
            RegisterLargestRadius(endpointRadius, road.NodeIds[road.NodeIds.Count - 1], road.Width * 0.5f);

            foreach (long nodeId in road.NodeIds)
            {
                if (!junctionUse.TryGetValue(nodeId, out int uses) || uses < 2) continue;
                float radius = road.Width * 0.5f + 0.35f;
                if (!junctionRadius.TryGetValue(nodeId, out float existing) || radius > existing)
                    junctionRadius[nodeId] = radius;
            }
        }

        foreach (KeyValuePair<long, float> junction in junctionRadius)
        {
            if (!nodes.TryGetValue(junction.Key, out Node node)) continue;
            Vector3 center = ToWorld(node);
            AddDisc(buffers, center, junction.Value + 1.6f, 0.026f, buffers.SidewalkTriangles);
            AddDisc(buffers, center, junction.Value + 0.2f, 0.068f, buffers.AsphaltTriangles);
        }

        foreach (KeyValuePair<long, float> endpoint in endpointRadius)
        {
            if (!nodes.TryGetValue(endpoint.Key, out Node node)) continue;
            Vector3 center = ToWorld(node);
            AddDisc(buffers, center, endpoint.Value + 1.6f, 0.026f, buffers.SidewalkTriangles);
            AddDisc(buffers, center, endpoint.Value + 0.12f, 0.068f, buffers.AsphaltTriangles);
        }

        EnsureFolder(OutputFolder);
        Material asphalt = GetOrCreateMaterial(OutputFolder + "/Neubeckum_Asphalt.mat", new Color(0.16f, 0.17f, 0.18f), 0.12f);
        Material sidewalk = GetOrCreateMaterial(OutputFolder + "/Neubeckum_Sidewalk.mat", new Color(0.48f, 0.49f, 0.48f), 0.05f);
        Material marking = GetOrCreateMaterial(OutputFolder + "/Neubeckum_Markings.mat", new Color(0.92f, 0.9f, 0.72f), 0.08f);

        string meshPath = OutputFolder + "/Neubeckum_RoadNetwork.asset";
        AssetDatabase.DeleteAsset(meshPath);
        Mesh mesh = BuildMesh(buffers);
        AssetDatabase.CreateAsset(mesh, meshPath);

        GameObject previous = GameObject.Find(RootName);
        if (previous != null) Undo.DestroyObjectImmediate(previous);

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Neubeckum road network");
        MeshFilter filter = root.AddComponent<MeshFilter>();
        MeshRenderer renderer = root.AddComponent<MeshRenderer>();
        MeshCollider collider = root.AddComponent<MeshCollider>();
        filter.sharedMesh = mesh;
        collider.sharedMesh = mesh;
        renderer.sharedMaterials = new[] { sidewalk, asphalt, marking };
        root.isStatic = true;

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log($"[Neubeckum Roads] Built {roads.Count} roads inside Terrain (0,0,0), {junctionRadius.Count} junction covers, {endpointRadius.Count} endpoint caps and {mesh.vertexCount} vertices.");
    }

    [MenuItem("Day Ones/City/Remove Neubeckum Road Network")]
    public static void Remove()
    {
        GameObject root = GameObject.Find(RootName);
        if (root != null)
        {
            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }

    private static Dictionary<long, Node> ReadNodes(XDocument document)
    {
        return document.Root.Elements("node").Select(element => new Node
        {
            Id = ParseLong(element.Attribute("id")?.Value),
            Latitude = ParseDouble(element.Attribute("lat")?.Value),
            Longitude = ParseDouble(element.Attribute("lon")?.Value)
        }).ToDictionary(node => node.Id);
    }

    private static List<Road> ReadRoads(XDocument document)
    {
        HashSet<string> allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "motorway", "motorway_link", "trunk", "trunk_link", "primary", "primary_link",
            "secondary", "secondary_link", "tertiary", "tertiary_link", "residential",
            "living_street", "unclassified", "service", "road"
        };

        List<Road> roads = new List<Road>();
        foreach (XElement way in document.Root.Elements("way"))
        {
            Dictionary<string, string> tags = way.Elements("tag")
                .Where(tag => tag.Attribute("k") != null)
                .GroupBy(tag => tag.Attribute("k").Value)
                .ToDictionary(group => group.Key, group => group.Last().Attribute("v")?.Value ?? string.Empty);

            if (!tags.TryGetValue("highway", out string highway) || !allowed.Contains(highway)) continue;
            if (tags.TryGetValue("access", out string access) && (access == "private" || access == "no")) continue;

            Road road = new Road
            {
                Highway = highway,
                Name = tags.TryGetValue("name", out string name) ? name : "Unnamed road",
                Width = RoadWidth(highway),
                HasCenterLine = highway != "service" && highway != "living_street"
            };
            road.NodeIds.AddRange(way.Elements("nd").Select(nd => ParseLong(nd.Attribute("ref")?.Value)));
            if (road.NodeIds.Count >= 2) roads.Add(road);
        }
        return roads;
    }

    private static Dictionary<long, int> CountJunctionUse(IEnumerable<Road> roads)
    {
        Dictionary<long, int> counts = new Dictionary<long, int>();
        foreach (Road road in roads)
        foreach (long id in road.NodeIds.Distinct())
            counts[id] = counts.TryGetValue(id, out int value) ? value + 1 : 1;
        return counts;
    }

    private static float RoadWidth(string highway)
    {
        switch (highway)
        {
            case "motorway": return 13f;
            case "trunk": return 11f;
            case "primary": return 9.5f;
            case "secondary": return 8.5f;
            case "tertiary": return 7.5f;
            case "service": return 4.2f;
            case "living_street": return 5.5f;
            default: return 6.2f;
        }
    }

    private static void RegisterLargestRadius(Dictionary<long, float> radii, long nodeId, float radius)
    {
        if (!radii.TryGetValue(nodeId, out float existing) || radius > existing)
            radii[nodeId] = radius;
    }

    private static void ConfigureMapPlacement(IReadOnlyDictionary<long, Node> nodes, IEnumerable<Road> roads)
    {
        Terrain terrain = FindMainTerrain();
        Vector3 terrainOrigin = terrain != null ? terrain.transform.position : Vector3.zero;
        Vector3 terrainSize = terrain != null ? terrain.terrainData.size : new Vector3(1000f, 0f, 1000f);

        IEnumerable<Vector2> projectedPoints = roads
            .SelectMany(road => road.NodeIds)
            .Distinct()
            .Where(nodes.ContainsKey)
            .Select(id => ProjectMeters(nodes[id]));

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;
        foreach (Vector2 point in projectedPoints)
        {
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minZ = Mathf.Min(minZ, point.y);
            maxZ = Mathf.Max(maxZ, point.y);
        }

        // Keep the dense forest band on the eastern edge as an undeveloped green zone.
        float availableWidth = Mathf.Max(1f, terrainSize.x * 0.52f - TerrainMargin * 2f);
        float availableDepth = Mathf.Max(1f, terrainSize.z * 0.75f - TerrainMargin * 2f);
        mapScale = Mathf.Min(availableWidth / Mathf.Max(1f, maxX - minX), availableDepth / Mathf.Max(1f, maxZ - minZ));
        float targetCenterX = terrainOrigin.x + terrainSize.x * 0.29f;
        float targetCenterZ = terrainOrigin.z + terrainSize.z * 0.5f;
        mapOffset = new Vector2(
            targetCenterX - (minX + maxX) * 0.5f * mapScale,
            targetCenterZ - (minZ + maxZ) * 0.5f * mapScale);
    }

    private static Vector2 ProjectMeters(Node node)
    {
        double latitudeRadians = CenterLatitude * Math.PI / 180.0;
        return new Vector2(
            (float)((node.Longitude - CenterLongitude) * 111320.0 * Math.Cos(latitudeRadians)),
            (float)((node.Latitude - CenterLatitude) * 111320.0));
    }

    private static Vector3 ToWorld(Node node)
    {
        Vector2 projected = ProjectMeters(node);
        float x = projected.x * mapScale + mapOffset.x;
        float z = projected.y * mapScale + mapOffset.y;
        Vector3 point = ClampToMainTerrain(new Vector3(x, 0f, z));
        Terrain terrain = FindTargetTerrain(point);
        if (terrain != null) point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        return point;
    }

    private static Terrain FindTargetTerrain(Vector3 point)
    {
        Terrain terrain = FindMainTerrain();
        if (terrain == null) return null;
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        if (point.x >= origin.x && point.x <= origin.x + size.x && point.z >= origin.z && point.z <= origin.z + size.z)
            return terrain;
        return null;
    }

    private static Terrain FindMainTerrain()
    {
        return Terrain.activeTerrains.FirstOrDefault(terrain =>
            Mathf.Abs(terrain.transform.position.x) <= 0.1f &&
            Mathf.Abs(terrain.transform.position.z) <= 0.1f);
    }

    private static Vector3 ClampToMainTerrain(Vector3 point)
    {
        Terrain terrain = FindMainTerrain();
        if (terrain == null) return point;
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        point.x = Mathf.Clamp(point.x, origin.x + TerrainMargin, origin.x + size.x - TerrainMargin);
        point.z = Mathf.Clamp(point.z, origin.z + TerrainMargin, origin.z + size.z - TerrainMargin);
        return point;
    }

    private static List<Vector3> Smooth(IReadOnlyList<Vector3> points, int samplesPerSegment)
    {
        List<Vector3> result = new List<Vector3>();
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 p0 = points[Mathf.Max(i - 1, 0)];
            Vector3 p1 = points[i];
            Vector3 p2 = points[i + 1];
            Vector3 p3 = points[Mathf.Min(i + 2, points.Count - 1)];
            for (int sample = 0; sample < samplesPerSegment; sample++)
            {
                float t = sample / (float)samplesPerSegment;
                Vector3 point = ClampToMainTerrain(CatmullRom(p0, p1, p2, p3, t));
                Terrain terrain = FindTargetTerrain(point);
                if (terrain != null) point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
                if (result.Count == 0 || Vector3.SqrMagnitude(result[result.Count - 1] - point) > 0.04f)
                    result.Add(point);
            }
        }
        result.Add(points[points.Count - 1]);
        return result;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static void AddRibbon(MeshBuffers buffers, IReadOnlyList<Vector3> points, float width, float lift, List<int> triangles, float vStart)
    {
        if (points.Count < 2) return;
        int start = buffers.Vertices.Count;
        float distance = vStart;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 previous = points[Mathf.Max(0, i - 1)];
            Vector3 next = points[Mathf.Min(points.Count - 1, i + 1)];
            Vector3 tangent = next - previous;
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.forward;
            tangent.Normalize();
            Vector3 side = new Vector3(-tangent.z, 0f, tangent.x) * width * 0.5f;
            Vector3 center = points[i] + Vector3.up * lift;
            buffers.Vertices.Add(center - side);
            buffers.Vertices.Add(center + side);
            if (i > 0) distance += Vector3.Distance(points[i - 1], points[i]);
            buffers.UVs.Add(new Vector2(0f, distance / 6f));
            buffers.UVs.Add(new Vector2(1f, distance / 6f));
        }
        for (int i = 0; i < points.Count - 1; i++)
        {
            int a = start + i * 2;
            triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 2);
            triangles.Add(a + 1); triangles.Add(a + 3); triangles.Add(a + 2);
        }
    }

    private static void AddDashedCenterLine(MeshBuffers buffers, IReadOnlyList<Vector3> points, List<int> triangles)
    {
        const float dashLength = 3.2f;
        const float gapLength = 3f;
        const float cycleLength = dashLength + gapLength;
        float distanceAlongRoad = 0f;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 start = points[i];
            Vector3 end = points[i + 1];
            float segmentLength = Vector3.Distance(start, end);
            if (segmentLength < 0.01f) continue;

            float travelled = 0f;
            while (travelled < segmentLength)
            {
                float phase = (distanceAlongRoad + travelled) % cycleLength;
                bool drawing = phase < dashLength;
                float remainingInPhase = drawing ? dashLength - phase : cycleLength - phase;
                float step = Mathf.Min(remainingInPhase, segmentLength - travelled);
                if (drawing && step > 0.05f)
                {
                    Vector3 dashStart = Vector3.Lerp(start, end, travelled / segmentLength);
                    Vector3 dashEnd = Vector3.Lerp(start, end, (travelled + step) / segmentLength);
                    AddRibbon(buffers, new[] { dashStart, dashEnd }, 0.2f, 0.09f, triangles, 0f);
                }
                travelled += Mathf.Max(step, 0.01f);
            }

            distanceAlongRoad += segmentLength;
        }
    }

    private static void AddDisc(MeshBuffers buffers, Vector3 center, float radius, float lift, List<int> triangles)
    {
        const int segments = 20;
        int start = buffers.Vertices.Count;
        center.y += lift;
        buffers.Vertices.Add(center);
        buffers.UVs.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            buffers.Vertices.Add(center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            buffers.UVs.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
        }
        for (int i = 0; i < segments; i++)
        {
            triangles.Add(start); triangles.Add(start + i + 2); triangles.Add(start + i + 1);
        }
    }

    private static Mesh BuildMesh(MeshBuffers buffers)
    {
        Mesh mesh = new Mesh { name = "Neubeckum Connected Road Network", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(buffers.Vertices);
        mesh.SetUVs(0, buffers.UVs);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(buffers.SidewalkTriangles, 0);
        mesh.SetTriangles(buffers.AsphaltTriangles, 1);
        mesh.SetTriangles(buffers.MarkingTriangles, 2);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material GetOrCreateMaterial(string path, Color color, float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static long ParseLong(string value) => long.Parse(value ?? "0", CultureInfo.InvariantCulture);
    private static double ParseDouble(string value) => double.Parse(value ?? "0", CultureInfo.InvariantCulture);
}
#endif

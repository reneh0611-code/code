using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static class ElevatedEdgeTreePlacer
    {
        private const string MenuPath = "Tools/CYDOY/Terrain/Place Trees On Elevated Map Edges";
        private const float EdgeBandMetres = 320f;
        private const float SampleSpacingMetres = 3.5f;
        private const float ImpassableOuterBandMetres = 34f;
        private const float MinimumRiseMetres = 3.5f;
        private const float MinimumSlopeDegrees = 6f;
        private const float MaximumSlopeDegrees = 46f;
        private const float InnerClearanceMetres = 7.5f;
        private const float OuterClearanceMetres = 2.35f;
        private const string AutomaticRunKey = "CYDOY.ElevatedEdgeTreesAndGrass.zzz.v3";

        static ElevatedEdgeTreePlacer()
        {
            EditorApplication.delayCall += PlaceTreesOnceForCurrentScene;
        }

        private static void PlaceTreesOnceForCurrentScene()
        {
            if (EditorPrefs.GetBool(AutomaticRunKey, false) || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "zzz")
                return;

            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains == null || terrains.Length == 0 ||
                Array.Find(terrains, terrain => terrain != null && terrain.terrainData != null && terrain.terrainData.treePrototypes.Length > 0) == null)
                return;

            PlaceTrees();
            EditorPrefs.SetBool(AutomaticRunKey, true);
        }

        [MenuItem(MenuPath)]
        public static void PlaceTrees()
        {
            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains == null || terrains.Length == 0)
            {
                EditorUtility.DisplayDialog("Bäume an Kartenrändern", "In der geöffneten Szene wurde kein aktives Terrain gefunden.", "OK");
                return;
            }

            Terrain selectedTerrain = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<Terrain>()
                : null;
            Terrain sourceTerrain = selectedTerrain != null && selectedTerrain.terrainData.treePrototypes.Length > 0
                ? selectedTerrain
                : Array.Find(terrains, terrain => terrain != null && terrain.terrainData != null && terrain.terrainData.treePrototypes.Length > 0);

            if (sourceTerrain == null)
            {
                EditorUtility.DisplayDialog("Bäume an Kartenrändern", "Bitte zuerst im Terrain-Werkzeug mindestens einen Baum hinzufügen und auswählen.", "OK");
                return;
            }

            TreePrototype selectedPrototype = sourceTerrain.terrainData.treePrototypes[0];
            if (selectedPrototype.prefab == null)
            {
                EditorUtility.DisplayDialog("Bäume an Kartenrändern", "Der erste Baum-Prototyp besitzt kein Prefab.", "OK");
                return;
            }

            Terrain grassSourceTerrain = selectedTerrain != null && selectedTerrain.terrainData.detailPrototypes.Length > 0
                ? selectedTerrain
                : Array.Find(terrains, terrain => terrain != null && terrain.terrainData != null && terrain.terrainData.detailPrototypes.Length > 0);
            DetailPrototype selectedGrass = grassSourceTerrain != null ? grassSourceTerrain.terrainData.detailPrototypes[0] : null;

            Bounds worldBounds = CalculateWorldBounds(terrains);
            string backupFolder = CreateBackupFolder();
            int totalPlaced = 0;
            long totalGrassCells = 0;
            int changedTerrains = 0;

            foreach (Terrain terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null)
                    continue;

                TerrainData data = terrain.terrainData;
                BackupTerrainData(data, backupFolder);
                Undo.RegisterCompleteObjectUndo(data, "Bäume an erhöhten Kartenrändern platzieren");

                int prototypeIndex = EnsurePrototype(data, selectedPrototype);
                List<TreeInstance> trees = new List<TreeInstance>(data.treeInstances);
                OccupiedTreeGrid occupiedWorldPositions = BuildOccupiedPositions(terrain, trees);
                int beforeCount = trees.Count;
                PlaceOnTerrain(terrain, prototypeIndex, worldBounds, trees, occupiedWorldPositions);
                bool changed = trees.Count != beforeCount;
                if (changed)
                {
                    data.SetTreeInstances(trees.ToArray(), true);
                    totalPlaced += trees.Count - beforeCount;
                }

                if (selectedGrass != null && data.detailResolution > 0)
                {
                    int detailIndex = EnsureDetailPrototype(data, selectedGrass);
                    totalGrassCells += PaintThinGrass(terrain, detailIndex, worldBounds);
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(data);
                    changedTerrains++;
                }
            }

            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();

            string grassMessage = selectedGrass != null
                ? $"\nDünnes Gras wurde auf {totalGrassCells:N0} Detailfeldern verteilt."
                : "\nHinweis: Es war noch keine Gras-Detailart im Terrain eingetragen.";
            string message = $"{totalPlaced} zusätzliche Bäume wurden auf {changedTerrains} Terrain-Flächen verteilt.{grassMessage}\n\nSicherung: {backupFolder}";
            Debug.Log($"[CYDOY TREES] {message}");
            EditorUtility.DisplayDialog("Bäume an Kartenrändern", message, "Fertig");
        }

        private static Bounds CalculateWorldBounds(Terrain[] terrains)
        {
            bool initialized = false;
            Bounds bounds = default;
            foreach (Terrain terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null)
                    continue;

                Vector3 size = terrain.terrainData.size;
                Bounds terrainBounds = new Bounds(terrain.transform.position + size * 0.5f, size);
                if (!initialized)
                {
                    bounds = terrainBounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(terrainBounds.min);
                    bounds.Encapsulate(terrainBounds.max);
                }
            }
            return bounds;
        }

        private static void PlaceOnTerrain(
            Terrain terrain,
            int prototypeIndex,
            Bounds worldBounds,
            List<TreeInstance> trees,
            OccupiedTreeGrid occupiedWorldPositions)
        {
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            float minimumHeight = FindMinimumHeight(terrain);
            int seed = terrain.gameObject.scene.path.GetHashCode() ^ terrain.name.GetHashCode() ^ 0x51A7;
            System.Random random = new System.Random(seed);

            int xSteps = Mathf.Max(1, Mathf.CeilToInt(size.x / SampleSpacingMetres));
            int zSteps = Mathf.Max(1, Mathf.CeilToInt(size.z / SampleSpacingMetres));

            for (int z = 0; z < zSteps; z++)
            {
                for (int x = 0; x < xSteps; x++)
                {
                    float worldX = origin.x + (x + 0.2f + (float)random.NextDouble() * 0.6f) * size.x / xSteps;
                    float worldZ = origin.z + (z + 0.2f + (float)random.NextDouble() * 0.6f) * size.z / zSteps;
                    float nx = Mathf.Clamp01((worldX - origin.x) / size.x);
                    float nz = Mathf.Clamp01((worldZ - origin.z) / size.z);
                    float worldHeight = origin.y + data.GetInterpolatedHeight(nx, nz);
                    float rise = worldHeight - minimumHeight;
                    float slope = data.GetSteepness(nx, nz);
                    float edgeDistance = Mathf.Min(
                        Mathf.Min(worldX - worldBounds.min.x, worldBounds.max.x - worldX),
                        Mathf.Min(worldZ - worldBounds.min.z, worldBounds.max.z - worldZ));

                    bool outerBarrier = edgeDistance <= ImpassableOuterBandMetres;
                    bool elevatedEdge = edgeDistance <= EdgeBandMetres &&
                                        (outerBarrier || rise >= MinimumRiseMetres || slope >= MinimumSlopeDegrees);
                    if (!elevatedEdge || slope > MaximumSlopeDegrees)
                        continue;

                    float edgeStrength = 1f - Mathf.Clamp01(edgeDistance / EdgeBandMetres);
                    float heightStrength = Mathf.Clamp01(rise / 18f);
                    float densityGradient = edgeStrength * edgeStrength;
                    float probability = outerBarrier
                        ? 1f
                        : Mathf.Lerp(0.14f, 0.94f, Mathf.Max(densityGradient, heightStrength * 0.52f));
                    if (random.NextDouble() > probability)
                        continue;

                    Vector3 worldPosition = new Vector3(worldX, worldHeight, worldZ);
                    float clearance = outerBarrier
                        ? OuterClearanceMetres
                        : Mathf.Lerp(InnerClearanceMetres, OuterClearanceMetres, densityGradient);
                    if (occupiedWorldPositions.IsTooClose(worldPosition, clearance))
                        continue;

                    float scale = Mathf.Lerp(0.82f, 1.22f, (float)random.NextDouble());
                    trees.Add(new TreeInstance
                    {
                        position = new Vector3(nx, Mathf.Clamp01((worldHeight - origin.y) / size.y), nz),
                        prototypeIndex = prototypeIndex,
                        widthScale = scale * Mathf.Lerp(0.9f, 1.08f, (float)random.NextDouble()),
                        heightScale = scale,
                        rotation = (float)random.NextDouble() * Mathf.PI * 2f,
                        color = Color.white,
                        lightmapColor = Color.white
                    });
                    occupiedWorldPositions.Add(worldPosition);
                }
            }
        }

        private static float FindMinimumHeight(Terrain terrain)
        {
            float minimum = float.MaxValue;
            const int samples = 24;
            for (int z = 0; z <= samples; z++)
            {
                for (int x = 0; x <= samples; x++)
                {
                    float height = terrain.transform.position.y + terrain.terrainData.GetInterpolatedHeight(x / (float)samples, z / (float)samples);
                    minimum = Mathf.Min(minimum, height);
                }
            }
            return minimum;
        }

        private static OccupiedTreeGrid BuildOccupiedPositions(Terrain terrain, List<TreeInstance> trees)
        {
            OccupiedTreeGrid positions = new OccupiedTreeGrid(InnerClearanceMetres);
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            foreach (TreeInstance tree in trees)
            {
                positions.Add(origin + Vector3.Scale(tree.position, size));
            }
            return positions;
        }

        private sealed class OccupiedTreeGrid
        {
            private readonly float cellSize;
            private readonly Dictionary<long, List<Vector3>> cells = new Dictionary<long, List<Vector3>>();

            public OccupiedTreeGrid(float cellSize)
            {
                this.cellSize = cellSize;
            }

            public void Add(Vector3 position)
            {
                int cellX = Mathf.FloorToInt(position.x / cellSize);
                int cellZ = Mathf.FloorToInt(position.z / cellSize);
                long key = Key(cellX, cellZ);
                if (!cells.TryGetValue(key, out List<Vector3> positions))
                {
                    positions = new List<Vector3>();
                    cells.Add(key, positions);
                }
                positions.Add(position);
            }

            public bool IsTooClose(Vector3 candidate, float clearance)
            {
                int centreX = Mathf.FloorToInt(candidate.x / cellSize);
                int centreZ = Mathf.FloorToInt(candidate.z / cellSize);
                int radius = Mathf.Max(1, Mathf.CeilToInt(clearance / cellSize));
                float squaredClearance = clearance * clearance;

                for (int z = centreZ - radius; z <= centreZ + radius; z++)
                {
                    for (int x = centreX - radius; x <= centreX + radius; x++)
                    {
                        if (!cells.TryGetValue(Key(x, z), out List<Vector3> positions))
                            continue;
                        foreach (Vector3 position in positions)
                        {
                            float dx = candidate.x - position.x;
                            float dz = candidate.z - position.z;
                            if (dx * dx + dz * dz < squaredClearance)
                                return true;
                        }
                    }
                }
                return false;
            }

            private static long Key(int x, int z)
            {
                return ((long)x << 32) ^ (uint)z;
            }
        }

        private static int EnsurePrototype(TerrainData data, TreePrototype prototype)
        {
            TreePrototype[] prototypes = data.treePrototypes;
            for (int i = 0; i < prototypes.Length; i++)
            {
                if (prototypes[i].prefab == prototype.prefab)
                    return i;
            }

            Array.Resize(ref prototypes, prototypes.Length + 1);
            prototypes[prototypes.Length - 1] = prototype;
            data.treePrototypes = prototypes;
            return prototypes.Length - 1;
        }

        private static int EnsureDetailPrototype(TerrainData data, DetailPrototype prototype)
        {
            DetailPrototype[] prototypes = data.detailPrototypes;
            for (int i = 0; i < prototypes.Length; i++)
            {
                if (prototypes[i].prototype == prototype.prototype && prototypes[i].prototypeTexture == prototype.prototypeTexture)
                    return i;
            }

            Array.Resize(ref prototypes, prototypes.Length + 1);
            prototypes[prototypes.Length - 1] = prototype;
            data.detailPrototypes = prototypes;
            return prototypes.Length - 1;
        }

        private static long PaintThinGrass(Terrain terrain, int detailIndex, Bounds worldBounds)
        {
            TerrainData data = terrain.terrainData;
            int resolution = data.detailResolution;
            int[,] existing = data.GetDetailLayer(0, 0, resolution, resolution, detailIndex);
            int[,] density = new int[resolution, resolution];
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            long occupiedCells = 0;

            for (int z = 0; z < resolution; z++)
            {
                float nz = (z + 0.5f) / resolution;
                float worldZ = origin.z + nz * size.z;
                for (int x = 0; x < resolution; x++)
                {
                    float nx = (x + 0.5f) / resolution;
                    float worldX = origin.x + nx * size.x;
                    float edgeDistance = Mathf.Min(
                        Mathf.Min(worldX - worldBounds.min.x, worldBounds.max.x - worldX),
                        Mathf.Min(worldZ - worldBounds.min.z, worldBounds.max.z - worldZ));
                    float edgeStrength = 1f - Mathf.Clamp01(edgeDistance / (EdgeBandMetres + 120f));
                    float slope = data.GetSteepness(nx, nz);
                    if (slope > 42f)
                    {
                        density[z, x] = existing[z, x];
                        continue;
                    }

                    float patchNoise = Mathf.PerlinNoise(worldX * 0.035f + 17.3f, worldZ * 0.035f + 41.7f);
                    float broadNoise = Mathf.PerlinNoise(worldX * 0.009f + 5.1f, worldZ * 0.009f + 9.8f);
                    float coverage = Mathf.Lerp(0.16f, 0.9f, edgeStrength) * Mathf.Lerp(0.45f, 1f, broadNoise);
                    int target = patchNoise <= coverage
                        ? Mathf.RoundToInt(Mathf.Lerp(1f, 7f, edgeStrength) * Mathf.Lerp(0.65f, 1.2f, patchNoise))
                        : 0;
                    density[z, x] = Mathf.Max(existing[z, x], target);
                    if (density[z, x] > 0)
                        occupiedCells++;
                }
            }

            data.SetDetailLayer(0, 0, detailIndex, density);
            return occupiedCells;
        }

        private static string CreateBackupFolder()
        {
            EnsureFolder("Assets/Recovery");
            EnsureFolder("Assets/Recovery/BeforeEdgeTrees");
            string folder = $"Assets/Recovery/BeforeEdgeTrees/{DateTime.Now:yyyyMMdd_HHmmss}";
            EnsureFolder(folder);
            return folder;
        }

        private static void BackupTerrainData(TerrainData data, string folder)
        {
            string sourcePath = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(sourcePath))
                return;

            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            string destination = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
            if (!AssetDatabase.CopyAsset(sourcePath, destination))
                Debug.LogWarning($"[CYDOY TREES] Terrain-Sicherung konnte nicht erstellt werden: {sourcePath}");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class LargeCityTerrainBuilder
    {
        private const string TerrainObjectName = "CityTerrain";
        private const string TerrainDataPath = "Assets/Environment/Terrain/CityTerrainData.asset";

        private const int HeightmapResolution = 513;
        private const float TerrainSize = 1200f;
        private const float TerrainHeight = 180f;
        private const float BaseHeight = 0.22f;

        [MenuItem("Tools/CYDOY/Terrain/Build Large City Terrain")]
        public static void BuildLargeTerrain()
        {
            EnsureFolders();
            RemoveOldTerrain();

            TerrainData data = new TerrainData
            {
                heightmapResolution = HeightmapResolution,
                size = new Vector3(TerrainSize, TerrainHeight, TerrainSize)
            };

            float[,] heights = new float[HeightmapResolution, HeightmapResolution];

            for (int z = 0; z < HeightmapResolution; z++)
            {
                float nz = z / (float)(HeightmapResolution - 1);
                float worldZ = nz * TerrainSize - TerrainSize * 0.5f;

                for (int x = 0; x < HeightmapResolution; x++)
                {
                    float nx = x / (float)(HeightmapResolution - 1);
                    float worldX = nx * TerrainSize - TerrainSize * 0.5f;

                    float h = BaseHeight;

                    // Large buildable city plateau in the middle.
                    float centerDist = Mathf.Sqrt(worldX * worldX + worldZ * worldZ);
                    float cityBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(230f, 430f, centerDist));

                    // Broad rolling hills mostly around the perimeter.
                    float noise1 = Mathf.PerlinNoise((worldX + 800f) * 0.0028f, (worldZ + 400f) * 0.0028f);
                    float noise2 = Mathf.PerlinNoise((worldX - 300f) * 0.0065f, (worldZ + 1100f) * 0.0065f);
                    float noise = (noise1 * 0.72f + noise2 * 0.28f) - 0.47f;
                    h += noise * 0.23f * cityBlend;

                    // Hand-shaped surrounding hills.
                    h += Hill(worldX, worldZ, -430f, 300f, 230f, 190f, 0.26f);
                    h += Hill(worldX, worldZ, 380f, 350f, 260f, 210f, 0.22f);
                    h += Hill(worldX, worldZ, -390f, -330f, 260f, 240f, 0.19f);
                    h += Hill(worldX, worldZ, 430f, -280f, 220f, 250f, 0.24f);
                    h += Hill(worldX, worldZ, 80f, 500f, 330f, 150f, 0.13f);

                    // Broad valley entering from the south-west and opening toward the centre.
                    float valleyCenterX = -155f + Mathf.Sin((worldZ + 250f) * 0.0045f) * 65f;
                    float valleyDistance = Mathf.Abs(worldX - valleyCenterX);
                    float valleyAlong = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(470f, -200f, worldZ));
                    float valley = Mathf.Exp(-(valleyDistance * valleyDistance) / (2f * 180f * 180f)) * valleyAlong;
                    h -= valley * 0.105f;

                    // River basin / carved channel. Meanders through one side of the map.
                    float riverCenterX = -250f
                                         + Mathf.Sin(worldZ * 0.009f) * 72f
                                         + Mathf.Sin(worldZ * 0.0038f + 1.7f) * 36f;
                    float riverDistance = Mathf.Abs(worldX - riverCenterX);

                    float basin = Mathf.Exp(-(riverDistance * riverDistance) / (2f * 72f * 72f));
                    float channel = Mathf.Exp(-(riverDistance * riverDistance) / (2f * 28f * 28f));

                    // Wide banks plus a deeper centre channel.
                    h -= basin * 0.075f;
                    h -= channel * 0.055f;

                    // Slightly flatten the future central city area while preserving large-scale valley shape.
                    float centralFlat = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(170f, 320f, centerDist));
                    float targetCityHeight = BaseHeight - valley * 0.035f;
                    h = Mathf.Lerp(h, targetCityHeight, centralFlat * 0.88f);

                    // Edge uplift makes the whole terrain feel contained rather than cut off flat.
                    float edge = Mathf.Max(Mathf.Abs(worldX), Mathf.Abs(worldZ));
                    float edgeLift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(470f, 600f, edge));
                    h += edgeLift * 0.11f;

                    heights[z, x] = Mathf.Clamp01(h);
                }
            }

            data.SetHeights(0, 0, heights);
            data.baseMapResolution = 1024;
            data.SetDetailResolution(1024, 16);

            AssetDatabase.CreateAsset(data, TerrainDataPath);
            AssetDatabase.SaveAssets();

            GameObject terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = TerrainObjectName;
            terrainGo.transform.position = new Vector3(-TerrainSize * 0.5f, -BaseHeight * TerrainHeight, -TerrainSize * 0.5f);

            Terrain terrain = terrainGo.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 700f;

            TerrainCollider collider = terrainGo.GetComponent<TerrainCollider>();
            if (collider != null) collider.terrainData = data;

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrWhiteSpace(scene.path))
                EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = terrainGo;
            EditorGUIUtility.PingObject(terrainGo);

            Debug.Log("[CYDOY TERRAIN] Built 1200x1200 m city terrain with central buildable plateau, surrounding hills, valley and carved river basin.", terrainGo);
            EditorUtility.DisplayDialog(
                "CYDOY Terrain",
                "Large city terrain created.\n\nSize: 1200 x 1200 m\nCentral buildable area: roughly 450-550 m wide\nSurrounding hills: yes\nValley: yes\nRiver basin/channel: yes\n\nNo roads or buildings were added.",
                "Done");
        }

        [MenuItem("Tools/CYDOY/Terrain/Delete Generated City Terrain")]
        public static void DeleteGeneratedTerrain()
        {
            RemoveOldTerrain();
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath) != null)
                AssetDatabase.DeleteAsset(TerrainDataPath);
            AssetDatabase.SaveAssets();
        }

        private static float Hill(float x, float z, float cx, float cz, float rx, float rz, float amplitude)
        {
            float dx = (x - cx) / rx;
            float dz = (z - cz) / rz;
            float d2 = dx * dx + dz * dz;
            return Mathf.Exp(-d2 * 1.65f) * amplitude;
        }

        private static void RemoveOldTerrain()
        {
            GameObject old = GameObject.Find(TerrainObjectName);
            if (old != null)
                Undo.DestroyObjectImmediate(old);

            GameObject oldCity = GameObject.Find("SimulationCity");
            if (oldCity != null)
                Undo.DestroyObjectImmediate(oldCity);

            GameObject oldRoad = GameObject.Find("RoadFirst_CityLayout");
            if (oldRoad != null)
                Undo.DestroyObjectImmediate(oldRoad);

            if (AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath) != null)
                AssetDatabase.DeleteAsset(TerrainDataPath);
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Environment"))
                AssetDatabase.CreateFolder("Assets", "Environment");
            if (!AssetDatabase.IsValidFolder("Assets/Environment/Terrain"))
                AssetDatabase.CreateFolder("Assets/Environment", "Terrain");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static class RestoreSavedTerrainLayout
    {
        private const string BackupFolder = "Assets/Recovery/BeforeEdgeTrees/20260827_173253";
        private const string AutomaticRunKey = "CYDOY.RestoreSavedTerrainLayout.zzz.v1";

        static RestoreSavedTerrainLayout()
        {
            EditorApplication.delayCall += RestoreOnce;
        }

        [MenuItem("Tools/CYDOY/Terrain/Restore Last Complete Terrain Layout")]
        public static void Restore()
        {
            Scene scene = SceneManager.GetActiveScene();
            Terrain[] terrains = Terrain.activeTerrains
                .Where(terrain => terrain != null && terrain.gameObject.scene == scene)
                .OrderBy(terrain => terrain.transform.position.z)
                .ThenBy(terrain => terrain.transform.position.x)
                .ToArray();

            string[] assetGuids = AssetDatabase.FindAssets("t:TerrainData", new[] { BackupFolder });
            TerrainData[] backups = assetGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TerrainData>)
                .Where(data => data != null)
                .ToArray();

            if (terrains.Length != 4 || backups.Length != 4)
            {
                EditorUtility.DisplayDialog(
                    "Terrain wiederherstellen",
                    $"Erwartet wurden 4 Terrain-Objekte und 4 Sicherungen. Gefunden: {terrains.Length} Terrain-Objekte, {backups.Length} Sicherungen.",
                    "OK");
                return;
            }

            string sceneBackupPath = BackupOpenScene(scene);
            int[] bestAssignment = FindBestAssignment(terrains, backups);

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                TerrainData restoredData = backups[bestAssignment[i]];
                Undo.RecordObject(terrain, "Vollständiges Terrain wiederherstellen");
                terrain.terrainData = restoredData;
                TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
                if (collider != null)
                {
                    Undo.RecordObject(collider, "Vollständiges Terrain wiederherstellen");
                    collider.terrainData = restoredData;
                    EditorUtility.SetDirty(collider);
                }
                terrain.drawTreesAndFoliage = true;
                terrain.detailObjectDistance = Mathf.Max(terrain.detailObjectDistance, 180f);
                terrain.treeDistance = Mathf.Max(terrain.treeDistance, 1000f);
                EditorUtility.SetDirty(terrain);
            }

            ConnectNeighbours(terrains);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();

            Debug.Log($"[CYDOY TERRAIN RESTORE] Vier vollständige Terrain-Flächen aus {BackupFolder} wiederhergestellt. Vorherige Szene: {sceneBackupPath}");
            EditorUtility.DisplayDialog(
                "Terrain wiederhergestellt",
                $"Die vier vollständigen Terrain-Flächen mit Höhen und Bäumen sind wieder da und die Szene wurde gespeichert.\n\nSicherung der vorherigen Szene:\n{sceneBackupPath}",
                "Fertig");
        }

        private static void RestoreOnce()
        {
            if (EditorPrefs.GetBool(AutomaticRunKey, false) || SceneManager.GetActiveScene().name != "zzz" || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (Terrain.activeTerrains.Length != 4)
                return;

            Restore();
            EditorPrefs.SetBool(AutomaticRunKey, true);
        }

        private static string BackupOpenScene(Scene scene)
        {
            EnsureFolder("Assets/Recovery");
            EnsureFolder("Assets/Recovery/Scenes");
            string sceneName = string.IsNullOrWhiteSpace(scene.name) ? "UnsavedScene" : scene.name;
            string path = AssetDatabase.GenerateUniqueAssetPath($"Assets/Recovery/Scenes/{sceneName}_before_terrain_restore_{DateTime.Now:yyyyMMdd_HHmmss}.unity");
            EditorSceneManager.SaveScene(scene, path, true);
            return path;
        }

        private static int[] FindBestAssignment(Terrain[] terrains, TerrainData[] backups)
        {
            int[] values = { 0, 1, 2, 3 };
            int[] best = (int[])values.Clone();
            float bestCost = float.MaxValue;
            Permute(values, 0, permutation =>
            {
                float cost = CalculateSeamCost(terrains, backups, permutation);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = (int[])permutation.Clone();
                }
            });
            return best;
        }

        private static float CalculateSeamCost(Terrain[] terrains, TerrainData[] backups, int[] assignment)
        {
            float cost = 0f;
            const int samples = 48;
            for (int a = 0; a < terrains.Length; a++)
            {
                for (int b = a + 1; b < terrains.Length; b++)
                {
                    Vector3 pa = terrains[a].transform.position;
                    Vector3 pb = terrains[b].transform.position;
                    TerrainData da = backups[assignment[a]];
                    TerrainData db = backups[assignment[b]];
                    float sizeX = da.size.x;
                    float sizeZ = da.size.z;

                    if (Mathf.Abs(pb.x - (pa.x + sizeX)) < 1f && Mathf.Abs(pb.z - pa.z) < 1f)
                    {
                        for (int i = 0; i <= samples; i++)
                            cost += Mathf.Abs(da.GetInterpolatedHeight(1f, i / (float)samples) - db.GetInterpolatedHeight(0f, i / (float)samples));
                    }
                    else if (Mathf.Abs(pa.x - (pb.x + db.size.x)) < 1f && Mathf.Abs(pa.z - pb.z) < 1f)
                    {
                        for (int i = 0; i <= samples; i++)
                            cost += Mathf.Abs(db.GetInterpolatedHeight(1f, i / (float)samples) - da.GetInterpolatedHeight(0f, i / (float)samples));
                    }

                    if (Mathf.Abs(pb.z - (pa.z + sizeZ)) < 1f && Mathf.Abs(pb.x - pa.x) < 1f)
                    {
                        for (int i = 0; i <= samples; i++)
                            cost += Mathf.Abs(da.GetInterpolatedHeight(i / (float)samples, 1f) - db.GetInterpolatedHeight(i / (float)samples, 0f));
                    }
                    else if (Mathf.Abs(pa.z - (pb.z + db.size.z)) < 1f && Mathf.Abs(pa.x - pb.x) < 1f)
                    {
                        for (int i = 0; i <= samples; i++)
                            cost += Mathf.Abs(db.GetInterpolatedHeight(i / (float)samples, 1f) - da.GetInterpolatedHeight(i / (float)samples, 0f));
                    }
                }
            }
            return cost;
        }

        private static void Permute(int[] values, int index, Action<int[]> visitor)
        {
            if (index == values.Length)
            {
                visitor(values);
                return;
            }
            for (int i = index; i < values.Length; i++)
            {
                (values[index], values[i]) = (values[i], values[index]);
                Permute(values, index + 1, visitor);
                (values[index], values[i]) = (values[i], values[index]);
            }
        }

        private static void ConnectNeighbours(Terrain[] terrains)
        {
            foreach (Terrain terrain in terrains)
            {
                Terrain left = FindAt(terrains, terrain.transform.position + Vector3.left * terrain.terrainData.size.x);
                Terrain right = FindAt(terrains, terrain.transform.position + Vector3.right * terrain.terrainData.size.x);
                Terrain top = FindAt(terrains, terrain.transform.position + Vector3.forward * terrain.terrainData.size.z);
                Terrain bottom = FindAt(terrains, terrain.transform.position + Vector3.back * terrain.terrainData.size.z);
                terrain.SetNeighbors(left, top, right, bottom);
            }
        }

        private static Terrain FindAt(IEnumerable<Terrain> terrains, Vector3 position)
        {
            return terrains.FirstOrDefault(candidate => Vector3.SqrMagnitude(candidate.transform.position - position) < 1f);
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

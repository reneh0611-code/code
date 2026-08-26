using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class AJQuickIslandTester
    {
        private const string CurrentIslandKey = "CYDOY_AJ_TEST_ISLAND";
        private const int RendererIndex = 0;
        private static Mesh _originalMesh;
        private static SkinnedMeshRenderer _renderer;

        [MenuItem("Tools/CYDOY/Backpack Leftovers/Test Next Island On Selected NPC")]
        public static void TestNext()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Island Test", "Select one NPC in the Hierarchy first.", "OK");
                return;
            }

            SkinnedMeshRenderer[] renderers = selected.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length <= RendererIndex || renderers[RendererIndex].sharedMesh == null)
            {
                EditorUtility.DisplayDialog("CYDOY · Island Test", "Renderer 1 was not found on the selected NPC.", "OK");
                return;
            }

            RestoreInternal();
            _renderer = renderers[RendererIndex];
            _originalMesh = _renderer.sharedMesh;

            if (!_originalMesh.isReadable)
            {
                EditorUtility.DisplayDialog("CYDOY · Island Test", "Renderer 1 mesh is not readable.", "OK");
                return;
            }

            List<Island> islands = BuildIslands(_originalMesh);
            if (islands.Count == 0)
                return;

            int current = EditorPrefs.GetInt(CurrentIslandKey, 0);
            current++;
            if (current > islands.Count)
                current = 1;

            // Skip the already confirmed backpack body and cap/hood island.
            if (current == 12 || current == 13)
            {
                current = 14;
                if (current > islands.Count) current = 1;
            }

            EditorPrefs.SetInt(CurrentIslandKey, current);
            Island remove = islands[current - 1];
            Mesh preview = CreateMeshWithoutIsland(_originalMesh, remove);
            preview.name = _originalMesh.name + "_QuickIslandTest_" + current;
            _renderer.sharedMesh = preview;

            SceneView.RepaintAll();
            Debug.Log($"[CYDOY] QUICK ISLAND TEST: Renderer 1 / Island {current} hidden on selected NPC '{selected.name}'. If the leftover package disappears, remember Island {current}. Use Reset Test to restore the NPC.");
        }

        [MenuItem("Tools/CYDOY/Backpack Leftovers/Reset Test")]
        public static void ResetTest()
        {
            RestoreInternal();
            EditorPrefs.SetInt(CurrentIslandKey, 0);
            SceneView.RepaintAll();
            Debug.Log("[CYDOY] Quick island test reset.");
        }

        private static void RestoreInternal()
        {
            if (_renderer != null && _originalMesh != null)
                _renderer.sharedMesh = _originalMesh;

            _renderer = null;
            _originalMesh = null;
        }

        private sealed class Island
        {
            public int subMesh;
            public List<int> triangles = new();
        }

        private static List<Island> BuildIslands(Mesh mesh)
        {
            List<Island> islands = new();

            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] tris = mesh.GetTriangles(sub);
                int triCount = tris.Length / 3;
                Dictionary<int, List<int>> vertexToTriangles = new();

                for (int t = 0; t < triCount; t++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int v = tris[t * 3 + k];
                        if (!vertexToTriangles.TryGetValue(v, out List<int> list))
                        {
                            list = new List<int>();
                            vertexToTriangles[v] = list;
                        }
                        list.Add(t);
                    }
                }

                bool[] visited = new bool[triCount];
                Queue<int> queue = new();

                for (int start = 0; start < triCount; start++)
                {
                    if (visited[start]) continue;

                    Island island = new() { subMesh = sub };
                    visited[start] = true;
                    queue.Enqueue(start);

                    while (queue.Count > 0)
                    {
                        int tri = queue.Dequeue();
                        int a = tris[tri * 3];
                        int b = tris[tri * 3 + 1];
                        int c = tris[tri * 3 + 2];
                        island.triangles.Add(a);
                        island.triangles.Add(b);
                        island.triangles.Add(c);

                        int[] verts = { a, b, c };
                        foreach (int vertex in verts)
                        {
                            foreach (int neighbor in vertexToTriangles[vertex])
                            {
                                if (visited[neighbor]) continue;
                                visited[neighbor] = true;
                                queue.Enqueue(neighbor);
                            }
                        }
                    }

                    islands.Add(island);
                }
            }

            return islands.OrderByDescending(i => i.triangles.Count).ToList();
        }

        private static Mesh CreateMeshWithoutIsland(Mesh source, Island remove)
        {
            Mesh result = UnityEngine.Object.Instantiate(source);
            HashSet<string> removeKeys = new();

            for (int i = 0; i < remove.triangles.Count; i += 3)
                removeKeys.Add(Key(remove.triangles[i], remove.triangles[i + 1], remove.triangles[i + 2]));

            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] tris = source.GetTriangles(sub);
                if (sub != remove.subMesh)
                {
                    result.SetTriangles(tris, sub, false);
                    continue;
                }

                List<int> kept = new();
                for (int i = 0; i < tris.Length; i += 3)
                {
                    if (!removeKeys.Contains(Key(tris[i], tris[i + 1], tris[i + 2])))
                    {
                        kept.Add(tris[i]);
                        kept.Add(tris[i + 1]);
                        kept.Add(tris[i + 2]);
                    }
                }

                result.SetTriangles(kept, sub, false);
            }

            result.RecalculateBounds();
            return result;
        }

        private static string Key(int a, int b, int c)
        {
            int[] x = { a, b, c };
            Array.Sort(x);
            return $"{x[0]}_{x[1]}_{x[2]}";
        }
    }
}

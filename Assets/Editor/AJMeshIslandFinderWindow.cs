using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public sealed class AJMeshIslandFinderWindow : EditorWindow
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string GeneratedFolder = "Assets/Models/Characters/Generated";

        private GameObject _root;
        private Transform _aj;
        private SkinnedMeshRenderer[] _renderers = Array.Empty<SkinnedMeshRenderer>();
        private int _rendererIndex;
        private List<Island> _islands = new();
        private Vector2 _scroll;

        private sealed class Island
        {
            public int subMesh;
            public List<int> triangles = new();
            public HashSet<int> vertices = new();
            public Bounds localBounds;
        }

        [MenuItem("Tools/CYDOY/AJ Mesh Island Finder")]
        public static void Open()
        {
            AJMeshIslandFinderWindow w = GetWindow<AJMeshIslandFinderWindow>("AJ Mesh Islands");
            w.minSize = new Vector2(620, 420);
            w.Reload();
        }

        private void OnEnable() => Reload();
        private void OnDisable() => Cleanup();

        private void Reload()
        {
            Cleanup();
            _root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            _aj = FindRecursive(_root.transform, "Mixamo_AJ");
            _renderers = _aj != null ? _aj.GetComponentsInChildren<SkinnedMeshRenderer>(true) : Array.Empty<SkinnedMeshRenderer>();
            _rendererIndex = Mathf.Clamp(_rendererIndex, 0, Mathf.Max(0, _renderers.Length - 1));
            BuildIslands();
            Repaint();
        }

        private void Cleanup()
        {
            if (_root != null)
                PrefabUtility.UnloadPrefabContents(_root);
            _root = null;
            _aj = null;
            _renderers = Array.Empty<SkinnedMeshRenderer>();
            _islands.Clear();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("AJ Mesh Island Finder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Der Rucksack ist offenbar innerhalb eines SkinnedMeshes eingebaut. Dieses Tool zerlegt das gewählte Mesh in getrennte, zusammenhängende Geometrie-Inseln. " +
                "Mit 'Insel testweise entfernen' kannst du einzeln prüfen, bei welcher Insel nur der Rucksack verschwindet.",
                MessageType.Info);

            if (_renderers.Length == 0)
            {
                EditorGUILayout.HelpBox("Keine SkinnedMeshRenderer gefunden.", MessageType.Error);
                return;
            }

            string[] labels = _renderers.Select((r, i) => $"{i + 1}: {r.name} / {(r.sharedMesh != null ? r.sharedMesh.name : "<none>")}").ToArray();
            int newIndex = EditorGUILayout.Popup("Renderer", _rendererIndex, labels);
            if (newIndex != _rendererIndex)
            {
                _rendererIndex = newIndex;
                BuildIslands();
            }

            if (GUILayout.Button("Neu analysieren"))
                BuildIslands();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Gefundene Inseln: {_islands.Count}");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _islands.Count; i++)
            {
                Island island = _islands[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Insel {i + 1}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("SubMesh", island.subMesh.ToString());
                EditorGUILayout.LabelField("Dreiecke", (island.triangles.Count / 3).ToString());
                EditorGUILayout.LabelField("Vertices", island.vertices.Count.ToString());
                EditorGUILayout.LabelField("Bounds Center", island.localBounds.center.ToString("F3"));
                EditorGUILayout.LabelField("Bounds Size", island.localBounds.size.ToString("F3"));

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Insel testweise entfernen"))
                    PreviewRemoveIsland(i);
                if (GUILayout.Button("Diese Insel = Backpack dauerhaft entfernen"))
                    PermanentlyRemoveIsland(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Änderungen verwerfen / neu laden"))
                Reload();
        }

        private void BuildIslands()
        {
            _islands.Clear();
            if (_rendererIndex < 0 || _rendererIndex >= _renderers.Length)
                return;

            Mesh mesh = _renderers[_rendererIndex].sharedMesh;
            if (mesh == null || !mesh.isReadable)
                return;

            Vector3[] verts = mesh.vertices;

            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] tris = mesh.GetTriangles(sub);
                int triCount = tris.Length / 3;

                Dictionary<int, List<int>> vertexToTri = new();
                for (int t = 0; t < triCount; t++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int v = tris[t * 3 + k];
                        if (!vertexToTri.TryGetValue(v, out List<int> list))
                        {
                            list = new List<int>();
                            vertexToTri[v] = list;
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
                    queue.Enqueue(start);
                    visited[start] = true;

                    while (queue.Count > 0)
                    {
                        int t = queue.Dequeue();
                        int a = tris[t * 3];
                        int b = tris[t * 3 + 1];
                        int c = tris[t * 3 + 2];

                        island.triangles.Add(a);
                        island.triangles.Add(b);
                        island.triangles.Add(c);
                        island.vertices.Add(a);
                        island.vertices.Add(b);
                        island.vertices.Add(c);

                        foreach (int v in new[] { a, b, c })
                        {
                            foreach (int n in vertexToTri[v])
                            {
                                if (visited[n]) continue;
                                visited[n] = true;
                                queue.Enqueue(n);
                            }
                        }
                    }

                    if (island.vertices.Count > 0)
                    {
                        int first = island.vertices.First();
                        Bounds b = new(verts[first], Vector3.zero);
                        foreach (int v in island.vertices)
                            b.Encapsulate(verts[v]);
                        island.localBounds = b;
                    }

                    _islands.Add(island);
                }
            }

            _islands = _islands.OrderByDescending(i => i.triangles.Count).ToList();
        }

        private void PreviewRemoveIsland(int islandIndex)
        {
            if (!TryCreateMeshWithoutIsland(islandIndex, out Mesh preview))
                return;

            SkinnedMeshRenderer renderer = _renderers[_rendererIndex];
            renderer.sharedMesh = preview;
            SceneView.RepaintAll();

            EditorUtility.DisplayDialog(
                "CYDOY · Test",
                "Die gewählte Insel wurde nur in dieser temporären Vorschau entfernt. Schau jetzt im Scene-/Prefab-Fenster, ob genau der Rucksack verschwunden ist. Mit 'Änderungen verwerfen / neu laden' stellst du alles wieder her.",
                "OK");
        }

        private void PermanentlyRemoveIsland(int islandIndex)
        {
            // Reload a clean prefab copy before making the permanent asset.
            string rendererName = _renderers[_rendererIndex].name;
            string meshName = _renderers[_rendererIndex].sharedMesh != null ? _renderers[_rendererIndex].sharedMesh.name : string.Empty;
            int requestedIsland = islandIndex;

            Reload();

            _rendererIndex = Array.FindIndex(_renderers, r => r != null && r.name == rendererName && r.sharedMesh != null && r.sharedMesh.name == meshName);
            if (_rendererIndex < 0)
                _rendererIndex = 0;
            BuildIslands();

            if (requestedIsland < 0 || requestedIsland >= _islands.Count)
                return;

            if (!TryCreateMeshWithoutIsland(requestedIsland, out Mesh cleaned))
                return;

            EnsureGeneratedFolder();
            SkinnedMeshRenderer renderer = _renderers[_rendererIndex];
            string safe = Sanitize(renderer.name);
            string path = $"{GeneratedFolder}/AJ_NoBackpack_Island_{safe}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(cleaned, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            renderer.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            EditorUtility.SetDirty(renderer);
            PrefabUtility.SaveAsPrefabAsset(_root, PlayerPrefabPath);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "CYDOY · Backpack entfernt",
                "Die ausgewählte Geometrie-Insel wurde dauerhaft aus dem Player-AJ entfernt. Wenn das die richtige Insel war, erzeuge danach die NPCs neu.",
                "Perfekt");

            Reload();
        }

        private bool TryCreateMeshWithoutIsland(int islandIndex, out Mesh result)
        {
            result = null;
            if (_rendererIndex < 0 || _rendererIndex >= _renderers.Length || islandIndex < 0 || islandIndex >= _islands.Count)
                return false;

            Mesh source = _renderers[_rendererIndex].sharedMesh;
            if (source == null || !source.isReadable)
                return false;

            Island remove = _islands[islandIndex];
            result = Instantiate(source);
            result.name = source.name + "_IslandPreview";

            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] tris = source.GetTriangles(sub);
                if (sub != remove.subMesh)
                {
                    result.SetTriangles(tris, sub, false);
                    continue;
                }

                HashSet<string> removeKeys = new();
                for (int i = 0; i < remove.triangles.Count; i += 3)
                {
                    int a = remove.triangles[i];
                    int b = remove.triangles[i + 1];
                    int c = remove.triangles[i + 2];
                    removeKeys.Add(Key(a, b, c));
                }

                List<int> kept = new();
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int a = tris[i];
                    int b = tris[i + 1];
                    int c = tris[i + 2];
                    if (!removeKeys.Contains(Key(a, b, c)))
                    {
                        kept.Add(a); kept.Add(b); kept.Add(c);
                    }
                }

                result.SetTriangles(kept, sub, false);
            }

            result.RecalculateBounds();
            return true;
        }

        private static string Key(int a, int b, int c)
        {
            int[] x = { a, b, c };
            Array.Sort(x);
            return $"{x[0]}_{x[1]}_{x[2]}";
        }

        private static Transform FindRecursive(Transform root, string targetName)
        {
            if (root.name == targetName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform r = FindRecursive(root.GetChild(i), targetName);
                if (r != null) return r;
            }
            return null;
        }

        private static void EnsureGeneratedFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models")) AssetDatabase.CreateFolder("Assets", "Models");
            if (!AssetDatabase.IsValidFolder("Assets/Models/Characters")) AssetDatabase.CreateFolder("Assets/Models", "Characters");
            if (!AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.CreateFolder("Assets/Models/Characters", "Generated");
        }

        private static string Sanitize(string s)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        }
    }
}

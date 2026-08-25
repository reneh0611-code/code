using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public sealed class AJSelectedNPCMeshIslandTester : EditorWindow
    {
        private SkinnedMeshRenderer[] _renderers = Array.Empty<SkinnedMeshRenderer>();
        private int _rendererIndex;
        private List<Island> _islands = new();
        private Vector2 _scroll;
        private GameObject _selectedNpc;
        private Mesh _originalMesh;
        private SkinnedMeshRenderer _activeRenderer;

        private sealed class Island
        {
            public int subMesh;
            public List<int> triangles = new();
            public HashSet<int> vertices = new();
            public Bounds localBounds;
        }

        [MenuItem("Tools/CYDOY/Test Mesh Islands On Selected NPC")]
        public static void Open()
        {
            AJSelectedNPCMeshIslandTester w = GetWindow<AJSelectedNPCMeshIslandTester>("NPC Mesh Islands");
            w.minSize = new Vector2(620, 420);
            w.BindSelection();
        }

        private void OnSelectionChange()
        {
            RestorePreview();
            BindSelection();
            Repaint();
        }

        private void OnDisable()
        {
            RestorePreview();
        }

        private void BindSelection()
        {
            _selectedNpc = Selection.activeGameObject;
            if (_selectedNpc == null)
            {
                _renderers = Array.Empty<SkinnedMeshRenderer>();
                _islands.Clear();
                return;
            }

            _renderers = _selectedNpc.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _rendererIndex = Mathf.Clamp(_rendererIndex, 0, Mathf.Max(0, _renderers.Length - 1));
            BuildIslands();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Selected NPC Mesh Island Tester", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Wähle in der Hierarchy direkt einen NPC unter Generated_NPCs aus. Dieses Tool verändert genau diesen NPC in der offenen Szene als Vorschau. " +
                "Damit siehst du sofort, welche Mesh-Insel der Rucksack ist.",
                MessageType.Info);

            if (_selectedNpc == null)
            {
                EditorGUILayout.HelpBox("Kein GameObject ausgewählt.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Ausgewählt", _selectedNpc.name);

            if (_renderers.Length == 0)
            {
                EditorGUILayout.HelpBox("Im ausgewählten Objekt wurden keine SkinnedMeshRenderer gefunden.", MessageType.Warning);
                return;
            }

            string[] labels = _renderers.Select((r, i) => $"{i + 1}: {r.name} / {(r.sharedMesh != null ? r.sharedMesh.name : "<none>")}").ToArray();
            int next = EditorGUILayout.Popup("Renderer", _rendererIndex, labels);
            if (next != _rendererIndex)
            {
                RestorePreview();
                _rendererIndex = next;
                BuildIslands();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Vorschau zurücksetzen"))
            {
                RestorePreview();
                BuildIslands();
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Auswahl neu einlesen"))
            {
                RestorePreview();
                BindSelection();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Gefundene Inseln: {_islands.Count}");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _islands.Count; i++)
            {
                Island island = _islands[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Insel {i + 1}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Dreiecke", (island.triangles.Count / 3).ToString());
                EditorGUILayout.LabelField("Vertices", island.vertices.Count.ToString());
                EditorGUILayout.LabelField("Bounds Center", island.localBounds.center.ToString("F3"));
                EditorGUILayout.LabelField("Bounds Size", island.localBounds.size.ToString("F3"));

                if (GUILayout.Button("DIESE Insel am ausgewählten NPC ausblenden"))
                    PreviewRemoveIsland(i);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(
                "Wenn du die richtige Insel findest, merke dir Renderer-Nummer + Insel-Nummer. Dann kann ich exakt diese Geometrie dauerhaft aus Player-AJ und damit auch aus allen neu erzeugten NPCs entfernen.",
                MessageType.None);
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
                            foreach (int neighbor in vertexToTri[v])
                            {
                                if (visited[neighbor]) continue;
                                visited[neighbor] = true;
                                queue.Enqueue(neighbor);
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

            _islands = _islands.OrderByDescending(x => x.triangles.Count).ToList();
        }

        private void PreviewRemoveIsland(int islandIndex)
        {
            RestorePreview();

            if (_rendererIndex < 0 || _rendererIndex >= _renderers.Length || islandIndex < 0 || islandIndex >= _islands.Count)
                return;

            SkinnedMeshRenderer renderer = _renderers[_rendererIndex];
            Mesh source = renderer.sharedMesh;
            if (source == null || !source.isReadable)
                return;

            Island remove = _islands[islandIndex];
            Mesh preview = Instantiate(source);
            preview.name = source.name + "_NPCPreviewWithoutIsland";

            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] tris = source.GetTriangles(sub);
                if (sub != remove.subMesh)
                {
                    preview.SetTriangles(tris, sub, false);
                    continue;
                }

                HashSet<string> removeKeys = new();
                for (int i = 0; i < remove.triangles.Count; i += 3)
                    removeKeys.Add(Key(remove.triangles[i], remove.triangles[i + 1], remove.triangles[i + 2]));

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

                preview.SetTriangles(kept, sub, false);
            }

            preview.RecalculateBounds();
            _activeRenderer = renderer;
            _originalMesh = source;
            renderer.sharedMesh = preview;
            SceneView.RepaintAll();
        }

        private void RestorePreview()
        {
            if (_activeRenderer != null && _originalMesh != null)
                _activeRenderer.sharedMesh = _originalMesh;

            _activeRenderer = null;
            _originalMesh = null;
        }

        private static string Key(int a, int b, int c)
        {
            int[] x = { a, b, c };
            Array.Sort(x);
            return $"{x[0]}_{x[1]}_{x[2]}";
        }
    }
}

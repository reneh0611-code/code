using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public sealed class AJRendererFinderWindow : EditorWindow
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";

        private GameObject _previewRoot;
        private Transform _ajRoot;
        private SkinnedMeshRenderer[] _renderers = Array.Empty<SkinnedMeshRenderer>();
        private Vector2 _scroll;
        private int _activeIndex = -1;

        [MenuItem("Tools/CYDOY/AJ Renderer Finder")]
        public static void Open()
        {
            AJRendererFinderWindow window = GetWindow<AJRendererFinderWindow>("AJ Renderer Finder");
            window.minSize = new Vector2(520f, 360f);
            window.Reload();
        }

        private void OnEnable()
        {
            Reload();
        }

        private void OnDisable()
        {
            CleanupPreview();
        }

        private void Reload()
        {
            CleanupPreview();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
                return;

            _previewRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            _ajRoot = FindRecursive(_previewRoot.transform, "Mixamo_AJ");
            _renderers = _ajRoot != null
                ? _ajRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                : Array.Empty<SkinnedMeshRenderer>();

            Repaint();
        }

        private void CleanupPreview()
        {
            if (_previewRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(_previewRoot);
                _previewRoot = null;
            }

            _ajRoot = null;
            _renderers = Array.Empty<SkinnedMeshRenderer>();
            _activeIndex = -1;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("AJ Backpack Renderer Finder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Der Rucksack steckt offenbar in einem SkinnedMesh mit neutralem Namen. " +
                "Schalte die vier Renderer einzeln aus. Sobald der Rucksack verschwindet, " +
                "klicke bei genau diesem Renderer auf 'Als Backpack markieren & speichern'.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            if (_previewRoot == null || _ajRoot == null)
            {
                EditorGUILayout.HelpBox("Player.prefab oder Mixamo_AJ konnte nicht geladen werden.", MessageType.Error);
                if (GUILayout.Button("Neu laden")) Reload();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Alle Renderer AN"))
            {
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null) _renderers[i].enabled = true;
                _activeIndex = -1;
            }
            if (GUILayout.Button("Neu laden / Änderungen verwerfen"))
                Reload();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"Gefundene SkinnedMeshRenderer: {_renderers.Length}");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < _renderers.Length; i++)
            {
                SkinnedMeshRenderer r = _renderers[i];
                if (r == null) continue;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Renderer {i + 1}: {r.name}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Mesh", r.sharedMesh != null ? r.sharedMesh.name : "<none>");
                EditorGUILayout.LabelField("Material", string.Join(", ", r.sharedMaterials.Where(m => m != null).Select(m => m.name)));
                EditorGUILayout.LabelField("Aktiv", r.enabled ? "JA" : "NEIN");

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(r.enabled ? "AUS zum Testen" : "AN"))
                {
                    r.enabled = !r.enabled;
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("Nur diesen AUS"))
                {
                    for (int j = 0; j < _renderers.Length; j++)
                        if (_renderers[j] != null) _renderers[j].enabled = true;
                    r.enabled = false;
                    _activeIndex = i;
                    SceneView.RepaintAll();
                }
                EditorGUILayout.EndHorizontal();

                GUI.backgroundColor = new Color(0.75f, 0.9f, 0.75f);
                if (GUILayout.Button("Als Backpack markieren & dauerhaft ausblenden"))
                {
                    SaveAsBackpack(i);
                    GUI.backgroundColor = Color.white;
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Tipp: Öffne parallel das Player.prefab oder die Szene im Scene-Fenster. " +
                "Mit 'Nur diesen AUS' siehst du sofort, welches Mesh verschwindet.",
                MessageType.None);
        }

        private void SaveAsBackpack(int index)
        {
            if (index < 0 || index >= _renderers.Length || _renderers[index] == null)
                return;

            string rendererName = _renderers[index].name;
            string meshName = _renderers[index].sharedMesh != null ? _renderers[index].sharedMesh.name : string.Empty;

            // Re-open a clean prefab instance so only the chosen renderer is permanently disabled.
            CleanupPreview();
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            try
            {
                Transform aj = FindRecursive(root.transform, "Mixamo_AJ");
                if (aj == null)
                    return;

                SkinnedMeshRenderer[] renderers = aj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                SkinnedMeshRenderer chosen = renderers.FirstOrDefault(r =>
                    r != null &&
                    r.name == rendererName &&
                    ((r.sharedMesh != null ? r.sharedMesh.name : string.Empty) == meshName));

                if (chosen == null && index < renderers.Length)
                    chosen = renderers[index];

                if (chosen == null)
                    return;

                chosen.enabled = false;
                chosen.gameObject.name = chosen.gameObject.name + "_CYDOY_BACKPACK_HIDDEN";
                EditorUtility.SetDirty(chosen);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog(
                    "CYDOY · Backpack gefunden",
                    $"Renderer dauerhaft deaktiviert:\n\n{rendererName}\nMesh: {meshName}\n\nErzeuge danach die NPCs einmal neu, damit sie denselben rucksackfreien AJ übernehmen.",
                    "Perfekt");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Reload();
        }

        private static Transform FindRecursive(Transform root, string targetName)
        {
            if (root.name == targetName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindRecursive(root.GetChild(i), targetName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}

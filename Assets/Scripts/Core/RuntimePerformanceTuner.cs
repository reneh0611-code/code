using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.Core
{
    /// <summary>
    /// Runtime-only rendering budget. This does not edit terrain height, roads, painted details or
    /// tree placement; it only prevents distant content from consuming the whole frame budget.
    /// </summary>
    public sealed class RuntimePerformanceTuner : MonoBehaviour
    {
        private const float MaximumTreeDistance = 420f;
        private const float MaximumDetailDistance = 65f;
        private const float MaximumBasemapDistance = 450f;
        private const float MinimumHeightmapPixelError = 12f;
        private const int MaximumFullLodTrees = 24;
        private const float MaximumShadowDistance = 65f;

        private static RuntimePerformanceTuner _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateAutomatically()
        {
            if (_instance != null) return;
            GameObject go = new("CYDOY_RuntimePerformance");
            _instance = go.AddComponent<RuntimePerformanceTuner>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Time.maximumDeltaTime = 0.1f;
            QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, MaximumShadowDistance);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start() => StartCoroutine(ApplyAfterSceneSettles());

        private void OnDestroy()
        {
            if (_instance == this)
                SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(ApplyAfterSceneSettles());

        private static IEnumerator ApplyAfterSceneSettles()
        {
            yield return null;

            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                if (terrain == null) continue;
                terrain.treeDistance = Mathf.Min(terrain.treeDistance, MaximumTreeDistance);
                terrain.treeBillboardDistance = Mathf.Min(terrain.treeBillboardDistance, 55f);
                terrain.treeMaximumFullLODCount = Mathf.Min(terrain.treeMaximumFullLODCount, MaximumFullLodTrees);
                terrain.detailObjectDistance = Mathf.Min(terrain.detailObjectDistance, MaximumDetailDistance);
                terrain.basemapDistance = Mathf.Min(terrain.basemapDistance, MaximumBasemapDistance);
                terrain.heightmapPixelError = Mathf.Max(terrain.heightmapPixelError, MinimumHeightmapPixelError);
                terrain.drawInstanced = true;
            }
        }
    }
}

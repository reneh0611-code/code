using System.Collections;
using CheatOnYourDayOnes.Player;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Core
{
    public sealed class AutoLocalHost : MonoBehaviour
    {
        [SerializeField] private bool autoStartHost = true;
        [SerializeField, Min(1f)] private float terrainEdgeMargin = 10f;

        private IEnumerator Start()
        {
            yield return null;

            NetworkManager manager = GetComponent<NetworkManager>();
            if (manager == null || !autoStartHost) yield break;

            if (!manager.IsListening && !manager.StartHost())
            {
                Debug.LogError("[CYDOY AUTO] Failed to start local host.");
                yield break;
            }

            NetworkObject player = null;
            for (int i = 0; i < 120 && player == null; i++)
            {
                player = manager.LocalClient?.PlayerObject;
                if (player == null) yield return null;
            }

            if (player == null)
            {
                Debug.LogError("[CYDOY AUTO] Local player was not spawned within two seconds.");
                yield break;
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            for (int i = 0; i < 120; i++)
            {
                bool foundCenter = TryFindFlatTerrainCenterSpawn(controller, terrainEdgeMargin, out Vector3 safePosition);
                if (foundCenter || TryFindSafeTerrainSpawn(player.transform.position, controller, terrainEdgeMargin, out safePosition))
                {
                    NetworkPlayerController movement = player.GetComponent<NetworkPlayerController>();
                    if (movement != null)
                        movement.TeleportServerAuthoritative(safePosition);
                    else
                        TeleportTransform(player.transform, controller, safePosition);

                    Debug.Log($"[CYDOY AUTO] Local player safely placed on terrain at {safePosition}.");
                    yield break;
                }
                yield return null;
            }

            Debug.LogError("[CYDOY AUTO] No active terrain collider was available for safe player placement.");
        }

        public static bool TryFindFlatTerrainCenterSpawn(
            CharacterController controller,
            float edgeMargin,
            out Vector3 safePosition)
        {
            safePosition = default;
            Terrain bestTerrain = null;
            Vector3 bestSample = default;
            float bestScore = float.MaxValue;

            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                if (terrain == null || !terrain.isActiveAndEnabled || terrain.terrainData == null) continue;
                TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
                if (collider == null || !collider.enabled) continue;

                TerrainData data = terrain.terrainData;
                Vector3 origin = terrain.transform.position;
                Vector3 size = data.size;
                float marginX = Mathf.Min(Mathf.Max(.5f, edgeMargin), Mathf.Max(.5f, size.x * .5f - .5f));
                float marginZ = Mathf.Min(Mathf.Max(.5f, edgeMargin), Mathf.Max(.5f, size.z * .5f - .5f));
                float probeRadius = Mathf.Clamp(Mathf.Min(size.x, size.z) * .01f, 2f, 6f);

                // Search only the central quarter of the terrain. Flatness has priority; distance
                // from the exact center is the tie-breaker, so the player never starts at an edge.
                for (int ix = -4; ix <= 4; ix++)
                for (int iz = -4; iz <= 4; iz++)
                {
                    float normalizedX = .5f + ix * .03f;
                    float normalizedZ = .5f + iz * .03f;
                    float x = Mathf.Clamp(origin.x + normalizedX * size.x, origin.x + marginX, origin.x + size.x - marginX);
                    float z = Mathf.Clamp(origin.z + normalizedZ * size.z, origin.z + marginZ, origin.z + size.z - marginZ);
                    normalizedX = Mathf.InverseLerp(origin.x, origin.x + size.x, x);
                    normalizedZ = Mathf.InverseLerp(origin.z, origin.z + size.z, z);

                    Vector3 sample = new(x, 0f, z);
                    float centerHeight = terrain.SampleHeight(sample) + origin.y;
                    float minHeight = centerHeight;
                    float maxHeight = centerHeight;
                    Vector3[] offsets =
                    {
                        new(probeRadius, 0f, 0f), new(-probeRadius, 0f, 0f),
                        new(0f, 0f, probeRadius), new(0f, 0f, -probeRadius)
                    };
                    foreach (Vector3 offset in offsets)
                    {
                        float height = terrain.SampleHeight(sample + offset) + origin.y;
                        minHeight = Mathf.Min(minHeight, height);
                        maxHeight = Mathf.Max(maxHeight, height);
                    }

                    float heightVariation = maxHeight - minHeight;
                    float slopePenalty = 1f - data.GetInterpolatedNormal(normalizedX, normalizedZ).y;
                    float centerPenalty = (ix * ix + iz * iz) * .0025f;
                    float score = heightVariation * 5f + slopePenalty * 20f + centerPenalty;
                    if (score >= bestScore) continue;

                    bestScore = score;
                    bestTerrain = terrain;
                    bestSample = sample;
                }
            }

            if (bestTerrain == null) return false;
            float groundY = bestTerrain.SampleHeight(bestSample) + bestTerrain.transform.position.y;
            float bottomOffset = 0f;
            if (controller != null)
            {
                float scaleY = Mathf.Abs(controller.transform.lossyScale.y);
                bottomOffset = (controller.center.y - controller.height * .5f) * scaleY;
            }
            safePosition = new Vector3(bestSample.x, groundY - bottomOffset + .012f, bestSample.z);
            return true;
        }

        public static bool TryFindSafeTerrainSpawn(
            Vector3 desiredPosition,
            CharacterController controller,
            float edgeMargin,
            out Vector3 safePosition)
        {
            safePosition = desiredPosition;
            Terrain bestTerrain = null;
            Vector3 bestSample = desiredPosition;
            float bestHorizontalDistance = float.MaxValue;

            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                if (terrain == null || !terrain.isActiveAndEnabled || terrain.terrainData == null) continue;
                TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
                if (collider == null || !collider.enabled) continue;

                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                float marginX = Mathf.Min(Mathf.Max(0.5f, edgeMargin), Mathf.Max(0.5f, size.x * 0.5f - 0.5f));
                float marginZ = Mathf.Min(Mathf.Max(0.5f, edgeMargin), Mathf.Max(0.5f, size.z * 0.5f - 0.5f));
                float x = Mathf.Clamp(desiredPosition.x, origin.x + marginX, origin.x + size.x - marginX);
                float z = Mathf.Clamp(desiredPosition.z, origin.z + marginZ, origin.z + size.z - marginZ);
                float sqrDistance = (new Vector2(x, z) - new Vector2(desiredPosition.x, desiredPosition.z)).sqrMagnitude;

                if (sqrDistance >= bestHorizontalDistance) continue;
                bestHorizontalDistance = sqrDistance;
                bestTerrain = terrain;
                bestSample = new Vector3(x, 0f, z);
            }

            if (bestTerrain == null) return false;

            float groundY = bestTerrain.SampleHeight(bestSample) + bestTerrain.transform.position.y;
            float bottomOffset = 0f;
            if (controller != null)
            {
                float scaleY = Mathf.Abs(controller.transform.lossyScale.y);
                bottomOffset = (controller.center.y - controller.height * 0.5f) * scaleY;
            }

            // Keep only a tiny numerical clearance. The former 8 cm clearance was visually
            // noticeable because the selected mesh is aligned to the controller root.
            safePosition = new Vector3(bestSample.x, groundY - bottomOffset + 0.012f, bestSample.z);
            return true;
        }

        private static void TeleportTransform(Transform player, CharacterController controller, Vector3 position)
        {
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled) controller.enabled = false;
            player.position = position;
            if (wasEnabled) controller.enabled = true;
            Physics.SyncTransforms();
        }
    }
}

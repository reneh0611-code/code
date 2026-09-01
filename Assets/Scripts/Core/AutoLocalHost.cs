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

        [Header("Preferred spawn")]
        [SerializeField] private string preferredSpawnBuildingName = "CONVENIENCE STORE - READY";
        [SerializeField, Min(.5f)] private float entranceSpawnDistance = 2.2f;
        [SerializeField] private bool useParkingSpawnFallback = true;
        [SerializeField] private Vector3 parkingSpawnPosition = new(258.51f, .06f, 676.44f);
        [SerializeField, Range(0f, 360f)] private float parkingSpawnYaw = 130f;

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
                bool foundPreferredSpawn = TryFindPreferredBuildingSpawn(
                    controller,
                    out Vector3 safePosition,
                    out Quaternion spawnRotation);
                bool foundCenter = !foundPreferredSpawn &&
                                   TryFindFlatTerrainCenterSpawn(controller, terrainEdgeMargin, out safePosition);
                if (foundPreferredSpawn || foundCenter ||
                    TryFindSafeTerrainSpawn(player.transform.position, controller, terrainEdgeMargin, out safePosition))
                {
                    NetworkPlayerController movement = player.GetComponent<NetworkPlayerController>();
                    if (movement != null)
                        movement.TeleportServerAuthoritative(
                            safePosition,
                            foundPreferredSpawn ? spawnRotation : player.transform.rotation);
                    else
                    {
                        TeleportTransform(player.transform, controller, safePosition);
                        if (foundPreferredSpawn) player.transform.rotation = spawnRotation;
                    }

                    string spawnDescription = foundPreferredSpawn
                        ? "on the building parking lot"
                        : "on terrain";
                    Debug.Log($"[CYDOY AUTO] Local player safely placed {spawnDescription} at {safePosition}.");
                    yield break;
                }
                yield return null;
            }

            Debug.LogError("[CYDOY AUTO] No active terrain collider was available for safe player placement.");
        }

        private bool TryFindPreferredBuildingSpawn(
            CharacterController controller,
            out Vector3 safePosition,
            out Quaternion spawnRotation)
        {
            safePosition = default;
            spawnRotation = Quaternion.identity;

            // A manually placed marker always wins. This makes future fine tuning possible by
            // moving an empty GameObject named PlayerSpawn without touching this script again.
            GameObject marker = GameObject.Find("PlayerSpawn");
            if (marker != null)
            {
                spawnRotation = Quaternion.Euler(0f, marker.transform.eulerAngles.y, 0f);
                return TryFindSafeTerrainSpawn(marker.transform.position, controller, 1f, out safePosition);
            }

            // Keep the intended world start stable even if the marker is accidentally
            // removed while the modular building is being edited.
            if (useParkingSpawnFallback)
            {
                spawnRotation = Quaternion.Euler(0f, parkingSpawnYaw, 0f);
                return TryFindSafeTerrainSpawn(parkingSpawnPosition, controller, 1f, out safePosition);
            }

            if (string.IsNullOrWhiteSpace(preferredSpawnBuildingName)) return false;
            GameObject building = GameObject.Find(preferredSpawnBuildingName);
            if (building == null) return false;

            Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;

            Bounds buildingBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                buildingBounds.Encapsulate(renderers[i].bounds);

            Renderer entranceAnchor = FindEntranceAnchor(renderers);
            Vector3 anchorPosition = entranceAnchor != null
                ? entranceAnchor.bounds.center
                : buildingBounds.center + building.transform.forward * buildingBounds.extents.z;
            Vector3 outward = anchorPosition - buildingBounds.center;
            outward.y = 0f;
            if (outward.sqrMagnitude < .01f)
            {
                outward = building.transform.forward;
                outward.y = 0f;
            }
            outward.Normalize();

            // The welcome mat/front-door mesh gives us the real entrance side, even after the
            // prefab has been rotated. Place the player just beyond that mesh instead of using a
            // hard-coded world coordinate.
            float anchorExtent = 0f;
            if (entranceAnchor != null)
            {
                Vector3 extents = entranceAnchor.bounds.extents;
                anchorExtent = Mathf.Abs(outward.x) * extents.x + Mathf.Abs(outward.z) * extents.z;
            }

            Vector3 desiredPosition = anchorPosition + outward * (anchorExtent + entranceSpawnDistance);
            if (!TryFindSafeTerrainSpawn(desiredPosition, controller, 1f, out safePosition)) return false;

            Vector3 lookDirection = anchorPosition - safePosition;
            lookDirection.y = 0f;
            spawnRotation = lookDirection.sqrMagnitude > .01f
                ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                : Quaternion.Euler(0f, building.transform.eulerAngles.y + 180f, 0f);
            return true;
        }

        private static Renderer FindEntranceAnchor(Renderer[] renderers)
        {
            Renderer best = null;
            int bestScore = int.MaxValue;
            foreach (Renderer candidate in renderers)
            {
                if (candidate == null) continue;
                string candidateName = candidate.gameObject.name.ToLowerInvariant();
                int score = int.MaxValue;
                if (candidateName == "welcome_matt") score = 0;
                else if (candidateName.StartsWith("welcome_matt")) score = 10;
                else if (candidateName == "welcome") score = 20;
                else if (candidateName.Contains("sliding_door")) score = 30;
                else if (candidateName.Contains("front_sign") || candidateName == "mart") score = 40;

                if (score >= bestScore) continue;
                best = candidate;
                bestScore = score;
            }
            return best;
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
            groundY = FindWalkableSurfaceHeight(bestSample, groundY, controller);
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

        private static float FindWalkableSurfaceHeight(
            Vector3 sample,
            float terrainHeight,
            CharacterController controller)
        {
            // Roads and parking modules sit a few centimetres above the terrain.
            // Prefer their actual upper surface so the controller never starts
            // inside the asphalt, while ignoring roofs and other high geometry.
            Vector3 origin = new(sample.x, terrainHeight + 2f, sample.z);
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                3f,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

            float highestWalkableY = terrainHeight;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.normal.y < .65f) continue;
                if (controller != null &&
                    (hit.collider == controller || hit.collider.transform.IsChildOf(controller.transform)))
                    continue;
                if (hit.point.y < terrainHeight - .2f || hit.point.y > terrainHeight + 1.25f) continue;

                highestWalkableY = Mathf.Max(highestWalkableY, hit.point.y);
            }

            return highestWalkableY;
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

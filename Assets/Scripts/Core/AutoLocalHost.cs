using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Core
{
    public sealed class AutoLocalHost : MonoBehaviour
    {
        [SerializeField] private bool autoStartHost = true;
        [SerializeField] private float spawnRayHeight = 500f;

        private IEnumerator Start()
        {
            yield return null;

            NetworkManager manager = GetComponent<NetworkManager>();
            if (manager == null || !autoStartHost) yield break;

            if (!manager.IsListening)
            {
                bool started = manager.StartHost();
                if (!started)
                {
                    Debug.LogError("[CYDOY AUTO] Failed to start local host.");
                    yield break;
                }
            }

            for (int i = 0; i < 60; i++)
            {
                NetworkObject player = manager.LocalClient?.PlayerObject;
                if (player != null)
                {
                    PlacePlayerOnTerrain(player.transform);
                    yield break;
                }
                yield return null;
            }
        }

        private void PlacePlayerOnTerrain(Transform player)
        {
            Vector3 origin = new(player.position.x, spawnRayHeight, player.position.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, spawnRayHeight * 2f, ~0, QueryTriggerInteraction.Ignore))
            {
                player.position = hit.point + Vector3.up * 1.05f;
                Debug.Log($"[CYDOY AUTO] Local player spawned on map at {player.position}.");
            }
        }
    }
}

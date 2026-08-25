using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Multiplayer
{
    public sealed class DevNetworkLauncher : MonoBehaviour
    {
        [SerializeField] private bool showDevelopmentGUI = true;

        private void OnGUI()
        {
            if (!showDevelopmentGUI || NetworkManager.Singleton == null)
                return;

            GUILayout.BeginArea(new Rect(20, 20, 220, 210), GUI.skin.box);

            if (!NetworkManager.Singleton.IsListening)
            {
                GUILayout.Label("CHEAT ON YOUR DAY ONES");
                GUILayout.Label("Phase 1 Network");

                if (GUILayout.Button("Start Host", GUILayout.Height(38)))
                    NetworkManager.Singleton.StartHost();

                if (GUILayout.Button("Start Client", GUILayout.Height(38)))
                    NetworkManager.Singleton.StartClient();

                if (GUILayout.Button("Start Server", GUILayout.Height(38)))
                    NetworkManager.Singleton.StartServer();
            }
            else
            {
                string role = NetworkManager.Singleton.IsHost ? "HOST" :
                              NetworkManager.Singleton.IsServer ? "SERVER" : "CLIENT";

                GUILayout.Label($"Running as {role}");
                GUILayout.Label($"Client ID: {NetworkManager.Singleton.LocalClientId}");

                if (GUILayout.Button("Shutdown"))
                    NetworkManager.Singleton.Shutdown();
            }

            GUILayout.EndArea();
        }
    }
}

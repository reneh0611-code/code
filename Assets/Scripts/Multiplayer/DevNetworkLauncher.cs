using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Multiplayer
{
    public sealed class DevNetworkLauncher : MonoBehaviour
    {
        [SerializeField] private bool showDevelopmentGUI = true;

        private GUIStyle _title;
        private GUIStyle _small;

        private void OnGUI()
        {
            if (!showDevelopmentGUI || NetworkManager.Singleton == null)
                return;

            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _small ??= new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.75f,0.78f,0.82f) } };

            if (!NetworkManager.Singleton.IsListening)
            {
                Rect area = new(Screen.width / 2f - 135f, 24f, 270f, 178f);
                GUILayout.BeginArea(area, GUI.skin.box);
                GUILayout.Label("CHEAT ON YOUR DAY ONES", _title);
                GUILayout.Label("Development Network", _small);
                GUILayout.Space(6);
                if (GUILayout.Button("START HOST", GUILayout.Height(34))) NetworkManager.Singleton.StartHost();
                if (GUILayout.Button("START CLIENT", GUILayout.Height(30))) NetworkManager.Singleton.StartClient();
                if (GUILayout.Button("START SERVER", GUILayout.Height(26))) NetworkManager.Singleton.StartServer();
                GUILayout.EndArea();
            }
            else
            {
                string role = NetworkManager.Singleton.IsHost ? "HOST" : NetworkManager.Singleton.IsServer ? "SERVER" : "CLIENT";
                Rect area = new(Screen.width - 150f, 14f, 136f, 62f);
                GUILayout.BeginArea(area, GUI.skin.box);
                GUILayout.Label($"DEV · {role}", _small);
                if (GUILayout.Button("Shutdown", GUILayout.Height(24))) NetworkManager.Singleton.Shutdown();
                GUILayout.EndArea();
            }
        }
    }
}

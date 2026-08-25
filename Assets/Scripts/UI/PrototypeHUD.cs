using CheatOnYourDayOnes.Interaction;
using CheatOnYourDayOnes.Player;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.UI
{
    public sealed class PrototypeHUD : MonoBehaviour
    {
        private GUIStyle _title;
        private GUIStyle _money;
        private GUIStyle _small;
        private GUIStyle _prompt;
        private PlayerAgent _player;
        private PlayerInteractor _interactor;

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.82f,0.82f,0.82f) } };
            _money = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            _prompt = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private void TryBind()
        {
            if (_player != null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;
            var local = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (local == null) return;
            _player = local.GetComponent<PlayerAgent>();
            _interactor = local.GetComponent<PlayerInteractor>();
        }

        private void OnGUI()
        {
            EnsureStyles();
            TryBind();
            if (_player == null) return;

            DrawPanel(new Rect(24, 24, 260, 132), 0.72f);
            GUI.Label(new Rect(42, 36, 220, 22), "CASH", _title);
            GUI.Label(new Rect(42, 54, 220, 38), $"${_player.Wallet.Cash.Value:N0}", _money);
            GUI.Label(new Rect(42, 96, 220, 22), $"BANK  ${_player.Wallet.Bank.Value:N0}", _small);
            GUI.Label(new Rect(42, 118, 220, 22), $"AURA  {_player.Aura.Aura.Value:+0;-0;0}", _small);

            float y = Screen.height - 142;
            DrawPanel(new Rect(24, y, 300, 112), 0.72f);
            DrawBar(new Rect(42, y + 22, 250, 16), _player.Needs.Health.Value / 100f, "HEALTH");
            DrawBar(new Rect(42, y + 50, 250, 16), _player.Needs.Hunger.Value / 100f, "HUNGER");
            DrawBar(new Rect(42, y + 78, 250, 16), _player.Needs.Energy.Value / 100f, "ENERGY");

            if (_interactor != null && !string.IsNullOrWhiteSpace(_interactor.CurrentPrompt))
            {
                Rect promptRect = new(Screen.width / 2f - 180f, Screen.height - 100f, 360f, 44f);
                DrawPanel(promptRect, 0.8f);
                GUI.Label(promptRect, _interactor.CurrentPrompt, _prompt);
            }
        }

        private static void DrawPanel(Rect rect, float alpha)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.04f, 0.05f, 0.07f, alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void DrawBar(Rect rect, float value, string label)
        {
            value = Mathf.Clamp01(value);
            GUI.Label(new Rect(rect.x, rect.y - 16, rect.width, 16), label, _title);
            Color old = GUI.color;
            GUI.color = new Color(0.16f, 0.17f, 0.20f, 0.95f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.88f, 0.88f, 0.84f, 1f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * value, rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}

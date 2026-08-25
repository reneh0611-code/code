using CheatOnYourDayOnes.Interaction;
using CheatOnYourDayOnes.Player;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.UI
{
    public sealed class PrototypeHUD : MonoBehaviour
    {
        private GUIStyle _eyebrow;
        private GUIStyle _money;
        private GUIStyle _label;
        private GUIStyle _value;
        private GUIStyle _prompt;
        private PlayerAgent _player;
        private PlayerInteractor _interactor;

        private void EnsureStyles(float scale)
        {
            int eyebrow = Mathf.RoundToInt(11 * scale);
            int money = Mathf.RoundToInt(28 * scale);
            int label = Mathf.RoundToInt(11 * scale);
            int value = Mathf.RoundToInt(13 * scale);
            int prompt = Mathf.RoundToInt(14 * scale);

            _eyebrow = new GUIStyle(GUI.skin.label) { fontSize = eyebrow, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.68f, 0.71f, 0.76f) } };
            _money = new GUIStyle(GUI.skin.label) { fontSize = money, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _label = new GUIStyle(GUI.skin.label) { fontSize = label, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.65f, 0.68f, 0.72f) } };
            _value = new GUIStyle(GUI.skin.label) { fontSize = value, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _prompt = new GUIStyle(GUI.skin.label) { fontSize = prompt, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private void TryBind()
        {
            if (_player != null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return;

            var local = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (local == null)
                return;

            _player = local.GetComponent<PlayerAgent>();
            _interactor = local.GetComponent<PlayerInteractor>();
        }

        private void OnGUI()
        {
            TryBind();
            if (_player == null)
                return;

            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.72f, 1.15f);
            EnsureStyles(scale);

            float margin = 22f * scale;
            float cardW = 250f * scale;
            float cardH = 108f * scale;

            Rect moneyCard = new(margin, margin, cardW, cardH);
            DrawPanel(moneyCard, 0.78f);
            GUI.Label(new Rect(moneyCard.x + 16 * scale, moneyCard.y + 10 * scale, cardW, 18 * scale), "WALLET", _eyebrow);
            GUI.Label(new Rect(moneyCard.x + 16 * scale, moneyCard.y + 28 * scale, cardW, 36 * scale), $"${_player.Wallet.Cash.Value:N0}", _money);
            GUI.Label(new Rect(moneyCard.x + 16 * scale, moneyCard.y + 73 * scale, 80 * scale, 18 * scale), "BANK", _label);
            GUI.Label(new Rect(moneyCard.x + 68 * scale, moneyCard.y + 73 * scale, 90 * scale, 18 * scale), $"${_player.Wallet.Bank.Value:N0}", _value);
            GUI.Label(new Rect(moneyCard.x + 154 * scale, moneyCard.y + 73 * scale, 46 * scale, 18 * scale), "AURA", _label);
            GUI.Label(new Rect(moneyCard.x + 198 * scale, moneyCard.y + 73 * scale, 50 * scale, 18 * scale), $"{_player.Aura.Aura.Value:+0;-0;0}", _value);

            float needsW = 260f * scale;
            float needsH = 92f * scale;
            Rect needsCard = new(margin, Screen.height - margin - needsH, needsW, needsH);
            DrawPanel(needsCard, 0.76f);
            DrawBar(new Rect(needsCard.x + 14 * scale, needsCard.y + 18 * scale, needsW - 28 * scale, 8 * scale), _player.Needs.Health.Value / 100f, "HEALTH", scale);
            DrawBar(new Rect(needsCard.x + 14 * scale, needsCard.y + 46 * scale, needsW - 28 * scale, 8 * scale), _player.Needs.Hunger.Value / 100f, "HUNGER", scale);
            DrawBar(new Rect(needsCard.x + 14 * scale, needsCard.y + 74 * scale, needsW - 28 * scale, 8 * scale), _player.Needs.Energy.Value / 100f, "ENERGY", scale);

            if (_interactor != null && !string.IsNullOrWhiteSpace(_interactor.CurrentPrompt))
            {
                Rect promptRect = new(Screen.width / 2f - 150f * scale, Screen.height - 76f * scale, 300f * scale, 38f * scale);
                DrawPanel(promptRect, 0.84f);
                GUI.Label(promptRect, _interactor.CurrentPrompt, _prompt);
            }
        }

        private static void DrawPanel(Rect rect, float alpha)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.025f, 0.03f, 0.04f, alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void DrawBar(Rect rect, float value, string label, float scale)
        {
            value = Mathf.Clamp01(value);
            GUI.Label(new Rect(rect.x, rect.y - 13 * scale, 100 * scale, 14 * scale), label, _label);

            Color old = GUI.color;
            GUI.color = new Color(0.15f, 0.17f, 0.20f, 0.95f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = value < 0.25f ? new Color(0.86f, 0.29f, 0.25f) : new Color(0.84f, 0.86f, 0.82f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * value, rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}

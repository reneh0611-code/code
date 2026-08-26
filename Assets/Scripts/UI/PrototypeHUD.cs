using CheatOnYourDayOnes.Interaction;
using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.Vehicles;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.UI
{
    public sealed class PrototypeHUD : MonoBehaviour
    {
        private static readonly Color Panel = new(0.045f, 0.052f, 0.060f, 0.88f);
        private static readonly Color PanelSoft = new(0.055f, 0.062f, 0.070f, 0.74f);
        private static readonly Color Text = new(0.965f, 0.958f, 0.925f, 1f);
        private static readonly Color Muted = new(0.59f, 0.61f, 0.61f, 1f);
        private static readonly Color Track = new(0.16f, 0.17f, 0.18f, 0.92f);
        private static readonly Color Good = new(0.78f, 0.83f, 0.72f, 1f);
        private static readonly Color Warning = new(0.92f, 0.67f, 0.31f, 1f);
        private static readonly Color Danger = new(0.88f, 0.31f, 0.27f, 1f);
        private static readonly Color Accent = new(0.93f, 0.86f, 0.68f, 1f);

        private PlayerAgent _player;
        private PlayerInteractor _interactor;
        private VehicleInteractor _vehicleInteractor;

        private GUIStyle _micro;
        private GUIStyle _cash;
        private GUIStyle _smallValue;
        private GUIStyle _barLabel;
        private GUIStyle _barValue;
        private GUIStyle _prompt;
        private GUIStyle _key;
        private GUIStyle _speed;
        private GUIStyle _speedUnit;

        private Texture2D _roundedMask;

        private void Awake()
        {
            _roundedMask = CreateRoundedMask(64, 15f);
            _roundedMask.hideFlags = HideFlags.HideAndDontSave;
        }

        private void OnDestroy()
        {
            if (_roundedMask != null)
                Destroy(_roundedMask);
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
            _vehicleInteractor = local.GetComponent<VehicleInteractor>();
        }

        private void BuildStyles(float scale)
        {
            _micro = Label(Mathf.RoundToInt(10f * scale), FontStyle.Bold, Muted, TextAnchor.MiddleLeft);
            _cash = Label(Mathf.RoundToInt(30f * scale), FontStyle.Bold, Text, TextAnchor.MiddleLeft);
            _smallValue = Label(Mathf.RoundToInt(13f * scale), FontStyle.Bold, Text, TextAnchor.MiddleLeft);
            _barLabel = Label(Mathf.RoundToInt(10f * scale), FontStyle.Bold, Muted, TextAnchor.MiddleLeft);
            _barValue = Label(Mathf.RoundToInt(10f * scale), FontStyle.Bold, Text, TextAnchor.MiddleRight);
            _prompt = Label(Mathf.RoundToInt(14f * scale), FontStyle.Bold, Text, TextAnchor.MiddleLeft);
            _key = Label(Mathf.RoundToInt(13f * scale), FontStyle.Bold, new Color(0.10f, 0.10f, 0.095f), TextAnchor.MiddleCenter);
            _speed = Label(Mathf.RoundToInt(38f * scale), FontStyle.Bold, Text, TextAnchor.LowerRight);
            _speedUnit = Label(Mathf.RoundToInt(10f * scale), FontStyle.Bold, Muted, TextAnchor.UpperRight);
        }

        private static GUIStyle Label(int size, FontStyle style, Color color, TextAnchor anchor)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = style,
                alignment = anchor,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                normal = { textColor = color }
            };
        }

        private void OnGUI()
        {
            TryBind();
            if (_player == null)
                return;

            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.72f, 1.18f);
            BuildStyles(scale);

            DrawMoney(scale);
            DrawNeeds(scale);
            DrawInteraction(scale);
            DrawVehicleSpeed(scale);
        }

        private void DrawMoney(float scale)
        {
            float x = 24f * scale;
            float y = 22f * scale;
            float w = 236f * scale;
            float h = 94f * scale;

            Rect panel = new(x, y, w, h);
            DrawCard(panel, Panel);

            GUI.Label(new Rect(x + 16f * scale, y + 11f * scale, 100f * scale, 14f * scale), "CASH", _micro);
            GUI.Label(new Rect(x + 16f * scale, y + 24f * scale, w - 32f * scale, 37f * scale), $"${_player.Wallet.Cash.Value:N0}", _cash);

            DrawHairline(new Rect(x + 16f * scale, y + 65f * scale, w - 32f * scale, 1f * scale), new Color(1f, 1f, 1f, 0.07f));

            GUI.Label(new Rect(x + 16f * scale, y + 70f * scale, 42f * scale, 15f * scale), "BANK", _micro);
            GUI.Label(new Rect(x + 59f * scale, y + 69f * scale, 76f * scale, 17f * scale), $"${_player.Wallet.Bank.Value:N0}", _smallValue);

            GUI.Label(new Rect(x + 149f * scale, y + 70f * scale, 40f * scale, 15f * scale), "AURA", _micro);
            Color old = _smallValue.normal.textColor;
            _smallValue.normal.textColor = _player.Aura.Aura.Value >= 0 ? Good : Danger;
            GUI.Label(new Rect(x + 191f * scale, y + 69f * scale, 34f * scale, 17f * scale), $"{_player.Aura.Aura.Value:+0;-0;0}", _smallValue);
            _smallValue.normal.textColor = old;
        }

        private void DrawNeeds(float scale)
        {
            float x = 24f * scale;
            float h = 96f * scale;
            float y = Screen.height - 24f * scale - h;
            float w = 250f * scale;

            DrawCard(new Rect(x, y, w, h), PanelSoft);

            DrawNeedRow(x + 15f * scale, y + 15f * scale, w - 30f * scale, "HEALTH", _player.Needs.Health.Value, scale);
            DrawNeedRow(x + 15f * scale, y + 42f * scale, w - 30f * scale, "HUNGER", _player.Needs.Hunger.Value, scale);
            DrawNeedRow(x + 15f * scale, y + 69f * scale, w - 30f * scale, "ENERGY", _player.Needs.Energy.Value, scale);
        }

        private void DrawNeedRow(float x, float y, float width, string label, float rawValue, float scale)
        {
            float value = Mathf.Clamp(rawValue, 0f, 100f);
            GUI.Label(new Rect(x, y, 90f * scale, 12f * scale), label, _barLabel);
            GUI.Label(new Rect(x + width - 40f * scale, y, 40f * scale, 12f * scale), Mathf.RoundToInt(value).ToString(), _barValue);

            Rect track = new(x, y + 14f * scale, width, 4f * scale);
            DrawRounded(track, Track);

            Color fill = value <= 25f ? Danger : value <= 50f ? Warning : Good;
            Rect fillRect = new(track.x, track.y, track.width * (value / 100f), track.height);
            if (fillRect.width > 1f)
                DrawRounded(fillRect, fill);
        }

        private void DrawInteraction(float scale)
        {
            string prompt = null;
            string key = "E";

            if (_vehicleInteractor != null && _vehicleInteractor.CanEnterVehicle)
            {
                prompt = "Fahren";
            }
            else if (_interactor != null && !string.IsNullOrWhiteSpace(_interactor.CurrentPrompt))
            {
                prompt = CleanPrompt(_interactor.CurrentPrompt);
            }

            if (string.IsNullOrWhiteSpace(prompt))
                return;

            float w = Mathf.Clamp(150f + prompt.Length * 7f, 190f, 330f) * scale;
            float h = 46f * scale;
            float x = Screen.width * 0.5f - w * 0.5f;
            float y = Screen.height - 96f * scale;

            DrawCard(new Rect(x, y, w, h), new Color(0.035f, 0.040f, 0.044f, 0.91f));

            Rect keyRect = new(x + 7f * scale, y + 7f * scale, 32f * scale, 32f * scale);
            DrawRounded(keyRect, Accent);
            GUI.Label(keyRect, key, _key);

            GUI.Label(new Rect(x + 51f * scale, y, w - 61f * scale, h), prompt, _prompt);
        }

        private void DrawVehicleSpeed(float scale)
        {
            DriveableCar occupied = null;
            foreach (DriveableCar car in Object.FindObjectsByType<DriveableCar>(FindObjectsSortMode.None))
            {
                if (car != null && car.IsOccupied)
                {
                    occupied = car;
                    break;
                }
            }

            if (occupied == null)
                return;

            float w = 142f * scale;
            float h = 82f * scale;
            float x = Screen.width - 24f * scale - w;
            float y = Screen.height - 24f * scale - h;

            DrawCard(new Rect(x, y, w, h), PanelSoft);

            string speed = Mathf.RoundToInt(occupied.SpeedKmh).ToString();
            GUI.Label(new Rect(x + 12f * scale, y + 8f * scale, w - 24f * scale, 48f * scale), speed, _speed);
            GUI.Label(new Rect(x + 12f * scale, y + 55f * scale, w - 24f * scale, 15f * scale), "KM/H", _speedUnit);
        }

        private static string CleanPrompt(string prompt)
        {
            string p = prompt.Trim();
            p = p.Replace("[ E ]", "").Replace("[E]", "").Replace("(E)", "").Replace("E -", "").Trim();
            return string.IsNullOrWhiteSpace(p) ? "Interagieren" : p;
        }

        private void DrawCard(Rect rect, Color color)
        {
            DrawRounded(new Rect(rect.x + 2f, rect.y + 3f, rect.width, rect.height), new Color(0f, 0f, 0f, 0.16f));
            DrawRounded(rect, color);
        }

        private void DrawRounded(Rect rect, Color color)
        {
            if (_roundedMask == null)
            {
                Color old = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = old;
                return;
            }

            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _roundedMask, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private static void DrawHairline(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static Texture2D CreateRoundedMask(int size, float radius)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[size * size];
            float r = radius;
            float left = r;
            float right = size - 1 - r;
            float bottom = r;
            float top = size - 1 - r;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x, left, right);
                    float cy = Mathf.Clamp(y, bottom, top);
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    float alpha = 1f - Mathf.Clamp01(distance - r + 1f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}

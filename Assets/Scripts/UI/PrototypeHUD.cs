using CheatOnYourDayOnes.Interaction;
using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.Vehicles;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.UI
{
    public sealed class PrototypeHUD : MonoBehaviour
    {
        // HUD surfaces are deliberately very transparent; text + bars stay fully legible.
        private static readonly Color Ink = new(0.025f, 0.027f, 0.03f, 0.30f);
        private static readonly Color InkSoft = new(0.035f, 0.038f, 0.043f, 0.22f);
        private static readonly Color Text = new(0.97f, 0.965f, 0.94f, 1f);
        private static readonly Color Muted = new(0.66f, 0.67f, 0.65f, 1f);
        private static readonly Color Track = new(0.13f, 0.14f, 0.15f, 0.95f);
        private static readonly Color Health = new(0.72f, 0.86f, 0.68f, 1f);
        private static readonly Color Hunger = new(0.93f, 0.73f, 0.38f, 1f);
        private static readonly Color Energy = new(0.73f, 0.80f, 0.95f, 1f);
        private static readonly Color Stamina = new(0.94f, 0.94f, 0.91f, 1f);
        private static readonly Color Danger = new(0.91f, 0.31f, 0.28f, 1f);
        private static readonly Color Gold = new(0.93f, 0.82f, 0.55f, 1f);

        private PlayerAgent _player;
        private PlayerInteractor _interactor;
        private VehicleInteractor _vehicleInteractor;
        private NetworkPlayerController _movement;
        private Texture2D _roundedMask;
        private float _staminaVisibleUntil;

        private GUIStyle _tiny;
        private GUIStyle _cash;
        private GUIStyle _secondary;
        private GUIStyle _needLabel;
        private GUIStyle _needValue;
        private GUIStyle _prompt;
        private GUIStyle _key;
        private GUIStyle _speedNumber;
        private GUIStyle _speedUnit;
        private GUIStyle _dialNumber;

        private void Awake()
        {
            _roundedMask = CreateRoundedMask(64, 14f);
            _roundedMask.hideFlags = HideFlags.HideAndDontSave;
        }

        private void OnDestroy()
        {
            if (_roundedMask != null) Destroy(_roundedMask);
        }

        private void TryBind()
        {
            if (_player != null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;
            var local = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (local == null) return;

            _player = local.GetComponent<PlayerAgent>();
            _interactor = local.GetComponent<PlayerInteractor>();
            _vehicleInteractor = local.GetComponent<VehicleInteractor>();
            _movement = local.GetComponent<NetworkPlayerController>();
        }

        private void BuildStyles(float scale)
        {
            _tiny = MakeStyle(10f, scale, FontStyle.Bold, Muted, TextAnchor.MiddleLeft);
            _cash = MakeStyle(35f, scale, FontStyle.Bold, Text, TextAnchor.MiddleLeft);
            _secondary = MakeStyle(13f, scale, FontStyle.Bold, Text, TextAnchor.MiddleLeft);
            _needLabel = MakeStyle(12f, scale, FontStyle.Bold, Text, TextAnchor.MiddleLeft);
            _needValue = MakeStyle(11f, scale, FontStyle.Bold, Muted, TextAnchor.MiddleRight);
            _prompt = MakeStyle(15f, scale, FontStyle.Bold, Text, TextAnchor.MiddleLeft);
            _key = MakeStyle(14f, scale, FontStyle.Bold, Ink, TextAnchor.MiddleCenter);
            _speedNumber = MakeStyle(30f, scale, FontStyle.Bold, Text, TextAnchor.MiddleCenter);
            _speedUnit = MakeStyle(9f, scale, FontStyle.Bold, Muted, TextAnchor.MiddleCenter);
            _dialNumber = MakeStyle(9f, scale, FontStyle.Bold, Muted, TextAnchor.MiddleCenter);
        }

        private static GUIStyle MakeStyle(float baseSize, float scale, FontStyle fontStyle, Color color, TextAnchor anchor)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(baseSize * scale),
                fontStyle = fontStyle,
                alignment = anchor,
                padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = color }
            };
        }

        private void OnGUI()
        {
            TryBind();
            if (_player == null) return;

            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.78f, 1.22f);
            BuildStyles(scale);

            DriveableCar occupiedCar = FindOccupiedCar();

            DrawCash(scale);
            DrawNeeds(scale);
            DrawStamina(scale, occupiedCar == null);
            DrawInteraction(scale, occupiedCar == null);

            if (occupiedCar != null)
                DrawAnalogSpeedometer(scale, occupiedCar);
        }

        private void DrawCash(float scale)
        {
            float x = 26f * scale;
            float y = 24f * scale;
            float w = 282f * scale;
            float h = 106f * scale;

            DrawCard(new Rect(x, y, w, h), Ink);

            Rect coin = new(x + 15f * scale, y + 16f * scale, 44f * scale, 44f * scale);
            DrawRounded(coin, new Color(Gold.r, Gold.g, Gold.b, 0.58f));
            GUIStyle dollar = MakeStyle(22f, scale, FontStyle.Bold, new Color(0.15f, 0.13f, 0.08f), TextAnchor.MiddleCenter);
            GUI.Label(coin, "$", dollar);

            GUI.Label(new Rect(x + 72f * scale, y + 12f * scale, 100f * scale, 15f * scale), "CASH", _tiny);
            GUI.Label(new Rect(x + 72f * scale, y + 25f * scale, w - 88f * scale, 42f * scale), $"{_player.Wallet.Cash.Value:N0}", _cash);

            DrawLine(new Rect(x + 16f * scale, y + 70f * scale, w - 32f * scale, 1f), new Color(1f, 1f, 1f, 0.12f));

            GUI.Label(new Rect(x + 16f * scale, y + 80f * scale, 48f * scale, 15f * scale), "BANK", _tiny);
            GUI.Label(new Rect(x + 62f * scale, y + 78f * scale, 90f * scale, 18f * scale), $"{_player.Wallet.Bank.Value:N0}", _secondary);

            GUI.Label(new Rect(x + 177f * scale, y + 80f * scale, 44f * scale, 15f * scale), "AURA", _tiny);
            Color old = _secondary.normal.textColor;
            _secondary.normal.textColor = _player.Aura.Aura.Value >= 0 ? Health : Danger;
            GUI.Label(new Rect(x + 221f * scale, y + 78f * scale, 48f * scale, 18f * scale), $"{_player.Aura.Aura.Value:+0;-0;0}", _secondary);
            _secondary.normal.textColor = old;
        }

        private void DrawNeeds(float scale)
        {
            float x = 26f * scale;
            float w = 330f * scale;
            float h = 160f * scale;
            float y = Screen.height - 26f * scale - h;

            DrawCard(new Rect(x, y, w, h), InkSoft);
            DrawNeed(x + 18f * scale, y + 19f * scale, w - 36f * scale, "HEALTH", _player.Needs.Health.Value, Health, scale);
            DrawNeed(x + 18f * scale, y + 67f * scale, w - 36f * scale, "HUNGER", _player.Needs.Hunger.Value, Hunger, scale);
            DrawNeed(x + 18f * scale, y + 115f * scale, w - 36f * scale, "ENERGY", _player.Needs.Energy.Value, Energy, scale);
        }

        private void DrawNeed(float x, float y, float width, string name, float raw, Color color, float scale)
        {
            float value = Mathf.Clamp(raw, 0f, 100f);
            GUI.Label(new Rect(x, y, 130f * scale, 18f * scale), name, _needLabel);
            GUI.Label(new Rect(x + width - 50f * scale, y, 50f * scale, 18f * scale), $"{Mathf.RoundToInt(value)}", _needValue);

            Rect track = new(x, y + 23f * scale, width, 10f * scale);
            DrawRounded(track, Track);
            Rect fill = new(track.x, track.y, track.width * value / 100f, track.height);
            if (fill.width > 2f)
                DrawRounded(fill, value <= 20f ? Danger : color);
        }

        private void DrawStamina(float scale, bool onFoot)
        {
            if (!onFoot || _movement == null) return;

            if (_movement.IsSprinting || _movement.Stamina01 < 0.995f)
                _staminaVisibleUntil = Time.unscaledTime + 1.1f;

            if (Time.unscaledTime > _staminaVisibleUntil) return;

            float w = 280f * scale;
            float x = Screen.width * 0.5f - w * 0.5f;
            float y = Screen.height - 145f * scale;

            GUI.Label(new Rect(x, y, 90f * scale, 14f * scale), "SPRINT", _tiny);
            GUI.Label(new Rect(x + w - 45f * scale, y, 45f * scale, 14f * scale), $"{Mathf.RoundToInt(_movement.Stamina)}", _needValue);

            Rect track = new(x, y + 17f * scale, w, 8f * scale);
            DrawRounded(track, new Color(0f, 0f, 0f, 0.55f));
            Rect fill = new(track.x, track.y, track.width * _movement.Stamina01, track.height);
            if (fill.width > 1f) DrawRounded(fill, _movement.Stamina01 <= .15f ? Danger : Stamina);
        }

        private void DrawInteraction(float scale, bool onFoot)
        {
            if (!onFoot) return;

            string prompt = null;
            if (_vehicleInteractor != null && _vehicleInteractor.enabled && _vehicleInteractor.CanEnterVehicle)
                prompt = "Fahren";
            else if (_interactor != null && _interactor.enabled && !string.IsNullOrWhiteSpace(_interactor.CurrentPrompt))
                prompt = CleanPrompt(_interactor.CurrentPrompt);

            if (string.IsNullOrWhiteSpace(prompt)) return;

            float w = Mathf.Clamp(155f + prompt.Length * 7f, 205f, 360f) * scale;
            float h = 50f * scale;
            float x = Screen.width * 0.5f - w * 0.5f;
            float y = Screen.height - 88f * scale;

            DrawCard(new Rect(x, y, w, h), new Color(0.02f, 0.022f, 0.025f, 0.28f));
            Rect key = new(x + 8f * scale, y + 8f * scale, 34f * scale, 34f * scale);
            DrawRounded(key, new Color(Text.r, Text.g, Text.b, 0.62f));
            GUI.Label(key, "E", _key);
            GUI.Label(new Rect(x + 55f * scale, y, w - 66f * scale, h), prompt, _prompt);
        }

        // Speedometer intentionally unchanged.
        private void DrawAnalogSpeedometer(float scale, DriveableCar car)
        {
            float size = 245f * scale;
            float x = Screen.width - 24f * scale - size;
            float y = Screen.height - 20f * scale - size;
            Rect dialRect = new(x, y, size, size);

            DrawRounded(dialRect, new Color(0.015f, 0.017f, 0.02f, 0.82f));

            Vector2 center = new(dialRect.center.x, dialRect.center.y + 16f * scale);
            float radius = 91f * scale;
            const float minAngle = -130f;
            const float maxAngle = 130f;
            const float maxKmh = 50f;

            for (int i = 0; i <= 25; i++)
            {
                float t = i / 25f;
                float angle = Mathf.Lerp(minAngle, maxAngle, t);
                bool major = i % 5 == 0;
                float tickLength = (major ? 15f : 8f) * scale;
                float tickWidth = (major ? 2.2f : 1.3f) * scale;
                DrawRadialTick(center, radius, angle, tickLength, tickWidth, major ? Text : new Color(1f, 1f, 1f, .34f));

                if (major)
                {
                    int label = Mathf.RoundToInt(maxKmh * t);
                    Vector2 lp = PointOnCircle(center, radius - 31f * scale, angle);
                    GUI.Label(new Rect(lp.x - 18f * scale, lp.y - 8f * scale, 36f * scale, 16f * scale), label.ToString(), _dialNumber);
                }
            }

            float speed = Mathf.Clamp(car.SpeedKmh, 0f, maxKmh);
            float needleAngle = Mathf.Lerp(minAngle, maxAngle, speed / maxKmh);
            DrawNeedle(center, needleAngle, 70f * scale, 3f * scale, Gold);
            DrawRounded(new Rect(center.x - 7f * scale, center.y - 7f * scale, 14f * scale, 14f * scale), Gold);

            GUI.Label(new Rect(center.x - 58f * scale, center.y + 31f * scale, 116f * scale, 36f * scale), Mathf.RoundToInt(car.SpeedKmh).ToString(), _speedNumber);
            GUI.Label(new Rect(center.x - 45f * scale, center.y + 64f * scale, 90f * scale, 15f * scale), "KM/H", _speedUnit);
        }

        private static Vector2 PointOnCircle(Vector2 center, float radius, float degrees)
        {
            float radians = (degrees - 90f) * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
        }

        private static void DrawRadialTick(Vector2 center, float radius, float angle, float length, float width, Color color)
        {
            Vector2 p = PointOnCircle(center, radius - length * .5f, angle);
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, p);
            GUI.DrawTexture(new Rect(p.x - width * .5f, p.y - length * .5f, width, length), Texture2D.whiteTexture);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private static void DrawNeedle(Vector2 center, float angle, float length, float width, Color color)
        {
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, center);
            GUI.DrawTexture(new Rect(center.x - width * .5f, center.y - length + 7f, width, length), Texture2D.whiteTexture);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private static DriveableCar FindOccupiedCar()
        {
            foreach (DriveableCar car in Object.FindObjectsByType<DriveableCar>(FindObjectsSortMode.None))
                if (car != null && car.IsOccupied) return car;
            return null;
        }

        private static string CleanPrompt(string prompt)
        {
            string p = prompt.Trim();
            p = p.Replace("[ E ]", "").Replace("[E]", "").Replace("(E)", "").Replace("E -", "").Trim();
            return string.IsNullOrWhiteSpace(p) ? "Interagieren" : p;
        }

        private void DrawCard(Rect rect, Color color)
        {
            // Very light shadow only; no opaque slabs behind the HUD.
            DrawRounded(new Rect(rect.x + 2f, rect.y + 3f, rect.width, rect.height), new Color(0f, 0f, 0f, .07f));
            DrawRounded(rect, color);
        }

        private void DrawRounded(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _roundedMask != null ? _roundedMask : Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        private static void DrawLine(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static Texture2D CreateRoundedMask(int size, float radius)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[size * size];
            float left = radius, right = size - 1 - radius, bottom = radius, top = size - 1 - radius;
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float cx = Mathf.Clamp(px, left, right);
                    float cy = Mathf.Clamp(py, bottom, top);
                    float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                    float a = 1f - Mathf.Clamp01(d - radius + 1f);
                    pixels[py * size + px] = new Color(1f, 1f, 1f, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}

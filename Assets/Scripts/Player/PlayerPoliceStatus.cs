using System.Collections.Generic;
using CheatOnYourDayOnes.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CheatOnYourDayOnes.Player
{
    public sealed class PlayerPoliceStatus : MonoBehaviour
    {
        private readonly Dictionary<int, float> _lastAssaultByVictim = new();
        private readonly HashSet<int> _homicideVictims = new();
        private int _assaultIncidentCount;
        private NetworkPlayerController _movement;
        private PlayerAgent _player;
        private global::MeleeAnimationBridge _melee;
        private PoliceOfficerAI _controlOfficer;
        private PoliceOfficerAI _taserOfficer;
        private PoliceOfficerAI _nearbyOfficer;
        private float _nextNearbyScan;
        private float _inputAllowedAfter;
        private float _taserImmunityUntil;
        private float _taserWarningStarted;
        private float _taserWarningEnds;
        private string _message = string.Empty;
        private bool _inPoliceControl;
        private Texture2D _darkTexture;
        private Texture2D _panelTexture;
        private Texture2D _blueTexture;
        private Texture2D _blueSoftTexture;
        private Texture2D _redTexture;
        private Texture2D _trackTexture;
        private Texture2D _buttonTexture;
        private Texture2D _buttonHoverTexture;

        public int WantedStars { get; private set; }
        public bool IsInPoliceControl => _inPoliceControl;
        public bool IsControlOfficer(PoliceOfficerAI officer) => _inPoliceControl && _controlOfficer == officer;
        public bool CanBeTasered => !_inPoliceControl && Time.time >= _taserImmunityUntil;
        public long CurrentFine => WantedStars switch
        {
            1 => 500,
            2 => 1500,
            3 => 3500,
            4 => 6500,
            5 => 10000,
            _ => 0
        };

        private void Awake()
        {
            _movement = GetComponent<NetworkPlayerController>();
            _player = GetComponent<PlayerAgent>();
            _melee = GetComponent<global::MeleeAnimationBridge>();
        }

        private void Update()
        {
            if (_taserOfficer != null && Time.time > _taserWarningEnds + .5f) CancelTaserWarning(_taserOfficer);
            if (Time.time >= _nextNearbyScan)
            {
                _nextNearbyScan = Time.time + .18f;
                _nearbyOfficer = FindNearestOfficer(2.6f);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || Time.time < _inputAllowedAfter) return;

            if (!_inPoliceControl)
            {
                if (_nearbyOfficer != null && keyboard.eKey.wasPressedThisFrame)
                    BeginPoliceControl(_nearbyOfficer, false);
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                PayFine();
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                SubmitToSearch();
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                AcceptPrison();
        }

        public static void RecordAssault(Transform attacker, Object victim)
        {
            PlayerPoliceStatus status = Find(attacker);
            if (status == null || victim == null) return;
            int id = victim.GetHashCode();
            if (!status._lastAssaultByVictim.TryGetValue(id, out float previous) || Time.time - previous >= 10f)
                status._assaultIncidentCount++;
            status._lastAssaultByVictim[id] = Time.time;
            status.RecalculateStars();
        }

        public static void RecordHomicide(Transform attacker, Object victim)
        {
            PlayerPoliceStatus status = Find(attacker);
            if (status == null || victim == null) return;
            int id = victim.GetHashCode();
            if (!status._lastAssaultByVictim.ContainsKey(id)) status._assaultIncidentCount++;
            status._lastAssaultByVictim[id] = Time.time;
            status._homicideVictims.Add(id);
            status.RecalculateStars();
        }

        public void ApplyTaser(PoliceOfficerAI officer)
        {
            if (!CanBeTasered) return;
            CancelTaserWarning(officer);
            _taserImmunityUntil = Time.time + 4f;
            BeginPoliceControl(officer, true);
        }

        public void BeginTaserWarning(PoliceOfficerAI officer, float duration)
        {
            if (_inPoliceControl || officer == null) return;
            _taserOfficer = officer;
            _taserWarningStarted = Time.time;
            _taserWarningEnds = Time.time + Mathf.Max(.25f, duration);
        }

        public void CancelTaserWarning(PoliceOfficerAI officer)
        {
            if (_taserOfficer != officer) return;
            _taserOfficer = null;
            _taserWarningStarted = 0f;
            _taserWarningEnds = 0f;
        }

        public void BeginPoliceControl(PoliceOfficerAI officer, bool tasered)
        {
            if (_inPoliceControl || officer == null) return;
            if (_movement == null) _movement = GetComponent<NetworkPlayerController>();
            if (_melee == null) _melee = GetComponent<global::MeleeAnimationBridge>();
            _controlOfficer = officer;
            _inPoliceControl = true;
            _inputAllowedAfter = Time.time + .35f;
            _message = tasered
                ? "Du wurdest getasert. Entscheide, wie du mit der Kontrolle umgehst."
                : WantedStars > 0
                    ? "Polizeikontrolle wegen laufender Fahndung."
                    : "Allgemeine Polizeikontrolle.";
            if (_movement != null) _movement.SetCombatMovementLocked(true);
            if (_melee != null) _melee.enabled = false;
            officer.HoldPoliceControl(transform);
        }

        private void PayFine()
        {
            long fine = CurrentFine;
            if (fine <= 0)
            {
                _message = "Es liegt keine offene Geldstrafe vor.";
                ReleaseFromControl(true);
                return;
            }
            if (_player == null || _player.Wallet == null || !_player.Wallet.CanAffordCash(fine))
            {
                _message = $"Nicht genug Bargeld. Benötigt: ${fine:N0}.";
                return;
            }

            if (_player.Wallet.IsServer) _player.Wallet.TrySpendCashServer(fine, "Police fine");
            else _player.Wallet.RequestPoliceFineRpc(fine);
            ClearWanted();
            ReleaseFromControl(true);
        }

        private void SubmitToSearch()
        {
            bool contraband = _player != null && _player.Inventory != null && _player.Inventory.ContainsPoliceContraband();
            if (!contraband)
            {
                ClearWanted();
                _message = "Nichts Illegales gefunden. Du darfst weitergehen.";
                ReleaseFromControl(true);
                return;
            }

            if (_player.Inventory.IsServer) _player.Inventory.ConfiscatePoliceContrabandServer();
            else _player.Inventory.RequestPoliceConfiscationRpc();
            WantedStars = Mathf.Max(2, WantedStars);
            _message = "Waffe oder Konterband gefunden und beschlagnahmt. Bezahle oder akzeptiere Gefängnis.";
            _inputAllowedAfter = Time.time + .35f;
        }

        private void AcceptPrison()
        {
            ClearWanted();
            _message = "Festnahme akzeptiert. Gefängnisübergabe ist vorbereitet.";
            GameObject prisonSpawn = GameObject.Find("PrisonSpawn") ?? GameObject.Find("JailSpawn");
            if (prisonSpawn != null && _movement != null && _movement.IsServer)
                _movement.TeleportServerAuthoritative(prisonSpawn.transform.position);
            ReleaseFromControl(true);
        }

        private void ReleaseFromControl(bool giveImmunity)
        {
            _inPoliceControl = false;
            _controlOfficer = null;
            if (giveImmunity) _taserImmunityUntil = Time.time + 5f;
            if (_movement != null) _movement.SetCombatMovementLocked(false);
            if (_melee != null) _melee.enabled = true;
        }

        private void ClearWanted()
        {
            _lastAssaultByVictim.Clear();
            _homicideVictims.Clear();
            _assaultIncidentCount = 0;
            WantedStars = 0;
        }

        private void RecalculateStars()
        {
            int assaultsBeyondHomicides = Mathf.Max(0, _assaultIncidentCount - _homicideVictims.Count);
            if (_homicideVictims.Count >= 2) WantedStars = 5;
            else if (_homicideVictims.Count == 1) WantedStars = Mathf.Clamp(3 + assaultsBeyondHomicides, 3, 5);
            else WantedStars = Mathf.Clamp(_assaultIncidentCount, 0, 2);
        }

        private PoliceOfficerAI FindNearestOfficer(float range)
        {
            float bestSqr = range * range;
            PoliceOfficerAI best = null;
            foreach (PoliceOfficerAI officer in PoliceOfficerAI.ActiveOfficers)
            {
                if (officer == null) continue;
                float sqr = (officer.transform.position - transform.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = officer;
            }
            return best;
        }

        private static PlayerPoliceStatus Find(Transform transform)
        {
            if (transform == null) return null;
            return transform.GetComponent<PlayerPoliceStatus>() ?? transform.GetComponentInParent<PlayerPoliceStatus>();
        }

        private void OnGUI()
        {
            EnsureGuiResources();
            if (_taserOfficer != null && !_inPoliceControl) DrawTaserWarning();
            if (WantedStars > 0)
            {
                GUIStyle wanted = new(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperCenter,
                    fontSize = 25,
                    fontStyle = FontStyle.Bold
                };
                wanted.normal.textColor = new Color(1f, .78f, .10f);
                GUI.Label(new Rect(Screen.width * .5f - 180f, 22f, 360f, 42f),
                    new string('★', WantedStars) + new string('☆', 5 - WantedStars), wanted);
            }

            if (!_inPoliceControl)
            {
                if (_nearbyOfficer == null) return;
                GUIStyle prompt = new(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
                GUI.Box(new Rect(Screen.width * .5f - 155f, Screen.height - 118f, 310f, 42f),
                    WantedStars > 0 ? "[E] Der Polizei stellen" : "[E] Polizeikontrolle", prompt);
                return;
            }

            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _darkTexture);
            float width = Mathf.Min(660f, Screen.width - 40f);
            float height = 342f;
            Rect panel = new(Screen.width * .5f - width * .5f, Screen.height * .5f - height * .5f, width, height);
            GUI.DrawTexture(panel, _blueTexture);
            Rect inner = new(panel.x + 3f, panel.y + 3f, panel.width - 6f, panel.height - 6f);
            GUI.DrawTexture(inner, _panelTexture);
            GUI.DrawTexture(new Rect(inner.x, inner.y, inner.width, 7f), _blueTexture);

            GUIStyle title = new(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 25, fontStyle = FontStyle.Bold };
            title.normal.textColor = Color.white;
            GUIStyle badge = new(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold };
            badge.normal.textColor = new Color(.72f, .91f, 1f);
            GUIStyle body = new(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 16, wordWrap = true };
            body.normal.textColor = new Color(.88f, .92f, .96f);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 22f, width - 210f, 36f), "POLIZEI  //  KONTROLLE", title);
            GUI.DrawTexture(new Rect(panel.x + width - 170f, panel.y + 23f, 140f, 32f), _blueSoftTexture);
            GUI.Label(new Rect(panel.x + width - 170f, panel.y + 23f, 140f, 32f),
                WantedStars > 0 ? $"FAHNDUNG  {WantedStars}/5" : "ROUTINE", badge);
            GUI.DrawTexture(new Rect(panel.x + 27f, panel.y + 73f, width - 54f, 1f), _blueSoftTexture);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 88f, width - 60f, 54f), _message, body);

            string firstOption = WantedStars > 0
                ? $"[1] Geldstrafe bezahlen  –  ${CurrentFine:N0}"
                : "[1] Kooperieren und Personalien zeigen";
            GUIStyle option = new(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(20, 14, 0, 0)
            };
            option.normal.background = _buttonTexture;
            option.hover.background = _buttonHoverTexture;
            option.active.background = _blueSoftTexture;
            option.normal.textColor = new Color(.92f, .96f, 1f);
            option.hover.textColor = Color.white;
            option.active.textColor = Color.white;

            Rect first = new(panel.x + 28f, panel.y + 154f, width - 56f, 48f);
            Rect second = new(panel.x + 28f, panel.y + 211f, width - 56f, 48f);
            Rect third = new(panel.x + 28f, panel.y + 268f, width - 56f, 48f);
            if (GUI.Button(first, firstOption, option)) PayFine();
            if (GUI.Button(second, "[2]  Durchsuchen lassen", option)) SubmitToSearch();
            if (GUI.Button(third, "[3]  Gefängnis akzeptieren", option)) AcceptPrison();
        }

        private void DrawTaserWarning()
        {
            float blue = 5f;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, blue), _blueTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - blue, Screen.width, blue), _blueTexture);
            GUI.DrawTexture(new Rect(0f, 0f, blue, Screen.height), _blueTexture);
            GUI.DrawTexture(new Rect(Screen.width - blue, 0f, blue, Screen.height), _blueTexture);

            float width = Mathf.Min(520f, Screen.width - 40f);
            Rect warning = new(Screen.width * .5f - width * .5f, 78f, width, 92f);
            GUI.DrawTexture(warning, _panelTexture);
            GUI.DrawTexture(new Rect(warning.x, warning.y, 6f, warning.height), _redTexture);
            GUIStyle warningTitle = new(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 19, fontStyle = FontStyle.Bold };
            warningTitle.normal.textColor = Color.white;
            GUIStyle hint = new(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold };
            hint.normal.textColor = new Color(.72f, .87f, 1f);
            GUI.Label(new Rect(warning.x + 15f, warning.y + 7f, width - 30f, 28f), "TASER ERFASST DICH", warningTitle);
            float duration = Mathf.Max(.01f, _taserWarningEnds - _taserWarningStarted);
            float progress = Mathf.Clamp01((Time.time - _taserWarningStarted) / duration);
            Rect track = new(warning.x + 28f, warning.y + 40f, width - 56f, 14f);
            GUI.DrawTexture(track, _trackTexture);
            GUI.DrawTexture(new Rect(track.x + 2f, track.y + 2f, (track.width - 4f) * progress, track.height - 4f), _redTexture);
            GUI.Label(new Rect(warning.x + 15f, warning.y + 59f, width - 30f, 24f),
                "RAUS AUS 10 METER ODER AUSWEICHROLLE", hint);
        }

        private void EnsureGuiResources()
        {
            if (_panelTexture != null) return;
            _darkTexture = MakeTexture(new Color(.015f, .025f, .045f, .72f));
            _panelTexture = MakeTexture(new Color(.025f, .055f, .09f, .98f));
            _blueTexture = MakeTexture(new Color(.05f, .55f, 1f, 1f));
            _blueSoftTexture = MakeTexture(new Color(.06f, .32f, .56f, .72f));
            _redTexture = MakeTexture(new Color(1f, .12f, .14f, 1f));
            _trackTexture = MakeTexture(new Color(.12f, .14f, .18f, 1f));
            _buttonTexture = MakeTexture(new Color(.045f, .105f, .16f, 1f));
            _buttonHoverTexture = MakeTexture(new Color(.055f, .23f, .36f, 1f));
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            Destroy(_darkTexture);
            Destroy(_panelTexture);
            Destroy(_blueTexture);
            Destroy(_blueSoftTexture);
            Destroy(_redTexture);
            Destroy(_trackTexture);
            Destroy(_buttonTexture);
            Destroy(_buttonHoverTexture);
        }
    }
}

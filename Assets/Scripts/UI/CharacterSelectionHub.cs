using System.Collections;
using CheatOnYourDayOnes.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CheatOnYourDayOnes.UI
{
    public sealed class CharacterSelectionHub : MonoBehaviour
    {
        public static CharacterSelectionHub Instance { get; private set; }
        public static bool SelectionFinished { get; private set; }
        public static int SelectedCharacterIndex { get; private set; } = -1;

        private const string Character01Resource = "PlayableCharacters/Character01";
        private const string Character02Resource = "PlayableCharacters/Character02";
        private const string Character01Controller = "PlayableCharacters/Character01";
        private const string Character02Controller = "PlayableCharacters/Character02";
        private const string FallbackController = "Tripo_Locomotion_ExactGeneric";

        private Canvas canvas;
        private GameObject landingPanel;
        private GameObject selectionPanel;
        private RectTransform card1;
        private RectTransform card2;
        private RawImage preview1;
        private RawImage preview2;
        private Button startSelectionButton;
        private GameObject previewWorld;
        private Camera cam1;
        private Camera cam2;
        private RenderTexture rt1;
        private RenderTexture rt2;
        private GameObject previewModel1;
        private GameObject previewModel2;
        private Coroutine cardTween;
        private GameObject localPlayer;
        private Behaviour movement;
        private MeleeAnimationBridge melee;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateAutomatically()
        {
            if (Instance != null || FindFirstObjectByType<CharacterSelectionHub>() != null) return;
            GameObject go = new("CYDOY_CharacterSelectionHub");
            go.AddComponent<CharacterSelectionHub>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
            EnsureEventSystem();
            BuildUI();
            BuildPreviewWorld();
            yield return WaitForLocalPlayerAndLock();
        }

        private IEnumerator WaitForLocalPlayerAndLock()
        {
            while (localPlayer == null)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkObject no = NetworkManager.Singleton.LocalClient?.PlayerObject;
                    if (no != null) localPlayer = no.gameObject;
                }
                yield return null;
            }
            LockGameplay(true);
        }

        private void LockGameplay(bool value)
        {
            if (localPlayer == null) return;
            NetworkPlayerController controller = localPlayer.GetComponent<NetworkPlayerController>();
            if (controller != null)
            {
                movement = controller;
                controller.enabled = !value;
            }
            melee = localPlayer.GetComponent<MeleeAnimationBridge>();
            if (melee != null) melee.enabled = !value;
            Cursor.visible = value;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
        }

        private void BuildUI()
        {
            GameObject canvasGo = new("CharacterHubCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            Image dim = MakeImage(canvasGo.transform, "Backdrop", new Color(0.025f, 0.028f, 0.035f, .98f));
            Stretch(dim.rectTransform);

            landingPanel = new GameObject("LandingPanel", typeof(RectTransform));
            landingPanel.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)landingPanel.transform);

            Text title = MakeText(landingPanel.transform, "CHEAT ON YOUR DAY ONES", 48, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(.5f, .64f), new Vector2(860, 90));
            title.color = new Color(.96f, .96f, .97f, 1f);
            Text sub = MakeText(landingPanel.transform, "DEINE STADT. DEINE STORY.", 17, FontStyle.Normal, TextAnchor.MiddleCenter);
            SetRect(sub.rectTransform, new Vector2(.5f, .565f), new Vector2(600, 50));
            sub.color = new Color(.58f, .60f, .65f, 1f);
            Button enter = MakeButton(landingPanel.transform, "SPIEL STARTEN", new Vector2(.5f, .42f), new Vector2(300, 72));
            enter.onClick.AddListener(OpenSelection);

            selectionPanel = new GameObject("SelectionPanel", typeof(RectTransform));
            selectionPanel.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)selectionPanel.transform);
            selectionPanel.SetActive(false);

            Text choose = MakeText(selectionPanel.transform, "WÄHLE DEINEN CHARAKTER", 38, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(choose.rectTransform, new Vector2(.5f, .91f), new Vector2(820, 70));
            Text chooseSub = MakeText(selectionPanel.transform, "Du kannst deine Wahl später wieder ändern.", 16, FontStyle.Normal, TextAnchor.MiddleCenter);
            SetRect(chooseSub.rectTransform, new Vector2(.5f, .855f), new Vector2(700, 42));
            chooseSub.color = new Color(.58f, .60f, .65f, 1f);

            card1 = MakeCharacterCard(selectionPanel.transform, "Character01", new Vector2(.34f, .51f), out preview1, () => SelectCharacter(0));
            card2 = MakeCharacterCard(selectionPanel.transform, "Character02", new Vector2(.66f, .51f), out preview2, () => SelectCharacter(1));
            Text n1 = MakeText(card1, "RENÉ", 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(n1.rectTransform, new Vector2(.5f, .065f), new Vector2(250, 42));
            Text n2 = MakeText(card2, "DAVID", 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(n2.rectTransform, new Vector2(.5f, .065f), new Vector2(250, 42));
            startSelectionButton = MakeButton(selectionPanel.transform, "STARTEN", new Vector2(.5f, .105f), new Vector2(300, 68));
            startSelectionButton.interactable = false;
            startSelectionButton.onClick.AddListener(ConfirmSelection);
        }

        private RectTransform MakeCharacterCard(Transform parent, string name, Vector2 anchor, out RawImage raw, UnityEngine.Events.UnityAction click)
        {
            GameObject card = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
            card.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)card.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(440, 610);
            Image bg = card.GetComponent<Image>();
            bg.color = new Color(.07f, .075f, .09f, .94f);
            Button button = card.GetComponent<Button>();
            ColorBlock cb = button.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
            cb.pressedColor = new Color(.9f, .9f, .9f, 1f);
            button.colors = cb;
            button.onClick.AddListener(click);

            GameObject p = new("Preview", typeof(RectTransform), typeof(RawImage));
            p.transform.SetParent(card.transform, false);
            RectTransform pr = (RectTransform)p.transform;
            pr.anchorMin = new Vector2(.04f, .13f);
            pr.anchorMax = new Vector2(.96f, .97f);
            pr.offsetMin = pr.offsetMax = Vector2.zero;
            raw = p.GetComponent<RawImage>();
            raw.color = Color.white;
            return rt;
        }

        private void OpenSelection()
        {
            landingPanel.SetActive(false);
            selectionPanel.SetActive(true);
            SelectCharacter(0);
        }

        private void SelectCharacter(int index)
        {
            SelectedCharacterIndex = index;
            startSelectionButton.interactable = true;
            if (cardTween != null) StopCoroutine(cardTween);
            cardTween = StartCoroutine(AnimateCards(index));
        }

        private IEnumerator AnimateCards(int selected)
        {
            Vector3 from1 = card1.localScale;
            Vector3 from2 = card2.localScale;
            Vector3 to1 = selected == 0 ? Vector3.one * 1.11f : Vector3.one * .91f;
            Vector3 to2 = selected == 1 ? Vector3.one * 1.11f : Vector3.one * .91f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / .18f;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                card1.localScale = Vector3.LerpUnclamped(from1, to1, e);
                card2.localScale = Vector3.LerpUnclamped(from2, to2, e);
                yield return null;
            }
            card1.localScale = to1;
            card2.localScale = to2;
        }

        private void ConfirmSelection()
        {
            if (SelectedCharacterIndex < 0 || localPlayer == null) return;

            GameObject selectedPrefab = Resources.Load<GameObject>(SelectedCharacterIndex == 0 ? Character01Resource : Character02Resource);
            if (selectedPrefab == null)
            {
                Debug.LogError("[CYDOY CHARACTER HUB] Character prefab missing. Wait for PlayableCharacterAutoBuilder READY.");
                return;
            }

            RuntimeAnimatorController selectedController = LoadControllerForIndex(SelectedCharacterIndex);
            if (selectedController == null)
            {
                selectedController = Resources.Load<RuntimeAnimatorController>(FallbackController);
                Debug.LogWarning($"[CYDOY CHARACTER HUB] Dedicated controller for Character{SelectedCharacterIndex + 1:00} missing; using stable locomotion fallback.");
            }

            if (selectedController == null)
            {
                Debug.LogError("[CYDOY CHARACTER HUB] No usable animation controller found.");
                return;
            }

            ApplyVisualToPlayer(localPlayer, selectedPrefab, selectedController);
            PlayerPrefs.SetInt("CYDOY_SelectedCharacter", SelectedCharacterIndex);
            PlayerPrefs.Save();
            SelectionFinished = true;
            LockGameplay(false);
            canvas.gameObject.SetActive(false);
            if (previewWorld != null) previewWorld.SetActive(false);
        }

        public static RuntimeAnimatorController LoadControllerForIndex(int index)
        {
            RuntimeAnimatorController dedicated = Resources.Load<RuntimeAnimatorController>(index == 0 ? Character01Controller : Character02Controller);
            return dedicated != null ? dedicated : Resources.Load<RuntimeAnimatorController>(FallbackController);
        }

        private static void ApplyVisualToPlayer(GameObject player, GameObject visualPrefab, RuntimeAnimatorController controller)
        {
            Transform visualRoot = player.transform.Find("CharacterVisual");
            if (visualRoot == null)
            {
                GameObject vr = new("CharacterVisual");
                vr.transform.SetParent(player.transform, false);
                visualRoot = vr.transform;
            }

            for (int i = visualRoot.childCount - 1; i >= 0; i--)
                Destroy(visualRoot.GetChild(i).gameObject);

            GameObject visual = Instantiate(visualPrefab, visualRoot);
            visual.name = "SelectedCharacterVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            FitCharacterToHeightAndGround(visual, 1.82f, visualRoot.position.y);

            Animator animator = visual.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);

            CharacterAnimationDriver driver = player.GetComponent<CharacterAnimationDriver>();
            if (driver == null) driver = player.AddComponent<CharacterAnimationDriver>();
            driver.RebindToCurrentVisual(controller);

            Debug.Log($"[CYDOY CHARACTER HUB] Selected Character{SelectedCharacterIndex + 1:00}; locomotion rebound to '{controller.name}' and feet grounded.");
        }

        private void BuildPreviewWorld()
        {
            GameObject p1 = Resources.Load<GameObject>(Character01Resource);
            GameObject p2 = Resources.Load<GameObject>(Character02Resource);
            if (p1 == null || p2 == null) return;

            previewWorld = new GameObject("CharacterPreviewWorld");
            previewWorld.transform.SetParent(transform, false);
            previewWorld.transform.position = new Vector3(10000, 10000, 10000);
            previewModel1 = Instantiate(p1, previewWorld.transform);
            previewModel2 = Instantiate(p2, previewWorld.transform);
            previewModel1.transform.localPosition = new Vector3(-4, 0, 0);
            previewModel2.transform.localPosition = new Vector3(4, 0, 0);
            FitCharacterToHeightAndGround(previewModel1, 2.0f, previewWorld.transform.position.y);
            FitCharacterToHeightAndGround(previewModel2, 2.0f, previewWorld.transform.position.y);
            SetPreviewIdle(previewModel1, 0);
            SetPreviewIdle(previewModel2, 1);

            rt1 = new RenderTexture(700, 900, 24, RenderTextureFormat.ARGB32) { name = "Character01_RT" };
            rt2 = new RenderTexture(700, 900, 24, RenderTextureFormat.ARGB32) { name = "Character02_RT" };
            cam1 = MakePreviewCamera("PreviewCam01", previewModel1.transform.position, rt1);
            cam2 = MakePreviewCamera("PreviewCam02", previewModel2.transform.position, rt2);
            preview1.texture = rt1;
            preview2.texture = rt2;

            GameObject lightGo = new("PreviewKeyLight");
            lightGo.transform.SetParent(previewWorld.transform, false);
            lightGo.transform.localPosition = new Vector3(0, 4, -4);
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.45f;
            lightGo.transform.rotation = Quaternion.Euler(35, -25, 0);
        }

        private Camera MakePreviewCamera(string name, Vector3 modelPosition, RenderTexture target)
        {
            GameObject go = new(name);
            go.transform.SetParent(previewWorld.transform, true);
            Camera c = go.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(.035f, .038f, .048f, 1f);
            c.targetTexture = target;
            c.fieldOfView = 24f;
            c.nearClipPlane = .05f;
            c.farClipPlane = 30f;
            go.transform.position = modelPosition + new Vector3(0, 1.02f, -5.5f);
            go.transform.LookAt(modelPosition + Vector3.up * 1.0f);
            return c;
        }

        private static void SetPreviewIdle(GameObject go, int characterIndex)
        {
            Animator a = go.GetComponentInChildren<Animator>(true);
            if (a == null) return;
            RuntimeAnimatorController c = LoadControllerForIndex(characterIndex);
            if (c != null) a.runtimeAnimatorController = c;
            a.applyRootMotion = false;
            a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            a.enabled = true;
            a.Rebind();
            a.Update(0f);
            int idle = Animator.StringToHash("Base Layer.Idle");
            if (a.HasState(0, idle)) a.Play(idle, 0, Random.Range(0f, .25f));
        }

        private static void FitCharacterToHeightAndGround(GameObject go, float targetHeight, float groundY)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y < .01f) return;

            float scale = targetHeight / b.size.y;
            go.transform.localScale *= scale;

            // Force an exact renderer-bounds refresh after scaling before calculating the sole height.
            foreach (SkinnedMeshRenderer skin in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.updateWhenOffscreen = true;
                skin.forceMatrixRecalculationPerRender = true;
            }

            renderers = go.GetComponentsInChildren<Renderer>(true);
            b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            float deltaY = groundY - b.min.y;
            go.transform.position += Vector3.up * deltaY;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            GameObject es = new("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        private static Image MakeImage(Transform parent, string name, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image i = go.GetComponent<Image>();
            i.color = color;
            return i;
        }

        private static Text MakeText(Transform parent, string value, int size, FontStyle style, TextAnchor alignment)
        {
            GameObject go = new("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.text = value;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = alignment;
            t.color = Color.white;
            return t;
        }

        private static Button MakeButton(Transform parent, string label, Vector2 anchor, Vector2 size)
        {
            GameObject go = new(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            SetRect(rt, anchor, size);
            Image img = go.GetComponent<Image>();
            img.color = new Color(.88f, .88f, .9f, 1f);
            Button b = go.GetComponent<Button>();
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(.91f, .92f, .95f, 1f);
            cb.pressedColor = new Color(.78f, .79f, .82f, 1f);
            cb.disabledColor = new Color(.35f, .36f, .4f, .65f);
            b.colors = cb;
            Text text = MakeText(go.transform, label, 17, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.color = new Color(.05f, .055f, .065f, 1f);
            Stretch(text.rectTransform);
            return b;
        }

        private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (rt1 != null) { rt1.Release(); Destroy(rt1); }
            if (rt2 != null) { rt2.Release(); Destroy(rt2); }
        }
    }
}

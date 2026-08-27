using System.Collections;
using CheatOnYourDayOnes.Player;
using CheatOnYourDayOnes.UI;
using CheatOnYourDayOnes.Vehicles;
using CheatOnYourDayOnes.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CheatOnYourDayOnes.Core
{
    public sealed class SharedWorldBootstrap : MonoBehaviour
    {
        private static SharedWorldBootstrap _instance;
        private RuntimeAnimatorController _playerController;
        private RuntimeAnimatorController _npcController;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateAutomatically()
        {
            if (FindFirstObjectByType<SharedWorldBootstrap>() != null) return;
            GameObject go = new("CYDOY_SharedWorldBootstrap");
            _instance = go.AddComponent<SharedWorldBootstrap>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _playerController = Resources.Load<RuntimeAnimatorController>("Tripo_Locomotion_ExactGeneric");
            _npcController = Resources.Load<RuntimeAnimatorController>("LittleGuys_Locomotion");
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy(){ if (_instance == this) SceneManager.sceneLoaded -= OnSceneLoaded; }
        private void Start(){ RepairStaticWorld(); StartCoroutine(KeepRuntimeLinksHealthy()); }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode){ RepairStaticWorld(); }

        private IEnumerator KeepRuntimeLinksHealthy()
        {
            var wait = new WaitForSecondsRealtime(0.5f);
            while (true){ RepairLocalPlayer(); yield return wait; }
        }

        private void RepairStaticWorld(){ EnsureHud(); RepairCars(); RepairNpcs(); }

        private static void EnsureHud()
        {
            if (FindFirstObjectByType<PremiumHUDCanvas>() != null) return;
            GameObject hud = new("PremiumHUD_Runtime");
            hud.AddComponent<PremiumHUDCanvas>();
            DontDestroyOnLoad(hud);
        }

        private void RepairLocalPlayer()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;
            NetworkObject local = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (local == null) return;

            GameObject player = local.gameObject;
            if (player.GetComponent<VehicleInteractor>() == null) player.AddComponent<VehicleInteractor>();
            if (player.GetComponent<AmbientNPCSpawner>() == null) player.AddComponent<AmbientNPCSpawner>();
            if (player.GetComponent<MeleeAnimationBridge>() == null) player.AddComponent<MeleeAnimationBridge>();

            // Old prototype combat would otherwise compete with the new left-click system.
            PlayerMeleeCombat oldCombat = player.GetComponent<PlayerMeleeCombat>();
            if (oldCombat != null) oldCombat.enabled = false;

            Animator animator = FindPlayerAnimator(player.transform);
            if (animator != null)
            {
                if (_playerController != null && animator.runtimeAnimatorController != _playerController)
                    animator.runtimeAnimatorController = _playerController;
                animator.avatar = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;
            }
        }

        private void RepairCars()
        {
            DriveableCar[] cars = FindObjectsByType<DriveableCar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (DriveableCar car in cars) EnsureCarPhysics(car.gameObject);
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.parent != null) continue;
                string n = t.name.ToLowerInvariant();
                if (!(n == "car" || n.StartsWith("car_") || n.Contains("driveablecar") || n.Contains("vehicle"))) continue;
                if (t.GetComponentInChildren<Renderer>(true) == null) continue;
                if (t.GetComponent<DriveableCar>() == null) t.gameObject.AddComponent<DriveableCar>();
                EnsureCarPhysics(t.gameObject);
            }
        }

        private static void EnsureCarPhysics(GameObject car)
        {
            Rigidbody rb = car.GetComponent<Rigidbody>();
            if (rb == null) rb = car.AddComponent<Rigidbody>();
            rb.mass = 1350f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void RepairNpcs()
        {
            GameObject root = GameObject.Find("Generated_NPCs");
            if (root == null) return;
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                GameObject npc = FindNpcRoot(animator.transform, root.transform).gameObject;
                if (animator.runtimeAnimatorController == null && _npcController != null) animator.runtimeAnimatorController = _npcController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;
                CharacterController cc = npc.GetComponent<CharacterController>();
                if (cc == null)
                {
                    cc = npc.AddComponent<CharacterController>();
                    Bounds b = RendererBounds(npc);
                    cc.height = Mathf.Max(1.4f, b.size.y * 0.94f);
                    cc.radius = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.25f, 0.20f, 0.38f);
                    cc.center = npc.transform.InverseTransformPoint(b.center);
                    cc.stepOffset = Mathf.Min(0.22f, cc.height * 0.13f);
                }
                if (npc.GetComponent<NPCWanderer>() == null)
                {
                    NPCWanderer wanderer = npc.AddComponent<NPCWanderer>();
                    wanderer.Configure(Random.Range(1.05f, 1.28f), Random.Range(7f, 12f));
                }
            }
        }

        private static Transform FindNpcRoot(Transform child, Transform generatedRoot)
        {
            Transform current = child;
            while (current.parent != null && current.parent != generatedRoot) current = current.parent;
            return current;
        }

        private static Animator FindPlayerAnimator(Transform root)
        {
            Transform visual = root.Find("CharacterVisual");
            if (visual != null)
            {
                Animator a = visual.GetComponentInChildren<Animator>(true);
                if (a != null) return a;
            }
            Animator[] all = root.GetComponentsInChildren<Animator>(true);
            return all.Length > 0 ? all[0] : null;
        }

        private static Bounds RendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position + Vector3.up, new Vector3(.6f, 1.8f, .6f));
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}

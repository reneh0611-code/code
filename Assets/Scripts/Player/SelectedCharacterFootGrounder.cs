using System;
using CheatOnYourDayOnes.Vehicles;
using CheatOnYourDayOnes.World;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    /// <summary>
    /// Keeps the VISIBLE selected character planted on the world surface.
    /// Grounding is based on the actual foot/toe bones instead of the combined renderer bounds,
    /// because imported FBX models can contain stray/hidden geometry below the visible shoes.
    /// </summary>
    public sealed class SelectedCharacterFootGrounder : MonoBehaviour
    {
        [SerializeField] private float rayStartHeight = 3.0f;
        [SerializeField] private float rayDistance = 12.0f;
        [SerializeField] private float refreshInterval = 0.08f;

        // Mixamo/Tripo foot and toe pivots normally sit a little ABOVE the visible sole.
        // This distance converts the lowest foot/toe pivot into the approximate bottom of the shoe.
        [SerializeField] private float boneToSoleDistance = 0.085f;

        // Tiny intentional sink removes any remaining light gap/shadow seam.
        [SerializeField] private float soleSink = 0.012f;
        [SerializeField] private float maxCorrectionPerPass = 0.75f;

        private Transform _visualRoot;
        private Transform _animatedVisual;
        private Animator _animator;
        private Transform _leftFoot;
        private Transform _rightFoot;
        private Transform _leftToe;
        private Transform _rightToe;
        private float _nextGroundCheck;
        private bool _prepared;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnLocalPlayer()
        {
            RuntimeInstaller.EnsureExists();
        }

        private void Update()
        {
            Transform visualRoot = transform.Find("CharacterVisual");
            if (visualRoot == null || visualRoot.childCount == 0) return;

            Transform current = visualRoot.GetChild(0);
            if (_animatedVisual != current || _visualRoot != visualRoot)
            {
                _visualRoot = visualRoot;
                _animatedVisual = current;
                _prepared = false;
                _nextGroundCheck = 0f;
            }

            if (!_prepared)
                PrepareCurrentVisual();

            if (Time.unscaledTime < _nextGroundCheck) return;
            _nextGroundCheck = Time.unscaledTime + refreshInterval;
            GroundVisibleFeet();
        }

        private void PrepareCurrentVisual()
        {
            DisableCompetingGrounders();

            _animator = _animatedVisual.GetComponentInChildren<Animator>(true);
            _leftFoot = null;
            _rightFoot = null;
            _leftToe = null;
            _rightToe = null;

            if (_animator != null && _animator.isHuman)
            {
                _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
                _leftToe = _animator.GetBoneTransform(HumanBodyBones.LeftToes);
                _rightToe = _animator.GetBoneTransform(HumanBodyBones.RightToes);
            }

            // Generic Mixamo/Tripo rigs are not always imported as Humanoid, so resolve names too.
            _leftFoot ??= FindBoneByTokens(_animatedVisual, "leftfoot", "l_foot", "foot_l");
            _rightFoot ??= FindBoneByTokens(_animatedVisual, "rightfoot", "r_foot", "foot_r");
            _leftToe ??= FindBoneByTokens(_animatedVisual, "lefttoebase", "lefttoe", "l_toe", "toe_l");
            _rightToe ??= FindBoneByTokens(_animatedVisual, "righttoebase", "righttoe", "r_toe", "toe_r");

            foreach (SkinnedMeshRenderer skin in _animatedVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = false;

            _prepared = true;
        }

        private void DisableCompetingGrounders()
        {
            FixedWorldVisualGrounder oldWorldGrounder = GetComponent<FixedWorldVisualGrounder>();
            if (oldWorldGrounder != null) oldWorldGrounder.enabled = false;

            MixamoRuntimePoseAndGrounder oldMixamoGrounder = GetComponent<MixamoRuntimePoseAndGrounder>();
            if (oldMixamoGrounder != null) oldMixamoGrounder.enabled = false;
        }

        private void GroundVisibleFeet()
        {
            if (_visualRoot == null || _animatedVisual == null) return;
            if (!TryFindWorldGround(out float groundY)) return;

            if (!TryGetFootReferenceY(out float footPivotY))
            {
                // Last-resort fallback only if this model has no recognisable foot bones at all.
                if (!TryGetVisibleSkinnedBottom(out float visibleBottomY)) return;
                float fallbackCorrection = (groundY - soleSink) - visibleBottomY;
                _visualRoot.position += Vector3.up * Mathf.Clamp(fallbackCorrection, -maxCorrectionPerPass, maxCorrectionPerPass);
                return;
            }

            // Bone pivot is above the physical sole. Translate it down by the measured anatomical
            // offset, then put that sole directly onto/slightly into the ground.
            float currentSoleY = footPivotY - boneToSoleDistance;
            float desiredSoleY = groundY - soleSink;
            float correction = desiredSoleY - currentSoleY;
            correction = Mathf.Clamp(correction, -maxCorrectionPerPass, maxCorrectionPerPass);

            _visualRoot.position += Vector3.up * correction;
        }

        private bool TryGetFootReferenceY(out float footY)
        {
            footY = float.PositiveInfinity;
            bool found = false;

            // Toe pivots are normally closest to the actual shoe sole; foot/ankle bones are fallback.
            AddLowest(_leftToe, ref footY, ref found);
            AddLowest(_rightToe, ref footY, ref found);

            if (!found)
            {
                AddLowest(_leftFoot, ref footY, ref found);
                AddLowest(_rightFoot, ref footY, ref found);
            }

            return found;
        }

        private static void AddLowest(Transform bone, ref float y, ref bool found)
        {
            if (bone == null) return;
            if (!found || bone.position.y < y) y = bone.position.y;
            found = true;
        }

        private bool TryFindWorldGround(out float groundY)
        {
            Vector3 origin = transform.position + Vector3.up * rayStartHeight;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, rayDistance + rayStartHeight, ~0, QueryTriggerInteraction.Ignore);

            bool found = false;
            float bestDistance = float.MaxValue;
            groundY = 0f;

            foreach (RaycastHit hit in hits)
            {
                Collider c = hit.collider;
                if (c == null || hit.normal.y < .45f) continue;
                Transform ht = c.transform;

                if (ht == transform || ht.IsChildOf(transform)) continue;
                if (c.GetComponentInParent<NPCWanderer>() != null) continue;
                if (c.GetComponentInParent<DriveableCar>() != null) continue;

                CharacterController cc = c.GetComponentInParent<CharacterController>();
                if (cc != null) continue;

                Rigidbody rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;

                bool worldSurface = c is TerrainCollider || c.gameObject.isStatic || rb == null;
                if (!worldSurface) continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    groundY = hit.point.y;
                    found = true;
                }
            }

            return found;
        }

        private bool TryGetVisibleSkinnedBottom(out float bottomY)
        {
            bottomY = float.PositiveInfinity;
            bool found = false;

            foreach (SkinnedMeshRenderer skin in _animatedVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin == null || !skin.enabled || !skin.gameObject.activeInHierarchy) continue;

                // Ignore tiny imported helper/FX meshes that are common in generated FBXs.
                Bounds b = skin.bounds;
                if (b.size.y < 0.20f) continue;

                if (!found || b.min.y < bottomY)
                {
                    bottomY = b.min.y;
                    found = true;
                }
            }

            return found;
        }

        private static Transform FindBoneByTokens(Transform root, params string[] tokens)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in all)
            {
                string normalized = Normalize(t.name);
                foreach (string token in tokens)
                {
                    if (normalized.Contains(Normalize(token)))
                        return t;
                }
            }
            return null;
        }

        private static string Normalize(string value)
        {
            return value.Replace(":", string.Empty)
                        .Replace("_", string.Empty)
                        .Replace("-", string.Empty)
                        .Replace(" ", string.Empty)
                        .ToLowerInvariant();
        }

        private sealed class RuntimeInstaller : MonoBehaviour
        {
            private static RuntimeInstaller _instance;

            public static void EnsureExists()
            {
                if (_instance != null) return;
                GameObject go = new("CYDOY_SelectedCharacterGroundInstaller");
                _instance = go.AddComponent<RuntimeInstaller>();
                DontDestroyOnLoad(go);
            }

            private void Update()
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;
                NetworkObject player = NetworkManager.Singleton.LocalClient?.PlayerObject;
                if (player == null) return;

                if (player.GetComponent<SelectedCharacterFootGrounder>() == null)
                    player.gameObject.AddComponent<SelectedCharacterFootGrounder>();

                enabled = false;
            }
        }
    }
}

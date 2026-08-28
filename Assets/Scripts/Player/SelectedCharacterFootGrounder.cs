using CheatOnYourDayOnes.Vehicles;
using CheatOnYourDayOnes.World;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    /// <summary>
    /// Keeps the selected visual on the real world surface without moving the gameplay capsule.
    /// The correction is smoothed and paused in the air so locomotion and jumping stay stable.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class SelectedCharacterFootGrounder : MonoBehaviour
    {
        [SerializeField] private float rayStartHeight = 3f;
        [SerializeField] private float rayDistance = 12f;
        [SerializeField, Min(.02f)] private float refreshInterval = .04f;
        [SerializeField] private float toeToSoleDistance = .025f;
        [SerializeField] private float footToSoleDistance = .085f;
        [SerializeField] private float soleSink = .015f;
        [SerializeField, Min(.05f)] private float maxCorrectionPerPass = .45f;
        [SerializeField, Min(1f)] private float correctionSharpness = 24f;

        private readonly RaycastHit[] _groundHits = new RaycastHit[16];
        private CharacterController _controller;
        private NetworkPlayerController _movement;
        private Transform _visualRoot;
        private Transform _animatedVisual;
        private Animator _animator;
        private Transform _leftFoot;
        private Transform _rightFoot;
        private Transform _leftToe;
        private Transform _rightToe;
        private float _boneToSoleDistance;
        private float _nextGroundCheck;
        private float _pendingCorrection;
        private bool _prepared;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnLocalPlayer() => RuntimeInstaller.EnsureExists();

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _movement = GetComponent<NetworkPlayerController>();
        }

        private void LateUpdate()
        {
            if (transform.parent != null)
            {
                _pendingCorrection = 0f;
                return;
            }

            Transform visualRoot = transform.Find("CharacterVisual");
            Transform current = visualRoot != null && visualRoot.childCount > 0 ? visualRoot.GetChild(0) : null;
            if (current == null) return;

            if (_visualRoot != visualRoot || _animatedVisual != current)
            {
                _visualRoot = visualRoot;
                _animatedVisual = current;
                _prepared = false;
                _nextGroundCheck = 0f;
                _pendingCorrection = 0f;
            }

            if (!_prepared) PrepareCurrentVisual();
            if (!_prepared) return;

            bool grounded = _movement != null ? _movement.IsGrounded : _controller != null && _controller.isGrounded;
            if (!grounded)
            {
                _pendingCorrection = 0f;
                return;
            }

            if (Time.unscaledTime >= _nextGroundCheck)
            {
                _nextGroundCheck = Time.unscaledTime + refreshInterval;
                RefreshGroundCorrection();
            }

            if (Mathf.Abs(_pendingCorrection) < .0005f) return;
            float blend = 1f - Mathf.Exp(-correctionSharpness * Time.unscaledDeltaTime);
            float correction = Mathf.Abs(_pendingCorrection) < .004f
                ? _pendingCorrection
                : _pendingCorrection * blend;
            _visualRoot.position += Vector3.up * correction;
            _pendingCorrection -= correction;
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

            _leftFoot ??= FindBoneByTokens(_animatedVisual, "leftfoot", "l_foot", "foot_l");
            _rightFoot ??= FindBoneByTokens(_animatedVisual, "rightfoot", "r_foot", "foot_r");
            _leftToe ??= FindBoneByTokens(_animatedVisual, "lefttoebase", "lefttoe", "l_toe", "toe_l");
            _rightToe ??= FindBoneByTokens(_animatedVisual, "righttoebase", "righttoe", "r_toe", "toe_r");

            foreach (SkinnedMeshRenderer skin in _animatedVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.updateWhenOffscreen = true;
                skin.forceMatrixRecalculationPerRender = false;
            }

            // Renderer bounds on these imported FBX files include hidden geometry below the shoes.
            // Calibrating against those bounds produced the visible ~10 cm air gap. Toe pivots and
            // ankle pivots therefore use separate anatomical offsets instead.
            bool hasToeReference = _leftToe != null || _rightToe != null;
            _boneToSoleDistance = hasToeReference ? toeToSoleDistance : footToSoleDistance;

            _prepared = true;
        }

        private void DisableCompetingGrounders()
        {
            FixedWorldVisualGrounder oldWorldGrounder = GetComponent<FixedWorldVisualGrounder>();
            if (oldWorldGrounder != null) oldWorldGrounder.enabled = false;

            MixamoRuntimePoseAndGrounder oldMixamoGrounder = GetComponent<MixamoRuntimePoseAndGrounder>();
            if (oldMixamoGrounder != null) oldMixamoGrounder.enabled = false;
        }

        private void RefreshGroundCorrection()
        {
            if (_visualRoot == null || _animatedVisual == null || !TryFindWorldGround(out float groundY)) return;

            float correction;
            if (TryGetFootReferenceY(out float footPivotY))
            {
                float currentSoleY = footPivotY - _boneToSoleDistance;
                correction = (groundY - soleSink) - currentSoleY;
            }
            else
            {
                if (!TryGetVisibleSkinnedBottom(out float visibleBottomY)) return;
                correction = (groundY - soleSink) - visibleBottomY;
            }

            _pendingCorrection = Mathf.Clamp(correction, -maxCorrectionPerPass, maxCorrectionPerPass);
        }

        private bool TryGetFootReferenceY(out float footY)
        {
            footY = float.PositiveInfinity;
            bool found = false;
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
            int count = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _groundHits,
                rayDistance + rayStartHeight,
                ~0,
                QueryTriggerInteraction.Ignore);

            bool found = false;
            float bestDistance = float.MaxValue;
            groundY = 0f;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _groundHits[i];
                Collider collider = hit.collider;
                if (collider == null || hit.normal.y < .45f) continue;
                Transform hitTransform = collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
                if (collider.GetComponentInParent<NPCWanderer>() != null) continue;
                if (collider.GetComponentInParent<DriveableCar>() != null) continue;
                if (collider.GetComponentInParent<CharacterController>() != null) continue;

                Rigidbody body = collider.attachedRigidbody;
                if (body != null && !body.isKinematic) continue;
                if (hit.distance >= bestDistance) continue;

                bestDistance = hit.distance;
                groundY = hit.point.y;
                found = true;
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
                Bounds bounds = skin.bounds;
                if (bounds.size.y < .2f) continue;
                bottomY = Mathf.Min(bottomY, bounds.min.y);
                found = true;
            }
            return found;
        }

        private static Transform FindBoneByTokens(Transform root, params string[] tokens)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                string normalized = Normalize(candidate.name);
                foreach (string token in tokens)
                    if (normalized.Contains(Normalize(token))) return candidate;
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

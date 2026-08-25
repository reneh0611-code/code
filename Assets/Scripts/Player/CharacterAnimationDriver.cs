using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    public sealed class CharacterAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform trackedRoot;
        [SerializeField] private float maxReferenceSpeed = 6.8f;
        [SerializeField] private float damping = 0.10f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private Vector3 _lastPosition;
        private bool _initialized;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
            if (trackedRoot == null)
                trackedRoot = transform;
        }

        private void OnEnable()
        {
            if (trackedRoot != null)
            {
                _lastPosition = trackedRoot.position;
                _initialized = true;
            }
        }

        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null || trackedRoot == null)
                return;

            if (!_initialized)
            {
                _lastPosition = trackedRoot.position;
                _initialized = true;
                return;
            }

            Vector3 delta = trackedRoot.position - _lastPosition;
            _lastPosition = trackedRoot.position;
            delta.y = 0f;

            float worldSpeed = Time.deltaTime > 0.0001f ? delta.magnitude / Time.deltaTime : 0f;
            float normalizedSpeed = Mathf.Clamp01(worldSpeed / Mathf.Max(0.1f, maxReferenceSpeed));

            animator.SetFloat(SpeedHash, normalizedSpeed, damping, Time.deltaTime);
        }
    }
}

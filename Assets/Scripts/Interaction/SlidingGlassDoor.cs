using CheatOnYourDayOnes.Player;
using UnityEngine;

namespace CheatOnYourDayOnes.Interaction
{
    [DisallowMultipleComponent]
    public sealed class SlidingGlassDoor : MonoBehaviour, IInteractable
    {
        [Header("Door panels")]
        [SerializeField] private Transform leftPanel;
        [SerializeField] private Transform rightPanel;
        [SerializeField] private Collider doorwayBlocker;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float slideDistance = 0.72f;
        [SerializeField, Min(0.1f)] private float transitionDuration = 0.7f;
        [SerializeField] private Vector3 localSlideAxis = Vector3.forward;

        private Vector3 _leftClosedPosition;
        private Vector3 _rightClosedPosition;
        private float _openAmount;
        private float _targetOpenAmount;
        private bool _configured;

        public bool IsOpen => _targetOpenAmount > 0.5f;

        private void Awake()
        {
            CaptureClosedPositionsIfNeeded();
            ApplyPose();
        }

        private void OnEnable()
        {
            CaptureClosedPositionsIfNeeded();
            ApplyPose();
        }

        private void Update()
        {
            if (!_configured || Mathf.Approximately(_openAmount, _targetOpenAmount))
                return;

            float speed = 1f / Mathf.Max(0.1f, transitionDuration);
            _openAmount = Mathf.MoveTowards(_openAmount, _targetOpenAmount, speed * Time.deltaTime);
            ApplyPose();
        }

        public string GetInteractionText(PlayerAgent player)
        {
            return IsOpen ? "Schiebetür schließen" : "Schiebetür öffnen";
        }

        public bool CanInteract(PlayerAgent player)
        {
            return player != null && leftPanel != null && rightPanel != null;
        }

        public void InteractServer(PlayerAgent player)
        {
            if (!CanInteract(player))
                return;

            _targetOpenAmount = IsOpen ? 0f : 1f;

            // Keep the passage physically clear throughout the animation. The
            // blocker returns only once both panels are completely closed.
            if (doorwayBlocker != null && _targetOpenAmount > 0.5f)
                doorwayBlocker.enabled = false;
        }

        public void Configure(
            Transform newLeftPanel,
            Transform newRightPanel,
            Collider newDoorwayBlocker,
            Vector3 newLocalSlideAxis,
            float newSlideDistance,
            float newTransitionDuration)
        {
            leftPanel = newLeftPanel;
            rightPanel = newRightPanel;
            doorwayBlocker = newDoorwayBlocker;
            localSlideAxis = newLocalSlideAxis.sqrMagnitude > 0.001f
                ? newLocalSlideAxis.normalized
                : Vector3.forward;
            slideDistance = Mathf.Max(0.1f, newSlideDistance);
            transitionDuration = Mathf.Max(0.1f, newTransitionDuration);

            _leftClosedPosition = leftPanel != null ? leftPanel.localPosition : Vector3.zero;
            _rightClosedPosition = rightPanel != null ? rightPanel.localPosition : Vector3.zero;
            _configured = leftPanel != null && rightPanel != null;
            _openAmount = 0f;
            _targetOpenAmount = 0f;
            ApplyPose();
        }

        private void CaptureClosedPositionsIfNeeded()
        {
            if (_configured || leftPanel == null || rightPanel == null)
                return;

            _leftClosedPosition = leftPanel.localPosition;
            _rightClosedPosition = rightPanel.localPosition;
            _configured = true;
        }

        private void ApplyPose()
        {
            if (!_configured || leftPanel == null || rightPanel == null)
                return;

            float eased = _openAmount * _openAmount * (3f - 2f * _openAmount);
            Vector3 offset = localSlideAxis * (slideDistance * eased);
            leftPanel.localPosition = _leftClosedPosition - offset;
            rightPanel.localPosition = _rightClosedPosition + offset;

            if (doorwayBlocker != null)
                doorwayBlocker.enabled = _openAmount <= 0.001f && _targetOpenAmount <= 0.001f;
        }
    }
}

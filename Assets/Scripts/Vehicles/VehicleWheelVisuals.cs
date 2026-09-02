using UnityEngine;

namespace CheatOnYourDayOnes.Vehicles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DriveableCar))]
    public sealed class VehicleWheelVisuals : MonoBehaviour
    {
        [System.Serializable]
        private struct Wheel
        {
            public Transform steeringPivot;
            public Transform spinPivot;
            public bool steers;
        }

        [SerializeField] private Wheel frontLeft;
        [SerializeField] private Wheel frontRight;
        [SerializeField] private Wheel rearLeft;
        [SerializeField] private Wheel rearRight;
        [SerializeField, Min(.05f)] private float wheelRadius = .38f;
        [SerializeField, Range(10f, 45f)] private float maximumSteeringAngle = 30f;
        [SerializeField, Min(1f)] private float steeringVisualResponse = 11f;

        private DriveableCar _car;
        private float _spinDegrees;
        private float _steeringDegrees;

        private void Awake()
        {
            _car = GetComponent<DriveableCar>();
        }

        private void LateUpdate()
        {
            if (_car == null) return;

            float radius = Mathf.Max(.05f, wheelRadius);
            _spinDegrees = Mathf.Repeat(
                _spinDegrees + (_car.SignedWheelSpeed / radius) * Mathf.Rad2Deg * Time.deltaTime,
                360f);

            float targetSteering = _car.VisualSteeringInput * maximumSteeringAngle;
            float blend = 1f - Mathf.Exp(-steeringVisualResponse * Time.deltaTime);
            _steeringDegrees = Mathf.Lerp(_steeringDegrees, targetSteering, blend);

            Animate(frontLeft);
            Animate(frontRight);
            Animate(rearLeft);
            Animate(rearRight);
        }

        private void Animate(Wheel wheel)
        {
            if (wheel.steeringPivot != null)
                wheel.steeringPivot.localRotation = wheel.steers
                    ? Quaternion.Euler(0f, _steeringDegrees, 0f)
                    : Quaternion.identity;

            if (wheel.spinPivot != null)
                wheel.spinPivot.localRotation = Quaternion.Euler(_spinDegrees, 0f, 0f);
        }

#if UNITY_EDITOR
        public void Configure(
            Transform frontLeftSteering, Transform frontLeftSpin,
            Transform frontRightSteering, Transform frontRightSpin,
            Transform rearLeftSteering, Transform rearLeftSpin,
            Transform rearRightSteering, Transform rearRightSpin,
            float radius)
        {
            frontLeft = NewWheel(frontLeftSteering, frontLeftSpin, true);
            frontRight = NewWheel(frontRightSteering, frontRightSpin, true);
            rearLeft = NewWheel(rearLeftSteering, rearLeftSpin, false);
            rearRight = NewWheel(rearRightSteering, rearRightSpin, false);
            wheelRadius = Mathf.Max(.05f, radius);
        }

        private static Wheel NewWheel(Transform steering, Transform spin, bool steers)
        {
            return new Wheel { steeringPivot = steering, spinPivot = spin, steers = steers };
        }
#endif
    }
}

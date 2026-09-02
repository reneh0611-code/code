using UnityEngine;
using UnityEngine.InputSystem;

namespace CheatOnYourDayOnes.Vehicles
{
    public sealed class VehicleInteractor : MonoBehaviour
    {
        [SerializeField] private float enterRadius = 3.5f;

        private DriveableCar _nearbyCar;
        private float _nextVehicleScan;
        private float _allowEnterAfter;
        private bool _waitForERelease;

        public DriveableCar NearbyCar => _nearbyCar;
        public bool CanEnterVehicle => IsValidCandidate(_nearbyCar);
        public string NearbyVehicleName => CanEnterVehicle ? _nearbyCar.VehicleLabel : string.Empty;

        private void Update()
        {
            if (_waitForERelease)
            {
                if (Keyboard.current == null || !Keyboard.current.eKey.isPressed)
                    _waitForERelease = false;
                else
                    return;
            }

            if (!IsValidCandidate(_nearbyCar))
                _nearbyCar = null;

            if (Time.time >= _nextVehicleScan)
            {
                _nextVehicleScan = Time.time + .1f;
                _nearbyCar = FindNearestAvailableCar();
            }

            if (_nearbyCar == null || Keyboard.current == null || Time.unscaledTime < _allowEnterAfter)
                return;

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                bool entered = _nearbyCar.TryEnter(transform);
                if (entered)
                    _nearbyCar = null;
                if (!entered)
                    Debug.LogWarning($"[CYDOY] E pressed near {_nearbyCar.name}, but entering failed.", _nearbyCar);
            }
        }

        private void OnDisable()
        {
            _nearbyCar = null;
        }

        private void OnEnable()
        {
            _nearbyCar = null;
            _nextVehicleScan = 0f;
            _allowEnterAfter = Time.unscaledTime + .2f;
            _waitForERelease = Keyboard.current != null && Keyboard.current.eKey.isPressed;
        }

        private DriveableCar FindNearestAvailableCar()
        {
            DriveableCar nearest = null;
            float best = enterRadius;

            DriveableCar[] cars = Object.FindObjectsByType<DriveableCar>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (DriveableCar car in cars)
            {
                if (!IsValidCandidate(car))
                    continue;

                float d = car.DistanceFrom(transform.position);
                if (d <= best)
                {
                    best = d;
                    nearest = car;
                }
            }

            return nearest;
        }

        private bool IsValidCandidate(DriveableCar car)
        {
            if (car == null || !car.isActiveAndEnabled || car.IsOccupied)
                return false;
            Vector3 toCar = car.transform.position - transform.position;
            toCar.y = 0f;
            if (toCar.sqrMagnitude > (enterRadius + 3f) * (enterRadius + 3f))
                return false;
            return car.DistanceFrom(transform.position) <= enterRadius;
        }
    }
}

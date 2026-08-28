using UnityEngine;
using UnityEngine.InputSystem;

namespace CheatOnYourDayOnes.Vehicles
{
    public sealed class VehicleInteractor : MonoBehaviour
    {
        [SerializeField] private float enterRadius = 3.5f;

        private DriveableCar _nearbyCar;
        private float _nextVehicleScan;

        public DriveableCar NearbyCar => _nearbyCar;
        public bool CanEnterVehicle => _nearbyCar != null;

        private void Update()
        {
            if (Time.time >= _nextVehicleScan)
            {
                _nextVehicleScan = Time.time + .15f;
                _nearbyCar = FindNearestAvailableCar();
            }

            if (_nearbyCar == null || Keyboard.current == null)
                return;

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                bool entered = _nearbyCar.TryEnter(transform);
                if (!entered)
                    Debug.LogWarning($"[CYDOY] E pressed near {_nearbyCar.name}, but entering failed.", _nearbyCar);
            }
        }

        private DriveableCar FindNearestAvailableCar()
        {
            DriveableCar nearest = null;
            float best = enterRadius;

            foreach (DriveableCar car in DriveableCar.ActiveCars)
            {
                if (car == null || car.IsOccupied)
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
    }
}

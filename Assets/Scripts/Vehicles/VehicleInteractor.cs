using UnityEngine;
using UnityEngine.InputSystem;

namespace CheatOnYourDayOnes.Vehicles
{
    public sealed class VehicleInteractor : MonoBehaviour
    {
        [SerializeField] private float enterRadius = 3.0f;

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
            DriveableCar nearest = null;
            float best = enterRadius;
            foreach (DriveableCar car in Object.FindObjectsByType<DriveableCar>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(transform.position, car.transform.position);
                if (!car.IsOccupied && d < best) { best = d; nearest = car; }
            }
            if (nearest != null) nearest.TryEnter(transform);
        }
    }
}

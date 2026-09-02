using UnityEngine;

namespace CheatOnYourDayOnes.Vehicles
{
    public sealed class ParkedVehicleSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject[] vehiclePrefabs;
        [SerializeField] private ParkingSlotMarker[] slots;
        [SerializeField] private bool spawnOnStart=true;

        private void Start()
        {
            if(!spawnOnStart)return;
            SpawnParkedCars();
        }

        public void Configure(GameObject[] prefabs,ParkingSlotMarker[] parkingSlots)
        {
            vehiclePrefabs=prefabs;
            slots=parkingSlots;
        }

        public void SpawnParkedCars()
        {
            if(vehiclePrefabs==null||vehiclePrefabs.Length==0||slots==null)return;
            for(int i=0;i<slots.Length;i++)
            {
                ParkingSlotMarker slot=slots[i];
                if(slot==null||slot.GetComponentInChildren<DriveableCar>(true)!=null)continue;
                GameObject prefab=vehiclePrefabs[i%vehiclePrefabs.Length];
                if(prefab==null)continue;
                GameObject car=Instantiate(prefab,slot.transform.position,slot.transform.rotation,slot.transform);
                car.name=prefab.name;
                GroundCar(car,slot.transform.position.y);
            }
        }

        public static void GroundCar(GameObject car,float surfaceY)
        {
            if(car==null)return;
            Renderer[] renderers=car.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0)return;
            float bottom=float.PositiveInfinity;
            foreach(Renderer renderer in renderers)if(renderer!=null&&renderer.enabled)bottom=Mathf.Min(bottom,renderer.bounds.min.y);
            if(float.IsInfinity(bottom))return;
            car.transform.position+=Vector3.up*(surfaceY+.02f-bottom);
            Physics.SyncTransforms();
        }
    }
}

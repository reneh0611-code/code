using CheatOnYourDayOnes.Vehicles;
using CheatOnYourDayOnes.Player;
using UnityEditor;
using UnityEngine;

namespace CheatOnYourDayOnes.EditorTools
{
    public static class DriveableCarInstaller
    {
        [MenuItem("Tools/CYDOY/Vehicles/Make Selected Car Driveable")]
        public static void InstallCar()
        {
            GameObject car = Selection.activeGameObject;
            if (car == null) { EditorUtility.DisplayDialog("CYDOY", "Select the root GameObject of the car first.", "OK"); return; }
            Undo.RegisterFullObjectHierarchyUndo(car, "Make Car Driveable");
            Rigidbody rb = car.GetComponent<Rigidbody>();
            if (rb == null) rb = Undo.AddComponent<Rigidbody>(car);
            rb.mass = 1350f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (car.GetComponent<DriveableCar>() == null) Undo.AddComponent<DriveableCar>(car);

            NetworkPlayerController player = Object.FindFirstObjectByType<NetworkPlayerController>(FindObjectsInactive.Include);
            if (player != null && player.GetComponent<VehicleInteractor>() == null) Undo.AddComponent<VehicleInteractor>(player.gameObject);

            EditorUtility.SetDirty(car);
            EditorUtility.DisplayDialog("CYDOY · Vehicle", "Done. The selected car now has a Rigidbody, automatic body hitbox and driving physics. E enters/exits, WASD drives, Space brakes. If the scene player was found, E interaction was installed there too.", "Test it");
        }
    }
}

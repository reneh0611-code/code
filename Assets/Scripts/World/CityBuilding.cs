using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    public enum CityDistrict
    {
        Downtown,
        Residential,
        Commercial,
        Industrial,
        Leisure,
        Civic
    }

    public enum CityBuildingType
    {
        Apartment,
        Supermarket,
        GasStation,
        Bank,
        CarDealer,
        Workshop,
        Warehouse,
        Restaurant,
        Gym,
        Hospital,
        PoliceStation,
        FireStation,
        JobCenter,
        ClothingStore,
        CityHall,
        Office,
        GenericShop,
        Residential,
        Industrial,
        Leisure
    }

    public sealed class CityBuilding : MonoBehaviour
    {
        [SerializeField] private string buildingId;
        [SerializeField] private string displayName;
        [SerializeField] private CityDistrict district;
        [SerializeField] private CityBuildingType buildingType;
        [SerializeField] private bool supportsEmployment;
        [SerializeField] private bool playerCanEnterLater = true;

        public string BuildingId => buildingId;
        public string DisplayName => displayName;
        public CityDistrict District => district;
        public CityBuildingType BuildingType => buildingType;
        public bool SupportsEmployment => supportsEmployment;
        public bool PlayerCanEnterLater => playerCanEnterLater;

        public void Configure(string id, string label, CityDistrict cityDistrict, CityBuildingType type, bool employment, bool enterable = true)
        {
            buildingId = id;
            displayName = label;
            district = cityDistrict;
            buildingType = type;
            supportsEmployment = employment;
            playerCanEnterLater = enterable;
        }
    }
}

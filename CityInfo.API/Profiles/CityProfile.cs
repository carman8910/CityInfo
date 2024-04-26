using AutoMapper;
using CityInfo.API.Models;

namespace CityInfo.API.Profiles
{
    public class CityProfile: Profile
    {
        public CityProfile()
        {
            CreateMap<Entities.City, CityWithoutPointsOfInterestDto>();
            CreateMap<Entities.City, CityDto>();
        }
    }
}

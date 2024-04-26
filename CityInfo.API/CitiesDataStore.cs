using CityInfo.API.Models;

namespace CityInfo.API
{
    public class CitiesDataStore
    {
        public List<CityDto> Cities { get; set; }

        // public static CitiesDataStore Current { get; } = new CitiesDataStore();

        public CitiesDataStore()
        {
            Cities = new List<CityDto>()
            {
                new CityDto()
                {
                    Id = 1,
                    Name = "New York City",
                    Description = "The one with that big park",
                    PointsOfInterest =
                    {
                        new PointOfInterestDto ()
                        {
                            Id = 1,
                            Name = "Central Park",
                            Description="The most visited park"

                        },
                        new PointOfInterestDto ()
                        {Id = 2,
                        Name = "Empire State Building",
                        Description = "A 102-store skyscraper"

                        },
                    }
                },
                new CityDto()
                {
                    Id = 2,
                    Name = "Antwerp",
                    Description = "The one the cathedral that was never really..",
                },
                new CityDto()
                {
                    Id = 3,
                    Name = "Paris",
                    Description = "The one with that big tower",
                    PointsOfInterest =
                    {
                        new PointOfInterestDto ()
                        {
                            Id = 5,
                            Name = "Eiffel Tower",
                            Description="An iron build"

                        },
                        new PointOfInterestDto ()
                        {Id = 6,
                        Name = "The Louvre",
                        Description = "The world's largest museum."

                        },
                    }
                },

            };
        }
    }
}

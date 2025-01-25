using Dapper;
using Microsoft.Data.SqlClient;
using System.Diagnostics.Eventing.Reader;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.DAL
{
    public class RestaurantDAL
    {
        private readonly ConfigPerso _config;
        private readonly GoogleMapsService googleMapsService;

        public RestaurantDAL(ConfigPerso config)
        {
            _config = config;
            googleMapsService = new GoogleMapsService(config);
        }

        //public RestaurantOut GetRestaurantById(long id)
        //{

        //}
        public async Task<RestaurantOut> PostRestaurantAsync(long idUser, RestaurantIn restaurant)
        {
            await googleMapsService.FillLatLongAsync(restaurant);

            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();

                var query = @"
                            INSERT INTO [Restaurant] 
                            (UserId, RestaurantName, StreetNumber, StreetName, PostalCode, City, Latitude, Longitude) 
                            OUTPUT 
                                INSERTED.Id, INSERTED.UserId, INSERTED.RestaurantName, INSERTED.StreetNumber,INSERTED.StreetName, 
                                INSERTED.PostalCode, INSERTED.City, INSERTED.Latitude, INSERTED.Longitude, INSERTED.CreatedAt 
                            VALUES 
                            (@UserId, @RestaurantName, @StreetNumber, @StreetName, @PostalCode, @City, @Latitude, @Longitude)";

                var insertedRestaurant = connection.QuerySingle<RestaurantOut>(query, new
                {
                    UserId = idUser,restaurant.RestaurantName,restaurant.StreetNumber,restaurant.StreetName,
                    restaurant.PostalCode,restaurant.City,restaurant.Latitude,restaurant.Longitude
                });
                insertedRestaurant.DailyActivitys = [];
                insertedRestaurant.ClosedDayExceptions = [];
                insertedRestaurant.Tables = [];
                return insertedRestaurant;
            }
        }

        public async Task<RestaurantOut> putRestaurant(long idUser, long id, RestaurantIn restaurant)
        {
            await googleMapsService.FillLatLongAsync(restaurant);
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = @"
                            UPDATE [Restaurant] 
                            SET RestaurantName = @RestaurantName, StreetNumber = @StreetNumber, StreetName = @StreetName, PostalCode = @PostalCode, City = @City, Latitude = @Latitude, Longitude = @Longitude
                            OUTPUT 
                                INSERTED.Id, INSERTED.UserId, INSERTED.RestaurantName, INSERTED.StreetNumber,INSERTED.StreetName, 
                                INSERTED.PostalCode, INSERTED.City, INSERTED.Latitude, INSERTED.Longitude, INSERTED.CreatedAt 
                            WHERE Id = @Id and UserId = @UserId";
                var updatedRestaurant = connection.QuerySingle<RestaurantOut>(query, new
                {
                    UserId = idUser,
                    Id = id,
                    restaurant.RestaurantName,
                    restaurant.StreetNumber,
                    restaurant.StreetName,
                    restaurant.PostalCode,
                    restaurant.City,
                    restaurant.Latitude,
                    restaurant.Longitude
                });
                updatedRestaurant.DailyActivitys = [];
                updatedRestaurant.ClosedDayExceptions = [];
                updatedRestaurant.Tables = [];
                return updatedRestaurant;
            }
        }

        public bool DeleteRestaurant(long idUser, long id)
        {
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = "DELETE FROM [Restaurant] WHERE Id = @Id and UserId = @IdUser";
                var affectedRows = connection.Execute(query, new { Id = id, IdUser = idUser });
                return affectedRows > 0;
            }
        }
    }
}

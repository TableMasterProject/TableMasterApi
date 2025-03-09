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

        public async Task<IEnumerable<RestaurantOut>> GetRestaurants(SearchRestaurant search)
        {
            var query = @"
                SELECT
                    R.*,
                    ROUND(
                        6371000 * 
                        ACOS(
                            COS(RADIANS(@Latitude)) * COS(RADIANS(R.Latitude)) * 
                            COS(RADIANS(R.Longitude) - RADIANS(@Longitude)) + 
                            SIN(RADIANS(@Latitude)) * SIN(RADIANS(R.Latitude))
                        ),
                        0
                    ) AS Distance,
                    ROUND(
                        (SELECT AVG(CAST(Rv.Rating AS DECIMAL(10, 2)))
                         FROM [Review] Rv
                         WHERE Rv.RestaurantId = R.Id), 
                    2) AS AverageRating,
                    (SELECT COUNT(*) 
                    FROM [Review] Rv 
                    WHERE Rv.RestaurantId = R.Id) AS NumberOfReviews
                FROM 
                    [Restaurant] R
                WHERE 
                    (@CuisineType IS NULL OR R.CuisineType LIKE '%'+@CuisineType+'%') AND
                    (@PaymentMethods IS NULL OR R.PaymentMethods LIKE '%'+@PaymentMethods+'%')
                ORDER BY 
                    Distance ASC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                IEnumerable<RestaurantOut> IEnumerablerestaurants = await connection.QueryAsync<RestaurantOut>(query, new
                {
                    search.Offset,
                    search.PageSize,
                    search.Latitude,
                    search.Longitude,
                    search.CuisineType,
                    search.PaymentMethods
                });

                var restaurants = IEnumerablerestaurants.ToList();

                foreach (var restaurant in restaurants)
                {
                    restaurant.DailyActivitys = await GetDailyActivity(restaurant.Id);
                    restaurant.ClosedDayExceptions = await GetClosedDayException(restaurant.Id);
                }
                return restaurants;
            }
        }

        public async Task<RestaurantOut?> PostRestaurantAsync(long idUser, RestaurantIn restaurant)
        {
            await googleMapsService.FillLatLongAsync(restaurant);

            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();

                var query = @"
                            INSERT INTO [Restaurant] 
                                (UserId, RestaurantName, StreetNumber, StreetName, 
                                PostalCode, City, Latitude, Longitude, Phone, 
                                CuisineType, PaymentMethods, Description, IsAutoValidateReservation) 
                            OUTPUT 
                                INSERTED.Id, INSERTED.UserId, INSERTED.RestaurantName, INSERTED.StreetNumber,INSERTED.StreetName, 
                                INSERTED.PostalCode, INSERTED.City, INSERTED.Latitude, INSERTED.Longitude, INSERTED.Phone, 
                                INSERTED.CuisineType, INSERTED.PaymentMethods, INSERTED.Description, INSERTED.CreatedAt, INSERTED.IsAutoValidateReservation
                            VALUES 
                                (@UserId, @RestaurantName, @StreetNumber, @StreetName, 
                                @PostalCode, @City, @Latitude, @Longitude, @Phone, 
                                @CuisineType, @PaymentMethods, @Description, @IsAutoValidateReservation)";

                var insertedRestaurant = await connection.QuerySingleAsync<RestaurantOut>(query, new
                {
                    UserId = idUser,restaurant.RestaurantName,restaurant.StreetNumber,restaurant.StreetName,
                    restaurant.PostalCode,restaurant.City,restaurant.Latitude,restaurant.Longitude, restaurant.Phone,
                    restaurant.CuisineType, restaurant.PaymentMethods, restaurant.Description, restaurant.IsAutoValidateReservation
                });
                insertedRestaurant.DailyActivitys = [];
                insertedRestaurant.ClosedDayExceptions = [];
                insertedRestaurant.Tables = [];
                insertedRestaurant.Menu = [];
                return insertedRestaurant;
            }
        }

        public async Task<RestaurantOut?> putRestaurant(long idUser, long id, RestaurantIn restaurant)
        {
            await googleMapsService.FillLatLongAsync(restaurant);
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = @"
                            UPDATE [Restaurant] 
                                SET RestaurantName = @RestaurantName, StreetNumber = @StreetNumber, StreetName = @StreetName, 
                                PostalCode = @PostalCode, City = @City, Latitude = @Latitude, Longitude = @Longitude, Phone = @Phone,
                                CuisineType = @CuisineType, PaymentMethods = @PaymentMethods, Description = @Description, IsAutoValidateReservation = @IsAutoValidateReservation
                            OUTPUT 
                                INSERTED.Id, INSERTED.UserId, INSERTED.RestaurantName, INSERTED.StreetNumber,INSERTED.StreetName, 
                                INSERTED.PostalCode, INSERTED.City, INSERTED.Latitude, INSERTED.Longitude, INSERTED.Phone, 
                                INSERTED.CuisineType, INSERTED.PaymentMethods, INSERTED.Description, INSERTED.CreatedAt, INSERTED.IsAutoValidateReservation
                            WHERE Id = @Id and UserId = @UserId";
                var updatedRestaurant = await connection.QuerySingleAsync<RestaurantOut>(query, new
                {
                    UserId = idUser,
                    Id = id,
                    restaurant.RestaurantName,
                    restaurant.StreetNumber,
                    restaurant.StreetName,
                    restaurant.PostalCode,
                    restaurant.City,
                    restaurant.Latitude,
                    restaurant.Longitude,
                    restaurant.Phone,
                    restaurant.CuisineType,
                    restaurant.PaymentMethods,
                    restaurant.Description,
                    restaurant.IsAutoValidateReservation
                });
                updatedRestaurant.DailyActivitys = [];
                updatedRestaurant.ClosedDayExceptions = [];
                updatedRestaurant.Tables = [];
                updatedRestaurant.Menu = [];
                return updatedRestaurant;
            }
        }

        public async Task<RestaurantOut?> GetRestaurantById(long id)
        {
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = @"
                                SELECT 
                                    R.*, 
                                    ROUND(
                                        (SELECT AVG(CAST(Rv.Rating AS DECIMAL(10, 2)))
                                         FROM [Review] Rv
                                         WHERE Rv.RestaurantId = R.Id), 
                                    2) AS AverageRating,
                                    (SELECT COUNT(*) 
                                     FROM [Review] Rv 
                                     WHERE Rv.RestaurantId = R.Id) AS NumberOfReviews
                                FROM [Restaurant] R 
                                WHERE R.Id = @Id;
                                ";
                var restaurant = await connection.QuerySingleOrDefaultAsync<RestaurantOut>(query, new { Id = id });
                if(restaurant == null)
                    return null;

                restaurant.DailyActivitys = await GetDailyActivity(restaurant.Id);
                restaurant.ClosedDayExceptions = await GetClosedDayException(restaurant.Id);
                restaurant.Tables = await GetTables(restaurant.Id);
                restaurant.Menu = await GetMenus(restaurant.Id);
                restaurant.Reviews = await GetReviews(restaurant.Id);

                return restaurant;
            }
        }

        private async Task<IEnumerable<MenuOut>> GetMenus(long idRestaurent)
        {
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = "SELECT * FROM [Menu] WHERE RestaurantId = @Id";
                var menus = await connection.QueryAsync<MenuOut>(query, new { Id = idRestaurent });
                return menus;
            }
        }
        private async Task<IEnumerable<TableEntityOut>> GetTables(long idRestaurent)
        {
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = "SELECT * FROM [TableEntity] WHERE RestaurantId = @Id";
                var tables = await connection.QueryAsync<TableEntityOut>(query, new { Id = idRestaurent });
                return tables;
            }
        }
        private async Task<IEnumerable<DailyActivityOut>> GetDailyActivity(long idRestaurent)
        {
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = "SELECT * FROM [DailyActivity] WHERE RestaurantId = @Id";
                var dailyActivitys = await connection.QueryAsync<DailyActivityOut>(query, new { Id = idRestaurent });
                return dailyActivitys;
            }
        }
        private async Task<IEnumerable<ClosedDayExceptionOut>> GetClosedDayException(long idRestaurent)
        {
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = "SELECT * FROM [ClosedDayException] WHERE RestaurantId = @Id AND GETDATE() <= ExceptionDateEnd;";
                var closedDayExceptions = await connection.QueryAsync<ClosedDayExceptionOut>(query, new { Id = idRestaurent });
                return closedDayExceptions;
            }
        }
        private async Task<IEnumerable<ReviewOut>> GetReviews(long idRestaurent)
        {
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = "SELECT * FROM [Review] WHERE RestaurantId = @Id;";
                var closedDayExceptions = await connection.QueryAsync<ReviewOut>(query, new { Id = idRestaurent });
                return closedDayExceptions;
            }
        }

        public async Task<bool> DeleteRestaurant(long idUser, long id)
        {
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = "DELETE FROM [Restaurant] WHERE Id = @Id and UserId = @IdUser";
                var affectedRows = await connection.ExecuteAsync(query, new { Id = id, IdUser = idUser });
                return affectedRows > 0;
            }
        }
    }
}

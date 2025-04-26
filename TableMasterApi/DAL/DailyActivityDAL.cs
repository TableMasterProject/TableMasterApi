using Dapper;
using Microsoft.Data.SqlClient;
using System.Data.SqlClient;
using TableMasterApi.Model;

namespace TableMasterApi.DAL
{
    public class DailyActivityDAL
    {
        private readonly ConfigPerso _config;

        public DailyActivityDAL(ConfigPerso config)
        {
            _config = config;
        }

        public async Task<DailyActivityOut?> GetByIdAsync(long id)
        {
            var query = "SELECT * FROM DailyActivity WHERE Id = @Id";
            using var connection = new SqlConnection(_config.ConnectionString);
            return await connection.QueryFirstOrDefaultAsync<DailyActivityOut>(query, new { Id = id });
        }

        public async Task<IEnumerable<DailyActivityOut>> GetByRestaurantAsync(long restaurantId)
        {
            var query = "SELECT * FROM DailyActivity WHERE RestaurantId = @RestaurantId ORDER BY DayOfWeek, StartTime";
            using var connection = new SqlConnection(_config.ConnectionString);
            return await connection.QueryAsync<DailyActivityOut>(query, new { RestaurantId = restaurantId });
        }

        public async Task<DailyActivityOut> InsertAsync(DailyActivityIn input)
        {
            var query = @"
                INSERT INTO DailyActivity (RestaurantId, DayOfWeek, StartTime, EndTime)
                OUTPUT INSERTED.Id, INSERTED.RestaurantId, INSERTED.DayOfWeek, INSERTED.StartTime, 
                       INSERTED.EndTime, INSERTED.CreatedAt
                VALUES (@RestaurantId, @DayOfWeek, @StartTime, @EndTime);";

            using var connection = new SqlConnection(_config.ConnectionString);
            return await connection.QuerySingleAsync<DailyActivityOut>(query, input);
        }

        public async Task<DailyActivityOut?> UpdateAsync(long id, DailyActivityIn input)
        {
            var query = @"
                UPDATE DailyActivity
                SET RestaurantId = @RestaurantId,
                    DayOfWeek = @DayOfWeek,
                    StartTime = @StartTime,
                    EndTime = @EndTime
                OUTPUT INSERTED.Id, INSERTED.RestaurantId, INSERTED.DayOfWeek, INSERTED.StartTime, 
                       INSERTED.EndTime, INSERTED.CreatedAt
                WHERE Id = @Id";

            using var connection = new SqlConnection(_config.ConnectionString);
            return await connection.QuerySingleOrDefaultAsync<DailyActivityOut>(query, new
            {
                Id = id,
                input.RestaurantId,
                input.DayOfWeek,
                input.StartTime,
                input.EndTime
            });
        }


        public async Task<bool> DeleteAsync(long id)
        {
            var query = "DELETE FROM DailyActivity WHERE Id = @Id";
            using var connection = new SqlConnection(_config.ConnectionString);
            var affectedRows = await connection.ExecuteAsync(query, new { Id = id });
            return affectedRows > 0;
        }
    }
}

using Dapper;
using Npgsql;
using TableMasterApi.Model;
using TableMasterApi.DAL.Interfaces;

namespace TableMasterApi.DAL
{
    /// <summary>
    /// Data Access Layer pour les activités quotidiennes
    /// </summary>
    public class DailyActivityDAL : IDailyActivityDAL
    {
        private readonly ConfigPerso _config;

        public DailyActivityDAL(ConfigPerso config)
        {
            _config = config;
        }

        public async Task<DailyActivityOut?> GetByIdAsync(long id)
        {
            var query = @"SELECT * FROM ""DailyActivity"" WHERE ""Id"" = @Id";
            using var connection = new NpgsqlConnection(_config.ConnectionString);
            return await connection.QueryFirstOrDefaultAsync<DailyActivityOut>(query, new { Id = id });
        }

        public async Task<IEnumerable<DailyActivityOut>> GetByRestaurantAsync(long restaurantId)
        {
            var query = @"SELECT * FROM ""DailyActivity"" WHERE ""RestaurantId"" = @RestaurantId ORDER BY ""DayOfWeek"", ""StartTime""";
            using var connection = new NpgsqlConnection(_config.ConnectionString);
            return await connection.QueryAsync<DailyActivityOut>(query, new { RestaurantId = restaurantId });
        }

        public async Task<DailyActivityOut> InsertAsync(DailyActivityIn input)
        {
            var query = @"
                INSERT INTO ""DailyActivity"" (""RestaurantId"", ""DayOfWeek"", ""StartTime"", ""EndTime"")
                VALUES (@RestaurantId, @DayOfWeek, @StartTime, @EndTime)
                RETURNING ""Id"", ""RestaurantId"", ""DayOfWeek"", ""StartTime"", ""EndTime"", ""CreatedAt"";";

            using var connection = new NpgsqlConnection(_config.ConnectionString);
            return await connection.QuerySingleAsync<DailyActivityOut>(query, input);
        }

        public async Task<DailyActivityOut?> UpdateAsync(long id, DailyActivityIn input)
        {
            var query = @"
                UPDATE ""DailyActivity""
                SET ""RestaurantId"" = @RestaurantId,
                    ""DayOfWeek"" = @DayOfWeek,
                    ""StartTime"" = @StartTime,
                    ""EndTime"" = @EndTime
                WHERE ""Id"" = @Id
                RETURNING ""Id"", ""RestaurantId"", ""DayOfWeek"", ""StartTime"", ""EndTime"", ""CreatedAt""";

            using var connection = new NpgsqlConnection(_config.ConnectionString);
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
            var query = @"DELETE FROM ""DailyActivity"" WHERE ""Id"" = @Id";
            using var connection = new NpgsqlConnection(_config.ConnectionString);
            var affectedRows = await connection.ExecuteAsync(query, new { Id = id });
            return affectedRows > 0;
        }
    }
}

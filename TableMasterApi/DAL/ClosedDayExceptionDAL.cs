using Dapper;
using Npgsql;
using TableMasterApi.Model;
using TableMasterApi.DAL.Interfaces;

namespace TableMasterApi.DAL
{
    /// <summary>
    /// Data Access Layer pour les jours de fermeture exceptionnels
    /// </summary>
    public class ClosedDayExceptionDAL : IClosedDayExceptionDAL
    {
        private readonly ConfigPerso _config;

        public ClosedDayExceptionDAL(ConfigPerso config)
        {
            _config = config;
        }

        public async Task<ClosedDayExceptionOut?> GetByIdAsync(long id)
        {
            var query = @"SELECT * FROM ""ClosedDayException"" WHERE ""Id"" = @Id";
            using var connection = new NpgsqlConnection(_config.ConnectionString);
            return await connection.QueryFirstOrDefaultAsync<ClosedDayExceptionOut>(query, new { Id = id });
        }


        public async Task<IEnumerable<ClosedDayExceptionOut>> GetByRestaurantAsync(long restaurantId)
        {
            var query = @"SELECT * FROM ""ClosedDayException"" WHERE ""RestaurantId"" = @RestaurantId ORDER BY ""ExceptionDateBegin""";
            using var connection = new NpgsqlConnection(_config.ConnectionString);
            return await connection.QueryAsync<ClosedDayExceptionOut>(query, new { RestaurantId = restaurantId });
        }

        public async Task<ClosedDayExceptionOut> InsertAsync(ClosedDayExceptionIn input)
        {
            var query = @"
                INSERT INTO ""ClosedDayException"" (""RestaurantId"", ""ExceptionDateBegin"", ""ExceptionDateEnd"", ""Reason"")
                VALUES (@RestaurantId, @ExceptionDateBegin, @ExceptionDateEnd, @Reason)
                RETURNING ""Id"", ""RestaurantId"", ""ExceptionDateBegin"", ""ExceptionDateEnd"", ""Reason"", ""CreatedAt"";
            ";

            using var connection = new NpgsqlConnection(_config.ConnectionString);
            return await connection.QuerySingleAsync<ClosedDayExceptionOut>(query, input);
        }


        public async Task<ClosedDayExceptionOut?> UpdateAsync(long id, ClosedDayExceptionIn input)
        {
            var query = @"
                UPDATE ""ClosedDayException""
                SET ""ExceptionDateBegin"" = @ExceptionDateBegin,
                    ""ExceptionDateEnd"" = @ExceptionDateEnd,
                    ""Reason"" = @Reason
                WHERE ""Id"" = @Id AND ""RestaurantId"" = @RestaurantId
                RETURNING ""Id"", ""RestaurantId"", ""ExceptionDateBegin"", ""ExceptionDateEnd"", ""Reason"", ""CreatedAt"";
            ";

            using var connection = new NpgsqlConnection(_config.ConnectionString);
            return await connection.QuerySingleOrDefaultAsync<ClosedDayExceptionOut>(query, new
            {
                Id = id,
                input.RestaurantId,
                input.ExceptionDateBegin,
                input.ExceptionDateEnd,
                input.Reason
            });
        }


        public async Task<bool> DeleteAsync(long id)
        {
            var query = @"DELETE FROM ""ClosedDayException"" WHERE ""Id"" = @Id";
            using var connection = new NpgsqlConnection(_config.ConnectionString);
            var affectedRows = await connection.ExecuteAsync(query, new { Id = id });
            return affectedRows > 0;
        }
    }
}

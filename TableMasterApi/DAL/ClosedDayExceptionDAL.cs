using Dapper;
using Microsoft.Data.SqlClient;
using TableMasterApi.Model;

namespace TableMasterApi.DAL
{
    public class ClosedDayExceptionDAL
    {
        private readonly ConfigPerso _config;

        public ClosedDayExceptionDAL(ConfigPerso config)
        {
            _config = config;
        }

        public async Task<ClosedDayExceptionOut?> GetByIdAsync(long id)
        {
            var query = "SELECT * FROM ClosedDayException WHERE Id = @Id";
            using var connection = new SqlConnection(_config.ConnectionString);
            return await connection.QueryFirstOrDefaultAsync<ClosedDayExceptionOut>(query, new { Id = id });
        }


        public async Task<IEnumerable<ClosedDayExceptionOut>> GetByRestaurantAsync(long restaurantId)
        {
            var query = "SELECT * FROM ClosedDayException WHERE RestaurantId = @RestaurantId ORDER BY ExceptionDateBegin";
            using var connection = new SqlConnection(_config.ConnectionString);
            return await connection.QueryAsync<ClosedDayExceptionOut>(query, new { RestaurantId = restaurantId });
        }

        public async Task<ClosedDayExceptionOut> InsertAsync(ClosedDayExceptionIn input)
        {
            var query = @"
                DECLARE @OutputTable TABLE (
                    Id BIGINT,
                    RestaurantId BIGINT,
                    ExceptionDateBegin DATETIME,
                    ExceptionDateEnd DATETIME,
                    Reason NVARCHAR(MAX),
                    CreatedAt DATETIME
                );

                INSERT INTO ClosedDayException (RestaurantId, ExceptionDateBegin, ExceptionDateEnd, Reason)
                OUTPUT INSERTED.Id, INSERTED.RestaurantId, INSERTED.ExceptionDateBegin, INSERTED.ExceptionDateEnd, 
                       INSERTED.Reason, INSERTED.CreatedAt
                INTO @OutputTable
                VALUES (@RestaurantId, @ExceptionDateBegin, @ExceptionDateEnd, @Reason);

                SELECT * FROM @OutputTable;
            ";

            using var connection = new SqlConnection(_config.ConnectionString);
            return await connection.QuerySingleAsync<ClosedDayExceptionOut>(query, input);
        }


        public async Task<ClosedDayExceptionOut?> UpdateAsync(long id, ClosedDayExceptionIn input)
        {
            var query = @"
                DECLARE @OutputTable TABLE (
                    Id BIGINT,
                    RestaurantId BIGINT,
                    ExceptionDateBegin DATETIME,
                    ExceptionDateEnd DATETIME,
                    Reason NVARCHAR(MAX),
                    CreatedAt DATETIME
                );

                UPDATE ClosedDayException
                SET RestaurantId = @RestaurantId,
                    ExceptionDateBegin = @ExceptionDateBegin,
                    ExceptionDateEnd = @ExceptionDateEnd,
                    Reason = @Reason
                OUTPUT INSERTED.Id, INSERTED.RestaurantId, INSERTED.ExceptionDateBegin, INSERTED.ExceptionDateEnd, 
                       INSERTED.Reason, INSERTED.CreatedAt
                INTO @OutputTable
                WHERE Id = @Id;

                SELECT * FROM @OutputTable;
            ";

            using var connection = new SqlConnection(_config.ConnectionString);
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
            var query = "DELETE FROM ClosedDayException WHERE Id = @Id";
            using var connection = new SqlConnection(_config.ConnectionString);
            var affectedRows = await connection.ExecuteAsync(query, new { Id = id });
            return affectedRows > 0;
        }
    }
}

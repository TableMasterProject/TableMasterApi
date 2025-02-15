using Dapper;
using Microsoft.Data.SqlClient;
using TableMasterApi.Model;

namespace TableMasterApi.DAL
{
    public class TableDAL
    {
        private readonly ConfigPerso _config;

        public TableDAL(ConfigPerso config)
        {
            _config = config;
        }
        public async Task<TableEntityOut?> GetTablesById(long TableId)
        {
            var query = @"
                  SELECT [Id]
                      ,[RestaurantId]
                      ,[TableNumber]
                      ,[NumberOfSeats]
                      ,[CreatedAt]
                  FROM [TableMaster].[dbo].[TableEntity]
                  where [Id] = @Id
                  ORDER BY [TableNumber] ASC;";
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<TableEntityOut>(query,
                new
                {
                    Id = TableId,
                });
                return result;
            }
        }
        public async Task<IEnumerable<TableEntityOut>> GetTablesByRestaurantAsync(long restaurantId)
        {
            var query = @"
                  SELECT [Id]
                      ,[RestaurantId]
                      ,[TableNumber]
                      ,[NumberOfSeats]
                      ,[CreatedAt]
                  FROM [TableMaster].[dbo].[TableEntity]
                  where [RestaurantId] = @RestaurantId
                  ORDER BY [TableNumber] ASC;";
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                var result = await connection.QueryAsync<TableEntityOut>(query, 
                new {
                    RestaurantId = restaurantId,
                });
                return result;
            }
        }
        public async Task<TableEntityOut?> CreateTable(TableEntityIn table)
        {
            var query = @"
                    INSERT INTO [TableMaster].[dbo].[TableEntity] ([RestaurantId], [TableNumber], [NumberOfSeats])
                    OUTPUT 
                        INSERTED.Id, 
                        INSERTED.[RestaurantId],
                        INSERTED.[TableNumber],
                        INSERTED.RestaurantId,
                        INSERTED.[NumberOfSeats],
                        INSERTED.CreatedAt
                    VALUES (@RestaurantId, @TableNumber, @NumberOfSeats);";
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<TableEntityOut>(query,table);
                return result;
            }
        }
        public async Task<TableEntityOut?> UpdateTable(long tableId, TableEntityIn table)
        {
            var query = @"
                UPDATE [TableMaster].[dbo].[TableEntity]
                SET 
                    [TableNumber] = @TableNumber,
                    [NumberOfSeats] = @NumberOfSeats
                OUTPUT 
                    INSERTED.Id, 
                    INSERTED.[RestaurantId],
                    INSERTED.[TableNumber],
                    INSERTED.[NumberOfSeats],
                    INSERTED.CreatedAt
                WHERE Id = @TableId;";

            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                var parameters = new
                {
                    TableId = tableId,
                    table.TableNumber,
                    table.NumberOfSeats
                };

                var result = await connection.QuerySingleOrDefaultAsync<TableEntityOut>(query, parameters);
                return result;
            }
        }

        public async Task<bool> DeleteTable(long tableId)
        {
            var query = @"DELETE FROM [TableMaster].[dbo].[TableEntity] WHERE Id = @TableId;";

            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                var affectedRows = await connection.ExecuteAsync(query, new { TableId = tableId });
                return affectedRows > 0;
            }
        }


    }
}

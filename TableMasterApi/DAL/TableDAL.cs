using Dapper;
using Npgsql;
using TableMasterApi.Model;
using TableMasterApi.DAL.Interfaces;

namespace TableMasterApi.DAL
{
    /// <summary>
    /// Data Access Layer pour la gestion des tables
    /// </summary>
    public class TableDAL : ITableDAL
    {
        private readonly ConfigPerso _config;

        public TableDAL(ConfigPerso config)
        {
            _config = config;
        }
        public async Task<TableEntityOut?> GetTablesById(long TableId)
        {
            var query = @"
                  SELECT ""Id"",
                         ""RestaurantId"",
                         ""TableNumber"",
                         ""NumberOfSeats"",
                         ""CreatedAt""
                  FROM ""TableEntity""
                  WHERE ""Id"" = @Id
                  ORDER BY ""TableNumber"" ASC;";
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
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
                  SELECT ""Id"",
                         ""RestaurantId"",
                         ""TableNumber"",
                         ""NumberOfSeats"",
                         ""CreatedAt""
                  FROM ""TableEntity""
                  WHERE ""RestaurantId"" = @RestaurantId
                  ORDER BY ""TableNumber"" ASC;";
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
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
                    INSERT INTO ""TableEntity"" (""RestaurantId"", ""TableNumber"", ""NumberOfSeats"")
                    VALUES (@RestaurantId, @TableNumber, @NumberOfSeats)
                    RETURNING ""Id"", ""RestaurantId"", ""TableNumber"", ""NumberOfSeats"", ""CreatedAt"";";
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<TableEntityOut>(query,table);
                return result;
            }
        }
        public async Task<TableEntityOut?> UpdateTable(long tableId, TableEntityIn table)
        {
            var query = @"
                UPDATE ""TableEntity""
                SET 
                    ""TableNumber"" = @TableNumber,
                    ""NumberOfSeats"" = @NumberOfSeats
                WHERE ""Id"" = @TableId
                RETURNING ""Id"", ""RestaurantId"", ""TableNumber"", ""NumberOfSeats"", ""CreatedAt"";";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
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
            var query = @"DELETE FROM ""TableEntity"" WHERE ""Id"" = @TableId;";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                var affectedRows = await connection.ExecuteAsync(query, new { TableId = tableId });
                return affectedRows > 0;
            }
        }


        public async Task<IEnumerable<TableEntityOut>> ReplaceTablesAsync(long restaurantId, IEnumerable<TableEntityIn> tables)
        {
            var deleteQuery = @"DELETE FROM ""TableEntity"" WHERE ""RestaurantId"" = @RestaurantId;";

            var insertQuery = @"
        INSERT INTO ""TableEntity"" (""RestaurantId"", ""TableNumber"", ""NumberOfSeats"")
        VALUES (@RestaurantId, @TableNumber, @NumberOfSeats)
        RETURNING ""Id"", ""RestaurantId"", ""TableNumber"", ""NumberOfSeats"", ""CreatedAt"";";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Supprimer les anciennes tables
                        await connection.ExecuteAsync(deleteQuery, new { RestaurantId = restaurantId }, transaction);

                        // 2. Insérer les nouvelles tables
                        var createdTables = new List<TableEntityOut>();
                        foreach (var table in tables)
                        {
                            // On force l'ID du restaurant pour chaque table de la liste
                            var result = await connection.QuerySingleAsync<TableEntityOut>(insertQuery, new
                            {
                                RestaurantId = restaurantId,
                                table.TableNumber,
                                table.NumberOfSeats
                            }, transaction);

                            createdTables.Add(result);
                        }

                        // 3. Valider l'opération
                        transaction.Commit();
                        return createdTables;
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }


    }
}

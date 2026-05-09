using Dapper;
using Npgsql;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Model;

namespace TableMasterApi.DAL
{
    public class TableDAL : ITableDAL
    {
        private readonly ConfigPerso _config;

        public TableDAL(ConfigPerso config)
        {
            _config = config;
        }

        public async Task<TableEntityOut?> GetTablesById(long tableId)
        {
            var query = TableSelect + @"
                  WHERE ""Id"" = @Id
                  ORDER BY ""TableNumber"" ASC;";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                return await connection.QuerySingleOrDefaultAsync<TableEntityOut>(query, new { Id = tableId });
            }
        }

        public async Task<IEnumerable<TableEntityOut>> GetTablesByRestaurantAsync(long restaurantId)
        {
            var query = TableSelect + @"
                  WHERE ""RestaurantId"" = @RestaurantId
                  ORDER BY ""RoomId"" NULLS LAST, ""TableNumber"" ASC;";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                return await connection.QueryAsync<TableEntityOut>(query, new { RestaurantId = restaurantId });
            }
        }

        public async Task<TableEntityOut?> CreateTable(TableEntityIn table)
        {
            var query = @"
                    INSERT INTO ""TableEntity""
                        (""RestaurantId"", ""RoomId"", ""TableNumber"", ""NumberOfSeats"", ""Shape"",
                         ""PositionX"", ""PositionY"", ""Width"", ""Height"", ""RotationDegrees"")
                    VALUES
                        (@RestaurantId, @RoomId, @TableNumber, @NumberOfSeats, @Shape,
                         @PositionX, @PositionY, @Width, @Height, @RotationDegrees)
                    RETURNING ""Id"", ""RestaurantId"", ""RoomId"", ""TableNumber"", ""NumberOfSeats"", ""Shape"",
                              ""PositionX"", ""PositionY"", ""Width"", ""Height"", ""RotationDegrees"", ""CreatedAt"";";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                return await connection.QuerySingleOrDefaultAsync<TableEntityOut>(query, ToParameters(table));
            }
        }

        public async Task<TableEntityOut?> UpdateTable(long tableId, TableEntityIn table)
        {
            var query = @"
                UPDATE ""TableEntity""
                SET
                    ""RoomId"" = @RoomId,
                    ""TableNumber"" = @TableNumber,
                    ""NumberOfSeats"" = @NumberOfSeats,
                    ""Shape"" = @Shape,
                    ""PositionX"" = @PositionX,
                    ""PositionY"" = @PositionY,
                    ""Width"" = @Width,
                    ""Height"" = @Height,
                    ""RotationDegrees"" = @RotationDegrees
                WHERE ""Id"" = @TableId
                RETURNING ""Id"", ""RestaurantId"", ""RoomId"", ""TableNumber"", ""NumberOfSeats"", ""Shape"",
                          ""PositionX"", ""PositionY"", ""Width"", ""Height"", ""RotationDegrees"", ""CreatedAt"";";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                var parameters = ToParameters(table, tableId);
                return await connection.QuerySingleOrDefaultAsync<TableEntityOut>(query, parameters);
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
                INSERT INTO ""TableEntity""
                    (""RestaurantId"", ""RoomId"", ""TableNumber"", ""NumberOfSeats"", ""Shape"",
                     ""PositionX"", ""PositionY"", ""Width"", ""Height"", ""RotationDegrees"")
                VALUES
                    (@RestaurantId, @RoomId, @TableNumber, @NumberOfSeats, @Shape,
                     @PositionX, @PositionY, @Width, @Height, @RotationDegrees)
                RETURNING ""Id"", ""RestaurantId"", ""RoomId"", ""TableNumber"", ""NumberOfSeats"", ""Shape"",
                          ""PositionX"", ""PositionY"", ""Width"", ""Height"", ""RotationDegrees"", ""CreatedAt"";";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        await connection.ExecuteAsync(deleteQuery, new { RestaurantId = restaurantId }, transaction);

                        var createdTables = new List<TableEntityOut>();
                        foreach (var table in tables)
                        {
                            table.RestaurantId = restaurantId;
                            var result = await connection.QuerySingleAsync<TableEntityOut>(
                                insertQuery,
                                ToParameters(table),
                                transaction);
                            createdTables.Add(result);
                        }

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

        private static object ToParameters(TableEntityIn table, long? tableId = null) => new
        {
            TableId = tableId,
            table.RestaurantId,
            table.RoomId,
            table.TableNumber,
            table.NumberOfSeats,
            Shape = (short)table.Shape,
            table.PositionX,
            table.PositionY,
            table.Width,
            table.Height,
            table.RotationDegrees
        };

        private const string TableSelect = @"
                  SELECT ""Id"",
                         ""RestaurantId"",
                         ""RoomId"",
                         ""TableNumber"",
                         ""NumberOfSeats"",
                         ""Shape"",
                         ""PositionX"",
                         ""PositionY"",
                         ""Width"",
                         ""Height"",
                         ""RotationDegrees"",
                         ""CreatedAt""
                  FROM ""TableEntity""";
    }
}

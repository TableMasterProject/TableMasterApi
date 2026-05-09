using System.Text.Json;
using Dapper;
using Npgsql;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Model;

namespace TableMasterApi.DAL
{
    public class RoomDAL : IRoomDAL
    {
        private readonly ConfigPerso _config;

        public RoomDAL(ConfigPerso config)
        {
            _config = config;
        }

        public async Task<IEnumerable<RestaurantRoomOut>> GetRoomsByRestaurantAsync(long restaurantId)
        {
            var query = RoomSelect + @"
                WHERE ""RestaurantId"" = @RestaurantId
                ORDER BY ""SortOrder"" ASC, ""Id"" ASC;";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                return await connection.QueryAsync<RestaurantRoomOut>(query, new { RestaurantId = restaurantId });
            }
        }

        public async Task<RestaurantRoomOut?> GetRoomByIdAsync(long roomId)
        {
            var query = RoomSelect + @" WHERE ""Id"" = @Id;";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                return await connection.QuerySingleOrDefaultAsync<RestaurantRoomOut>(query, new { Id = roomId });
            }
        }

        public async Task<RestaurantRoomOut?> CreateRoomAsync(RestaurantRoomIn room)
        {
            var query = @"
                INSERT INTO ""RestaurantRoom"" (""RestaurantId"", ""Name"", ""SortOrder"", ""BoundaryPointsJson"")
                VALUES (@RestaurantId, @Name, @SortOrder, CAST(@BoundaryPointsJson AS jsonb))
                RETURNING ""Id"", ""RestaurantId"", ""Name"", ""SortOrder"",
                          ""BoundaryPointsJson""::text AS ""BoundaryPointsJson"", ""CreatedAt"";";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                return await connection.QuerySingleOrDefaultAsync<RestaurantRoomOut>(query, ToRoomParameters(room));
            }
        }

        public async Task<RestaurantRoomOut?> UpdateRoomAsync(long roomId, RestaurantRoomIn room)
        {
            var query = @"
                UPDATE ""RestaurantRoom""
                SET ""Name"" = @Name,
                    ""SortOrder"" = @SortOrder,
                    ""BoundaryPointsJson"" = CAST(@BoundaryPointsJson AS jsonb)
                WHERE ""Id"" = @Id
                RETURNING ""Id"", ""RestaurantId"", ""Name"", ""SortOrder"",
                          ""BoundaryPointsJson""::text AS ""BoundaryPointsJson"", ""CreatedAt"";";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                return await connection.QuerySingleOrDefaultAsync<RestaurantRoomOut>(
                    query,
                    ToRoomParameters(room, roomId));
            }
        }

        public async Task<bool> DeleteRoomAsync(long roomId)
        {
            var query = @"DELETE FROM ""RestaurantRoom"" WHERE ""Id"" = @Id;";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                var affectedRows = await connection.ExecuteAsync(query, new { Id = roomId });
                return affectedRows > 0;
            }
        }

        public async Task<bool> RoomHasTablesAsync(long roomId)
        {
            var query = @"SELECT EXISTS(SELECT 1 FROM ""TableEntity"" WHERE ""RoomId"" = @Id);";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                return await connection.QuerySingleAsync<bool>(query, new { Id = roomId });
            }
        }

        public async Task<RestaurantRoomLayoutOut?> SaveLayoutAsync(long roomId, RestaurantRoomLayoutIn layout)
        {
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    var room = await connection.QuerySingleOrDefaultAsync<RestaurantRoomOut>(
                        RoomSelect + @" WHERE ""Id"" = @Id;",
                        new { Id = roomId },
                        transaction);

                    if (room == null)
                    {
                        transaction.Rollback();
                        return null;
                    }

                    layout.Room.RestaurantId = room.RestaurantId;
                    var updatedRoom = await connection.QuerySingleAsync<RestaurantRoomOut>(
                        @"
                        UPDATE ""RestaurantRoom""
                        SET ""Name"" = @Name,
                            ""SortOrder"" = @SortOrder,
                            ""BoundaryPointsJson"" = CAST(@BoundaryPointsJson AS jsonb)
                        WHERE ""Id"" = @Id
                        RETURNING ""Id"", ""RestaurantId"", ""Name"", ""SortOrder"",
                                  ""BoundaryPointsJson""::text AS ""BoundaryPointsJson"", ""CreatedAt"";",
                        ToRoomParameters(layout.Room, roomId),
                        transaction);

                    if (layout.TableIdsToDelete.Count > 0)
                    {
                        await connection.ExecuteAsync(
                            @"DELETE FROM ""TableEntity"" WHERE ""RoomId"" = @RoomId AND ""Id"" = ANY(@Ids);",
                            new { RoomId = roomId, Ids = layout.TableIdsToDelete.ToArray() },
                            transaction);
                    }

                    foreach (var table in layout.TablesToUpdate)
                    {
                        table.RestaurantId = room.RestaurantId;
                        table.RoomId = roomId;

                        await connection.ExecuteAsync(
                            @"
                            UPDATE ""TableEntity""
                            SET ""TableNumber"" = @TableNumber,
                                ""NumberOfSeats"" = @NumberOfSeats,
                                ""Shape"" = @Shape,
                                ""PositionX"" = @PositionX,
                                ""PositionY"" = @PositionY,
                                ""Width"" = @Width,
                                ""Height"" = @Height,
                                ""RotationDegrees"" = @RotationDegrees
                            WHERE ""Id"" = @Id AND ""RoomId"" = @RoomId;",
                            ToTableParameters(table, table.Id),
                            transaction);
                    }

                    foreach (var table in layout.TablesToAdd)
                    {
                        table.RestaurantId = room.RestaurantId;
                        table.RoomId = roomId;

                        await connection.ExecuteAsync(
                            @"
                            INSERT INTO ""TableEntity""
                                (""RestaurantId"", ""RoomId"", ""TableNumber"", ""NumberOfSeats"", ""Shape"",
                                 ""PositionX"", ""PositionY"", ""Width"", ""Height"", ""RotationDegrees"")
                            VALUES
                                (@RestaurantId, @RoomId, @TableNumber, @NumberOfSeats, @Shape,
                                 @PositionX, @PositionY, @Width, @Height, @RotationDegrees);",
                            ToTableParameters(table),
                            transaction);
                    }

                    var tables = await connection.QueryAsync<TableEntityOut>(
                        TableSelect + @" WHERE ""RoomId"" = @RoomId ORDER BY ""TableNumber"" ASC;",
                        new { RoomId = roomId },
                        transaction);

                    transaction.Commit();

                    return new RestaurantRoomLayoutOut
                    {
                        Room = updatedRoom,
                        Tables = tables
                    };
                }
            }
        }

        private static object ToRoomParameters(RestaurantRoomIn room, long? id = null) => new
        {
            Id = id,
            room.RestaurantId,
            room.Name,
            room.SortOrder,
            BoundaryPointsJson = JsonSerializer.Serialize(room.BoundaryPoints)
        };

        private static object ToTableParameters(TableEntityIn table, long? id = null) => new
        {
            Id = id,
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

        private const string RoomSelect = @"
            SELECT ""Id"",
                   ""RestaurantId"",
                   ""Name"",
                   ""SortOrder"",
                   ""BoundaryPointsJson""::text AS ""BoundaryPointsJson"",
                   ""CreatedAt""
            FROM ""RestaurantRoom""";

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

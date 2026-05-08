using Dapper;
using Npgsql;
using TableMasterApi.Model;
using TableMasterApi.DAL.Interfaces;

namespace TableMasterApi.DAL
{
    /// <summary>
    /// Data Access Layer pour la gestion des menus
    /// </summary>
    public class MenuDAL : IMenuDAL
    {
        private readonly ConfigPerso _config;

        public MenuDAL(ConfigPerso config)
        {
            _config = config;
        }

        public async Task<MenuOut?> GetByIdAsync(long id)
        {
            var query = @"SELECT * FROM ""Menu"" WHERE ""Id"" = @Id";
            using var connection = new NpgsqlConnection(_config.ConnectionString);
            return await connection.QueryFirstOrDefaultAsync<MenuOut>(query, new { Id = id });
        }

        public async Task<IEnumerable<MenuOut>> GetByRestaurantAsync(long restaurantId)
        {
            var query = @"SELECT * FROM ""Menu"" WHERE ""RestaurantId"" = @RestaurantId ORDER BY ""Category"", ""ItemName""";
            using var connection = new NpgsqlConnection(_config.ConnectionString);
            return await connection.QueryAsync<MenuOut>(query, new { RestaurantId = restaurantId });
        }

        public async Task<MenuOut> InsertAsync(MenuIn input)
        {
            var query = @"
                INSERT INTO ""Menu"" (""RestaurantId"", ""Category"", ""ItemName"", ""Description"", ""Price"")
                VALUES (@RestaurantId, @Category, @ItemName, @Description, @Price)
                RETURNING ""Id"", ""RestaurantId"", ""Category"", ""ItemName"", ""Description"", ""Price"", ""CreatedAt"";";

            using var connection = new NpgsqlConnection(_config.ConnectionString);
            return await connection.QuerySingleAsync<MenuOut>(query, input);
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var query = @"DELETE FROM ""Menu"" WHERE ""Id"" = @Id";
            using var connection = new NpgsqlConnection(_config.ConnectionString);
            var affectedRows = await connection.ExecuteAsync(query, new { Id = id });
            return affectedRows > 0;
        }
    }
}

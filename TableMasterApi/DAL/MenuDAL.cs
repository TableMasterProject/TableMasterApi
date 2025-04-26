using Dapper;
using Microsoft.Data.SqlClient;
using TableMasterApi.Model;

namespace TableMasterApi.DAL
{
    public class MenuDAL
    {
        private readonly ConfigPerso _config;

        public MenuDAL(ConfigPerso config)
        {
            _config = config;
        }

        public async Task<MenuOut?> GetByIdAsync(long id)
        {
            var query = "SELECT * FROM Menu WHERE Id = @Id";
            using var connection = new SqlConnection(_config.ConnectionString);
            return await connection.QueryFirstOrDefaultAsync<MenuOut>(query, new { Id = id });
        }

        public async Task<IEnumerable<MenuOut>> GetByRestaurantAsync(long restaurantId)
        {
            var query = "SELECT * FROM Menu WHERE RestaurantId = @RestaurantId ORDER BY Category, ItemName";
            using var connection = new SqlConnection(_config.ConnectionString);
            return await connection.QueryAsync<MenuOut>(query, new { RestaurantId = restaurantId });
        }

        public async Task<MenuOut> InsertAsync(MenuIn input)
        {
            var query = @"
                INSERT INTO Menu (RestaurantId, Category, ItemName, Description, Price)
                OUTPUT INSERTED.Id, INSERTED.RestaurantId, INSERTED.Category, 
                       INSERTED.ItemName, INSERTED.Description, INSERTED.Price, INSERTED.CreatedAt
                VALUES (@RestaurantId, @Category, @ItemName, @Description, @Price);";

            using var connection = new SqlConnection(_config.ConnectionString);
            return await connection.QuerySingleAsync<MenuOut>(query, input);
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var query = "DELETE FROM Menu WHERE Id = @Id";
            using var connection = new SqlConnection(_config.ConnectionString);
            var affectedRows = await connection.ExecuteAsync(query, new { Id = id });
            return affectedRows > 0;
        }
    }
}

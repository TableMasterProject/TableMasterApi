using Dapper;
using Npgsql;
using TableMasterApi.Model;
using TableMasterApi.Service;
using TableMasterApi.DAL.Interfaces;

namespace TableMasterApi.DAL
{
    /// <summary>
    /// Data Access Layer pour la gestion des avis
    /// </summary>
    public class ReviewDAL : IReviewDAL
    {
        private readonly ConfigPerso _config;
        public ReviewDAL(ConfigPerso config)
        {
            _config = config;
        }

        public async Task<IEnumerable<ReviewOut>> GetByIdRestaurant(long idRestaurant, SearchReviews searchReviews)
        {
            var query = @"SELECT * FROM ""Review"" WHERE ""RestaurantId"" = @RestaurantId
                            ORDER BY ""Id""
                            LIMIT @PageSize OFFSET @Offset;";
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                IEnumerable<ReviewOut> ennum = await connection.QueryAsync<ReviewOut>(query, new
                {
                    RestaurantId = idRestaurant,
                    Offset = searchReviews.Offset ?? 0,
                    PageSize = searchReviews.PageSize ?? 20
                });

                return ennum;
            }
        }
        public async Task<IEnumerable<ReviewOut>> GetMy(long idUser, SearchReviews searchReviews)
        {
            var query = @"SELECT * FROM ""Review"" WHERE ""UserId"" = @UserId
                            ORDER BY ""Id""
                            LIMIT @PageSize OFFSET @Offset;";
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                IEnumerable<ReviewOut> ennum = await connection.QueryAsync<ReviewOut>(query, new
                {
                    UserId = idUser,
                    Offset = searchReviews.Offset ?? 0,
                    PageSize = searchReviews.PageSize ?? 20
                });

                return ennum;
            }
        }

        public async Task<ReviewOut> Add(long idUser, ReviewIn review)
        {
            var query = @"INSERT INTO ""Review"" (""UserId"", ""RestaurantId"", ""Rating"", ""Comment"")
                  VALUES (@UserId, @RestaurantId, @Rating, @Comment)
                  RETURNING ""Id"", ""UserId"", ""RestaurantId"", ""Rating"", ""Comment"", ""CreatedAt"";";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                return await connection.QuerySingleAsync<ReviewOut>(query, new
                {
                    UserId = idUser,
                    review.RestaurantId,
                    review.Rating,
                    review.Comment
                });
            }
        }

        public async Task<ReviewOut?> Update(long idUser, long reviewId, ReviewIn review)
        {
            var query = @"UPDATE ""Review"" 
                  SET ""Rating"" = @Rating, ""Comment"" = @Comment 
                  WHERE ""Id"" = @ReviewId AND ""UserId"" = @UserId
                  RETURNING ""Id"", ""UserId"", ""RestaurantId"", ""Rating"", ""Comment"", ""CreatedAt"";";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                return await connection.QuerySingleOrDefaultAsync<ReviewOut>(query, new
                {
                    ReviewId = reviewId,
                    UserId = idUser,
                    review.Rating,
                    review.Comment
                });
            }
        }

        public async Task<bool> Delete(long idUser, long reviewId)
        {
            var query = @"DELETE FROM ""Review"" 
                  WHERE ""Id"" = @ReviewId AND ""UserId"" = @UserId;";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                var affectedRows = await connection.ExecuteAsync(query, new
                {
                    ReviewId = reviewId,
                    UserId = idUser
                });
                return affectedRows > 0;
            }
        }



    }
}

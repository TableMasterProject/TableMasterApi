using Dapper;
using Microsoft.Data.SqlClient;
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
            var query = @"SELECT * FROM review WHERE RestaurantId = @RestaurantId
                            ORDER BY Id
                            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                IEnumerable<ReviewOut> ennum = await connection.QueryAsync<ReviewOut>(query, new
                {
                    RestaurantId = idRestaurant,
                    searchReviews.Offset,
                    searchReviews.PageSize
                });

                return ennum;
            }
        }
        public async Task<IEnumerable<ReviewOut>> GetMy(long idUser, SearchReviews searchReviews)
        {
            var query = @"SELECT * FROM review WHERE UserId = @UserId
                            ORDER BY Id
                            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                IEnumerable<ReviewOut> ennum = await connection.QueryAsync<ReviewOut>(query, new
                {
                    UserId = idUser,
                    searchReviews.Offset,
                    searchReviews.PageSize
                });

                return ennum;
            }
        }

        public async Task<ReviewOut> Add(long idUser, ReviewIn review)
        {
            var query = @"INSERT INTO Review (UserId, RestaurantId, Rating, Comment)
                  OUTPUT INSERTED.Id, INSERTED.UserId, INSERTED.RestaurantId, INSERTED.Rating, 
                         INSERTED.Comment, INSERTED.CreatedAt
                  VALUES (@UserId, @RestaurantId, @Rating, @Comment);";

            using (var connection = new SqlConnection(_config.ConnectionString))
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
            var query = @"UPDATE Review 
                  SET Rating = @Rating, Comment = @Comment 
                  OUTPUT INSERTED.Id, INSERTED.UserId, INSERTED.RestaurantId, INSERTED.Rating, 
                         INSERTED.Comment, INSERTED.CreatedAt
                  WHERE Id = @ReviewId AND UserId = @UserId;";

            using (var connection = new SqlConnection(_config.ConnectionString))
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
            var query = @"DELETE FROM Review 
                  WHERE Id = @ReviewId AND UserId = @UserId;";

            using (var connection = new SqlConnection(_config.ConnectionString))
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

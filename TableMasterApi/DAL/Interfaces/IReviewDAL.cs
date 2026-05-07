using System.Collections.Generic;
using System.Threading.Tasks;
using TableMasterApi.Model;

namespace TableMasterApi.DAL.Interfaces
{
    /// <summary>
    /// Interface pour la gestion des avis/reviews
    /// </summary>
    public interface IReviewDAL
    {
        Task<IEnumerable<ReviewOut>> GetByIdRestaurant(long idRestaurant, SearchReviews searchReviews);
        Task<IEnumerable<ReviewOut>> GetMy(long idUser, SearchReviews searchReviews);
        Task<ReviewOut> Add(long idUser, ReviewIn review);
        Task<ReviewOut?> Update(long idUser, long reviewId, ReviewIn review);
        Task<bool> Delete(long idUser, long reviewId);
    }
}

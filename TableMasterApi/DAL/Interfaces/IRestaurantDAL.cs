using System.Collections.Generic;
using System.Threading.Tasks;
using TableMasterApi.Model;

namespace TableMasterApi.DAL.Interfaces
{
    /// <summary>
    /// Interface pour la gestion des restaurants
    /// </summary>
    public interface IRestaurantDAL
    {
        Task<IEnumerable<RestaurantOut>> GetRestaurants(SearchRestaurant search);
        Task<RestaurantOut?> GetRestaurantById(long id);
        Task<RestaurantOut?> PostRestaurantAsync(long idUser, RestaurantIn restaurant);
        Task<RestaurantOut?> PutRestaurantAsync(long idUser, long id, RestaurantIn restaurant);
        Task<bool> DeleteRestaurant(long idUser, long id);
    }
}

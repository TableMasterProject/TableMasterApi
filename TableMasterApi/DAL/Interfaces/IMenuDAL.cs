using System.Collections.Generic;
using System.Threading.Tasks;
using TableMasterApi.Model;

namespace TableMasterApi.DAL.Interfaces
{
    /// <summary>
    /// Interface pour la gestion des menus
    /// </summary>
    public interface IMenuDAL
    {
        Task<MenuOut?> GetByIdAsync(long id);
        Task<IEnumerable<MenuOut>> GetByRestaurantAsync(long restaurantId);
        Task<MenuOut> InsertAsync(MenuIn input);
        Task<bool> DeleteAsync(long id);
    }
}

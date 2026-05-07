using System.Collections.Generic;
using System.Threading.Tasks;
using TableMasterApi.Model;

namespace TableMasterApi.DAL.Interfaces
{
    /// <summary>
    /// Interface pour la gestion des activités quotidiennes des restaurants
    /// </summary>
    public interface IDailyActivityDAL
    {
        Task<DailyActivityOut?> GetByIdAsync(long id);
        Task<IEnumerable<DailyActivityOut>> GetByRestaurantAsync(long restaurantId);
        Task<DailyActivityOut> InsertAsync(DailyActivityIn input);
        Task<DailyActivityOut?> UpdateAsync(long id, DailyActivityIn input);
        Task<bool> DeleteAsync(long id);
    }
}

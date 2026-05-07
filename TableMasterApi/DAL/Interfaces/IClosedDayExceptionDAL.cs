using System.Collections.Generic;
using System.Threading.Tasks;
using TableMasterApi.Model;

namespace TableMasterApi.DAL.Interfaces
{
    /// <summary>
    /// Interface pour la gestion des jours de fermeture exceptionnels
    /// </summary>
    public interface IClosedDayExceptionDAL
    {
        Task<ClosedDayExceptionOut?> GetByIdAsync(long id);
        Task<IEnumerable<ClosedDayExceptionOut>> GetByRestaurantAsync(long restaurantId);
        Task<ClosedDayExceptionOut> InsertAsync(ClosedDayExceptionIn input);
        Task<ClosedDayExceptionOut?> UpdateAsync(long id, ClosedDayExceptionIn input);
        Task<bool> DeleteAsync(long id);
    }
}

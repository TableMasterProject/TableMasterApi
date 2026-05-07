using System.Collections.Generic;
using System.Threading.Tasks;

namespace TableMasterApi.DAL.Interfaces
{
    /// <summary>
    /// Interface pour la gestion des tokens d'appareils (FCM/notifications)
    /// </summary>
    public interface IDeviceTokenDAL
    {
        Task<bool> SaveDeviceTokenAsync(long userId, string deviceToken, string devicePlatform);
        Task<IEnumerable<string>> GetDeviceTokensForUserIdsAsync(IEnumerable<long> userIds);
        Task<bool> DeleteDeviceTokenAsync(long userId, string deviceToken);
    }
}

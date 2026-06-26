using System.Collections.Generic;
using System.Threading.Tasks;
using TableMasterApi.Model;

namespace TableMasterApi.DAL.Interfaces
{
    /// <summary>
    /// Interface pour la gestion des utilisateurs
    /// </summary>
    public interface IUserDAL
    {
        Task<UserDb?> GetUserById(long id);
        Task<UserDb?> GetUserByEmail(string email);
        Task<UserOut> AddUser(UserIn user);
        Task<UserOut> PutUser(long idUser, UserIn user);
        Task<bool> PutPassword(long userId, string newPassword);
        Task<bool> DeletePassword(long id);
        Task<bool> SaveRefreshToken(long userId, string hashedToken, DateTime expiry);
        Task<long?> GetUserIdByRefreshToken(string hashedToken);
        Task<bool> DeleteRefreshToken(string hashedToken);
        Task<bool> DeleteAllRefreshTokensForUser(long userId);
        Task<bool> SavePasswordResetToken(long userId, string hashedToken, DateTime expiry);
        Task<long?> GetUserIdByPasswordResetToken(string hashedToken);
        Task<bool> DeletePasswordResetTokensForUser(long userId);
        Task<bool> DeleteExpiredPasswordResetTokens();
    }
}

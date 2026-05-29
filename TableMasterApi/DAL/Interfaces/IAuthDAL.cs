using System.Threading.Tasks;

namespace TableMasterApi.DAL.Interfaces
{
    /// <summary>
    /// Interface pour l'authentification et la vérification des mots de passe
    /// </summary>
    public interface IAuthDAL
    {
        bool VerifyPassword(string? hashedPassword, string plainPassword);
    }
}

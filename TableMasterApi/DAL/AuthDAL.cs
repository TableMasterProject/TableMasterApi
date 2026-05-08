using Microsoft.AspNetCore.Identity;
using TableMasterApi.Model;
using TableMasterApi.DAL.Interfaces;

namespace TableMasterApi.DAL
{
    /// <summary>
    /// Data Access Layer pour l'authentification
    /// </summary>
    public class AuthDAL : IAuthDAL
    {
        public bool VerifyPassword(string hashedPassword, string plainPassword)
        {
            var passwordHasher = new PasswordHasher<UserIn>();

            // Vérifier si le mot de passe en clair correspond au mot de passe haché
            var result = passwordHasher.VerifyHashedPassword(null!, hashedPassword, plainPassword);

            // Vérifier le résultat
            return result == PasswordVerificationResult.Success;
        }
    }
}

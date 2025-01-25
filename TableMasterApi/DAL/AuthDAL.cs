using Microsoft.AspNetCore.Identity;
using TableMasterApi.Model;

namespace TableMasterApi.DAL
{
    public class AuthDAL
    {
        public bool VerifyPassword(string hashedPassword, string plainPassword)
        {
            var passwordHasher = new PasswordHasher<UserIn>();

            // Vérifier si le mot de passe en clair correspond au mot de passe haché
            var result = passwordHasher.VerifyHashedPassword(null, hashedPassword, plainPassword);

            // Vérifier le résultat
            return result == PasswordVerificationResult.Success;
        }
    }
}

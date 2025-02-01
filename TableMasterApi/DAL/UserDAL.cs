using Microsoft.AspNetCore.Mvc;
using System.Data;
using Dapper;                      // Nécessaire pour utiliser Dapper
using System.Data;                  // Pour IDbConnection
using System.Collections.Generic;   // Pour IEnumerable
using System.Threading.Tasks;
using TableMasterApi.Model;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;       // Pour utiliser async et await


namespace TableMasterApi.DAL
{
    public class UserDAL : Controller
    {
        private readonly ConfigPerso _config;

        public UserDAL(ConfigPerso config)
        {
            _config = config;
        }

        // Méthode pour récupérer un utilisateur par son ID
        public async Task<UserOut?> GetUserById(long id)
        {
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = "SELECT Id, Email, Password, FirstName, LastName, AccountType, CreatedAt FROM [User] WHERE Id = @Id";
                IEnumerable<UserOut> IEnumerableuser = await connection.QueryAsync<UserOut>(query, new { Id = id });
                UserOut? user = IEnumerableuser.FirstOrDefault();
                return user;
            }
        }

        public async Task<UserOut?> GetUserByEmail(string email)
        {
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = "SELECT Id, Email, Password, FirstName, LastName, AccountType, CreatedAt FROM [User] WHERE Email = @Email";
                IEnumerable<UserOut> IEnumerableuser = await connection.QueryAsync<UserOut>(query, new { Email = email });
                UserOut? user = IEnumerableuser.FirstOrDefault();
                return user;
            }
        }

        // Ajouter un utilisateur
        public async Task<UserOut> AddUser(UserIn user)
        {
            var passwordHasher = new PasswordHasher<UserIn>();
            var hashedPassword = passwordHasher.HashPassword(user, user.Password);

            // Mettre à jour le mot de passe haché dans l'objet utilisateur
            user.Password = hashedPassword;

            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();

                var query = "INSERT INTO [User] (Email, Password, FirstName, LastName, AccountType) " +
                            "OUTPUT INSERTED.Id, INSERTED.Email, INSERTED.Password, INSERTED.FirstName, " +
                            "INSERTED.LastName, INSERTED.AccountType, INSERTED.CreatedAt " +
                            "VALUES (@Email, @Password, @FirstName, @LastName, @AccountType)";

                var insertedUser = await connection.QuerySingleAsync<UserOut>(query, user);
                return insertedUser;
            }
        }

        public async Task<UserOut> PutUser(long idUser, UserIn user)
        {
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();

                var query = "UPDATE [User] SET Email = @Email, FirstName = @FirstName, " +
                            "LastName = @LastName, AccountType = @AccountType " +
                            "OUTPUT INSERTED.Id, INSERTED.Email, INSERTED.Password, INSERTED.FirstName, " +
                            "INSERTED.LastName, INSERTED.AccountType, INSERTED.CreatedAt " +
                            "WHERE Id = @IdUser";

                // Ajout de l'IdUser pour la mise à jour
                var updatedUser = await connection.QuerySingleAsync<UserOut>(query, new { IdUser = idUser, user.Email, user.FirstName, user.LastName, user.AccountType });
                return updatedUser;
            }
        }

        public async Task<bool> PutPassword(UserOut user, string newPassword)
        {
            var passwordHasher = new PasswordHasher<UserOut>();
            var hashedPassword = passwordHasher.HashPassword(user, newPassword);

            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();

                var query = "UPDATE [User] SET Password = @hashedPassword " +
                            "WHERE Id = @IdUser";

                // Utilise Execute pour une mise à jour
                var rowsAffected = await connection.ExecuteAsync(query, new { IdUser = user.Id, hashedPassword });

                // Si aucune ligne n'a été affectée, la mise à jour n'a pas eu lieu
                return rowsAffected > 0;
            }
        }
        public async Task<bool> DeletePassword(long id)
        {
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();

                var query = "DELETE FROM [User] WHERE Id = @IdUser";

                // Execute la requête de suppression
                var rowsAffected = await connection.ExecuteAsync(query, new { IdUser = id });

                // Si une ligne a été affectée, la suppression a réussi
                return rowsAffected > 0;
            }
        }


    }
}

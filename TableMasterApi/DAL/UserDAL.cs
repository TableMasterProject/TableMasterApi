using Microsoft.AspNetCore.Mvc;
using System.Data;
using Dapper;                      // Nécessaire pour utiliser Dapper
using System.Data;                  // Pour IDbConnection
using System.Collections.Generic;   // Pour IEnumerable
using System.Threading.Tasks;
using TableMasterApi.Model;
using Microsoft.Data.SqlClient;       // Pour utiliser async et await


namespace TableMasterApi.DAL
{
    public class UserDAL : Controller
    {
        private readonly string _connectionString = "Server=127.0.0.1,1433;Database=TableMaster;User Id=sa;Password=Max2003?;Encrypt=False;TrustServerCertificate=False;";

        public UserDAL()
        {
        }

        // Méthode pour récupérer un utilisateur par son ID
        public UserOut? GetUserById(long id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var query = "SELECT Id, Email, Password, FirstName, LastName, AccountType, CreatedAt FROM [User] WHERE Id = @Id";
                var user = connection.Query<UserOut>(query, new { Id = id }).FirstOrDefault();
                return user;
            }
        }

        // Méthode pour récupérer tous les utilisateurs
        public IEnumerable<UserOut> GetAllUsers()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var query = "SELECT Id, Email, Password, FirstName, LastName, AccountType, CreatedAt FROM [User]";
                var users = connection.Query<UserOut>(query).ToList();
                return users;
            }
        }

        // Ajouter un utilisateur
        public UserOut AddUser(UserIn user)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                var query = "INSERT INTO [User] (Email, Password, FirstName, LastName, AccountType) " +
                            "OUTPUT INSERTED.Id, INSERTED.Email, INSERTED.Password, INSERTED.FirstName, " +
                            "INSERTED.LastName, INSERTED.AccountType, INSERTED.CreatedAt " +
                            "VALUES (@Email, @Password, @FirstName, @LastName, @AccountType)";

                var insertedUser = connection.QuerySingle<UserOut>(query, user);
                return insertedUser;
            }
        }
    }
}

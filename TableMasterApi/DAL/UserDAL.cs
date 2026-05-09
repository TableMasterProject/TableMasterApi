using Dapper;
using System.Collections.Generic;
using System.Threading.Tasks;
using TableMasterApi.Model;
using TableMasterApi.DAL.Interfaces;
using Npgsql;
using Microsoft.AspNetCore.Identity;

namespace TableMasterApi.DAL
{
    /// <summary>
    /// Data Access Layer pour la gestion des utilisateurs
    /// </summary>
    public class UserDAL : IUserDAL
    {
        private readonly ConfigPerso _config;

        public UserDAL(ConfigPerso config)
        {
            _config = config;
        }

        // Méthode pour récupérer un utilisateur par son ID
        public async Task<UserDb?> GetUserById(long id)
        {
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = @"
                    SELECT 
                        u.""Id"", u.""Email"", u.""Password"", u.""FirstName"", u.""LastName"", u.""AccountType"", u.""CreatedAt"",
                        r.""Id"" AS ""RestaurantId""
                    FROM ""User"" u
                    LEFT JOIN ""Restaurant"" r ON u.""Id"" = r.""UserId""
                    WHERE u.""Id"" = @Id";
                
                var result = await connection.QueryAsync<UserDb>(query, new { Id = id });
                UserDb? user = result.FirstOrDefault();
                return user;
            }
        }

        public async Task<UserDb?> GetUserByEmail(string email)
        {
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = @"
                    SELECT 
                        u.""Id"", u.""Email"", u.""Password"", u.""FirstName"", u.""LastName"", u.""AccountType"", u.""CreatedAt"",
                        r.""Id"" AS ""RestaurantId""
                    FROM ""User"" u
                    LEFT JOIN ""Restaurant"" r ON u.""Id"" = r.""UserId""
                    WHERE u.""Email"" = @Email";
                
                var result = await connection.QueryAsync<UserDb>(query, new { Email = email });
                UserDb? user = result.FirstOrDefault();
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

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                connection.Open();

                var query = @"
                    INSERT INTO ""User"" (""Email"", ""Password"", ""FirstName"", ""LastName"", ""AccountType"")
                    VALUES (@Email, @Password, @FirstName, @LastName, @AccountType)
                    RETURNING ""Id"", ""Email"", ""FirstName"", ""LastName"", ""AccountType"", ""CreatedAt""";

                var insertedUser = await connection.QuerySingleAsync<UserOut>(query, user);
                return insertedUser;
            }
        }

        public async Task<UserOut> PutUser(long idUser, UserIn user)
        {
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                connection.Open();

                var query = @"
                    UPDATE ""User""
                    SET ""Email"" = @Email,
                        ""FirstName"" = @FirstName,
                        ""LastName"" = @LastName,
                        ""AccountType"" = @AccountType
                    WHERE ""Id"" = @IdUser
                    RETURNING ""Id"", ""Email"", ""FirstName"", ""LastName"", ""AccountType"", ""CreatedAt""";

                // Ajout de l'IdUser pour la mise à jour
                var updatedUser = await connection.QuerySingleAsync<UserOut>(query, new { IdUser = idUser, user.Email, user.FirstName, user.LastName, user.AccountType });
                return updatedUser;
            }
        }

        public async Task<bool> PutPassword(long userId, string newPassword)
        {
            var passwordHasher = new PasswordHasher<UserIn>();
            var hashedPassword = passwordHasher.HashPassword(null!, newPassword);

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                connection.Open();

                var query = @"UPDATE ""User"" SET ""Password"" = @hashedPassword WHERE ""Id"" = @IdUser";

                // Utilise Execute pour une mise à jour
                var rowsAffected = await connection.ExecuteAsync(query, new { IdUser = userId, hashedPassword });

                // Si aucune ligne n'a été affectée, la mise à jour n'a pas eu lieu
                return rowsAffected > 0;
            }
        }
        public async Task<bool> DeletePassword(long id)
        {
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                await connection.OpenAsync();
                using (var transaction = await connection.BeginTransactionAsync())
                {
                    try
                    {
                        var userExists = await connection.QuerySingleAsync<bool>(
                            @"SELECT EXISTS(SELECT 1 FROM ""User"" WHERE ""Id"" = @IdUser);",
                            new { IdUser = id },
                            transaction);

                        if (!userExists)
                        {
                            await transaction.RollbackAsync();
                            return false;
                        }

                        var restaurantIds = (await connection.QueryAsync<long>(
                            @"SELECT ""Id"" FROM ""Restaurant"" WHERE ""UserId"" = @IdUser;",
                            new { IdUser = id },
                            transaction)).ToArray();

                        var tableIds = restaurantIds.Length == 0
                            ? []
                            : (await connection.QueryAsync<long>(
                                @"SELECT ""Id"" FROM ""TableEntity"" WHERE ""RestaurantId"" = ANY(@RestaurantIds);",
                                new { RestaurantIds = restaurantIds },
                                transaction)).ToArray();

                        await connection.ExecuteAsync(
                            @"UPDATE ""Reservation"" SET ""UserId"" = NULL WHERE ""UserId"" = @IdUser;",
                            new { IdUser = id },
                            transaction);

                        if (restaurantIds.Length > 0)
                        {
                            await connection.ExecuteAsync(
                                @"UPDATE ""Reservation"" SET ""RestaurantId"" = NULL WHERE ""RestaurantId"" = ANY(@RestaurantIds);",
                                new { RestaurantIds = restaurantIds },
                                transaction);

                            if (tableIds.Length > 0)
                            {
                                await connection.ExecuteAsync(
                                    @"UPDATE ""Reservation"" SET ""TableId"" = NULL WHERE ""TableId"" = ANY(@TableIds);",
                                    new { TableIds = tableIds },
                                    transaction);
                            }

                            await connection.ExecuteAsync(
                                @"DELETE FROM ""Review"" WHERE ""RestaurantId"" = ANY(@RestaurantIds) OR ""UserId"" = @IdUser;",
                                new { RestaurantIds = restaurantIds, IdUser = id },
                                transaction);

                            await connection.ExecuteAsync(
                                @"DELETE FROM ""Menu"" WHERE ""RestaurantId"" = ANY(@RestaurantIds);",
                                new { RestaurantIds = restaurantIds },
                                transaction);

                            await connection.ExecuteAsync(
                                @"DELETE FROM ""DailyActivity"" WHERE ""RestaurantId"" = ANY(@RestaurantIds);",
                                new { RestaurantIds = restaurantIds },
                                transaction);

                            await connection.ExecuteAsync(
                                @"DELETE FROM ""ClosedDayException"" WHERE ""RestaurantId"" = ANY(@RestaurantIds);",
                                new { RestaurantIds = restaurantIds },
                                transaction);

                            await connection.ExecuteAsync(
                                @"DELETE FROM ""TableEntity"" WHERE ""RestaurantId"" = ANY(@RestaurantIds);",
                                new { RestaurantIds = restaurantIds },
                                transaction);

                            await connection.ExecuteAsync(
                                @"DELETE FROM ""RestaurantRoom"" WHERE ""RestaurantId"" = ANY(@RestaurantIds);",
                                new { RestaurantIds = restaurantIds },
                                transaction);

                            await connection.ExecuteAsync(
                                @"DELETE FROM ""Restaurant"" WHERE ""Id"" = ANY(@RestaurantIds);",
                                new { RestaurantIds = restaurantIds },
                                transaction);
                        }
                        else
                        {
                            await connection.ExecuteAsync(
                                @"DELETE FROM ""Review"" WHERE ""UserId"" = @IdUser;",
                                new { IdUser = id },
                                transaction);
                        }

                        var rowsAffected = await connection.ExecuteAsync(
                            @"DELETE FROM ""User"" WHERE ""Id"" = @IdUser;",
                            new { IdUser = id },
                            transaction);

                        await transaction.CommitAsync();
                        return rowsAffected > 0;
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task<bool> SaveRefreshToken(long userId, string hashedToken, DateTime expiry)
        {
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                connection.Open();

                var query = @"INSERT INTO ""UserRefreshTokens"" (""UserId"", ""TokenHash"", ""ExpiryDate"") 
                     VALUES (@UserId, @TokenHash, @ExpiryDate)";

                // Execute la requête de suppression
                var rowsAffected = await connection.ExecuteAsync(query, new { UserId = userId, TokenHash = hashedToken, ExpiryDate = expiry });

                // Si une ligne a été affectée, la suppression a réussi
                return rowsAffected > 0;
            }
        }

        public async Task<long?> GetUserIdByRefreshToken(string hashedToken)
        {
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                connection.Open();

                string query = @"SELECT ""UserId"" FROM ""UserRefreshTokens"" 
                     WHERE ""TokenHash"" = @TokenHash AND ""ExpiryDate"" > CURRENT_TIMESTAMP";

                // Execute la requête
                IEnumerable<long> IEnumerableuser = await connection.QueryAsync<long>(query, new { TokenHash = hashedToken });
                long? userId = IEnumerableuser.FirstOrDefault();
                return userId;
            }
        }
        
        public async Task<bool> DeleteRefreshToken(string hashedToken)
        {
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                await connection.OpenAsync();

                var query = @"DELETE FROM ""UserRefreshTokens"" WHERE ""TokenHash"" = @TokenHash";

                var rowsAffected = await connection.ExecuteAsync(query, new { TokenHash = hashedToken });

                return rowsAffected > 0;
            }
        }

        public async Task<bool> DeleteAllRefreshTokensForUser(long userId)
        {
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                await connection.OpenAsync();

                var query = @"DELETE FROM ""UserRefreshTokens"" WHERE ""UserId"" = @UserId";

                var rowsAffected = await connection.ExecuteAsync(query, new { UserId = userId });

                return rowsAffected > 0;
            }
        }


    }
}

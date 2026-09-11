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
        private const string UserSelect = @"
            SELECT
                u.""Id"", u.""Email"", u.""Password"", u.""FirstName"", u.""LastName"", u.""AccountType"", u.""CreatedAt"",
                r.""Id"" AS ""RestaurantId""
            FROM ""User"" u
            LEFT JOIN ""Restaurant"" r ON u.""Id"" = r.""UserId""";

        private readonly ConfigPerso _config;

        public UserDAL(ConfigPerso config)
        {
            _config = config;
        }

        // Méthode pour récupérer un utilisateur par son ID
        public async Task<UserDb?> GetUserById(long id)
        {
            await using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                await connection.OpenAsync();
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
            await using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                await connection.OpenAsync();
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

            await using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    INSERT INTO ""User"" (""Email"", ""Password"", ""FirstName"", ""LastName"", ""AccountType"")
                    VALUES (@Email, @Password, @FirstName, @LastName, @AccountType)
                    RETURNING ""Id"", ""Email"", ""FirstName"", ""LastName"", ""AccountType"", ""CreatedAt""";

                var insertedUser = await connection.QuerySingleAsync<UserOut>(query, new
                {
                    user.Email,
                    Password = hashedPassword,
                    user.FirstName,
                    user.LastName,
                    user.AccountType
                });
                return insertedUser;
            }
        }

        public async Task<UserOut> AddUserWithSession(
            UserIn user,
            string hashedRefreshToken,
            DateTime refreshTokenExpiry)
        {
            var passwordHasher = new PasswordHasher<UserIn>();
            var hashedPassword = passwordHasher.HashPassword(user, user.Password);

            await using var connection = new NpgsqlConnection(_config.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var insertedUser = await connection.QuerySingleAsync<UserOut>(
                @"INSERT INTO ""User"" (""Email"", ""Password"", ""FirstName"", ""LastName"", ""AccountType"")
                  VALUES (@Email, @Password, @FirstName, @LastName, @AccountType)
                  RETURNING ""Id"", ""Email"", ""FirstName"", ""LastName"", ""AccountType"", ""CreatedAt""",
                new
                {
                    user.Email,
                    Password = hashedPassword,
                    user.FirstName,
                    user.LastName,
                    user.AccountType
                },
                transaction);

            await connection.ExecuteAsync(
                @"INSERT INTO ""UserRefreshTokens"" (""UserId"", ""TokenHash"", ""ExpiryDate"")
                  VALUES (@UserId, @TokenHash, @ExpiryDate)",
                new
                {
                    UserId = insertedUser.Id,
                    TokenHash = hashedRefreshToken,
                    ExpiryDate = refreshTokenExpiry
                },
                transaction);

            await transaction.CommitAsync();
            return insertedUser;
        }

        public async Task<UserOut> PutUser(long idUser, UserIn user)
        {
            await using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                await connection.OpenAsync();

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

            await using var connection = new NpgsqlConnection(_config.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var lockedUserId = await connection.QuerySingleOrDefaultAsync<long?>(
                @"SELECT ""Id"" FROM ""User"" WHERE ""Id"" = @UserId FOR UPDATE",
                new { UserId = userId },
                transaction);
            if (lockedUserId is null)
            {
                await transaction.RollbackAsync();
                return false;
            }

            await connection.ExecuteAsync(
                @"UPDATE ""User"" SET ""Password"" = @HashedPassword WHERE ""Id"" = @UserId",
                new { UserId = userId, HashedPassword = hashedPassword },
                transaction);
            await RevokeAuthenticationTokens(connection, transaction, userId);
            await transaction.CommitAsync();
            return true;
        }

        public async Task<PasswordChangeResult> ChangePassword(long userId, string oldPassword, string newPassword)
        {
            await using var connection = new NpgsqlConnection(_config.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var storedHash = await connection.QuerySingleOrDefaultAsync<string?>(
                @"SELECT ""Password"" FROM ""User"" WHERE ""Id"" = @UserId FOR UPDATE",
                new { UserId = userId },
                transaction);
            if (storedHash is null)
            {
                await transaction.RollbackAsync();
                return PasswordChangeResult.UserNotFound;
            }

            var passwordHasher = new PasswordHasher<UserIn>();
            if (passwordHasher.VerifyHashedPassword(null!, storedHash, oldPassword) == PasswordVerificationResult.Failed)
            {
                await transaction.RollbackAsync();
                return PasswordChangeResult.InvalidOldPassword;
            }

            var newHash = passwordHasher.HashPassword(null!, newPassword);
            await connection.ExecuteAsync(
                @"UPDATE ""User"" SET ""Password"" = @NewHash WHERE ""Id"" = @UserId",
                new { UserId = userId, NewHash = newHash },
                transaction);
            await RevokeAuthenticationTokens(connection, transaction, userId);
            await transaction.CommitAsync();
            return PasswordChangeResult.Success;
        }
        public async Task<bool> DeletePassword(long id)
        {
            await using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                await connection.OpenAsync();
                using (var transaction = await connection.BeginTransactionAsync())
                {
                    try
                    {
                        var userExists = await connection.QuerySingleOrDefaultAsync<long?>(
                            @"SELECT ""Id"" FROM ""User"" WHERE ""Id"" = @IdUser FOR UPDATE;",
                            new { IdUser = id },
                            transaction);

                        if (userExists is null)
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
            await using var connection = new NpgsqlConnection(_config.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var userExists = await LockUser(connection, transaction, userId);
            if (!userExists)
            {
                await transaction.RollbackAsync();
                return false;
            }

            var rowsAffected = await connection.ExecuteAsync(
                @"INSERT INTO ""UserRefreshTokens"" (""UserId"", ""TokenHash"", ""ExpiryDate"")
                  VALUES (@UserId, @TokenHash, @ExpiryDate)",
                new { UserId = userId, TokenHash = hashedToken, ExpiryDate = expiry },
                transaction);
            await transaction.CommitAsync();
            return rowsAffected > 0;
        }

        public async Task<UserDb?> CreateSession(
            string email,
            string password,
            string hashedRefreshToken,
            DateTime refreshTokenExpiry)
        {
            await using var connection = new NpgsqlConnection(_config.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var user = await connection.QuerySingleOrDefaultAsync<UserDb>(
                UserSelect + @" WHERE u.""Email"" = @Email FOR UPDATE OF u",
                new { Email = email },
                transaction);
            if (user is null)
            {
                await transaction.RollbackAsync();
                return null;
            }

            var passwordHasher = new PasswordHasher<UserIn>();
            if (passwordHasher.VerifyHashedPassword(null!, user.Password, password) == PasswordVerificationResult.Failed)
            {
                await transaction.RollbackAsync();
                return null;
            }

            await connection.ExecuteAsync(
                @"INSERT INTO ""UserRefreshTokens"" (""UserId"", ""TokenHash"", ""ExpiryDate"")
                  VALUES (@UserId, @TokenHash, @ExpiryDate)",
                new
                {
                    UserId = user.Id,
                    TokenHash = hashedRefreshToken,
                    ExpiryDate = refreshTokenExpiry
                },
                transaction);
            await transaction.CommitAsync();
            return user;
        }

        public async Task<UserDb?> RotateRefreshToken(
            string hashedRefreshToken,
            string newHashedRefreshToken,
            DateTime newExpiry)
        {
            await using var connection = new NpgsqlConnection(_config.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var userId = await connection.QuerySingleOrDefaultAsync<long?>(
                @"SELECT ""UserId"" FROM ""UserRefreshTokens""
                  WHERE ""TokenHash"" = @TokenHash
                    AND ""ExpiryDate"" > CURRENT_TIMESTAMP
                  LIMIT 1",
                new { TokenHash = hashedRefreshToken },
                transaction);
            if (userId is null)
            {
                await transaction.RollbackAsync();
                return null;
            }

            var user = await connection.QuerySingleOrDefaultAsync<UserDb>(
                UserSelect + @" WHERE u.""Id"" = @UserId FOR UPDATE OF u",
                new { UserId = userId.Value },
                transaction);
            if (user is null)
            {
                await transaction.RollbackAsync();
                return null;
            }

            var consumedUserId = await connection.QuerySingleOrDefaultAsync<long?>(
                @"DELETE FROM ""UserRefreshTokens""
                  WHERE ""TokenHash"" = @TokenHash
                    AND ""UserId"" = @UserId
                    AND ""ExpiryDate"" > CURRENT_TIMESTAMP
                  RETURNING ""UserId""",
                new { TokenHash = hashedRefreshToken, UserId = user.Id },
                transaction);
            if (consumedUserId is null)
            {
                await transaction.RollbackAsync();
                return null;
            }

            await connection.ExecuteAsync(
                @"INSERT INTO ""UserRefreshTokens"" (""UserId"", ""TokenHash"", ""ExpiryDate"")
                  VALUES (@UserId, @TokenHash, @ExpiryDate)",
                new { UserId = user.Id, TokenHash = newHashedRefreshToken, ExpiryDate = newExpiry },
                transaction);
            await transaction.CommitAsync();
            return user;
        }

        public async Task<long?> GetUserIdByRefreshToken(string hashedToken)
        {
            await using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                await connection.OpenAsync();

                string query = @"SELECT ""UserId"" FROM ""UserRefreshTokens""
                     WHERE ""TokenHash"" = @TokenHash AND ""ExpiryDate"" > CURRENT_TIMESTAMP";

                return await connection.QuerySingleOrDefaultAsync<long?>(query, new { TokenHash = hashedToken });
            }
        }
        
        public async Task<bool> DeleteRefreshToken(string hashedToken)
        {
            await using var connection = new NpgsqlConnection(_config.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var userId = await connection.QuerySingleOrDefaultAsync<long?>(
                @"SELECT ""UserId"" FROM ""UserRefreshTokens"" WHERE ""TokenHash"" = @TokenHash LIMIT 1",
                new { TokenHash = hashedToken },
                transaction);
            if (userId is null || !await LockUser(connection, transaction, userId.Value))
            {
                await transaction.RollbackAsync();
                return false;
            }

            var rowsAffected = await connection.ExecuteAsync(
                @"DELETE FROM ""UserRefreshTokens"" WHERE ""TokenHash"" = @TokenHash",
                new { TokenHash = hashedToken },
                transaction);
            await transaction.CommitAsync();
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteAllRefreshTokensForUser(long userId)
        {
            await using var connection = new NpgsqlConnection(_config.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            if (!await LockUser(connection, transaction, userId))
            {
                await transaction.RollbackAsync();
                return false;
            }

            var rowsAffected = await connection.ExecuteAsync(
                @"DELETE FROM ""UserRefreshTokens"" WHERE ""UserId"" = @UserId",
                new { UserId = userId },
                transaction);
            await transaction.CommitAsync();
            return rowsAffected > 0;
        }

        public async Task<bool> SavePasswordResetToken(long userId, string hashedToken, DateTime expiry)
        {
            await using var connection = new NpgsqlConnection(_config.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            if (!await LockUser(connection, transaction, userId))
            {
                await transaction.RollbackAsync();
                return false;
            }

            var rowsAffected = await connection.ExecuteAsync(
                @"INSERT INTO ""UserPasswordResetTokens"" (""UserId"", ""TokenHash"", ""ExpiryDate"")
                  VALUES (@UserId, @TokenHash, @ExpiryDate)",
                new { UserId = userId, TokenHash = hashedToken, ExpiryDate = expiry },
                transaction);
            await transaction.CommitAsync();
            return rowsAffected > 0;
        }

        public async Task<long?> GetUserIdByPasswordResetToken(string hashedToken)
        {
            await using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                await connection.OpenAsync();

                var query = @"SELECT ""UserId"" FROM ""UserPasswordResetTokens""
                     WHERE ""TokenHash"" = @TokenHash
                       AND ""ExpiryDate"" > CURRENT_TIMESTAMP
                       AND ""UsedAt"" IS NULL";

                return await connection.QuerySingleOrDefaultAsync<long?>(query, new { TokenHash = hashedToken });
            }
        }

        public async Task<bool> ResetPassword(string hashedToken, string newPassword)
        {
            await using var connection = new NpgsqlConnection(_config.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var userId = await connection.QuerySingleOrDefaultAsync<long?>(
                @"SELECT ""UserId"" FROM ""UserPasswordResetTokens""
                  WHERE ""TokenHash"" = @TokenHash
                    AND ""ExpiryDate"" > CURRENT_TIMESTAMP
                    AND ""UsedAt"" IS NULL
                  LIMIT 1",
                new { TokenHash = hashedToken },
                transaction);
            if (userId is null || !await LockUser(connection, transaction, userId.Value))
            {
                await transaction.RollbackAsync();
                return false;
            }

            var consumedUserId = await connection.QuerySingleOrDefaultAsync<long?>(
                @"UPDATE ""UserPasswordResetTokens""
                  SET ""UsedAt"" = CURRENT_TIMESTAMP
                  WHERE ""TokenHash"" = @TokenHash
                    AND ""UserId"" = @UserId
                    AND ""ExpiryDate"" > CURRENT_TIMESTAMP
                    AND ""UsedAt"" IS NULL
                  RETURNING ""UserId""",
                new { TokenHash = hashedToken, UserId = userId.Value },
                transaction);
            if (consumedUserId is null)
            {
                await transaction.RollbackAsync();
                return false;
            }

            var passwordHasher = new PasswordHasher<UserIn>();
            var hashedPassword = passwordHasher.HashPassword(null!, newPassword);
            await connection.ExecuteAsync(
                @"UPDATE ""User"" SET ""Password"" = @HashedPassword WHERE ""Id"" = @UserId",
                new { HashedPassword = hashedPassword, UserId = userId.Value },
                transaction);
            await RevokeAuthenticationTokens(connection, transaction, userId.Value);
            await transaction.CommitAsync();
            return true;
        }

        public async Task<bool> DeletePasswordResetTokensForUser(long userId)
        {
            await using var connection = new NpgsqlConnection(_config.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            if (!await LockUser(connection, transaction, userId))
            {
                await transaction.RollbackAsync();
                return false;
            }

            var rowsAffected = await connection.ExecuteAsync(
                @"DELETE FROM ""UserPasswordResetTokens"" WHERE ""UserId"" = @UserId",
                new { UserId = userId },
                transaction);
            await transaction.CommitAsync();
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteExpiredPasswordResetTokens()
        {
            await using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                await connection.OpenAsync();

                var query = @"DELETE FROM ""UserPasswordResetTokens"" WHERE ""ExpiryDate"" <= CURRENT_TIMESTAMP OR ""UsedAt"" IS NOT NULL";

                var rowsAffected = await connection.ExecuteAsync(query);
                return rowsAffected > 0;
            }
        }

        private static async Task<bool> LockUser(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            long userId)
        {
            var lockedUserId = await connection.QuerySingleOrDefaultAsync<long?>(
                @"SELECT ""Id"" FROM ""User"" WHERE ""Id"" = @UserId FOR UPDATE",
                new { UserId = userId },
                transaction);
            return lockedUserId is not null;
        }

        private static async Task RevokeAuthenticationTokens(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            long userId)
        {
            await connection.ExecuteAsync(
                @"DELETE FROM ""UserRefreshTokens"" WHERE ""UserId"" = @UserId",
                new { UserId = userId },
                transaction);
            await connection.ExecuteAsync(
                @"DELETE FROM ""UserPasswordResetTokens"" WHERE ""UserId"" = @UserId",
                new { UserId = userId },
                transaction);
        }

    }
}

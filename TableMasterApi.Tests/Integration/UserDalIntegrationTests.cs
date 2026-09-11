using Dapper;
using Npgsql;
using TableMasterApi.DAL;
using TableMasterApi.Model;
using Xunit;

namespace TableMasterApi.Tests.Integration
{
    [Collection(PostgresCollection.Name)]
    public class UserDalIntegrationTests
    {
        private readonly PostgresFixture _fx;

        public UserDalIntegrationTests(PostgresFixture fx)
        {
            _fx = fx;
        }

        [SkippableFact]
        public async Task AddUser_PersistsUser_HashesPassword_AndReturnsGeneratedId()
        {
            Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Docker indisponible");
            await _fx.ResetAsync();

            var dal = new UserDAL(_fx.Config);
            var input = new UserIn
            {
                Email = "alice@example.com",
                Password = "Sup3rPa$$word",
                FirstName = "Alice",
                LastName = "Dupont",
                AccountType = 1
            };

            var inserted = await dal.AddUser(input);

            inserted.Id.Should().BeGreaterThan(0);
            inserted.Email.Should().Be("alice@example.com");

            await using var conn = new NpgsqlConnection(_fx.ConnectionString);
            var stored = await conn.QuerySingleAsync<(string Email, string Password)>(
                @"SELECT ""Email"", ""Password"" FROM ""User"" WHERE ""Id"" = @Id",
                new { inserted.Id });

            stored.Email.Should().Be("alice@example.com");
            stored.Password.Should().NotBe("Sup3rPa$$word", "le mot de passe doit etre hashe avant insertion");
            stored.Password.Should().StartWith("AQAAAA", "PasswordHasher v3 produit un hash prefixe par AQAAAA");
        }

        [SkippableFact]
        public async Task GetUserByEmail_ReturnsNull_WhenUnknown()
        {
            Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Docker indisponible");
            await _fx.ResetAsync();

            var dal = new UserDAL(_fx.Config);

            var user = await dal.GetUserByEmail("missing@example.com");

            user.Should().BeNull();
        }

        [SkippableFact]
        public async Task GetUserById_JoinsRestaurantId_WhenUserOwnsOne()
        {
            Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Docker indisponible");
            await _fx.ResetAsync();

            var dal = new UserDAL(_fx.Config);
            var inserted = await dal.AddUser(new UserIn
            {
                Email = "owner@example.com",
                Password = "Sup3rPa$$word",
                FirstName = "Owner",
                LastName = "Pro",
                AccountType = 2
            });

            await using var conn = new NpgsqlConnection(_fx.ConnectionString);
            await conn.OpenAsync();
            var restaurantId = await conn.QuerySingleAsync<long>(
                @"INSERT INTO ""Restaurant"" (""UserId"", ""RestaurantName"")
                  VALUES (@UserId, 'Chez Owner') RETURNING ""Id""",
                new { UserId = inserted.Id });

            var fetched = await dal.GetUserById(inserted.Id);

            fetched.Should().NotBeNull();
            fetched!.RestaurantId.Should().Be(restaurantId);
        }

        [SkippableFact]
        public async Task PutPassword_UpdatesHash_AndAllowsLogin()
        {
            Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Docker indisponible");
            await _fx.ResetAsync();

            var dal = new UserDAL(_fx.Config);
            var auth = new AuthDAL();
            var inserted = await dal.AddUser(new UserIn
            {
                Email = "pwd@example.com",
                Password = "OldPa$$w0rd",
                FirstName = "Pwd",
                LastName = "User",
                AccountType = 1
            });

            var updated = await dal.PutPassword(inserted.Id, "NewPa$$w0rd!");

            updated.Should().BeTrue();
            var reloaded = await dal.GetUserById(inserted.Id);
            reloaded.Should().NotBeNull();
            auth.VerifyPassword(reloaded!.Password, "NewPa$$w0rd!").Should().BeTrue();
            auth.VerifyPassword(reloaded!.Password, "OldPa$$w0rd").Should().BeFalse();
        }

        [SkippableFact]
        public async Task DeletePassword_CascadesCleanup_AndDetachesReservationsFromOtherUsers()
        {
            Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Docker indisponible");
            await _fx.ResetAsync();

            var dal = new UserDAL(_fx.Config);
            await using var conn = new NpgsqlConnection(_fx.ConnectionString);
            await conn.OpenAsync();

            var owner = await dal.AddUser(new UserIn
            {
                Email = "owner@example.com",
                Password = "Sup3rPa$$word",
                FirstName = "Owner",
                LastName = "Pro",
                AccountType = 2
            });
            var customer = await dal.AddUser(new UserIn
            {
                Email = "customer@example.com",
                Password = "Sup3rPa$$word",
                FirstName = "Cust",
                LastName = "Omer",
                AccountType = 1
            });

            var restaurantId = await conn.QuerySingleAsync<long>(
                @"INSERT INTO ""Restaurant"" (""UserId"", ""RestaurantName"") VALUES (@UserId, 'Chez Owner') RETURNING ""Id""",
                new { UserId = owner.Id });
            var tableId = await conn.QuerySingleAsync<long>(
                @"INSERT INTO ""TableEntity"" (""RestaurantId"", ""TableNumber"", ""NumberOfSeats"") VALUES (@R, 1, 4) RETURNING ""Id""",
                new { R = restaurantId });
            var menuId = await conn.QuerySingleAsync<long>(
                @"INSERT INTO ""Menu"" (""RestaurantId"", ""Category"", ""ItemName"", ""Price"") VALUES (@R, 'cat', 'item', 9.5) RETURNING ""Id""",
                new { R = restaurantId });
            var reservationFromCustomerId = await conn.QuerySingleAsync<long>(
                @"INSERT INTO ""Reservation"" (""UserId"", ""TableId"", ""RestaurantId"", ""ReservationDate"", ""NumberOfPeople"")
                  VALUES (@U, @T, @R, @D, 2) RETURNING ""Id""",
                new { U = customer.Id, T = tableId, R = restaurantId, D = DateTime.UtcNow.AddDays(1) });

            var deleted = await dal.DeletePassword(owner.Id);

            deleted.Should().BeTrue();
            (await conn.ExecuteScalarAsync<long>(@"SELECT COUNT(*) FROM ""User"" WHERE ""Id"" = @Id", new { Id = owner.Id }))
                .Should().Be(0);
            (await conn.ExecuteScalarAsync<long>(@"SELECT COUNT(*) FROM ""Restaurant"" WHERE ""Id"" = @Id", new { Id = restaurantId }))
                .Should().Be(0);
            (await conn.ExecuteScalarAsync<long>(@"SELECT COUNT(*) FROM ""Menu"" WHERE ""Id"" = @Id", new { Id = menuId }))
                .Should().Be(0);

            var detached = await conn.QuerySingleAsync<(long? UserId, long? RestaurantId, long? TableId)>(
                @"SELECT ""UserId"", ""RestaurantId"", ""TableId"" FROM ""Reservation"" WHERE ""Id"" = @Id",
                new { Id = reservationFromCustomerId });
            detached.UserId.Should().Be(customer.Id, "la reservation du client doit etre conservee");
            detached.RestaurantId.Should().BeNull("le restaurant supprime doit etre detache");
            detached.TableId.Should().BeNull("la table supprimee doit etre detachee");
        }

        [SkippableFact]
        public async Task DeletePassword_ReturnsFalse_WhenUserDoesNotExist()
        {
            Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Docker indisponible");
            await _fx.ResetAsync();

            var dal = new UserDAL(_fx.Config);

            var deleted = await dal.DeletePassword(999_999);

            deleted.Should().BeFalse();
        }

        [SkippableFact]
        public async Task ResetPassword_ConsumesTokenOnce_AndRevokesAllSessions()
        {
            Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Docker indisponible");
            await _fx.ResetAsync();

            var setupDal = new UserDAL(_fx.Config);
            var user = await setupDal.AddUser(new UserIn
            {
                Email = "reset-once@example.com",
                Password = "OldPa$$w0rd",
                FirstName = "Reset",
                LastName = "Once",
                AccountType = 1
            });
            await setupDal.SaveRefreshToken(user.Id, "session-to-revoke", DateTime.UtcNow.AddDays(7));
            await setupDal.SavePasswordResetToken(user.Id, "one-use-reset", DateTime.UtcNow.AddHours(1));

            var firstReset = new UserDAL(_fx.Config).ResetPassword("one-use-reset", "FirstNewPa$$word");
            var secondReset = new UserDAL(_fx.Config).ResetPassword("one-use-reset", "SecondNewPa$$word");
            var results = await Task.WhenAll(firstReset, secondReset);

            results.Count(result => result).Should().Be(1);
            var stored = await setupDal.GetUserById(user.Id);
            stored.Should().NotBeNull();
            var auth = new AuthDAL();
            new[]
            {
                auth.VerifyPassword(stored!.Password, "FirstNewPa$$word"),
                auth.VerifyPassword(stored.Password, "SecondNewPa$$word")
            }.Count(matches => matches).Should().Be(1);

            await using var connection = new NpgsqlConnection(_fx.ConnectionString);
            (await connection.ExecuteScalarAsync<long>(
                @"SELECT COUNT(*) FROM ""UserRefreshTokens"" WHERE ""UserId"" = @UserId",
                new { UserId = user.Id })).Should().Be(0);
            (await connection.ExecuteScalarAsync<long>(
                @"SELECT COUNT(*) FROM ""UserPasswordResetTokens"" WHERE ""UserId"" = @UserId",
                new { UserId = user.Id })).Should().Be(0);
        }

        [SkippableFact]
        public async Task CreateSession_RacingPasswordChange_CannotPersistSessionForOldPassword()
        {
            Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Docker indisponible");
            await _fx.ResetAsync();

            var setupDal = new UserDAL(_fx.Config);
            var user = await setupDal.AddUser(new UserIn
            {
                Email = "login-race@example.com",
                Password = "OldPa$$w0rd",
                FirstName = "Login",
                LastName = "Race",
                AccountType = 1
            });

            var session = new UserDAL(_fx.Config).CreateSession(
                user.Email, "OldPa$$w0rd", "old-password-session", DateTime.UtcNow.AddDays(7));
            var passwordChange = new UserDAL(_fx.Config).ChangePassword(
                user.Id, "OldPa$$w0rd", "NewPa$$w0rd!");
            await Task.WhenAll(session, passwordChange);

            passwordChange.Result.Should().Be(PasswordChangeResult.Success);
            await using var connection = new NpgsqlConnection(_fx.ConnectionString);
            (await connection.ExecuteScalarAsync<long>(
                @"SELECT COUNT(*) FROM ""UserRefreshTokens"" WHERE ""UserId"" = @UserId",
                new { UserId = user.Id })).Should().Be(0);
        }
    }
}

using Dapper;
using Npgsql;
using TableMasterApi.DAL;
using TableMasterApi.Model;
using Xunit;

namespace TableMasterApi.Tests.Integration
{
    [Collection(PostgresCollection.Name)]
    public class RefreshTokenIntegrationTests
    {
        private readonly PostgresFixture _fx;

        public RefreshTokenIntegrationTests(PostgresFixture fx)
        {
            _fx = fx;
        }

        [SkippableFact]
        public async Task SaveAndLookup_ReturnsUserId_ForActiveToken()
        {
            Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Docker indisponible");
            await _fx.ResetAsync();

            var dal = new UserDAL(_fx.Config);
            var user = await dal.AddUser(new UserIn
            {
                Email = "refresh@example.com",
                Password = "Sup3rPa$$word",
                FirstName = "Re",
                LastName = "Fresh",
                AccountType = 1
            });

            var saved = await dal.SaveRefreshToken(user.Id, "hashed-token", DateTime.UtcNow.AddDays(7));

            saved.Should().BeTrue();
            var lookedUp = await dal.GetUserIdByRefreshToken("hashed-token");
            lookedUp.Should().Be(user.Id);
        }

        [SkippableFact]
        public async Task GetUserIdByRefreshToken_ReturnsNull_ForExpiredToken()
        {
            Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Docker indisponible");
            await _fx.ResetAsync();

            var dal = new UserDAL(_fx.Config);
            var user = await dal.AddUser(new UserIn
            {
                Email = "expired@example.com",
                Password = "Sup3rPa$$word",
                FirstName = "Ex",
                LastName = "Pired",
                AccountType = 1
            });
            await dal.SaveRefreshToken(user.Id, "stale-token", DateTime.UtcNow.AddMinutes(-1));

            var lookedUp = await dal.GetUserIdByRefreshToken("stale-token");

            lookedUp.Should().BeNull("un refresh token expire ne doit jamais authentifier");
        }

        [SkippableFact]
        public async Task DeleteRefreshToken_RemovesOnlyMatchingRow()
        {
            Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Docker indisponible");
            await _fx.ResetAsync();

            var dal = new UserDAL(_fx.Config);
            var user = await dal.AddUser(new UserIn
            {
                Email = "delete@example.com",
                Password = "Sup3rPa$$word",
                FirstName = "De",
                LastName = "Lete",
                AccountType = 1
            });
            await dal.SaveRefreshToken(user.Id, "keep", DateTime.UtcNow.AddDays(7));
            await dal.SaveRefreshToken(user.Id, "drop", DateTime.UtcNow.AddDays(7));

            var deleted = await dal.DeleteRefreshToken("drop");

            deleted.Should().BeTrue();
            (await dal.GetUserIdByRefreshToken("drop")).Should().BeNull();
            (await dal.GetUserIdByRefreshToken("keep")).Should().Be(user.Id);
        }

        [SkippableFact]
        public async Task DeleteAllRefreshTokensForUser_RemovesEveryUserToken()
        {
            Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Docker indisponible");
            await _fx.ResetAsync();

            var dal = new UserDAL(_fx.Config);
            var user = await dal.AddUser(new UserIn
            {
                Email = "wipe@example.com",
                Password = "Sup3rPa$$word",
                FirstName = "Wi",
                LastName = "Pe",
                AccountType = 1
            });
            await dal.SaveRefreshToken(user.Id, "t1", DateTime.UtcNow.AddDays(7));
            await dal.SaveRefreshToken(user.Id, "t2", DateTime.UtcNow.AddDays(7));
            await dal.SaveRefreshToken(user.Id, "t3", DateTime.UtcNow.AddDays(7));

            var deleted = await dal.DeleteAllRefreshTokensForUser(user.Id);

            deleted.Should().BeTrue();
            await using var conn = new NpgsqlConnection(_fx.ConnectionString);
            var remaining = await conn.ExecuteScalarAsync<long>(
                @"SELECT COUNT(*) FROM ""UserRefreshTokens"" WHERE ""UserId"" = @Id",
                new { Id = user.Id });
            remaining.Should().Be(0);
        }

        [SkippableFact]
        public async Task RotateRefreshToken_AllowsOnlyOneConcurrentReplacement()
        {
            Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Docker indisponible");
            await _fx.ResetAsync();

            var setupDal = new UserDAL(_fx.Config);
            var user = await setupDal.AddUser(new UserIn
            {
                Email = "concurrent-refresh@example.com",
                Password = "Sup3rPa$$word",
                FirstName = "Concurrent",
                LastName = "Refresh",
                AccountType = 1
            });
            await setupDal.SaveRefreshToken(user.Id, "refresh-original", DateTime.UtcNow.AddDays(7));

            var firstRotation = new UserDAL(_fx.Config).RotateRefreshToken(
                "refresh-original", "refresh-replacement-1", DateTime.UtcNow.AddDays(7));
            var secondRotation = new UserDAL(_fx.Config).RotateRefreshToken(
                "refresh-original", "refresh-replacement-2", DateTime.UtcNow.AddDays(7));

            var results = await Task.WhenAll(firstRotation, secondRotation);

            results.Count(result => result is not null).Should().Be(1);
            await using var connection = new NpgsqlConnection(_fx.ConnectionString);
            var remainingHashes = (await connection.QueryAsync<string>(
                @"SELECT ""TokenHash"" FROM ""UserRefreshTokens"" WHERE ""UserId"" = @UserId",
                new { UserId = user.Id })).ToArray();
            remainingHashes.Should().ContainSingle();
            remainingHashes.Single().Should().BeOneOf("refresh-replacement-1", "refresh-replacement-2");
        }

        [SkippableFact]
        public async Task RotateRefreshToken_RacingPasswordChange_CannotLeaveAValidSession()
        {
            Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Docker indisponible");
            await _fx.ResetAsync();

            var setupDal = new UserDAL(_fx.Config);
            var user = await setupDal.AddUser(new UserIn
            {
                Email = "revoke-race@example.com",
                Password = "OldPa$$w0rd",
                FirstName = "Revoke",
                LastName = "Race",
                AccountType = 1
            });
            await setupDal.SaveRefreshToken(user.Id, "refresh-before-change", DateTime.UtcNow.AddDays(7));

            var rotation = new UserDAL(_fx.Config).RotateRefreshToken(
                "refresh-before-change", "refresh-after-change", DateTime.UtcNow.AddDays(7));
            var passwordChange = new UserDAL(_fx.Config).ChangePassword(
                user.Id, "OldPa$$w0rd", "NewPa$$w0rd!");

            await Task.WhenAll(rotation, passwordChange);

            passwordChange.Result.Should().Be(PasswordChangeResult.Success);
            await using var connection = new NpgsqlConnection(_fx.ConnectionString);
            var remaining = await connection.ExecuteScalarAsync<long>(
                @"SELECT COUNT(*) FROM ""UserRefreshTokens"" WHERE ""UserId"" = @UserId",
                new { UserId = user.Id });
            remaining.Should().Be(0);
        }

        [SkippableFact]
        public async Task SaveRefreshToken_RejectsDuplicateTokenHash()
        {
            Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Docker indisponible");
            await _fx.ResetAsync();

            var dal = new UserDAL(_fx.Config);
            var firstUser = await dal.AddUser(new UserIn
            {
                Email = "first-duplicate@example.com",
                Password = "Sup3rPa$$word",
                FirstName = "First",
                LastName = "Duplicate",
                AccountType = 1
            });
            var secondUser = await dal.AddUser(new UserIn
            {
                Email = "second-duplicate@example.com",
                Password = "Sup3rPa$$word",
                FirstName = "Second",
                LastName = "Duplicate",
                AccountType = 1
            });
            await dal.SaveRefreshToken(firstUser.Id, "globally-unique-hash", DateTime.UtcNow.AddDays(7));

            var saveDuplicate = () => dal.SaveRefreshToken(
                secondUser.Id, "globally-unique-hash", DateTime.UtcNow.AddDays(7));

            await saveDuplicate.Should().ThrowAsync<PostgresException>()
                .Where(exception => exception.SqlState == PostgresErrorCodes.UniqueViolation);
        }
    }
}

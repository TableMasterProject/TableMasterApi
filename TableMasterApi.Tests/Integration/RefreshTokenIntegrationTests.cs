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
    }
}

using System.Reflection;
using DbUp;
using Npgsql;
using TableMasterApi.DAL.Handlers;
using TableMasterApi.Model;
using Testcontainers.PostgreSql;
using Xunit;

namespace TableMasterApi.Tests.Integration
{
    /// <summary>
    /// Fixture partagée qui démarre un conteneur PostgreSQL Testcontainers,
    /// applique les migrations DbUp embarquées dans l'assembly de l'API,
    /// puis fournit une <see cref="ConfigPerso"/> branchée sur ce conteneur.
    ///
    /// Si Docker n'est pas disponible localement, <see cref="IsAvailable"/>
    /// reste à <c>false</c> et les tests d'intégration sont skippés.
    /// </summary>
    public sealed class PostgresFixture : IAsyncLifetime
    {
        private PostgreSqlContainer? _container;
        private static readonly object _dapperHandlerLock = new();
        private static bool _dapperHandlerRegistered;

        public bool IsAvailable { get; private set; }
        public string? UnavailableReason { get; private set; }
        public string ConnectionString { get; private set; } = string.Empty;
        public ConfigPerso Config { get; private set; } = new();

        public async Task InitializeAsync()
        {
            try
            {
                _container = new PostgreSqlBuilder()
                    .WithImage("postgres:16-alpine")
                    .WithDatabase("tablemaster_tests")
                    .WithUsername("tests")
                    .WithPassword("tests")
                    .Build();
                await _container.StartAsync();
            }
            catch (Exception ex)
            {
                IsAvailable = false;
                UnavailableReason = $"Docker indisponible: {ex.GetType().Name} - {ex.Message}";
                return;
            }

            ConnectionString = _container.GetConnectionString();
            Config = new ConfigPerso
            {
                ConnectionString = ConnectionString,
                SecretKey = "this_is_a_very_secret_key_for_testing_purposes_that_is_long_enough_for_jwt",
                Issuer = "TableMasterTestIssuer",
                Audience = "TableMasterTestAudience"
            };

            RegisterDapperHandlerOnce();

            var upgrader = DeployChanges.To
                .PostgresqlDatabase(ConnectionString)
                .WithScriptsEmbeddedInAssembly(typeof(TableMasterApi.Controllers.AuthController).Assembly)
                .LogToConsole()
                .Build();

            var result = upgrader.PerformUpgrade();
            if (!result.Successful)
            {
                throw new InvalidOperationException(
                    "Echec de l'execution des migrations DbUp sur le conteneur de tests.",
                    result.Error);
            }

            IsAvailable = true;
        }

        public async Task DisposeAsync()
        {
            if (_container is null) return;
            try
            {
                await _container.DisposeAsync();
            }
            catch
            {
                // Best effort cleanup.
            }
        }

        /// <summary>
        /// Vide les tables manipulees par les tests entre deux executions.
        /// Reset egalement les sequences d'identite.
        /// </summary>
        public async Task ResetAsync()
        {
            if (!IsAvailable) return;

            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();

            const string truncate = @"
                TRUNCATE TABLE
                    ""UserRefreshTokens"",
                    ""Reservation"",
                    ""Review"",
                    ""Menu"",
                    ""DailyActivity"",
                    ""ClosedDayException"",
                    ""TableEntity"",
                    ""RestaurantRoom"",
                    ""Restaurant"",
                    ""UserDeviceTokens"",
                    ""User""
                RESTART IDENTITY CASCADE;";

            await using var cmd = new NpgsqlCommand(truncate, connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private static void RegisterDapperHandlerOnce()
        {
            lock (_dapperHandlerLock)
            {
                if (_dapperHandlerRegistered) return;
                Dapper.SqlMapper.AddTypeHandler(new PostgresTimeSpanHandler());
                _dapperHandlerRegistered = true;
            }
        }
    }

    [CollectionDefinition(Name)]
    public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
    {
        public const string Name = "Postgres integration";
    }
}

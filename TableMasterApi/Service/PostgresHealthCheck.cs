using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using TableMasterApi.Model;

namespace TableMasterApi.Service
{
    public class PostgresHealthCheck : IHealthCheck
    {
        private readonly ConfigPerso _config;

        public PostgresHealthCheck(ConfigPerso config)
        {
            _config = config;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_config.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync(cancellationToken);

                return HealthCheckResult.Healthy();
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Unhealthy("PostgreSQL indisponible.", exception);
            }
        }
    }
}

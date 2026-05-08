using Dapper;
using Npgsql;
using System.Linq;
using TableMasterApi.Model;
using TableMasterApi.DAL.Interfaces;

namespace TableMasterApi.DAL
{
    /// <summary>
    /// Data Access Layer pour les tokens d'appareils
    /// </summary>
    public class DeviceTokenDAL : IDeviceTokenDAL
    {
        private readonly string _connectionString;

        public DeviceTokenDAL(ConfigPerso config)
        {
            _connectionString = config.ConnectionString;
        }

        public async Task<bool> SaveDeviceTokenAsync(long userId, string deviceToken, string devicePlatform)
        {
            if (string.IsNullOrWhiteSpace(deviceToken))
                return false;

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
INSERT INTO ""UserDeviceTokens"" (""UserId"", ""DeviceToken"", ""DevicePlatform"", ""LastSeenAt"")
VALUES (@UserId, @DeviceToken, @DevicePlatform, CURRENT_TIMESTAMP)
ON CONFLICT (""UserId"", ""DeviceToken"")
DO UPDATE SET
    ""DevicePlatform"" = EXCLUDED.""DevicePlatform"",
    ""LastSeenAt"" = CURRENT_TIMESTAMP;";

            var rows = await connection.ExecuteAsync(query, new { UserId = userId, DeviceToken = deviceToken, DevicePlatform = devicePlatform });
            return rows > 0;
        }

        public async Task<IEnumerable<string>> GetDeviceTokensForUserIdsAsync(IEnumerable<long> userIds)
        {
            if (userIds == null || !userIds.Any())
                return Enumerable.Empty<string>();

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"SELECT ""DeviceToken"" FROM ""UserDeviceTokens"" WHERE ""UserId"" = ANY(@UserIds)";
            return await connection.QueryAsync<string>(query, new { UserIds = userIds.ToArray() });
        }

        public async Task<bool> DeleteDeviceTokenAsync(long userId, string deviceToken)
        {
            if (string.IsNullOrWhiteSpace(deviceToken))
                return false;

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"DELETE FROM ""UserDeviceTokens"" WHERE ""UserId"" = @UserId AND ""DeviceToken"" = @DeviceToken";
            var rows = await connection.ExecuteAsync(query, new { UserId = userId, DeviceToken = deviceToken });
            return rows > 0;
        }
    }
}

using Dapper;
using Microsoft.Data.SqlClient;
using System.Linq;
using TableMasterApi.Model;

namespace TableMasterApi.DAL
{
    public class DeviceTokenDAL
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

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
IF EXISTS (SELECT 1 FROM UserDeviceTokens WHERE UserId = @UserId AND DeviceToken = @DeviceToken)
    UPDATE UserDeviceTokens
    SET DevicePlatform = @DevicePlatform,
        LastSeenAt = GETDATE()
    WHERE UserId = @UserId AND DeviceToken = @DeviceToken;
ELSE
    INSERT INTO UserDeviceTokens (UserId, DeviceToken, DevicePlatform, LastSeenAt)
    VALUES (@UserId, @DeviceToken, @DevicePlatform, GETDATE());";

            var rows = await connection.ExecuteAsync(query, new { UserId = userId, DeviceToken = deviceToken, DevicePlatform = devicePlatform });
            return rows > 0;
        }

        public async Task<IEnumerable<string>> GetDeviceTokensForUserIdsAsync(IEnumerable<long> userIds)
        {
            if (userIds == null || !userIds.Any())
                return Enumerable.Empty<string>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT DeviceToken FROM UserDeviceTokens WHERE UserId IN @UserIds";
            return await connection.QueryAsync<string>(query, new { UserIds = userIds });
        }

        public async Task<bool> DeleteDeviceTokenAsync(long userId, string deviceToken)
        {
            if (string.IsNullOrWhiteSpace(deviceToken))
                return false;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "DELETE FROM UserDeviceTokens WHERE UserId = @UserId AND DeviceToken = @DeviceToken";
            var rows = await connection.ExecuteAsync(query, new { UserId = userId, DeviceToken = deviceToken });
            return rows > 0;
        }
    }
}

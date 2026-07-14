namespace TableMasterApi.Model
{
    public class DeviceTokenIn
    {
        public string DeviceToken { get; set; } = string.Empty;
        public string DevicePlatform { get; set; } = string.Empty;
    }

    public class UserDeviceToken
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string DeviceToken { get; set; } = string.Empty;
        public string DevicePlatform { get; set; } = string.Empty;
        public DateTime LastSeenAt { get; set; }
    }
}

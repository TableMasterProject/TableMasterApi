namespace TableMasterApi.Model
{
    public class DeviceTokenIn
    {
        public string DeviceToken { get; set; }
        public string DevicePlatform { get; set; }
    }

    public class UserDeviceToken
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string DeviceToken { get; set; }
        public string DevicePlatform { get; set; }
        public DateTime LastSeenAt { get; set; }
    }
}

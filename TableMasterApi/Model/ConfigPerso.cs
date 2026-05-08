namespace TableMasterApi.Model
{
    public class ConfigPerso
    {
        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public string KeyApiGoogleMaps { get; set; } = string.Empty;
        public string FirebaseServiceAccountPath { get; set; } = "tablemaster-firebase.json";
    }
}

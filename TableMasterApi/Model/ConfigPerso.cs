namespace TableMasterApi.Model
{
    public class ConfigPerso
    {
        public string SecretKey { get; set; }
        public string Issuer { get; set; }
        public string Audience { get; set; }
        public string ConnectionString { get; set; }
        public string KeyApiGoogleMaps { get; set; }
        public string FirebaseServerKey { get; set; }
    }
}

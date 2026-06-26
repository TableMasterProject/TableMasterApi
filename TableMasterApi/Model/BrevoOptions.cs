namespace TableMasterApi.Model
{
    public class BrevoOptions
    {
        public bool Enabled { get; set; } = true;
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.brevo.com/v3/";
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = "TableMaster";
    }
}

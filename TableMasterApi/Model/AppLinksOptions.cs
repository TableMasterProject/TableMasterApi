namespace TableMasterApi.Model
{
    public class AppLinksOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string FallbackScheme { get; set; } = "tablemaster";
        public string PasswordResetPath { get; set; } = "/reset-password";
        public string ReservationPath { get; set; } = "/reservations/{reservationId}";
    }
}

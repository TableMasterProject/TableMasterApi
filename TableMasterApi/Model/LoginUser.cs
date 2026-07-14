namespace TableMasterApi.Model
{
    public class LoginUserIn
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
    public class LoginTokenIn
    {
        public required string RefreshToken { get; set; }
    }

    public class LogoutRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
    public class LoginUserOut
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public required UserOut User { get; set; }
    }
}

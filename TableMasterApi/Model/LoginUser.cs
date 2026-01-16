namespace TableMasterApi.Model
{
    public class LoginUserIn
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
    public class LoginTokenIn
    {
        public string RefreshToken { get; set; }
    }
    public class LoginUserOut
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public UserOut User { get; set; }
    }
}

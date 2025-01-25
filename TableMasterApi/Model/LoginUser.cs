namespace TableMasterApi.Model
{
    public class LoginUserIn
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
    public class LoginUserOut
    {
        public string Token { get; set; }
        public UserOut User { get; set; }
    }
}

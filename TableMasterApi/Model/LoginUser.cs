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
    public class LoginUserOut
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public required UserOut User { get; set; }
    }

    public class GoogleAuthCheckIn
    {
        public required string IdToken { get; set; }
    }

    public class GoogleAuthCheckOut
    {
        public bool NeedsOnboarding { get; set; }
        public string? GoogleRegistrationToken { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public UserOut? User { get; set; }
    }

    public class GoogleRegisterIn
    {
        public required string GoogleRegistrationToken { get; set; }
        public required string Email { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public byte AccountType { get; set; }
    }
}

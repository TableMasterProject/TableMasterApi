namespace TableMasterApi.Model
{
    public class ForgotPasswordRequest
    {
        public required string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        public required string Token { get; set; }
        public required string NewPassword { get; set; }
    }
}

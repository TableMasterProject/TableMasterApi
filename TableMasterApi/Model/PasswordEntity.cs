namespace TableMasterApi.Model
{
    public class PasswordEntity
    {
        public required string OldPassword { get; set; }
        public required string NewPassword { get; set; }
    }
}

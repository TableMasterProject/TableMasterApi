namespace TableMasterApi.Model
{
    public class UserIn
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public byte AccountType { get; set; }
        public string AuthProvider { get; set; } = "Password";
        public string? GoogleSubject { get; set; }
    }

    public class UserOut
    {
        public long Id { get; set; }
        public required string Email { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public byte AccountType { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? RestaurantId { get; set; }
        public string AuthProvider { get; set; } = "Password";
    }

    public class UserDb : UserOut
    {
        public string? Password { get; set; }
        public string? GoogleSubject { get; set; }

        public UserOut ToPublicUser()
        {
            return new UserOut
            {
                Id = Id,
                Email = Email,
                FirstName = FirstName,
                LastName = LastName,
                AccountType = AccountType,
                CreatedAt = CreatedAt,
                RestaurantId = RestaurantId,
                AuthProvider = AuthProvider
            };
        }
    }

}

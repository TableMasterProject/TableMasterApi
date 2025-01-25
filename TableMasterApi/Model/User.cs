namespace TableMasterApi.Model
{
    public class UserIn
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public byte AccountType { get; set; }
    }

    public class UserOut : UserIn
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public RestaurantOut? Restaurant { get; set; }
    }


}

namespace TableMasterApi.Tests.TestSupport
{
    internal static class TestData
    {
        public static UserIn UserIn(string email = "user@example.com") => new()
        {
            Email = email,
            Password = "Password123!",
            FirstName = "Test",
            LastName = "User",
            AccountType = 1
        };

        public static UserDb UserDb(long id = 42, string email = "user@example.com") => new()
        {
            Id = id,
            Email = email,
            Password = "stored-hash",
            FirstName = "Test",
            LastName = "User",
            AccountType = 1,
            CreatedAt = DateTime.UtcNow
        };

        public static UserOut UserOut(long id = 42, string email = "user@example.com") => new()
        {
            Id = id,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            AccountType = 1,
            CreatedAt = DateTime.UtcNow
        };

        public static RestaurantIn RestaurantIn(string name = "Test Bistro") => new()
        {
            RestaurantName = name,
            StreetNumber = "12",
            StreetName = "Rue des Tests",
            PostalCode = "75001",
            City = "Paris",
            Phone = "0102030405",
            CuisineType = "Francaise",
            PaymentMethods = "CB",
            Description = "Restaurant de test",
            IsAutoValidateReservation = false
        };

        public static RestaurantOut RestaurantOut(long id = 10, long userId = 42, bool autoValidate = false) => new()
        {
            Id = id,
            UserId = userId,
            RestaurantName = "Test Bistro",
            StreetNumber = "12",
            StreetName = "Rue des Tests",
            PostalCode = "75001",
            City = "Paris",
            Phone = "0102030405",
            CuisineType = "Francaise",
            PaymentMethods = "CB",
            Description = "Restaurant de test",
            IsAutoValidateReservation = autoValidate,
            CreatedAt = DateTime.UtcNow
        };

        public static TableEntityIn TableIn(long restaurantId = 10) => new()
        {
            RestaurantId = restaurantId,
            TableNumber = 1,
            NumberOfSeats = 4
        };

        public static TableEntityOut TableOut(long id = 4, long restaurantId = 10, int seats = 4) => new()
        {
            Id = id,
            RestaurantId = restaurantId,
            TableNumber = 1,
            NumberOfSeats = seats,
            CreatedAt = DateTime.UtcNow
        };

        public static ReservationIn ReservationIn(long restaurantId = 10, long tableId = 4) => new()
        {
            RestaurantId = restaurantId,
            TableId = tableId,
            ReservationDate = DateTime.UtcNow.AddDays(1),
            NumberOfPeople = 2
        };

        public static ReservationOut ReservationOut(long id = 1, long restaurantId = 10, long tableId = 4, long userId = 42) => new()
        {
            Id = id,
            RestaurantId = restaurantId,
            TableId = tableId,
            UserId = userId,
            ReservationDate = DateTime.UtcNow.AddDays(1),
            NumberOfPeople = 2,
            Status = ReservationStatus.EnAttente,
            CreatedAt = DateTime.UtcNow
        };

        public static RestaurantRoomIn RoomIn(string name = "Salle") => new()
        {
            Name = name,
            BoundaryPoints = RestaurantRoomDefaults.DefaultBoundary()
        };

        public static RestaurantRoomOut RoomOut(long id = 5, long restaurantId = 10) => new()
        {
            Id = id,
            RestaurantId = restaurantId,
            Name = "Salle",
            BoundaryPoints = RestaurantRoomDefaults.DefaultBoundary(),
            CreatedAt = DateTime.UtcNow
        };

        public static MenuIn MenuIn(long restaurantId = 10) => new()
        {
            RestaurantId = restaurantId,
            Category = "Entree",
            ItemName = "Soupe",
            Description = "Soupe du jour",
            Price = 8.5m
        };

        public static MenuOut MenuOut(long id = 3, long restaurantId = 10) => new()
        {
            Id = id,
            RestaurantId = restaurantId,
            Category = "Entree",
            ItemName = "Soupe",
            Description = "Soupe du jour",
            Price = 8.5m,
            CreatedAt = DateTime.UtcNow
        };

        public static ReviewIn ReviewIn(long restaurantId = 10) => new()
        {
            RestaurantId = restaurantId,
            Rating = 5,
            Comment = "Excellent"
        };

        public static ReviewOut ReviewOut(long id = 6, long restaurantId = 10, long userId = 42) => new()
        {
            Id = id,
            UserId = userId,
            RestaurantId = restaurantId,
            Rating = 5,
            Comment = "Excellent",
            CreatedAt = DateTime.UtcNow
        };

        public static DailyActivityIn DailyActivityIn(long restaurantId = 10) => new()
        {
            RestaurantId = restaurantId,
            DayOfWeek = 1,
            StartTime = TimeSpan.FromHours(12),
            EndTime = TimeSpan.FromHours(14)
        };

        public static DailyActivityOut DailyActivityOut(long id = 7, long restaurantId = 10) => new()
        {
            Id = id,
            RestaurantId = restaurantId,
            DayOfWeek = 1,
            StartTime = TimeSpan.FromHours(12),
            EndTime = TimeSpan.FromHours(14),
            CreatedAt = DateTime.UtcNow
        };

        public static ClosedDayExceptionIn ClosedDayExceptionIn(long restaurantId = 10) => new()
        {
            RestaurantId = restaurantId,
            ExceptionDateBegin = DateTime.UtcNow.Date.AddDays(5),
            ExceptionDateEnd = DateTime.UtcNow.Date.AddDays(6),
            Reason = "Travaux"
        };

        public static ClosedDayExceptionOut ClosedDayExceptionOut(long id = 8, long restaurantId = 10) => new()
        {
            Id = id,
            RestaurantId = restaurantId,
            ExceptionDateBegin = DateTime.UtcNow.Date.AddDays(5),
            ExceptionDateEnd = DateTime.UtcNow.Date.AddDays(6),
            Reason = "Travaux",
            CreatedAt = DateTime.UtcNow
        };
    }
}

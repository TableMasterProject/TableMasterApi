namespace TableMasterApi.Model
{
    public class RestaurantIn
    {
        public long UserId { get; set; }
        public string RestaurantName { get; set; } = string.Empty;
        public string StreetNumber { get; set; } = string.Empty;
        public string StreetName { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string CuisineType { get; set; } = string.Empty;
        public string PaymentMethods { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Boolean IsAutoValidateReservation { get; set; }
    }
    public class RestaurantOut : RestaurantIn
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal DistanceForSearch { get; set; }
        public decimal DistanceWithUser { get; set; }
        public decimal AverageRating { get; set; }
        public long NumberOfReviews { get; set; }
        public IEnumerable<RestaurantRoomOut>? Rooms { get; set; }
        public IEnumerable<TableEntityOut>? Tables { get; set; }
        public IEnumerable<DailyActivityOut>? DailyActivitys { get; set; }
        public IEnumerable<ClosedDayExceptionOut>? ClosedDayExceptions { get; set; }
        public IEnumerable<ReviewOut>? Reviews { get; set; }
        public IEnumerable<MenuOut>? Menu { get; set; }
    }


}

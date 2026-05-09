namespace TableMasterApi.Model
{
    public class RestaurantIn
    {
        public long UserId { get; set; }
        public string RestaurantName { get; set; }
        public string StreetNumber { get; set; }
        public string StreetName { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string Phone { get; set; }
        public string CuisineType { get; set; }
        public string PaymentMethods { get; set; }
        public string Description { get; set; }
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

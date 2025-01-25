namespace TableMasterApi.Model
{
    public class RestaurantIn
    {
        public string RestaurantName { get; set; }
        public string StreetNumber { get; set; }
        public string StreetName { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }
    public class RestaurantOut : RestaurantIn
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public IEnumerable<TableEntityOut>? Tables { get; set; }
        public IEnumerable<DailyActivityOut>? DailyActivitys { get; set; }
        public IEnumerable<ClosedDayExceptionOut>? ClosedDayExceptions { get; set; }
    }


}

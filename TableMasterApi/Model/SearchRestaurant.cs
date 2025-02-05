namespace TableMasterApi.Model
{
    public class SearchRestaurant
    {
        public long? Offset { get; set; }
        public long? PageSize { get; set; } = 20;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? CuisineType { get; set; }
        public string? PaymentMethods { get; set; }
    }
}

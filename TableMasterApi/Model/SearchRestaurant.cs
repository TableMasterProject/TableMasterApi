namespace TableMasterApi.Model
{
    public class SearchRestaurant
    {
        public long? Offset { get; set; }
        public long? PageSize { get; set; } = 20;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public decimal? CurrentUserLongitude { get; set; }
        public decimal? CurrentUserLatitude { get; set; }
        public decimal? SouthLatitude { get; set; }
        public decimal? NorthLatitude { get; set; }
        public decimal? WestLongitude { get; set; }
        public decimal? EastLongitude { get; set; }
        public string? CuisineType { get; set; }
        public string? PaymentMethods { get; set; }

        public bool HasViewport => SouthLatitude.HasValue && NorthLatitude.HasValue &&
            WestLongitude.HasValue && EastLongitude.HasValue;

        public bool HasValidViewport
        {
            get
            {
                var supplied = new[] { SouthLatitude, NorthLatitude, WestLongitude, EastLongitude }
                    .Count(value => value.HasValue);
                if (supplied == 0) return true;
                return supplied == 4 &&
                    SouthLatitude >= -90 && NorthLatitude <= 90 && SouthLatitude < NorthLatitude &&
                    WestLongitude >= -180 && WestLongitude <= 180 &&
                    EastLongitude >= -180 && EastLongitude <= 180;
            }
        }
    }
}

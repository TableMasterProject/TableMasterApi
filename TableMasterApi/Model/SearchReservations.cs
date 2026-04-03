namespace TableMasterApi.Model
{
    public class SearchReservations
    {
        public long? Offset { get; set; }
        public long? PageSize { get; set; } = 20;

        public long? tableId { get; set; }

        public List<ReservationStatus>? Statuses { get; set; }

        public DateOnly? minDate { get; set; }
        public DateOnly? maxDate { get; set; }

        public string? minDateString
        {
            get => minDate?.ToString("yyyy-MM-dd");
            set
            {
                if (DateOnly.TryParse(value, out var date))
                {
                    minDate = date;
                }
                else
                {
                    minDate = null;
                }
            }
        }
        public string? maxDateString
        {
            get => maxDate?.ToString("yyyy-MM-dd");
            set
            {
                if (DateOnly.TryParse(value, out var date))
                {
                    maxDate = date;
                }
                else
                {
                    maxDate = null;
                }
            }
        }

        public long? IdUser { get; set; }
        public long? restaurantId { get; set; }
    }
}

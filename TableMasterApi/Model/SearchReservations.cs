namespace TableMasterApi.Model
{
    public class SearchReservations
    {
        public long? Offset { get; set; }
        public long? PageSize { get; set; } = 20;
    }
}

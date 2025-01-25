namespace TableMasterApi.Model
{
    public class TableEntityIn
    {
        public long RestaurantId { get; set; }
        public int TableNumber { get; set; }
        public int NumberOfSeats { get; set; }
    }
    public class TableEntityOut : TableEntityIn
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public RestaurantOut Restaurant { get; set; }
    }


}

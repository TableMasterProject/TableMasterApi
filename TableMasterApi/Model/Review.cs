namespace TableMasterApi.Model
{
    public class ReviewIn
    {
        public long UserId { get; set; }
        public long RestaurantId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
    public class ReviewOut : ReviewIn
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
    }

}

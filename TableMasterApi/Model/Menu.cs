namespace TableMasterApi.Model
{
    public class MenuIn
    {
        public long RestaurantId { get; set; }
        public string Category { get; set; }
        public string ItemName { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
    }
    public class MenuOut : MenuIn
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

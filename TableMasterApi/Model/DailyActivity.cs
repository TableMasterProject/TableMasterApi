namespace TableMasterApi.Model
{
    public class DailyActivityIn
    {
        public long RestaurantId { get; set; }
        public byte DayOfWeek { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
    public class DailyActivityOut : DailyActivityIn
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
    }

}

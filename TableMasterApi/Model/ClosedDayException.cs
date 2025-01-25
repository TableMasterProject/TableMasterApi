namespace TableMasterApi.Model
{
    public class ClosedDayExceptionIn
    {
        public long RestaurantId { get; set; }
        public DateTime ExceptionDateBegin { get; set; }
        public DateTime ExceptionDateEnd { get; set; }
        public string Reason { get; set; }
    }
    public class ClosedDayExceptionOut : ClosedDayExceptionIn
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public RestaurantOut Restaurant { get; set; }
    }

}

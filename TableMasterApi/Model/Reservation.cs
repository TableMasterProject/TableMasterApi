namespace TableMasterApi.Model
{
    public class ReservationIn
    {
        public long UserId { get; set; }
        public long TableId { get; set; }
        public long RestaurantId { get; set; }
        public DateTime ReservationDate { get; set; }
        public int NumberOfPeople { get; set; }
        public string SpecialRequest { get; set; }
        public Boolean IsValidate { get; set; }
    }
    public class ReservationOut : ReservationIn
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserOut User { get; set; }
        public TableEntityOut Table { get; set; }
    }

}

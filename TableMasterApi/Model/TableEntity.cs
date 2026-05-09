namespace TableMasterApi.Model
{
    public enum TableShape : byte
    {
        Square = 0,
        Rectangle = 1,
        Circle = 2
    }

    public class TableEntityIn
    {
        public long RestaurantId { get; set; }
        public long? RoomId { get; set; }
        public int TableNumber { get; set; }
        public int NumberOfSeats { get; set; }
        public TableShape Shape { get; set; } = TableShape.Rectangle;
        public decimal PositionX { get; set; } = 0.1m;
        public decimal PositionY { get; set; } = 0.1m;
        public decimal Width { get; set; } = 0.16m;
        public decimal Height { get; set; } = 0.12m;
        public decimal RotationDegrees { get; set; }
    }

    public class TableEntityOut : TableEntityIn
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

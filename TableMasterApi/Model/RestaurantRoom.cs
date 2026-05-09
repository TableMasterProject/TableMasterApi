using System.Text.Json;
using System.Text.Json.Serialization;

namespace TableMasterApi.Model
{
    public class RoomPoint
    {
        public decimal X { get; set; }
        public decimal Y { get; set; }
    }

    public class RestaurantRoomIn
    {
        public long RestaurantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public List<RoomPoint> BoundaryPoints { get; set; } = RestaurantRoomDefaults.DefaultBoundary();
    }

    public class RestaurantRoomOut : RestaurantRoomIn
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }

        [JsonIgnore]
        public string? BoundaryPointsJson
        {
            get => JsonSerializer.Serialize(BoundaryPoints);
            set => BoundaryPoints = RestaurantRoomDefaults.ParseBoundary(value);
        }
    }

    public class RestaurantRoomLayoutIn
    {
        public RestaurantRoomIn Room { get; set; } = new();
        public List<TableEntityIn> TablesToAdd { get; set; } = [];
        public List<TableEntityOut> TablesToUpdate { get; set; } = [];
        public List<long> TableIdsToDelete { get; set; } = [];
    }

    public class RestaurantRoomLayoutOut
    {
        public RestaurantRoomOut Room { get; set; } = new();
        public IEnumerable<TableEntityOut> Tables { get; set; } = [];
    }

    public static class RestaurantRoomDefaults
    {
        public static List<RoomPoint> DefaultBoundary() =>
        [
            new RoomPoint { X = 0.05m, Y = 0.05m },
            new RoomPoint { X = 0.95m, Y = 0.05m },
            new RoomPoint { X = 0.95m, Y = 0.95m },
            new RoomPoint { X = 0.05m, Y = 0.95m }
        ];

        public static List<RoomPoint> ParseBoundary(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DefaultBoundary();
            }

            return JsonSerializer.Deserialize<List<RoomPoint>>(value, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? DefaultBoundary();
        }
    }
}

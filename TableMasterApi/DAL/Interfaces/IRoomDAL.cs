using TableMasterApi.Model;

namespace TableMasterApi.DAL.Interfaces
{
    public interface IRoomDAL
    {
        Task<IEnumerable<RestaurantRoomOut>> GetRoomsByRestaurantAsync(long restaurantId);
        Task<RestaurantRoomOut?> GetRoomByIdAsync(long roomId);
        Task<RestaurantRoomOut?> CreateRoomAsync(RestaurantRoomIn room);
        Task<RestaurantRoomOut?> UpdateRoomAsync(long roomId, RestaurantRoomIn room);
        Task<bool> DeleteRoomAsync(long roomId);
        Task<bool> RoomHasTablesAsync(long roomId);
        Task<RestaurantRoomLayoutOut?> SaveLayoutAsync(long roomId, RestaurantRoomLayoutIn layout);
    }
}

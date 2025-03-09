using Microsoft.AspNetCore.SignalR;
using TableMasterApi.Model;

namespace TableMasterApi.Hubs
{
    public class ReservationHub : Hub
    {
        public const string SEND_AT_ReceiveReservationCreated = "ReceiveReservationCreated";
        public const string SEND_AT_ReceiveReservationDeleted = "ReceiveReservationDeleted";
        public const string SEND_AT_ReceiveReservationValidate = "ReceiveReservationValidate";
        public const string RESTAURANT_GROUP_PREFIX = "restaurant_";
        // Rejoindre un groupe (restaurant)
        public async Task JoinRestaurantGroup(string restaurantId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RESTAURANT_GROUP_PREFIX + restaurantId);
        }

        // Quitter un groupe
        public async Task LeaveRestaurantGroup(string restaurantId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, RESTAURANT_GROUP_PREFIX + restaurantId);
        }
    }
}

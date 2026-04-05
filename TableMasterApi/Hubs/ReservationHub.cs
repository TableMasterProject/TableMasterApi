using Microsoft.AspNetCore.SignalR;
using TableMasterApi.Model;

namespace TableMasterApi.Hubs
{
    public class ReservationHub : Hub
    {
        public const string SEND_AT_ReceiveReservationCreated = "ReceiveReservationCreated";
        public const string SEND_AT_ReceiveReservationDeleted = "ReceiveReservationDeleted";
        public const string SEND_AT_ReceiveReservationUpdateStatus = "ReceiveReservationUpdateStatus";
        public const string RESTAURANT_GROUP_PREFIX = "restaurant_";
        public const string USER_GROUP_PREFIX = "user_";
        // Rejoindre un groupe (restaurant)
        public async Task JoinRestaurantGroup(string restaurantId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RESTAURANT_GROUP_PREFIX + restaurantId);
        }
        public async Task JoinUserGroup(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, USER_GROUP_PREFIX + userId);
        }

        // Quitter un groupe
        public async Task LeaveRestaurantGroup(string restaurantId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, RESTAURANT_GROUP_PREFIX + restaurantId);
        }
        public async Task LeaveUserGroup(string userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, USER_GROUP_PREFIX + userId);
        }
    }
}

using Microsoft.AspNetCore.SignalR;

namespace TableMasterApi.Hubs
{
    public class ReservationHub : Hub
    {
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

        // Envoyer une mise à jour uniquement aux clients du restaurant concerné
        public async Task SendReservationUpdate(string restaurantId, string tableId)
        {
            await Clients.Group(RESTAURANT_GROUP_PREFIX + restaurantId).SendAsync("ReceiveReservationUpdate", tableId);
        }
    }
}

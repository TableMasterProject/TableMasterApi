using Microsoft.AspNetCore.SignalR;

namespace TableMasterApi.Hubs
{
    public class ReservationHub : Hub
    {
        public const string SEND_AT_ReceiveReservationCreated = "ReceiveReservationCreated";
        public const string SEND_AT_ReceiveReservationDeleted = "ReceiveReservationDeleted";
        public const string SEND_AT_ReceiveReservationUpdateStatus = "ReceiveReservationUpdateStatus";
        public const string RESTAURANT_GROUP_PREFIX = "restaurant_";
        public const string USER_GROUP_PREFIX = "user_";

        private readonly ILogger<ReservationHub> _logger;

        public ReservationHub(ILogger<ReservationHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("SignalR: connexion {ConnectionId} etablie.", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception is null)
            {
                _logger.LogInformation("SignalR: connexion {ConnectionId} fermee.", Context.ConnectionId);
            }
            else
            {
                _logger.LogWarning(exception, "SignalR: connexion {ConnectionId} fermee sur erreur.", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Rejoindre un groupe (restaurant)
        // Les groupes sont indexes par ConnectionId : apres une reconnexion le client
        // recoit un nouveau ConnectionId et doit donc rejoindre a nouveau ses groupes.
        public async Task JoinRestaurantGroup(string restaurantId)
        {
            var group = RESTAURANT_GROUP_PREFIX + restaurantId;
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
            _logger.LogInformation("SignalR: {ConnectionId} rejoint le groupe {Group}.", Context.ConnectionId, group);
        }

        public async Task JoinUserGroup(string userId)
        {
            var group = USER_GROUP_PREFIX + userId;
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
            _logger.LogInformation("SignalR: {ConnectionId} rejoint le groupe {Group}.", Context.ConnectionId, group);
        }

        // Quitter un groupe
        public async Task LeaveRestaurantGroup(string restaurantId)
        {
            var group = RESTAURANT_GROUP_PREFIX + restaurantId;
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
            _logger.LogInformation("SignalR: {ConnectionId} quitte le groupe {Group}.", Context.ConnectionId, group);
        }

        public async Task LeaveUserGroup(string userId)
        {
            var group = USER_GROUP_PREFIX + userId;
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
            _logger.LogInformation("SignalR: {ConnectionId} quitte le groupe {Group}.", Context.ConnectionId, group);
        }
    }
}

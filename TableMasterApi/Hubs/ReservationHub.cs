using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Service;

namespace TableMasterApi.Hubs
{
    [Authorize]
    public class ReservationHub : Hub
    {
        public const string SEND_AT_ReceiveReservationCreated = "ReceiveReservationCreated";
        public const string SEND_AT_ReceiveReservationDeleted = "ReceiveReservationDeleted";
        public const string SEND_AT_ReceiveReservationUpdateStatus = "ReceiveReservationUpdateStatus";
        public const string RESTAURANT_GROUP_PREFIX = "restaurant_";
        public const string USER_GROUP_PREFIX = "user_";
        public const string AVAILABILITY_GROUP_PREFIX = "availability_";
        public const string SEND_AT_ReceiveAvailabilityChanged = "ReceiveAvailabilityChanged";

        private readonly ILogger<ReservationHub> _logger;
        private readonly IRestaurantDAL _restaurants;
        private readonly ICurrentUserService _currentUser;

        public ReservationHub(ILogger<ReservationHub> logger, IRestaurantDAL restaurants, ICurrentUserService currentUser)
        {
            _logger = logger;
            _restaurants = restaurants;
            _currentUser = currentUser;
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
            var id = ParseId(restaurantId);
            var userId = GetUserId();
            var restaurant = await _restaurants.GetRestaurantById(id);
            if (restaurant == null || restaurant.UserId != userId) throw new HubException("Accès au restaurant refusé.");
            var group = RESTAURANT_GROUP_PREFIX + id;
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
            _logger.LogInformation("SignalR: {ConnectionId} rejoint le groupe {Group}.", Context.ConnectionId, group);
        }

        public async Task JoinUserGroup(string userId)
        {
            var id = ParseId(userId);
            if (id != GetUserId()) throw new HubException("Accès au groupe refusé.");
            var group = USER_GROUP_PREFIX + id;
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
        public async Task JoinAvailabilityGroup(string restaurantId)
        {
            GetUserId();
            var id = ParseId(restaurantId);
            if (await _restaurants.GetRestaurantById(id) == null)
                throw new HubException("Restaurant introuvable.");
            await Groups.AddToGroupAsync(Context.ConnectionId, AVAILABILITY_GROUP_PREFIX + id);
        }

        public Task LeaveAvailabilityGroup(string restaurantId) =>
            Groups.RemoveFromGroupAsync(Context.ConnectionId, AVAILABILITY_GROUP_PREFIX + ParseId(restaurantId));

        private long GetUserId()
        {
            if (Context.User?.Identity?.IsAuthenticated != true)
                throw new HubException("Authentification requise.");
            try { return _currentUser.GetUserId(Context.User); }
            catch (UnauthorizedAccessException) { throw new HubException("Authentification requise."); }
        }

        private static long ParseId(string value)
        {
            if (!long.TryParse(value, out var id) || id <= 0)
                throw new HubException("Identifiant invalide.");
            return id;
        }
    }
}

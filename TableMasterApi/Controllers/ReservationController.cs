using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Asp.Versioning;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Hubs;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [ApiController]
    public class ReservationController : ControllerBase
    {
        private readonly IReservationDAL _reservationDAL;
        private readonly IRestaurantDAL _restaurantDAL;
        private readonly ITableDAL _tableDAL;
        private readonly IDeviceTokenDAL _deviceTokenDAL;
        private readonly IClosedDayExceptionDAL? _closedDayExceptionDAL;

        private readonly ICurrentUserService _currentUser;
        private readonly FcmService _fcmService;
        private readonly IEmailNotificationService _emailNotificationService;

        private readonly IHubContext<ReservationHub> _hubContext;
        private readonly ILogger<ReservationController> _logger;

        public ReservationController(
            IReservationDAL reservationDAL,
            IRestaurantDAL restaurantDAL,
            ITableDAL tableDAL,
            IDeviceTokenDAL deviceTokenDAL,
            JwtService jwtService,
            IHubContext<ReservationHub> hubContext,
            FcmService fcmService,
            IEmailNotificationService? emailNotificationService = null,
            IClosedDayExceptionDAL? closedDayExceptionDAL = null,
            ILogger<ReservationController>? logger = null,
            ICurrentUserService? currentUser = null)
        {
            _currentUser = currentUser ?? new CurrentUserService();
            _reservationDAL = reservationDAL;
            _restaurantDAL = restaurantDAL;
            _tableDAL = tableDAL;
            _deviceTokenDAL = deviceTokenDAL;
            _fcmService = fcmService;
            _emailNotificationService = emailNotificationService ?? NullEmailNotificationService.Instance;
            _hubContext = hubContext;
            _closedDayExceptionDAL = closedDayExceptionDAL;
            _logger = logger ?? NullLogger<ReservationController>.Instance;
        }

        /// <summary>
        /// Diffuse un evenement temps reel sans jamais faire echouer l'operation metier.
        /// A ce stade la reservation est deja committee en base : une panne du hub ne doit
        /// pas transformer une operation reussie en HTTP 500.
        /// </summary>
        private async Task NotifyGroupAsync(string group, string method, object? payload)
        {
            try
            {
                await _hubContext.Clients.Group(group).SendAsync(method, payload);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "SignalR: echec de l'envoi de {Method} au groupe {Group}.", method, group);
            }
        }

        private Task NotifyAvailabilityAsync(long? restaurantId) => restaurantId.HasValue
            ? NotifyGroupAsync(ReservationHub.AVAILABILITY_GROUP_PREFIX + restaurantId.Value,
                ReservationHub.SEND_AT_ReceiveAvailabilityChanged, new { restaurantId = restaurantId.Value })
            : Task.CompletedTask;

        [Authorize]
        [HttpGet("")]
        public async Task<ActionResult<IEnumerable<ReservationOut>>> GetReservations([FromQuery] SearchReservations searchReservations)
        {
            try
            {
                var idUserToken = _currentUser.GetUserId(User);

                if (searchReservations == null || searchReservations.restaurantId == null)
                    return BadRequest("Le restaurant est obligatoire.");

                var restaurant = await _restaurantDAL.GetRestaurantById(searchReservations.restaurantId.Value);
                if (restaurant == null)
                    return NotFound("Restaurant introuvable.");

                if (restaurant.UserId != idUserToken)
                    return Forbid();

                var resultes = await _reservationDAL.GetReservations(searchReservations);

                if (resultes == null)
                    return NotFound("Aucune réservation trouvée.");

                return Ok(resultes);
            }
            catch (ReservationRuleException e)
            {
                return Conflict(e.Message);
            }

        }

        [Authorize]
        [HttpGet("availability")]
        public async Task<ActionResult<IEnumerable<ReservationAvailabilityOut>>> GetAvailability([FromQuery] SearchReservationAvailability searchReservations)
        {
            if (searchReservations.restaurantId == null)
                return BadRequest("Le restaurant est obligatoire.");

            if (!searchReservations.IsValid)
                return BadRequest("Une journée unique est obligatoire (minDate = maxDate).");

            var restaurant = await _restaurantDAL.GetRestaurantById(searchReservations.restaurantId.Value);
            if (restaurant == null)
                return NotFound("Restaurant introuvable.");

            var availability = await _reservationDAL.GetAvailability(searchReservations);
            return Ok(availability);
        }
        
        [Authorize]
        [HttpGet("My")]
        public async Task<ActionResult<ReservationOut>> GetMyReservations([FromQuery] SearchReservations searchReservations)
        {
            try
            {
                var idUserToken = _currentUser.GetUserId(User);

                if (searchReservations == null)
                {
                    return BadRequest();
                }
                searchReservations.IdUser = idUserToken;
                var resultes = await _reservationDAL.GetReservations(searchReservations);

                if (resultes == null)
                    return NotFound("Aucune réservation trouvée.");

                return Ok(resultes);
            }
            catch (ReservationRuleException e)
            {
                return Conflict(e.Message);
            }

        }

        [Authorize]
        [HttpGet("{id:long}")]
        public async Task<ActionResult<ReservationOut>> GetReservationById(long id)
        {
            try
            {
                var idUserToken = _currentUser.GetUserId(User);

                var reservation = await _reservationDAL.GetMyReservationById(id);
                if (reservation == null)
                    return NotFound("Aucune réservation trouvée.");

                if (reservation.UserId == idUserToken)
                    return Ok(reservation);

                if (reservation.RestaurantId == null)
                    return NotFound("Le restaurant de cette réservation n'existe plus.");

                var restaurant = await _restaurantDAL.GetRestaurantById(reservation.RestaurantId.Value);
                if (restaurant == null)
                    return NotFound("Aucune Restaurant trouvée.");

                if (restaurant.UserId != idUserToken)
                    return Forbid();

                return Ok(reservation);
            }
            catch (ReservationRuleException e)
            {
                return Conflict(e.Message);
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<ReservationOut>> CreateReservation([FromBody] ReservationIn reservation)
        {
            try
            {
                var idUserToken = _currentUser.GetUserId(User);

                if (reservation == null)
                    return BadRequest("Données de réservation invalides.");

                if (reservation.TableId == null || reservation.RestaurantId == null)
                    return BadRequest("La table et le restaurant sont obligatoires.");

                if (reservation.NumberOfPeople <= 0)
                    return BadRequest("Le nombre de personnes doit être supérieur à zéro.");

                var table = await _tableDAL.GetTablesById(reservation.TableId.Value);
                if (table == null)
                    return NotFound("La table n'existe pas.");

                if (table.RestaurantId != reservation.RestaurantId.Value)
                    return BadRequest("La table ne fait pas partie de ce restaurant.");

                if (table.NumberOfSeats < reservation.NumberOfPeople)
                    return BadRequest("La table ne contient pas assez de places.");


                var restaurant = await _restaurantDAL.GetRestaurantById(reservation.RestaurantId.Value);
                if (restaurant == null)
                    return NotFound("Aucune Restaurant trouvée.");

                if (_closedDayExceptionDAL != null)
                {
                    var closedDays = await _closedDayExceptionDAL.GetByRestaurantAsync(reservation.RestaurantId.Value);
                    var requestedDay = reservation.ReservationDate.Date;
                    if (closedDays.Any(closedDay =>
                        requestedDay >= closedDay.ExceptionDateBegin.Date &&
                        requestedDay <= closedDay.ExceptionDateEnd.Date))
                    {
                        return Conflict("Le restaurant est fermé à cette date.");
                    }
                }

                reservation.UserId = idUserToken;
                reservation.Status = restaurant.IsAutoValidateReservation
                    ? ReservationStatus.Validee
                    : ReservationStatus.EnAttente;
                var created = await _reservationDAL.CreateReservationAsync(reservation);

                // Envoi en temps réel via SignalR
                await NotifyGroupAsync(ReservationHub.RESTAURANT_GROUP_PREFIX + reservation.RestaurantId.Value,
                                       ReservationHub.SEND_AT_ReceiveReservationCreated, JsonSerializer.Serialize(created));

                await NotifyGroupAsync(ReservationHub.USER_GROUP_PREFIX + created.UserId!.Value,
                                       ReservationHub.SEND_AT_ReceiveReservationCreated, JsonSerializer.Serialize(created));


                if (reservation.Status == ReservationStatus.Validee)
                {
                    // Envoi en temps réel via SignalR
                    await NotifyGroupAsync(ReservationHub.RESTAURANT_GROUP_PREFIX + restaurant.Id,
                                           ReservationHub.SEND_AT_ReceiveReservationUpdateStatus, JsonSerializer.Serialize(created));

                    await NotifyGroupAsync(ReservationHub.USER_GROUP_PREFIX + created.UserId!.Value,
                                           ReservationHub.SEND_AT_ReceiveReservationUpdateStatus, JsonSerializer.Serialize(created));

                }

                await NotifyAvailabilityAsync(created.RestaurantId);
                return Ok(created);
            }
            catch (ReservationConflictException e)
            {
                return Conflict(e.Message);
            }
            catch (ReservationRuleException e)
            {
                return Conflict(e.Message);
            }
            
        }

        [Authorize]
        [HttpPost("Restaurant/{restaurantId}/Quick")]
        public async Task<ActionResult<ReservationOut>> CreateQuickReservation(long restaurantId, [FromBody] QuickReservationIn reservation)
        {
            try
            {
                var idUserToken = _currentUser.GetUserId(User);

                if (reservation == null)
                    return BadRequest("Données de réservation invalides.");

                if (restaurantId <= 0 || reservation.TableId <= 0)
                    return BadRequest("La table et le restaurant sont obligatoires.");

                if (reservation.NumberOfPeople <= 0)
                    return BadRequest("Le nombre de personnes doit être supérieur à zéro.");

                var guestName = reservation.GuestName?.Trim();
                if (string.IsNullOrWhiteSpace(guestName))
                    return BadRequest("Le nom du client est obligatoire.");

                var restaurant = await _restaurantDAL.GetRestaurantById(restaurantId);
                if (restaurant == null)
                    return NotFound("Le restaurant n'existe pas.");

                if (restaurant.UserId != idUserToken)
                    return Forbid();

                if (_closedDayExceptionDAL != null)
                {
                    var closedDays = await _closedDayExceptionDAL.GetByRestaurantAsync(restaurantId);
                    var requestedDay = reservation.ReservationDate.Date;
                    if (closedDays.Any(closedDay =>
                        requestedDay >= closedDay.ExceptionDateBegin.Date &&
                        requestedDay <= closedDay.ExceptionDateEnd.Date))
                    {
                        return Conflict("Le restaurant est fermé à cette date.");
                    }
                }

                var table = await _tableDAL.GetTablesById(reservation.TableId);
                if (table == null)
                    return NotFound("La table n'existe pas.");

                if (table.RestaurantId != restaurantId)
                    return BadRequest("La table ne fait pas partie de ce restaurant.");

                if (table.NumberOfSeats < reservation.NumberOfPeople)
                    return BadRequest("La table ne contient pas assez de places.");


                var guestPhone = string.IsNullOrWhiteSpace(reservation.GuestPhone)
                    ? null
                    : reservation.GuestPhone.Trim();

                var input = new ReservationIn
                {
                    UserId = null,
                    TableId = reservation.TableId,
                    RestaurantId = restaurantId,
                    ReservationDate = reservation.ReservationDate,
                    NumberOfPeople = reservation.NumberOfPeople,
                    SpecialRequest = reservation.SpecialRequest,
                    GuestName = guestName,
                    GuestPhone = guestPhone,
                    Status = ReservationStatus.Validee
                };

                var created = await _reservationDAL.CreateReservationAsync(input);

                await NotifyGroupAsync(ReservationHub.RESTAURANT_GROUP_PREFIX + restaurantId,
                                       ReservationHub.SEND_AT_ReceiveReservationCreated, JsonSerializer.Serialize(created));

                await NotifyAvailabilityAsync(created.RestaurantId);
                return Ok(created);
            }
            catch (ReservationConflictException e)
            {
                return Conflict(e.Message);
            }
            catch (ReservationRuleException e)
            {
                return Conflict(e.Message);
            }
        }

        [Authorize]
        [HttpPut("{id}/Status")]
        public async Task<ActionResult<ReservationOut>> UpdateReservationStatus(long id, [FromQuery]ReservationStatus reservationStatus)
        {
            try
            {
                var idUserToken = _currentUser.GetUserId(User);

                var Reservation = await _reservationDAL.GetMyReservationById(id);
                if (Reservation == null)
                    return NotFound("Aucune réservation trouvée.");

                if (Reservation.RestaurantId == null)
                    return NotFound("Le restaurant de cette réservation n'existe plus.");

                var Restaurant = await _restaurantDAL.GetRestaurantById(Reservation.RestaurantId.Value);
                if (Restaurant == null)
                    return NotFound("Aucune Restaurant trouvée.");
                if (Restaurant.UserId != idUserToken && Reservation.UserId != idUserToken)
                {
                    return Forbid();
                }

                var isRestaurantOwner = Restaurant.UserId == idUserToken;
                var transitionAllowed = isRestaurantOwner
                    ? (Reservation.Status == ReservationStatus.EnAttente &&
                       reservationStatus is ReservationStatus.Validee or ReservationStatus.AnnuleeParResto) ||
                      (Reservation.Status == ReservationStatus.Validee &&
                       reservationStatus is ReservationStatus.Finie or ReservationStatus.AnnuleeParResto)
                    : Reservation.Status == ReservationStatus.Validee &&
                      reservationStatus == ReservationStatus.AnnuleeParClient;

                if (!transitionAllowed)
                    return Conflict("Cette transition de statut n'est pas autorisée.");

                var resultes = await _reservationDAL.UpdateReservationStatus(id, reservationStatus, Reservation.Status);
                if (resultes == null) return Conflict("La réservation a été modifiée.");

                // Envoi en temps réel via SignalR
                await NotifyGroupAsync(ReservationHub.RESTAURANT_GROUP_PREFIX + Restaurant.Id,
                                       ReservationHub.SEND_AT_ReceiveReservationUpdateStatus, JsonSerializer.Serialize(resultes));

                if (Reservation.UserId != null)
                {
                    await NotifyGroupAsync(ReservationHub.USER_GROUP_PREFIX + Reservation.UserId.Value,
                                           ReservationHub.SEND_AT_ReceiveReservationUpdateStatus, JsonSerializer.Serialize(resultes));
                }



                await NotifyAvailabilityAsync(resultes.RestaurantId);
                return Ok(resultes);
            }
            catch (ReservationConflictException e)
            {
                return Conflict(e.Message);
            }
            catch (ReservationRuleException e)
            {
                return Conflict(e.Message);
            }

        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult<bool>> Delete(long id)
        {
            try
            {
                var idUserToken = _currentUser.GetUserId(User);

                var reservation = await _reservationDAL.GetMyReservationById(id);
                if (reservation == null)
                    return NotFound("Réservation introuvable.");

                if (reservation.UserId != idUserToken)
                    return Forbid();

                if (reservation.Status != ReservationStatus.EnAttente)
                    return Conflict("Seule une demande en attente peut être supprimée.");

                var deleted = await _reservationDAL.Delete(id, reservation.Status);
                if (deleted == null )
                    return Conflict("La réservation a été modifiée.");

                // Envoi en temps réel via SignalR
                if (deleted.RestaurantId != null)
                {
                    await NotifyGroupAsync(ReservationHub.RESTAURANT_GROUP_PREFIX + deleted.RestaurantId.Value,
                                           ReservationHub.SEND_AT_ReceiveReservationDeleted, id);
                }

                if (deleted.UserId != null)
                {
                    await NotifyGroupAsync(ReservationHub.USER_GROUP_PREFIX + deleted.UserId.Value,
                                           ReservationHub.SEND_AT_ReceiveReservationDeleted, id);
                }



                await NotifyAvailabilityAsync(deleted.RestaurantId);
                return Ok(true);
            }
            catch (ReservationConflictException e)
            {
                return Conflict(e.Message);
            }
            catch (ReservationRuleException e)
            {
                return Conflict(e.Message);
            }
        }
    }
}

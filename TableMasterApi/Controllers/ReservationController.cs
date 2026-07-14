using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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

        private readonly JwtService _jwtService;
        private readonly FcmService _fcmService;
        private readonly IEmailNotificationService _emailNotificationService;

        private readonly IHubContext<ReservationHub> _hubContext;

        public ReservationController(
            IReservationDAL reservationDAL,
            IRestaurantDAL restaurantDAL,
            ITableDAL tableDAL,
            IDeviceTokenDAL deviceTokenDAL,
            JwtService jwtService,
            IHubContext<ReservationHub> hubContext,
            FcmService fcmService,
            IEmailNotificationService? emailNotificationService = null,
            IClosedDayExceptionDAL? closedDayExceptionDAL = null)
        {
            _jwtService = jwtService;
            _reservationDAL = reservationDAL;
            _restaurantDAL = restaurantDAL;
            _tableDAL = tableDAL;
            _deviceTokenDAL = deviceTokenDAL;
            _fcmService = fcmService;
            _emailNotificationService = emailNotificationService ?? NullEmailNotificationService.Instance;
            _hubContext = hubContext;
            _closedDayExceptionDAL = closedDayExceptionDAL;
        }

        private async Task SendPushNotificationToUsers(IEnumerable<long> userIds, string title, string body, object? data = null)
        {
            var tokens = await _deviceTokenDAL.GetDeviceTokensForUserIdsAsync(userIds);
            if (tokens?.Any() != true)
            {
                return;
            }

            await _fcmService.SendNotificationAsync(tokens, title, body, data);
        }

        private async Task<ActionResult?> ValidateTableAvailabilityAsync(long tableId, DateTime reservationDate)
        {
            var search = new SearchReservations
            {
                tableId = tableId,
                minDate = DateOnly.FromDateTime(reservationDate),
                maxDate = DateOnly.FromDateTime(reservationDate),
                Statuses = new List<ReservationStatus> { ReservationStatus.Validee }
            };

            var existingReservations = await _reservationDAL.GetReservations(search);
            if (existingReservations != null && existingReservations.Count() > 0)
            {
                TimeSpan margin = TimeSpan.FromMinutes(90);

                foreach (var res in existingReservations)
                {
                    var diff = (reservationDate - res.ReservationDate).Duration();

                    if (diff < margin)
                    {
                        return Conflict($"La table est déjà occupée. Une marge de 1h30 est requise (conflit avec la réservation de {res.ReservationDate:HH:mm}).");
                    }
                }
            }

            return null;
        }

        [Authorize]
        [HttpGet("")]
        public async Task<ActionResult<IEnumerable<ReservationOut>>> GetReservations([FromQuery] SearchReservations searchReservations)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

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
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }

        }

        [Authorize]
        [HttpGet("availability")]
        public async Task<ActionResult<IEnumerable<ReservationAvailabilityOut>>> GetAvailability([FromQuery] SearchReservations searchReservations)
        {
            if (searchReservations.restaurantId == null)
                return BadRequest("Le restaurant est obligatoire.");

            if (searchReservations.minDate == null || searchReservations.maxDate == null)
                return BadRequest("Une plage de dates est obligatoire.");

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
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

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
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }

        }

        [Authorize]
        [HttpGet("{id:long}")]
        public async Task<ActionResult<ReservationOut>> GetReservationById(long id)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                var reservation = await _reservationDAL.GetMyReservationById(id);
                if (reservation == null)
                    return NotFound("Aucune réservation trouvée.");

                if (reservation.UserId == idUserToken)
                    return Ok(reservation);

                if (reservation.RestaurantId == null)
                    return Unauthorized();

                var restaurant = await _restaurantDAL.GetRestaurantById(reservation.RestaurantId.Value);
                if (restaurant == null)
                    return NotFound("Aucune Restaurant trouvée.");

                if (restaurant.UserId != idUserToken)
                    return Forbid();

                return Ok(reservation);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<ReservationOut>> CreateReservation([FromBody] ReservationIn reservation)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

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

                var availabilityError = await ValidateTableAvailabilityAsync(reservation.TableId.Value, reservation.ReservationDate);
                if (availabilityError != null)
                    return availabilityError;

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
                await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + reservation.RestaurantId.Value)
                                          .SendAsync(ReservationHub.SEND_AT_ReceiveReservationCreated, JsonSerializer.Serialize(created));

                await _hubContext.Clients.Group(ReservationHub.USER_GROUP_PREFIX + created.UserId!.Value)
                              .SendAsync(ReservationHub.SEND_AT_ReceiveReservationCreated, JsonSerializer.Serialize(created));

                // Envoi de push si l'application est fermée
                await SendPushNotificationToUsers(new[] { restaurant.UserId },
                    "Nouvelle réservation",
                    $"Nouvelle réservation pour le {reservation.ReservationDate:dd/MM/yyyy HH:mm}.",
                    new { type = "reservation_created", reservationId = created.Id });

                await SendPushNotificationToUsers(new[] { created.UserId!.Value },
                    "Réservation enregistrée",
                    $"Votre réservation du {created.ReservationDate:dd/MM/yyyy HH:mm} a été créée.",
                    new { type = "reservation_created", reservationId = created.Id });

                await _emailNotificationService.SendReservationCreatedAsync(created, restaurant);

                if (reservation.Status == ReservationStatus.Validee)
                {
                    // Envoi en temps réel via SignalR
                    await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + restaurant.Id)
                                              .SendAsync(ReservationHub.SEND_AT_ReceiveReservationUpdateStatus, JsonSerializer.Serialize(created));

                    await _hubContext.Clients.Group(ReservationHub.USER_GROUP_PREFIX + created.UserId!.Value)
                              .SendAsync(ReservationHub.SEND_AT_ReceiveReservationUpdateStatus, JsonSerializer.Serialize(created));

                    await SendPushNotificationToUsers(new[] { restaurant.UserId },
                        "Réservation validée",
                        $"Réservation validée pour le {created.ReservationDate:dd/MM/yyyy HH:mm}.",
                        new { type = "reservation_status_updated", reservationId = created.Id, status = created.Status.ToString() });

                    await SendPushNotificationToUsers(new[] { created.UserId!.Value },
                        "Votre réservation est validée",
                        $"Votre réservation du {created.ReservationDate:dd/MM/yyyy HH:mm} a été validée.",
                        new { type = "reservation_status_updated", reservationId = created.Id, status = created.Status.ToString() });
                }

                return Ok(created);
            }
            catch (ReservationConflictException e)
            {
                return Conflict(e.Message);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
            
        }

        [Authorize]
        [HttpPost("Restaurant/{restaurantId}/Quick")]
        public async Task<ActionResult<ReservationOut>> CreateQuickReservation(long restaurantId, [FromBody] QuickReservationIn reservation)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

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

                var availabilityError = await ValidateTableAvailabilityAsync(reservation.TableId, reservation.ReservationDate);
                if (availabilityError != null)
                    return availabilityError;

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

                await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + restaurantId)
                                          .SendAsync(ReservationHub.SEND_AT_ReceiveReservationCreated, JsonSerializer.Serialize(created));

                return Ok(created);
            }
            catch (ReservationConflictException e)
            {
                return Conflict(e.Message);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [Authorize]
        [HttpPut("{id}/Status")]
        public async Task<ActionResult<ReservationOut>> UpdateReservationStatus(long id, [FromQuery]ReservationStatus reservationStatus)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

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

                var resultes = await _reservationDAL.UpdateReservationStatus(id, reservationStatus);

                // Envoi en temps réel via SignalR
                await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + Restaurant.Id)
                                          .SendAsync(ReservationHub.SEND_AT_ReceiveReservationUpdateStatus, JsonSerializer.Serialize(resultes));

                if (Reservation.UserId != null)
                {
                    await _hubContext.Clients.Group(ReservationHub.USER_GROUP_PREFIX + Reservation.UserId.Value)
                                  .SendAsync(ReservationHub.SEND_AT_ReceiveReservationUpdateStatus, JsonSerializer.Serialize(resultes));
                }

                await SendPushNotificationToUsers(new[] { Restaurant.UserId },
                    "Statut de réservation mis à jour",
                    $"La réservation #{id} est maintenant '{reservationStatus}'.",
                    new { type = "reservation_status_updated", reservationId = id, status = reservationStatus.ToString() });

                if (Reservation.UserId != null)
                {
                    await SendPushNotificationToUsers(new[] { Reservation.UserId.Value },
                        "Votre réservation a changé",
                        $"Votre réservation du {Reservation.ReservationDate:dd/MM/yyyy HH:mm} est maintenant '{reservationStatus}'.",
                        new { type = "reservation_status_updated", reservationId = id, status = reservationStatus.ToString() });
                }

                if (resultes != null)
                {
                    await _emailNotificationService.SendReservationStatusUpdatedAsync(resultes, Restaurant, reservationStatus);
                }

                return Ok(resultes);
            }
            catch (ReservationConflictException e)
            {
                return Conflict(e.Message);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }

        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult<bool>> Delete(long id)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                var reservation = await _reservationDAL.GetMyReservationById(id);
                if (reservation == null)
                    return NotFound("Réservation introuvable.");

                if (reservation.UserId != idUserToken)
                    return Forbid();

                if (reservation.Status != ReservationStatus.EnAttente)
                    return Conflict("Seule une demande en attente peut être supprimée.");

                var deleted = await _reservationDAL.Delete(id);
                if (deleted == null )
                    return NotFound("Restaurant not found.");

                // Envoi en temps réel via SignalR
                if (deleted.RestaurantId != null)
                {
                    await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + deleted.RestaurantId.Value)
                                              .SendAsync(ReservationHub.SEND_AT_ReceiveReservationDeleted, id);
                }

                if (deleted.UserId != null)
                {
                    await _hubContext.Clients.Group(ReservationHub.USER_GROUP_PREFIX + deleted.UserId.Value)
                                              .SendAsync(ReservationHub.SEND_AT_ReceiveReservationDeleted, id);
                }

                var restaurant = deleted.RestaurantId == null
                    ? null
                    : await _restaurantDAL.GetRestaurantById(deleted.RestaurantId.Value);
                if (restaurant != null)
                {
                    await SendPushNotificationToUsers(new[] { restaurant.UserId },
                        "Réservation annulée",
                        $"La réservation du {deleted.ReservationDate:dd/MM/yyyy HH:mm} a été annulée.",
                        new { type = "reservation_cancelled", reservationId = id });
                }

                if (deleted.UserId != null)
                {
                    await SendPushNotificationToUsers(new[] { deleted.UserId.Value },
                        "Réservation annulée",
                        $"Votre réservation du {deleted.ReservationDate:dd/MM/yyyy HH:mm} a été annulée.",
                        new { type = "reservation_cancelled", reservationId = id });
                }

                await _emailNotificationService.SendReservationCancelledAsync(deleted, restaurant);

                return Ok(deleted != null);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Asp.Versioning;
using TableMasterApi.DAL;
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
        private readonly ReservationDAL _reservationDAL;
        private readonly RestaurantDAL _restaurantDAL;
        private readonly DeviceTokenDAL _deviceTokenDAL;

        private readonly JwtService _jwtService;
        private readonly FcmService _fcmService;

        private readonly IHubContext<ReservationHub> _hubContext;

        public ReservationController(IOptions<ConfigPerso> config, JwtService jwtService, IHubContext<ReservationHub> hubContext, DeviceTokenDAL deviceTokenDAL, FcmService fcmService)
        {
            _jwtService = jwtService;
            _reservationDAL = new ReservationDAL(config.Value);
            _restaurantDAL = new RestaurantDAL(config.Value);
            _deviceTokenDAL = deviceTokenDAL;
            _fcmService = fcmService;
            _hubContext = hubContext;
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

        [Authorize]
        [HttpGet("")]
        public async Task<ActionResult<IEnumerable<ReservationOut>>> GetReservations([FromQuery] SearchReservations searchReservations)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (searchReservations == null)
                {
                    return BadRequest();
                }
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
        [HttpPost]
        public async Task<ActionResult<ReservationOut>> CreateReservation([FromBody] ReservationIn reservation)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (reservation == null)
                    return BadRequest("Données de réservation invalides.");

                var search = new SearchReservations
                {
                    tableId = reservation.TableId,
                    minDate = DateOnly.FromDateTime(reservation.ReservationDate),
                    maxDate = DateOnly.FromDateTime(reservation.ReservationDate),
                    Statuses = new List<ReservationStatus> { ReservationStatus.Validee }
                };

                // On récupère les réservations existantes via ta méthode DAL
                var existingReservations = await _reservationDAL.GetReservations(search);

                if (existingReservations != null && existingReservations.Count() > 0)
                {
                    // On définit la marge de sécurité (90 minutes)
                    TimeSpan margin = TimeSpan.FromMinutes(90);

                    foreach (var res in existingReservations)
                    {
                        // Calcul de l'écart entre la réservation existante et la nouvelle demande
                        var diff = (reservation.ReservationDate - res.ReservationDate).Duration();

                        if (diff < margin)
                        {
                            return Conflict($"La table est déjà occupée. Une marge de 1h30 est requise (conflit avec la réservation de {res.ReservationDate:HH:mm}).");
                        }
                    }
                }

                var restaurant = await _restaurantDAL.GetRestaurantById(reservation.RestaurantId);
                if (restaurant == null)
                    return NotFound("Aucune Restaurant trouvée.");

                reservation.UserId = idUserToken;
                if (restaurant.IsAutoValidateReservation)
                {
                    reservation.Status = ReservationStatus.Validee;
                }
                var created = await _reservationDAL.CreateReservationAsync(reservation);

                // Envoi en temps réel via SignalR
                await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + reservation.RestaurantId)
                                          .SendAsync(ReservationHub.SEND_AT_ReceiveReservationCreated, JsonSerializer.Serialize(created));

                await _hubContext.Clients.Group(ReservationHub.USER_GROUP_PREFIX + created.UserId)
                              .SendAsync(ReservationHub.SEND_AT_ReceiveReservationCreated, JsonSerializer.Serialize(created));

                // Envoi de push si l'application est fermée
                await SendPushNotificationToUsers(new[] { restaurant.UserId },
                    "Nouvelle réservation",
                    $"Nouvelle réservation pour le {reservation.ReservationDate:dd/MM/yyyy HH:mm}.",
                    new { type = "reservation_created", reservationId = created.Id });

                await SendPushNotificationToUsers(new[] { created.UserId },
                    "Réservation enregistrée",
                    $"Votre réservation du {created.ReservationDate:dd/MM/yyyy HH:mm} a été créée.",
                    new { type = "reservation_created", reservationId = created.Id });

                if (reservation.Status == ReservationStatus.Validee)
                {
                    // Envoi en temps réel via SignalR
                    await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + restaurant.Id)
                                              .SendAsync(ReservationHub.SEND_AT_ReceiveReservationUpdateStatus, JsonSerializer.Serialize(created));

                    await _hubContext.Clients.Group(ReservationHub.USER_GROUP_PREFIX + created.UserId)
                              .SendAsync(ReservationHub.SEND_AT_ReceiveReservationUpdateStatus, JsonSerializer.Serialize(created));

                    await SendPushNotificationToUsers(new[] { restaurant.UserId },
                        "Réservation validée",
                        $"Réservation validée pour le {created.ReservationDate:dd/MM/yyyy HH:mm}.",
                        new { type = "reservation_status_updated", reservationId = created.Id, status = created.Status.ToString() });

                    await SendPushNotificationToUsers(new[] { created.UserId },
                        "Votre réservation est validée",
                        $"Votre réservation du {created.ReservationDate:dd/MM/yyyy HH:mm} a été validée.",
                        new { type = "reservation_status_updated", reservationId = created.Id, status = created.Status.ToString() });
                }

                return Ok(created);
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

                if (id == null)
                {
                    return BadRequest();
                }
                var Reservation = await _reservationDAL.GetMyReservationById(id);
                if (Reservation == null)
                    return NotFound("Aucune réservation trouvée.");

                var Restaurant = await _restaurantDAL.GetRestaurantById(Reservation.RestaurantId);
                if (Restaurant == null)
                    return NotFound("Aucune Restaurant trouvée.");
                if (Restaurant.UserId != idUserToken && Reservation.UserId != idUserToken)
                {
                    return Unauthorized();
                }

                var resultes = await _reservationDAL.updateReservationStatus(id, reservationStatus);

                // Envoi en temps réel via SignalR
                await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + Restaurant.Id)
                                          .SendAsync(ReservationHub.SEND_AT_ReceiveReservationUpdateStatus, JsonSerializer.Serialize(resultes));

                await _hubContext.Clients.Group(ReservationHub.USER_GROUP_PREFIX + Reservation.UserId)
                              .SendAsync(ReservationHub.SEND_AT_ReceiveReservationUpdateStatus, JsonSerializer.Serialize(resultes));

                await SendPushNotificationToUsers(new[] { Restaurant.UserId },
                    "Statut de réservation mis à jour",
                    $"La réservation #{id} est maintenant '{reservationStatus}'.",
                    new { type = "reservation_status_updated", reservationId = id, status = reservationStatus.ToString() });

                await SendPushNotificationToUsers(new[] { Reservation.UserId },
                    "Votre réservation a changé",
                    $"Votre réservation du {Reservation.ReservationDate:dd/MM/yyyy HH:mm} est maintenant '{reservationStatus}'.",
                    new { type = "reservation_status_updated", reservationId = id, status = reservationStatus.ToString() });

                return Ok(resultes);
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

                var deleted = await _reservationDAL.Delete(id);
                if (deleted == null )
                    return NotFound("Restaurant not found.");

                // Envoi en temps réel via SignalR
                await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + deleted.RestaurantId)
                                          .SendAsync(ReservationHub.SEND_AT_ReceiveReservationDeleted, id);
                await _hubContext.Clients.Group(ReservationHub.USER_GROUP_PREFIX + deleted.UserId)
                                          .SendAsync(ReservationHub.SEND_AT_ReceiveReservationDeleted, id);

                var restaurant = await _restaurantDAL.GetRestaurantById(deleted.RestaurantId);
                if (restaurant != null)
                {
                    await SendPushNotificationToUsers(new[] { restaurant.UserId },
                        "Réservation annulée",
                        $"La réservation du {deleted.ReservationDate:dd/MM/yyyy HH:mm} a été annulée.",
                        new { type = "reservation_cancelled", reservationId = id });
                }

                await SendPushNotificationToUsers(new[] { deleted.UserId },
                    "Réservation annulée",
                    $"Votre réservation du {deleted.ReservationDate:dd/MM/yyyy HH:mm} a été annulée.",
                    new { type = "reservation_cancelled", reservationId = id });

                return Ok(deleted != null);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }
    }
}

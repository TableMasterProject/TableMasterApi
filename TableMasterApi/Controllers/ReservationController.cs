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

        private readonly JwtService _jwtService;

        private readonly IHubContext<ReservationHub> _hubContext;

        public ReservationController(IOptions<ConfigPerso> config, JwtService jwtService, IHubContext<ReservationHub> hubContext)
        {
            _jwtService = jwtService;
            _reservationDAL = new ReservationDAL(config.Value);
            _restaurantDAL = new RestaurantDAL(config.Value);
            _hubContext = hubContext;
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

                // Envoi de la mise à jour à tous les clients connectés au restaurant concerné
                await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + reservation.RestaurantId)
                                          .SendAsync(ReservationHub.SEND_AT_ReceiveReservationCreated, JsonSerializer.Serialize(created));

                if (reservation.Status == ReservationStatus.Validee)
                {
                    // Envoi de la mise à jour à tous les clients connectés au restaurant concerné
                    await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + restaurant.Id)
                                              .SendAsync(ReservationHub.SEND_AT_ReceiveReservationValidate, JsonSerializer.Serialize(created));
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
                if (Restaurant.UserId != idUserToken)
                {
                    return Unauthorized();
                }

                var resultes = await _reservationDAL.updateReservationStatus(id, reservationStatus);

                // Envoi de la mise à jour à tous les clients connectés au restaurant concerné
                await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + Restaurant.Id)
                                          .SendAsync(ReservationHub.SEND_AT_ReceiveReservationValidate, JsonSerializer.Serialize(resultes));

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

                // Envoi de la mise à jour à tous les clients connectés au restaurant concerné
                await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + deleted.RestaurantId)
                                          .SendAsync(ReservationHub.SEND_AT_ReceiveReservationDeleted, id);

                return Ok(deleted!=null);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }
    }
}

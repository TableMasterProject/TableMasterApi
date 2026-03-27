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
        [HttpGet("Restaurant/{Id}")]
        public async Task<ActionResult<IEnumerable<ReservationOut>>> GetReservations(long Id, [FromQuery] DateOnly reservationDate, [FromQuery] long? tableId = null)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (Id == null || reservationDate == null)
                {
                    return BadRequest();
                }
                var resultes = await _reservationDAL.GetReservationsByRestaurantAsync(Id, reservationDate, tableId);

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
        [HttpGet("Restaurant/{Id}/Pending")]
        public async Task<ActionResult<IEnumerable<ReservationOut>>> GetPendingReservations(long Id, [FromQuery] long? tableId = null)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);
                
                var restaurant = await _restaurantDAL.GetRestaurantById(Id);
                if (restaurant == null) return NotFound("Restaurant non trouvé.");
                if (restaurant.UserId != idUserToken) return Unauthorized();

                var results = await _reservationDAL.GetPendingReservationsAsync(Id, tableId);
                return Ok(results);
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
                var resultes = await _reservationDAL.GetMyReservations(idUserToken, searchReservations);

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

                var restaurant = await _restaurantDAL.GetRestaurantById(reservation.RestaurantId);
                if (restaurant == null)
                    return NotFound("Aucune Restaurant trouvée.");

                reservation.UserId = idUserToken;
                reservation.IsValidate = restaurant.IsAutoValidateReservation;
                var created = await _reservationDAL.CreateReservationAsync(reservation);

                // Envoi de la mise à jour à tous les clients connectés au restaurant concerné
                await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + reservation.RestaurantId)
                                          .SendAsync(ReservationHub.SEND_AT_ReceiveReservationCreated, JsonSerializer.Serialize(created));

                if (reservation.IsValidate)
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
        [HttpGet("{id}/Validate")]
        public async Task<ActionResult<ReservationOut>> ValidateReservations(long id, [FromQuery]bool IsValidate)
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

                var resultes = await _reservationDAL.validateReservation(id, IsValidate);

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

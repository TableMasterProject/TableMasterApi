using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TableMasterApi.DAL;
using TableMasterApi.Hubs;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReservationController : ControllerBase
    {
        private readonly ReservationDAL _reservationDAL;

        private readonly JwtService _jwtService;

        private readonly IHubContext<ReservationHub> _hubContext;

        public ReservationController(IOptions<ConfigPerso> config, JwtService jwtService, IHubContext<ReservationHub> hubContext)
        {
            _jwtService = jwtService;
            _reservationDAL = new ReservationDAL(config.Value);
            _hubContext = hubContext;
        }

        [Authorize]
        [HttpGet("Restaurant/{Id}")]
        public async Task<ActionResult<ReservationOut>> GetReservations(long Id, [FromQuery] DateOnly reservationDate)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (Id == null || reservationDate == null)
                {
                    return BadRequest();
                }
                var reservations = await _reservationDAL.GetReservationsByRestaurantAsync(Id, reservationDate);

                if (reservations == null)
                    return NotFound("Aucune réservation trouvée.");

                return Ok(reservations);
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
                var reservations = await _reservationDAL.GetMyReservations(idUserToken, searchReservations);

                if (reservations == null)
                    return NotFound("Aucune réservation trouvée.");

                return Ok(reservations);
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

                var createdReservation = await _reservationDAL.CreateReservationAsync(idUserToken, reservation);

                // Envoi de la mise à jour à tous les clients connectés au restaurant concerné
                await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + reservation.RestaurantId)
                                          .SendAsync(ReservationHub.SEND_AT_ReceiveReservationCreated, JsonSerializer.Serialize(createdReservation));

                return Ok(createdReservation);
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

                var deleted = await _reservationDAL.Delete(idUserToken, id);
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

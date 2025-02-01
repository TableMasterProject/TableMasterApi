using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TableMasterApi.DAL;
using TableMasterApi.Hubs;
using TableMasterApi.Model;

namespace TableMasterApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReservationController : ControllerBase
    {
        private readonly ReservationDAL _reservationDAL;
        private readonly IHubContext<ReservationHub> _hubContext;

        public ReservationController(ReservationDAL reservationDAL, IHubContext<ReservationHub> hubContext)
        {
            _reservationDAL = reservationDAL;
            _hubContext = hubContext;
        }

        // Récupérer les réservations pour un restaurant à une date donnée
        [HttpGet("restaurant/{restaurantId}")]
        public async Task<ActionResult<ReservationOut>> GetReservations(long restaurantId, [FromQuery] DateOnly reservationDate)
        {
            if (restaurantId == null || reservationDate == null)
            {
                return BadRequest();
            }
            var reservations = await _reservationDAL.GetReservationsByRestaurantAsync(restaurantId, reservationDate);

            if (reservations == null)
                return NotFound("Aucune réservation trouvée.");

            return Ok(reservations);
        }

        // Créer une nouvelle réservation
        [HttpPost]
        public async Task<ActionResult<ReservationOut>> CreateReservation([FromBody] ReservationIn reservation)
        {
            if (reservation == null)
                return BadRequest("Données de réservation invalides.");

            var createdReservation = await _reservationDAL.CreateReservationAsync(reservation);

            // Envoi de la mise à jour à tous les clients connectés au restaurant concerné
            await _hubContext.Clients.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + reservation.RestaurantId)
                                      .SendAsync("ReceiveReservationUpdate", createdReservation);

            return Ok(createdReservation);
        }
    }
}

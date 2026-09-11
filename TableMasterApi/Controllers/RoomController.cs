using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [ApiController]
    public class RoomController : ControllerBase
    {
        private readonly IRoomDAL _roomDAL;
        private readonly IRestaurantDAL _restaurantDAL;
        private readonly ICurrentUserService _currentUser;
        private readonly IAvailabilityNotifier? _availability;

        public RoomController(IRoomDAL roomDAL, IRestaurantDAL restaurantDAL, JwtService jwtService, ICurrentUserService? currentUser = null, IAvailabilityNotifier? availability = null)
        {
            _roomDAL = roomDAL;
            _restaurantDAL = restaurantDAL;
            _currentUser = currentUser ?? new CurrentUserService();
            _availability = availability;
        }

        [Authorize]
        [HttpGet("Restaurant/{id}")]
        public async Task<ActionResult<IEnumerable<RestaurantRoomOut>>> GetRooms(long id)
        {
            var restaurant = await _restaurantDAL.GetRestaurantById(id);
            if (restaurant == null)
            {
                return NotFound("Le restaurant n'existe pas");
            }

            var rooms = await _roomDAL.GetRoomsByRestaurantAsync(id);
            return Ok(rooms);
        }

        [Authorize]
        [HttpPost("Restaurant/{id}")]
        public async Task<ActionResult<RestaurantRoomOut>> CreateRoom(long id, [FromBody] RestaurantRoomIn room)
        {
            var idUserToken = GetUserIdFromToken();
            var restaurant = await _restaurantDAL.GetRestaurantById(id);
            if (restaurant == null)
            {
                return NotFound("Le restaurant n'existe pas");
            }

            if (restaurant.UserId != idUserToken)
            {
                return Forbid();
            }

            var validation = ValidateRoom(room);
            if (validation != null)
            {
                return validation;
            }

            room.RestaurantId = id;
            var created = await _roomDAL.CreateRoomAsync(room);
            if (created != null && _availability != null) await _availability.NotifyAsync(room.RestaurantId);
            return Ok(created);
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult<RestaurantRoomOut>> UpdateRoom(long id, [FromBody] RestaurantRoomIn room)
        {
            var existing = await GetOwnedRoom(id);
            if (existing.Result != null)
            {
                return existing.Result;
            }

            var validation = ValidateRoom(room);
            if (validation != null)
            {
                return validation;
            }

            room.RestaurantId = existing.Value!.RestaurantId;
            var updated = await _roomDAL.UpdateRoomAsync(id, room);
            if (updated != null && _availability != null) await _availability.NotifyAsync(room.RestaurantId);
            return Ok(updated);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult<bool>> DeleteRoom(long id)
        {
            var existing = await GetOwnedRoom(id);
            if (existing.Result != null)
            {
                return existing.Result;
            }

            if (await _roomDAL.RoomHasTablesAsync(id))
            {
                return Conflict("Impossible de supprimer une salle qui contient encore des tables.");
            }

            var deleted = await _roomDAL.DeleteRoomAsync(id);
            if (deleted && _availability != null) await _availability.NotifyAsync(existing.Value!.RestaurantId);
            return Ok(deleted);
        }

        [Authorize]
        [HttpPut("{id}/Layout")]
        public async Task<ActionResult<RestaurantRoomLayoutOut>> SaveLayout(long id, [FromBody] RestaurantRoomLayoutIn layout)
        {
            if (layout == null)
            {
                return BadRequest("Données de plan invalides.");
            }

            var existing = await GetOwnedRoom(id);
            if (existing.Result != null)
            {
                return existing.Result;
            }

            var validation = ValidateRoom(layout.Room);
            if (validation != null)
            {
                return validation;
            }

            layout.Room.RestaurantId = existing.Value!.RestaurantId;
            var saved = await _roomDAL.SaveLayoutAsync(id, layout);
            if (saved == null)
            {
                return NotFound("La salle n'existe pas");
            }

            if (_availability != null) await _availability.NotifyAsync(existing.Value!.RestaurantId);
            return Ok(saved);
        }

        private long GetUserIdFromToken()
        {
            return _currentUser.GetUserId(User);
        }

        private async Task<ActionResult<RestaurantRoomOut>> GetOwnedRoom(long roomId)
        {
            var idUserToken = GetUserIdFromToken();
            var room = await _roomDAL.GetRoomByIdAsync(roomId);
            if (room == null)
            {
                return NotFound("La salle n'existe pas");
            }

            var restaurant = await _restaurantDAL.GetRestaurantById(room.RestaurantId);
            if (restaurant == null)
            {
                return NotFound("Le restaurant n'existe pas");
            }

            if (restaurant.UserId != idUserToken)
            {
                return Forbid();
            }

            return room;
        }

        private BadRequestObjectResult? ValidateRoom(RestaurantRoomIn room)
        {
            if (room == null)
            {
                return BadRequest("Données de salle invalides.");
            }

            if (string.IsNullOrWhiteSpace(room.Name))
            {
                return BadRequest("Le nom de la salle est obligatoire.");
            }

            if (room.BoundaryPoints == null || room.BoundaryPoints.Count < 3)
            {
                return BadRequest("La salle doit contenir au moins trois points.");
            }

            if (room.BoundaryPoints.Any(point => point.X < 0 || point.X > 1 || point.Y < 0 || point.Y > 1))
            {
                return BadRequest("Les points de salle doivent être normalisés entre 0 et 1.");
            }

            return null;
        }
    }
}

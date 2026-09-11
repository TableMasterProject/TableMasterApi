using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Hubs;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [ApiController]
    public class TableController : ControllerBase
    {
        private readonly ITableDAL _tableDAL;
        private readonly IRoomDAL? _rooms;
        private readonly IRestaurantDAL _restaurantDAL;

        private readonly ICurrentUserService _currentUser;
        private readonly IAvailabilityNotifier? _availability;

        public TableController(ITableDAL tableDAL, IRestaurantDAL restaurantDAL, JwtService jwtService, ICurrentUserService? currentUser = null, IAvailabilityNotifier? availability = null, IRoomDAL? rooms = null)
        {
            _currentUser = currentUser ?? new CurrentUserService();
            _availability = availability;
            _rooms = rooms;
            _tableDAL = tableDAL;
            _restaurantDAL = restaurantDAL;
        }

        [Authorize]
        [HttpGet("Restaurant/{Id}")]
        public async Task<ActionResult<TableEntityOut>> GetTables(long Id)
        {
            var idUserToken = _currentUser.GetUserId(User);

            var tables = await _tableDAL.GetTablesByRestaurantAsync(Id);

            if (tables == null)
                return NotFound("Aucune Table trouvée.");

            return Ok(tables);

        }

        [Authorize]
        [HttpPost("Restaurant/{Id}")]
        public async Task<ActionResult<TableEntityOut>> CreateTable(long Id, [FromBody] TableEntityIn table)
        {
            var idUserToken = _currentUser.GetUserId(User);

            if (table == null)
                return BadRequest("Données de réservation invalides.");

            var restaurant = await _restaurantDAL.GetRestaurantById(Id);

            if (restaurant == null)
                return NotFound("Le restaurant n'existe pas");
            if (restaurant.UserId != idUserToken)
                return Forbid();

            table.RestaurantId = Id;

            var validation = await ValidateRoomAsync(table.RoomId, Id);
            if (validation != null) return validation;
            var created = await _tableDAL.CreateTable(table);
            if (created != null && _availability != null) await _availability.NotifyAsync(Id);

            return Ok(created);

        }
        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult<TableEntityOut>> UpdateTable(long id, [FromBody] TableEntityIn table)
        {
            var idUserToken = _currentUser.GetUserId(User);

            if (table == null)
                return BadRequest("Données de réservation invalides.");

            var tableBefore = await _tableDAL.GetTablesById(id);
            if (tableBefore == null )
                return NotFound("La table n'existe pas");

            var restaurant = await _restaurantDAL.GetRestaurantById(tableBefore.RestaurantId);

            if (restaurant == null)
                return NotFound("Le restaurant n'existe pas");
            if (restaurant.UserId != idUserToken)
                return Forbid();

            table.RestaurantId = tableBefore.RestaurantId;
            var validation = await ValidateRoomAsync(table.RoomId, tableBefore.RestaurantId);
            if (validation != null) return validation;
            var created = await _tableDAL.UpdateTable(id, table);
            if (created != null && _availability != null) await _availability.NotifyAsync(tableBefore.RestaurantId);

            return Ok(created);

        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult<bool>> Delete(long id)
        {
            var idUserToken = _currentUser.GetUserId(User);

            var tableBefore = await _tableDAL.GetTablesById(id);
            if (tableBefore == null)
                return NotFound("La table n'existe pas");

            var restaurant = await _restaurantDAL.GetRestaurantById(tableBefore.RestaurantId);

            if (restaurant == null)
                return NotFound("Le restaurant n'existe pas");
            if (restaurant.UserId != idUserToken)
                return Forbid();

            var deleted = await _tableDAL.DeleteTable(id);
            if (deleted && _availability != null) await _availability.NotifyAsync(tableBefore.RestaurantId);

            return Ok(deleted);
        }

        [Authorize]
        [HttpPost("Restaurant/{Id}/Bulk")]
        public async Task<ActionResult<IEnumerable<TableEntityOut>>> ReplaceTables(long Id, [FromBody] List<TableEntityIn> tables)
        {
            var idUserToken = _currentUser.GetUserId(User);

            if (tables == null || !tables.Any())
                return BadRequest("La liste des tables est vide.");

            // Vérification de sécurité : le restaurant appartient-il à l'utilisateur ?
            var restaurant = await _restaurantDAL.GetRestaurantById(Id);
            if (restaurant == null)
                return NotFound("Le restaurant n'existe pas");

            if (restaurant.UserId != idUserToken)
                return Forbid();

            // Appel de la méthode DAL
            foreach (var table in tables)
            {
                var validation = await ValidateRoomAsync(table.RoomId, Id);
                if (validation != null) return validation;
            }
            var results = await _tableDAL.ReplaceTablesAsync(Id, tables);
            if (_availability != null) await _availability.NotifyAsync(Id);

            return Ok(results);
        }
        private async Task<ActionResult?> ValidateRoomAsync(long? roomId, long restaurantId)
        {
            if (roomId == null) return null;
            var room = _rooms == null ? null : await _rooms.GetRoomByIdAsync(roomId.Value);
            return room?.RestaurantId == restaurantId ? null : BadRequest("La salle ne fait pas partie de ce restaurant.");
        }
    }
}

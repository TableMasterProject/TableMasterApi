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
        private readonly IRestaurantDAL _restaurantDAL;

        private readonly JwtService _jwtService;

        public TableController(ITableDAL tableDAL, IRestaurantDAL restaurantDAL, JwtService jwtService)
        {
            _jwtService = jwtService;
            _tableDAL = tableDAL;
            _restaurantDAL = restaurantDAL;
        }

        [Authorize]
        [HttpGet("Restaurant/{Id}")]
        public async Task<ActionResult<TableEntityOut>> GetTables(long Id)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (Id == null)
                {
                    return BadRequest();
                }
                var tables = await _tableDAL.GetTablesByRestaurantAsync(Id);

                if (tables == null)
                    return NotFound("Aucune Table trouvée.");

                return Ok(tables);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }

        }

        [Authorize]
        [HttpPost("Restaurant/{Id}")]
        public async Task<ActionResult<TableEntityOut>> CreateTable(long Id, [FromBody] TableEntityIn table)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (table == null)
                    return BadRequest("Données de réservation invalides.");

                var restaurant = await _restaurantDAL.GetRestaurantById(Id);

                if (restaurant == null)
                    return NotFound("Le restaurant n'existe pas");
                if (restaurant.UserId != idUserToken)
                    return Unauthorized("Vous n'êtes pas autorisé à modifier cette table");

                table.RestaurantId = Id;

                var created = await _tableDAL.CreateTable(table);

                return Ok(created);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }

        }
        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult<TableEntityOut>> UpdateTable(long id, [FromBody] TableEntityIn table)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (table == null)
                    return BadRequest("Données de réservation invalides.");

                var tableBefore = await _tableDAL.GetTablesById(id);
                if (tableBefore == null )
                    return NotFound("La table n'existe pas");

                var restaurant = await _restaurantDAL.GetRestaurantById(tableBefore.RestaurantId);

                if (restaurant == null)
                    return NotFound("Le restaurant n'existe pas");
                if (restaurant.UserId != idUserToken)
                    return Unauthorized("Vous n'êtes pas autorisé à modifier cette table");

                var created = await _tableDAL.UpdateTable(id, table);

                return Ok(created);
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

                var tableBefore = await _tableDAL.GetTablesById(id);
                if (tableBefore == null)
                    return NotFound("La table n'existe pas");

                var restaurant = await _restaurantDAL.GetRestaurantById(tableBefore.RestaurantId);

                if (restaurant == null)
                    return NotFound("Le restaurant n'existe pas");
                if (restaurant.UserId != idUserToken)
                    return Unauthorized("Vous n'êtes pas autorisé à modifier cette table");

                var deleted = await _tableDAL.DeleteTable(id);

                return Ok(deleted);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [Authorize]
        [HttpPost("Restaurant/{Id}/Bulk")]
        public async Task<ActionResult<IEnumerable<TableEntityOut>>> ReplaceTables(long Id, [FromBody] List<TableEntityIn> tables)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (tables == null || !tables.Any())
                    return BadRequest("La liste des tables est vide.");

                // Vérification de sécurité : le restaurant appartient-il à l'utilisateur ?
                var restaurant = await _restaurantDAL.GetRestaurantById(Id);
                if (restaurant == null)
                    return NotFound("Le restaurant n'existe pas");

                if (restaurant.UserId != idUserToken)
                    return Unauthorized("Vous n'êtes pas autorisé à modifier les tables de ce restaurant");

                // Appel de la méthode DAL
                var results = await _tableDAL.ReplaceTablesAsync(Id, tables);

                return Ok(results);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }
    }
}

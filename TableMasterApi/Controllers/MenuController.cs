using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [ApiController]
    public class MenuController : ControllerBase
    {
        private readonly IMenuDAL _dal;
        private readonly IRestaurantDAL _restaurantDAL;
        private readonly JwtService _jwtService;

        public MenuController(IMenuDAL menuDAL, IRestaurantDAL restaurantDAL, JwtService jwtService)
        {
            _jwtService = jwtService;
            _dal = menuDAL;
            _restaurantDAL = restaurantDAL;
        }

        [Authorize]
        [HttpGet("restaurant/{restaurantId}")]
        public async Task<ActionResult<IEnumerable<MenuOut>>> GetByRestaurant(long restaurantId)
        {
            try
            {
                var menus = await _dal.GetByRestaurantAsync(restaurantId);
                return Ok(menus);
            }
            catch (PostgresException e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult> Post([FromBody] MenuIn input)
        {
            try
            {
                if (input == null)
                    return BadRequest();

                if (string.IsNullOrWhiteSpace(input.Category) ||
                    string.IsNullOrWhiteSpace(input.ItemName) ||
                    input.Price < 0)
                {
                    return BadRequest("La catégorie, le nom et un prix positif ou nul sont obligatoires.");
                }

                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);
                var restaurant = await _restaurantDAL.GetRestaurantById(input.RestaurantId);
                if (restaurant == null)
                    return NotFound("Le restaurant n'existe pas");
                if (restaurant.UserId != idUserToken)
                    return Forbid();

                var result = await _dal.InsertAsync(input);
                return Ok(result);
            }
            catch (PostgresException e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(long id)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                var menuBefore = await _dal.GetByIdAsync(id);
                if (menuBefore == null)
                    return NotFound("Le menu n'existe pas");

                var restaurant = await _restaurantDAL.GetRestaurantById(menuBefore.RestaurantId);
                if (restaurant == null)
                    return NotFound("Le restaurant n'existe pas");
                if (restaurant.UserId != idUserToken)
                    return Unauthorized("Vous n'êtes pas autorisé à supprimer ce menu");

                var success = await _dal.DeleteAsync(id);
                return success ? Ok() : NotFound();
            }
            catch (PostgresException e)
            {
                return StatusCode(500, e.Message);
            }
        }
    }
}

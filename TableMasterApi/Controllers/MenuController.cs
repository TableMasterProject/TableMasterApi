using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TableMasterApi.DAL;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [ApiController]
    public class MenuController : ControllerBase
    {
        private readonly MenuDAL _dal;
        private readonly RestaurantDAL _RestaurantDAL;
        private readonly JwtService _jwtService;

        public MenuController(IOptions<ConfigPerso> config, JwtService jwtService)
        {
            _jwtService = jwtService;
            _dal = new MenuDAL(config.Value);
            _RestaurantDAL = new RestaurantDAL(config.Value);
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
            catch (SqlException e)
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

                var result = await _dal.InsertAsync(input);
                return Ok(result);
            }
            catch (SqlException e)
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

                var restaurant = await _RestaurantDAL.GetRestaurantById(menuBefore.RestaurantId);
                if (restaurant == null)
                    return NotFound("Le restaurant n'existe pas");
                if (restaurant.UserId != idUserToken)
                    return Unauthorized("Vous n'êtes pas autorisé à supprimer ce menu");

                var success = await _dal.DeleteAsync(id);
                return success ? Ok() : NotFound();
            }
            catch (SqlException e)
            {
                return StatusCode(500, e.Message);
            }
        }
    }
}

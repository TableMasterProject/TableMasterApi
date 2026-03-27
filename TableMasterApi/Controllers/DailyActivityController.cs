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
    public class DailyActivityController : ControllerBase
    {
        private readonly DailyActivityDAL _dal;
        private readonly RestaurantDAL _RestaurantDAL;

        private readonly JwtService _jwtService;

        public DailyActivityController(IOptions<ConfigPerso> config, JwtService jwtService)
        {
            _jwtService = jwtService;
            _dal = new DailyActivityDAL(config.Value);
            _RestaurantDAL = new RestaurantDAL(config.Value);
        }

        [Authorize]
        [HttpGet("restaurant/{restaurantId}")]
        public async Task<ActionResult<IEnumerable<DailyActivityOut>>> GetByRestaurant(long restaurantId)
        {
            try
            {
                var activities = await _dal.GetByRestaurantAsync(restaurantId);
                return Ok(activities);
            }
            catch (SqlException e)
            {
                return StatusCode(500, e.Message);
            }
            
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult> Post([FromBody] DailyActivityIn input)
        {
            try
            {
                if (input == null)
                {
                    return BadRequest();
                }

                var result = await _dal.InsertAsync(input);
                return Ok(result);
            }
            catch (SqlException e)
            {
                return StatusCode(500, e.Message);
            }

        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult> Put(long id, [FromBody] DailyActivityIn input)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (input == null)
                {
                    return BadRequest();
                }
                var tableBefore = await _dal.GetByIdAsync(id);
                if (tableBefore == null)
                    return NotFound("La DailyActivity n'existe pas");

                var restaurant = await _RestaurantDAL.GetRestaurantById(tableBefore.RestaurantId);

                if (restaurant == null)
                    return NotFound("Le restaurant n'existe pas");
                if (restaurant.UserId != idUserToken)
                    return Unauthorized("Vous n'êtes pas autorisé à modifier cette DailyActivity");

                var result = await _dal.UpdateAsync(id, input);
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

                var tableBefore = await _dal.GetByIdAsync(id);
                if (tableBefore == null)
                    return NotFound("La DailyActivity n'existe pas");

                var restaurant = await _RestaurantDAL.GetRestaurantById(tableBefore.RestaurantId);

                if (restaurant == null)
                    return NotFound("Le restaurant n'existe pas");
                if (restaurant.UserId != idUserToken)
                    return Unauthorized("Vous n'êtes pas autorisé à modifier cette DailyActivity");

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

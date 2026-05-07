using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [ApiController]
    public class ClosedDayExceptionController : ControllerBase
    {
        private readonly IClosedDayExceptionDAL _dal;
        private readonly IRestaurantDAL _restaurantDAL;

        private readonly JwtService _jwtService;

        public ClosedDayExceptionController(IClosedDayExceptionDAL closedDayExceptionDAL, IRestaurantDAL restaurantDAL, JwtService jwtService)
        {
            _jwtService = jwtService;
            _dal = closedDayExceptionDAL;
            _restaurantDAL = restaurantDAL;
        }

        [Authorize]
        [HttpGet("restaurant/{restaurantId}")]
        public async Task<ActionResult<IEnumerable<ClosedDayExceptionOut>>> GetByRestaurant(long restaurantId)
        {
            try
            {
                var result = await _dal.GetByRestaurantAsync(restaurantId);
                return Ok(result);
            }
            catch (SqlException e)
            {
                return StatusCode(500, e.Message);
            }

        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<ClosedDayExceptionOut>> Post([FromBody] ClosedDayExceptionIn input)
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
        public async Task<ActionResult<ClosedDayExceptionOut>> Put(long id, [FromBody] ClosedDayExceptionIn input)
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

                var restaurant = await _restaurantDAL.GetRestaurantById(tableBefore.RestaurantId);

                if (restaurant == null)
                    return NotFound("Le restaurant n'existe pas");
                if (restaurant.UserId != idUserToken)
                    return Unauthorized("Vous n'êtes pas autorisé à modifier cette DailyActivity");

                var result = await _dal.UpdateAsync(id, input);
                if (result == null) return NotFound();
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

                var restaurant = await _restaurantDAL.GetRestaurantById(tableBefore.RestaurantId);

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

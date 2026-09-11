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
    public class DailyActivityController : ControllerBase
    {
        private readonly IDailyActivityDAL _dal;
        private readonly IRestaurantDAL _restaurantDAL;

        private readonly ICurrentUserService _currentUser;
        private readonly IAvailabilityNotifier? _availability;

        public DailyActivityController(IDailyActivityDAL dailyActivityDAL, IRestaurantDAL restaurantDAL, JwtService jwtService, ICurrentUserService? currentUser = null, IAvailabilityNotifier? availability = null)
        {
            _currentUser = currentUser ?? new CurrentUserService();
            _availability = availability;
            _dal = dailyActivityDAL;
            _restaurantDAL = restaurantDAL;
        }

        [Authorize]
        [HttpGet("restaurant/{restaurantId}")]
        public async Task<ActionResult<IEnumerable<DailyActivityOut>>> GetByRestaurant(long restaurantId)
        {
            var activities = await _dal.GetByRestaurantAsync(restaurantId);
            return Ok(activities);
            
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult> Post([FromBody] DailyActivityIn input)
        {
            if (input == null)
            {
                return BadRequest();
            }


            var idUserToken = _currentUser.GetUserId(User);
            var restaurant = await _restaurantDAL.GetRestaurantById(input.RestaurantId);
            if (restaurant == null)
                return NotFound("Le restaurant n'existe pas");
            if (restaurant.UserId != idUserToken)
                return Forbid();

            if (input.DayOfWeek is < 1 or > 7 || input.StartTime < TimeSpan.Zero || input.EndTime > TimeSpan.FromDays(1) || input.EndTime <= input.StartTime)
                return BadRequest("Horaires invalides.");
            var result = await _dal.InsertAsync(input);
            if (_availability != null) await _availability.NotifyAsync(input.RestaurantId);
            return Ok(result);

        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult> Put(long id, [FromBody] DailyActivityIn input)
        {
            var idUserToken = _currentUser.GetUserId(User);

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
                return Forbid();

            if (input.RestaurantId != tableBefore.RestaurantId)
                return BadRequest("Le restaurant ne peut pas être modifié.");

            if (input.DayOfWeek is < 1 or > 7 || input.StartTime < TimeSpan.Zero || input.EndTime > TimeSpan.FromDays(1) || input.EndTime <= input.StartTime)
                return BadRequest("Horaires invalides.");
            var result = await _dal.UpdateAsync(id, input);
            if (result != null && _availability != null) await _availability.NotifyAsync(tableBefore.RestaurantId);
            return Ok(result);

        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(long id)
        {
            var idUserToken = _currentUser.GetUserId(User);

            var tableBefore = await _dal.GetByIdAsync(id);
            if (tableBefore == null)
                return NotFound("La DailyActivity n'existe pas");

            var restaurant = await _restaurantDAL.GetRestaurantById(tableBefore.RestaurantId);

            if (restaurant == null)
                return NotFound("Le restaurant n'existe pas");
            if (restaurant.UserId != idUserToken)
                return Forbid();

            var success = await _dal.DeleteAsync(id);
            if (success && _availability != null) await _availability.NotifyAsync(tableBefore.RestaurantId);
            return success ? Ok() : NotFound();

        }
    }
}

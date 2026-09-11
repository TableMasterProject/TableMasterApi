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
    public class ClosedDayExceptionController : ControllerBase
    {
        private readonly IClosedDayExceptionDAL _dal;
        private readonly IRestaurantDAL _restaurantDAL;

        private readonly ICurrentUserService _currentUser;
        private readonly IAvailabilityNotifier? _availability;

        public ClosedDayExceptionController(IClosedDayExceptionDAL closedDayExceptionDAL, IRestaurantDAL restaurantDAL, JwtService jwtService, ICurrentUserService? currentUser = null, IAvailabilityNotifier? availability = null)
        {
            _currentUser = currentUser ?? new CurrentUserService();
            _availability = availability;
            _dal = closedDayExceptionDAL;
            _restaurantDAL = restaurantDAL;
        }

        [Authorize]
        [HttpGet("restaurant/{restaurantId}")]
        public async Task<ActionResult<IEnumerable<ClosedDayExceptionOut>>> GetByRestaurant(long restaurantId)
        {
            var result = await _dal.GetByRestaurantAsync(restaurantId);
            return Ok(result);

        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<ClosedDayExceptionOut>> Post([FromBody] ClosedDayExceptionIn input)
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

            if (input.ExceptionDateEnd < input.ExceptionDateBegin) return BadRequest("Période de fermeture invalide.");
            var result = await _dal.InsertAsync(input);
            if (_availability != null) await _availability.NotifyAsync(input.RestaurantId);
            return Ok(result);

        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult<ClosedDayExceptionOut>> Put(long id, [FromBody] ClosedDayExceptionIn input)
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

            if (input.ExceptionDateEnd < input.ExceptionDateBegin) return BadRequest("Période de fermeture invalide.");
            var result = await _dal.UpdateAsync(id, input);
            if (result != null && _availability != null) await _availability.NotifyAsync(tableBefore.RestaurantId);
            if (result == null) return NotFound();
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

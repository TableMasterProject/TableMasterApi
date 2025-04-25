using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TableMasterApi.DAL;
using TableMasterApi.Model;

namespace TableMasterApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DailyActivityController : ControllerBase
    {
        private readonly DailyActivityDAL _dal;

        public DailyActivityController(IOptions<ConfigPerso> config)
        {
            _dal = new DailyActivityDAL(config.Value);
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
                if (input == null)
                {
                    return BadRequest();
                }

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

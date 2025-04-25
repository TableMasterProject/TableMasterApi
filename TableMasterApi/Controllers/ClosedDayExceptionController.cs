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
    public class ClosedDayExceptionController : ControllerBase
    {
        private readonly ClosedDayExceptionDAL _dal;

        public ClosedDayExceptionController(IOptions<ConfigPerso> config)
        {
            _dal = new ClosedDayExceptionDAL(config.Value);
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
                if (input == null)
                {
                    return BadRequest();
                }

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

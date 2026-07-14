using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewController : ControllerBase
    {
        private readonly IReviewDAL _reviewDAL;
        private readonly JwtService _jwtService;
        public ReviewController(IReviewDAL reviewDAL, JwtService jwtService)
        {
            _jwtService = jwtService;
            _reviewDAL = reviewDAL;
        }

        [Authorize]
        [HttpGet("Restaurant/{Id}")]
        public async Task<ActionResult<ICollection<ReviewOut>>> GetByIdRestaurant(long Id, [FromQuery] SearchReviews searchReviews)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (searchReviews == null)
                {
                    return BadRequest();
                }

                var liste = await _reviewDAL.GetByIdRestaurant(Id, searchReviews);
                if (liste == null)
                {
                    return NotFound();
                }
                return Ok(liste);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }
        [Authorize]
        [HttpGet("My")]
        public async Task<ActionResult<ICollection<ReviewOut>>> GetMy([FromQuery] SearchReviews searchReviews)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (searchReviews == null)
                {
                    return BadRequest();
                }

                var liste = await _reviewDAL.GetMy(idUserToken, searchReviews);
                if (liste == null)
                {
                    return NotFound();
                }
                return Ok(liste);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<ReviewOut>> Post([FromBody] ReviewIn Entity)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                var liste = await _reviewDAL.Add(idUserToken,Entity);
                if (liste == null)
                {
                    return NotFound();
                }
                return Ok(liste);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }
        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult<ReviewOut>> Put(long id, [FromBody] ReviewIn Entity)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                var liste = await _reviewDAL.Update(idUserToken, id, Entity);
                if (liste == null)
                {
                    return NotFound();
                }
                return Ok(liste);
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

                var deleted = await _reviewDAL.Delete(idUserToken, id);
                if (!deleted)
                    return NotFound("Restaurant not found.");

                return Ok(deleted);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }
    }
}

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
        private readonly ICurrentUserService _currentUser;
        public ReviewController(IReviewDAL reviewDAL, JwtService jwtService, ICurrentUserService? currentUser = null)
        {
            _currentUser = currentUser ?? new CurrentUserService();
            _reviewDAL = reviewDAL;
        }

        [Authorize]
        [HttpGet("Restaurant/{Id}")]
        public async Task<ActionResult<ICollection<ReviewOut>>> GetByIdRestaurant(long Id, [FromQuery] SearchReviews searchReviews)
        {
            var idUserToken = _currentUser.GetUserId(User);

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
        [Authorize]
        [HttpGet("My")]
        public async Task<ActionResult<ICollection<ReviewOut>>> GetMy([FromQuery] SearchReviews searchReviews)
        {
            var idUserToken = _currentUser.GetUserId(User);

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
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<ReviewOut>> Post([FromBody] ReviewIn Entity)
        {
            var idUserToken = _currentUser.GetUserId(User);

            var liste = await _reviewDAL.Add(idUserToken,Entity);
            if (liste == null)
            {
                return NotFound();
            }
            return Ok(liste);
        }
        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult<ReviewOut>> Put(long id, [FromBody] ReviewIn Entity)
        {
            var idUserToken = _currentUser.GetUserId(User);

            var liste = await _reviewDAL.Update(idUserToken, id, Entity);
            if (liste == null)
            {
                return NotFound();
            }
            return Ok(liste);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult<bool>> Delete(long id)
        {
            var idUserToken = _currentUser.GetUserId(User);

            var deleted = await _reviewDAL.Delete(idUserToken, id);
            if (!deleted)
                return NotFound("Restaurant not found.");

            return Ok(deleted);
        }
    }
}

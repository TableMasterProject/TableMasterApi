using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TableMasterApi.DAL;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RestaurantController : ControllerBase
    {
        private readonly RestaurantDAL _restaurantDAL;
        private readonly JwtService _jwtService;
        public RestaurantController(JwtService jwtService, IOptions<ConfigPerso> config)
        {
            _jwtService = jwtService;
            _restaurantDAL = new RestaurantDAL(config.Value);
        }

        // GET: api/Restaurant
        [HttpGet]
        [Authorize]
        public ActionResult<ICollection<RestaurantOut>> GetAll([FromQuery] SearchRestaurant search)
        {
            var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
            var idUserToken = _jwtService.ExtractUserIdFromToken(token);

            var restaurants = _restaurantDAL.GetRestaurants(search);
            return Ok(restaurants);
        }

        // GET: api/Restaurant/{id}
        [HttpGet("{id}")]
        [Authorize]
        public ActionResult<RestaurantOut> Get(long id)
        {
            var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
            var idUserToken = _jwtService.ExtractUserIdFromToken(token);

            var restaurant = _restaurantDAL.GetRestaurantById(id);
            if (restaurant == null)
                return NotFound("Restaurant not found.");

            return Ok(restaurant);
        }

        // POST: api/Restaurant
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<RestaurantOut>> Post([FromBody] RestaurantIn restaurant)
        {
            var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
            var idUserToken = _jwtService.ExtractUserIdFromToken(token);

            if (restaurant == null)
            {
                return BadRequest();
            }

            var createdRestaurant = await _restaurantDAL.PostRestaurantAsync(idUserToken, restaurant);
            return Ok(createdRestaurant);
        }

        // PUT: api/Restaurant/{id}
        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<RestaurantOut>> Put(long id, [FromBody] RestaurantIn restaurant)
        {
            var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
            var idUserToken = _jwtService.ExtractUserIdFromToken(token);

            if (restaurant == null)
            {
                return BadRequest();
            }

            var put = await _restaurantDAL.putRestaurant(idUserToken, id, restaurant);

            return Ok(put);
        }

        // DELETE: api/Restaurant/{id}
        [HttpDelete("{id}")]
        [Authorize]
        public ActionResult<bool> Delete(long id)
        {
            var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
            var idUserToken = _jwtService.ExtractUserIdFromToken(token);

            var deleted = _restaurantDAL.DeleteRestaurant(idUserToken, id);
            if (!deleted)
                return NotFound("Restaurant not found.");

            return Ok(deleted);
        }
    }
}

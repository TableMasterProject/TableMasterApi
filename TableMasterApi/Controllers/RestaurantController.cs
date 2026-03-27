using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TableMasterApi.DAL;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.Controllers
{
    [ApiVersion("1.0")]
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
        public async Task<ActionResult<ICollection<RestaurantOut>>> GetAll([FromQuery] SearchRestaurant search)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                var restaurants = await _restaurantDAL.GetRestaurants(search);
                return Ok(restaurants);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        // GET: api/Restaurant/{id}
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<RestaurantOut>> Get(long id)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                var restaurant = await _restaurantDAL.GetRestaurantById(id);
                if (restaurant == null)
                    return NotFound("Restaurant not found.");

                return Ok(restaurant);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        // POST: api/Restaurant
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<RestaurantOut>> Post([FromBody] RestaurantIn restaurant)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (restaurant == null)
                {
                    return BadRequest("Données du restaurant invalides.");
                }

                var createdRestaurant = await _restaurantDAL.PostRestaurantAsync(idUserToken, restaurant);
                return Ok(createdRestaurant);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        // PUT: api/Restaurant/{id}
        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<RestaurantOut>> Put(long id, [FromBody] RestaurantIn restaurant)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (restaurant == null)
                {
                    return BadRequest("Données de mise à jour invalides.");
                }

                // Ici tu pourrais ajouter une vérification : 
                // var existing = await _restaurantDAL.GetRestaurantById(id);
                // if (existing.UserId != idUserToken) return Unauthorized();

                var put = await _restaurantDAL.putRestaurant(idUserToken, id, restaurant);
                if (put == null)
                    return NotFound("Restaurant non trouvé ou modification impossible.");

                return Ok(put);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        // DELETE: api/Restaurant/{id}
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult<bool>> Delete(long id)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                var deleted = await _restaurantDAL.DeleteRestaurant(idUserToken, id);
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
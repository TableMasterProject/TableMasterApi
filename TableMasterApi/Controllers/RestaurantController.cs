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
    public class RestaurantController : ControllerBase
    {
        private readonly IRestaurantDAL _restaurantDAL;
        private readonly ICurrentUserService _currentUser;
        private readonly IAvailabilityNotifier? _availability;

        public RestaurantController(IRestaurantDAL restaurantDAL, JwtService jwtService, ICurrentUserService? currentUser = null, IAvailabilityNotifier? availability = null)
        {
            _restaurantDAL = restaurantDAL;
            _currentUser = currentUser ?? new CurrentUserService();
            _availability = availability;
        }

        // GET: api/Restaurant
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<ICollection<RestaurantOut>>> GetAll([FromQuery] SearchRestaurant search)
        {
            try
            {
                var idUserToken = _currentUser.GetUserId(User);

                var restaurants = await _restaurantDAL.GetRestaurants(search);
                return Ok(restaurants);
            }
            catch
            {
                throw;
            }
        }

        // GET: api/Restaurant/{id}
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<RestaurantOut>> Get(long id)
        {
            try
            {
                var idUserToken = _currentUser.GetUserId(User);

                var restaurant = await _restaurantDAL.GetRestaurantById(id);
                if (restaurant == null)
                    return NotFound("Restaurant not found.");

                return Ok(restaurant);
            }
            catch
            {
                throw;
            }
        }

        // POST: api/Restaurant
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<RestaurantOut>> Post([FromBody] RestaurantIn restaurant)
        {
            try
            {
                var idUserToken = _currentUser.GetUserId(User);

                if (restaurant == null)
                {
                    return BadRequest("Données du restaurant invalides.");
                }

                var createdRestaurant = await _restaurantDAL.PostRestaurantAsync(idUserToken, restaurant);
                return Ok(createdRestaurant);
            }
            catch (GoogleMapsException e)
            {
                return StatusCode(
                    e.StatusCode,
                    e.StatusCode >= StatusCodes.Status500InternalServerError
                        ? "Service de géolocalisation indisponible."
                        : e.Message);
            }
            catch
            {
                throw;
            }
        }

        // PUT: api/Restaurant/{id}
        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<RestaurantOut>> Put(long id, [FromBody] RestaurantIn restaurant)
        {
            try
            {
                var idUserToken = _currentUser.GetUserId(User);

                if (restaurant == null)
                {
                    return BadRequest("Données de mise à jour invalides.");
                }

                // Ici tu pourrais ajouter une vérification : 
                // var existing = await _restaurantDAL.GetRestaurantById(id);
                // if (existing.UserId != idUserToken) return Unauthorized();

                var put = await _restaurantDAL.PutRestaurantAsync(idUserToken, id, restaurant);
                if (put == null)
                    return NotFound("Restaurant non trouvé ou modification impossible.");

                if (_availability != null) await _availability.NotifyAsync(id);
                return Ok(put);
            }
            catch (GoogleMapsException e)
            {
                return StatusCode(
                    e.StatusCode,
                    e.StatusCode >= StatusCodes.Status500InternalServerError
                        ? "Service de géolocalisation indisponible."
                        : e.Message);
            }
            catch
            {
                throw;
            }
        }

        // DELETE: api/Restaurant/{id}
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult<bool>> Delete(long id)
        {
            try
            {
                var idUserToken = _currentUser.GetUserId(User);

                var deleted = await _restaurantDAL.DeleteRestaurant(idUserToken, id);
                if (!deleted)
                    return NotFound("Restaurant not found.");

                if (_availability != null) await _availability.NotifyAsync(id);
                return Ok(deleted);
            }
            catch
            {
                throw;
            }
        }
    }
}

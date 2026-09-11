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
    public class DeviceTokenController : ControllerBase
    {
        private readonly IDeviceTokenDAL _deviceTokenDAL;
        private readonly ICurrentUserService _currentUser;

        public DeviceTokenController(IDeviceTokenDAL deviceTokenDAL, JwtService jwtService, ICurrentUserService? currentUser = null)
        {
            _deviceTokenDAL = deviceTokenDAL;
            _currentUser = currentUser ?? new CurrentUserService();
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> RegisterDeviceToken([FromBody] DeviceTokenIn model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.DeviceToken))
                return BadRequest("Device token is required.");

            var userId = _currentUser.GetUserId(User);

            await _deviceTokenDAL.SaveDeviceTokenAsync(userId, model.DeviceToken, model.DevicePlatform ?? "unknown");
            return Ok();
        }

        [Authorize]
        [HttpDelete]
        public async Task<IActionResult> DeleteDeviceToken([FromQuery] string deviceToken) // On passe direct le string
        {
            if (string.IsNullOrWhiteSpace(deviceToken))
                return BadRequest("Device token is required.");

            var userId = _currentUser.GetUserId(User);

            await _deviceTokenDAL.DeleteDeviceTokenAsync(userId, deviceToken);
            return Ok();
        }
    }
}

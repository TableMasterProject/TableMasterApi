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
        private readonly JwtService _jwtService;

        public DeviceTokenController(IDeviceTokenDAL deviceTokenDAL, JwtService jwtService)
        {
            _deviceTokenDAL = deviceTokenDAL;
            _jwtService = jwtService;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> RegisterDeviceToken([FromBody] DeviceTokenIn model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.DeviceToken))
                return BadRequest("Device token is required.");

            var authorization = HttpContext.Request.Headers["Authorization"].ToString();
            var token = _jwtService.ExtractTokenFromAuthorization(authorization);
            var userId = _jwtService.ExtractUserIdFromToken(token);

            await _deviceTokenDAL.SaveDeviceTokenAsync(userId, model.DeviceToken, model.DevicePlatform ?? "unknown");
            return Ok();
        }

        [Authorize]
        [HttpDelete]
        public async Task<IActionResult> DeleteDeviceToken([FromQuery] string deviceToken) // On passe direct le string
        {
            if (string.IsNullOrWhiteSpace(deviceToken))
                return BadRequest("Device token is required.");

            var authorization = HttpContext.Request.Headers["Authorization"].ToString();
            var token = _jwtService.ExtractTokenFromAuthorization(authorization);
            var userId = _jwtService.ExtractUserIdFromToken(token);

            await _deviceTokenDAL.DeleteDeviceTokenAsync(userId, deviceToken);
            return Ok();
        }
    }
}

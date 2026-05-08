using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserDAL _userDAL;
        private readonly IAuthDAL _authDAL;
        private readonly JwtService _jwtService;

        public AuthController(IUserDAL userDAL, IAuthDAL authDAL, JwtService jwtService)
        {
            _jwtService = jwtService;
            _userDAL = userDAL;
            _authDAL = authDAL;
        }

        [EnableRateLimiting("AuthPolicy")]
        [HttpPost]
        public async Task<ActionResult<LoginUserOut>> Login([FromBody] LoginUserIn loginUser)
        {
            if (string.IsNullOrWhiteSpace(loginUser.Email) || string.IsNullOrWhiteSpace(loginUser.Password))
            {
                return BadRequest();
            }

            var user = await _userDAL.GetUserByEmail(loginUser.Email);
            if (user == null)
            {
                return NotFound("Email non existant");
            }

            if (!_authDAL.VerifyPassword(user.Password, loginUser.Password))
            {
                return NotFound("Mot de passe incorecte");
            }

            var refreshToken = _jwtService.GenerateRefreshToken();
            var hashedRefreshToken = _jwtService.HashToken(refreshToken);

            await _userDAL.SaveRefreshToken(user.Id, hashedRefreshToken, DateTime.UtcNow.AddDays(30));

            return Ok(new LoginUserOut
            {
                User = user.ToPublicUser(),
                AccessToken = _jwtService.GenerateAccessToken(user.Id),
                RefreshToken = refreshToken
            });
        }

        [EnableRateLimiting("AuthPolicy")]
        [HttpPost("refresh")]
        public async Task<ActionResult<LoginUserOut>> Refresh([FromBody] LoginTokenIn model)
        {
            if (string.IsNullOrWhiteSpace(model.RefreshToken))
            {
                return BadRequest();
            }

            var hashedInput = _jwtService.HashToken(model.RefreshToken);
            var userId = await _userDAL.GetUserIdByRefreshToken(hashedInput);
            if (userId == null)
            {
                return Unauthorized("Session expirée");
            }

            var user = await _userDAL.GetUserById(userId.Value);
            if (user == null)
            {
                return Unauthorized("Session expirée");
            }

            var newRefreshToken = _jwtService.GenerateRefreshToken();
            var newHashed = _jwtService.HashToken(newRefreshToken);

            await _userDAL.DeleteRefreshToken(hashedInput);
            await _userDAL.SaveRefreshToken(user.Id, newHashed, DateTime.UtcNow.AddDays(30));

            return Ok(new LoginUserOut
            {
                User = user.ToPublicUser(),
                AccessToken = _jwtService.GenerateAccessToken(user.Id),
                RefreshToken = newRefreshToken
            });
        }
    }
}

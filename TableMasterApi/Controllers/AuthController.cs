using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using TableMasterApi.DAL;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserDAL _userDAL;
        private readonly AuthDAL _authDAL;
        private readonly JwtService _jwtService;

        public AuthController(JwtService jwtService, IOptions<ConfigPerso> config)
        {
            _jwtService = jwtService;
            _userDAL = new UserDAL(config.Value);
            _authDAL = new AuthDAL();
        }

        [HttpPost]
        public async Task<ActionResult<LoginUserOut>> Login([FromBody] LoginUserIn loginUser)
        {
            try
            {
                if (loginUser == null || loginUser.Password == null || loginUser.Email == null)
                {
                    return BadRequest();
                }

                var user = await _userDAL.GetUserByEmail(loginUser.Email);

                if (user == null)
                {
                    return StatusCode(404, "Email non existant");
                }

                var result = _authDAL.VerifyPassword(user.Password, loginUser.Password);

                if (!result)
                {
                    return StatusCode(404, "Mot de passe incorecte");
                }

                string refreshToken = _jwtService.GenerateRefreshToken();
                string hashedRefreshToken = _jwtService.HashToken(refreshToken);

                await _userDAL.SaveRefreshToken(user.Id, hashedRefreshToken, DateTime.UtcNow.AddDays(30));

                LoginUserOut loginUserOut = new LoginUserOut();
                loginUserOut.User = user;
                loginUserOut.AccessToken = _jwtService.GenerateAccessToken(user.Id);
                loginUserOut.RefreshToken = refreshToken;
                

                return Ok(loginUserOut);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<LoginUserOut>> Refresh([FromBody] LoginTokenIn model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.RefreshToken)) return BadRequest();

                string hashedInput = _jwtService.HashToken(model.RefreshToken);

                var userId = await _userDAL.GetUserIdByRefreshToken(hashedInput);
                if (userId == null) return Unauthorized("Session expirée");

                var user = await _userDAL.GetUserById(userId.Value);
        
                string newRefreshToken = _jwtService.GenerateRefreshToken();
                string newHashed = _jwtService.HashToken(newRefreshToken);

                await _userDAL.DeleteRefreshToken(hashedInput);
                await _userDAL.SaveRefreshToken(user.Id, newHashed, DateTime.UtcNow.AddDays(30));

                return Ok(new LoginUserOut {
                    User = user,
                    AccessToken = _jwtService.GenerateAccessToken(user.Id),
                    RefreshToken = newRefreshToken
                });
            }
            catch (Exception e) { return StatusCode(500, e.Message); }
        }
    }
}

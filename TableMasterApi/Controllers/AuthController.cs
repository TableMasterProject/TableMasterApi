using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using TableMasterApi.DAL;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.Controllers
{
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
                string hashedRefreshToken;
                using (var sha256 = SHA256.Create())
                {
                    var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(refreshToken));
                    hashedRefreshToken = Convert.ToBase64String(bytes);
                }

                await _userDAL.SaveRefreshToken(user.Id, hashedRefreshToken, DateTime.Now.AddDays(30));

                LoginUserOut loginUserOut = new LoginUserOut();
                loginUserOut.User = user;
                loginUserOut.AccessToken = _jwtService.GenerateAccessToken(user.Id);
                loginUserOut.RefreshToken = hashedRefreshToken;
                

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

                // 1. On hashe le token reçu pour le comparer à la BDD
                string hashedRefreshToken;
                using (var sha256 = SHA256.Create())
                {
                    var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(model.RefreshToken));
                    hashedRefreshToken = Convert.ToBase64String(bytes);
                }

                // 2. On cherche l'utilisateur associé
                var userId = await _userDAL.GetUserIdByRefreshToken(hashedRefreshToken);

                if (userId == null) return Unauthorized("Session expirée ou invalide");

                // 3. On récupère les infos utilisateur pour renvoyer l'objet complet
                var user = await _userDAL.GetUserById(userId.Value);
                if (user == null) return NotFound();

                // 4. On génère un nouvel AccessToken (le refresh reste le même ou on peut le faire tourner)
                var newAccessToken = _jwtService.GenerateAccessToken(user.Id);

                return Ok(new LoginUserOut
                {
                    User = user,
                    AccessToken = newAccessToken,
                    RefreshToken = model.RefreshToken // On réutilise le même
                });
            }
            catch (Exception e) { return StatusCode(500, e.Message); }
        }
    }
}

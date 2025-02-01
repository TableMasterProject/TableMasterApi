using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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

                LoginUserOut loginUserOut = new LoginUserOut();
                loginUserOut.User = user;
                loginUserOut.Token = _jwtService.GenerateToken(user.Id);

                return Ok(loginUserOut);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }
    }
}

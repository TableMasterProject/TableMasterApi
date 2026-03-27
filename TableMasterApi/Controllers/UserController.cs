using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
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
    public class UserController : ControllerBase
    {
        private readonly UserDAL _userDAL;
        private readonly AuthDAL _authDAL;
        private readonly JwtService _jwtService;

        public UserController(JwtService jwtService, IOptions<ConfigPerso> config)
        {
            _jwtService = jwtService;
            _userDAL = new UserDAL(config.Value);
            _authDAL = new AuthDAL();
        }

        // GET api/user/{id} -> Récupérer un utilisateur par ID
        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<UserOut>> GetUserById(long id)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (id == null)
                {
                    return BadRequest();
                }

                var user = await _userDAL.GetUserById(id);
                if (user == null)
                {
                    return NotFound();
                }

                return Ok(user);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [Authorize]
        [HttpPut]
        public async Task<ActionResult<UserOut>> PutUser([FromBody] UserIn user)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (user == null)
                {
                    return BadRequest();
                }

                var userPut = await _userDAL.PutUser(idUserToken, user);
                if (user == null)
                {
                    return NotFound();
                }

                return Ok(userPut);
            }
            catch (SqlException e)
            {
                if (e.Number == 2627)
                {
                    return StatusCode(403, "L'Email existe deja dans la base");
                }

                return StatusCode(500, e.Message);
            }
        }

        [Authorize]
        [HttpPut]
        [Route("Password")]
        public async Task<ActionResult<bool>> PutPassword([FromBody] PasswordEntity passwordEntity)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                if (passwordEntity == null)
                {
                    return BadRequest();
                }

                var user = await _userDAL.GetUserById(idUserToken);
                if (user == null)
                {
                    return NotFound();
                }

                var resultMatchOldPassword = _authDAL.VerifyPassword(user.Password, passwordEntity.OldPassword);

                if (!resultMatchOldPassword)
                {
                    return StatusCode(404, "ancien Mot de passe incorrect");
                }

                var passwordChange = await _userDAL.PutPassword(user, passwordEntity.NewPassword);


                if (!passwordChange)
                    return NotFound("Probleme lors du changement de mot de passe.");

                return Ok(passwordChange);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        // POST api/user -> Ajouter un nouvel utilisateur
        [HttpPost]
        public async Task<ActionResult<LoginUserOut>> AddUser([FromBody] UserIn user)
        {
            try
            {
                if (user == null)
                {
                    return BadRequest();
                }

                var addedUser = await _userDAL.AddUser(user);

                string refreshToken = _jwtService.GenerateRefreshToken();
                string hashedRefreshToken;
                using (var sha256 = SHA256.Create())
                {
                    var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(refreshToken));
                    hashedRefreshToken = Convert.ToBase64String(bytes);
                }

                await _userDAL.SaveRefreshToken(addedUser.Id, hashedRefreshToken, DateTime.Now.AddDays(30));

                LoginUserOut loginUserOut = new LoginUserOut();
                loginUserOut.User = addedUser;
                loginUserOut.AccessToken = _jwtService.GenerateAccessToken(addedUser.Id);
                loginUserOut.RefreshToken = hashedRefreshToken;


                return Ok(loginUserOut);
            }
            catch (SqlException e)
            {
                if (e.Number == 2627)
                {
                    return StatusCode(403, "L'Email existe deja dans la base");
                }

                return StatusCode(500, e.Message);
            }
        }

        [Authorize]
        [HttpDelete]
        public async Task<ActionResult<UserOut>> DeleteUser()
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers["Authorization"]);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                var deleted = await _userDAL.DeletePassword(idUserToken);

                if (!deleted)
                    return NotFound("User not found.");

                return Ok(deleted);
            }
            catch (SqlException e)
            {
                return StatusCode(500, e.Message);
            }
        }

    }
}

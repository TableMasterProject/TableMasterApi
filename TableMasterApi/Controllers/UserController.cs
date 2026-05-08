using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserDAL _userDAL;
        private readonly IAuthDAL _authDAL;
        private readonly JwtService _jwtService;

        public UserController(IUserDAL userDAL, IAuthDAL authDAL, JwtService jwtService)
        {
            _userDAL = userDAL;
            _authDAL = authDAL;
            _jwtService = jwtService;
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
            catch (PostgresException e)
            {
                if (e.SqlState == PostgresErrorCodes.UniqueViolation)
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
            catch (PostgresException e)
            {
                if (e.SqlState == PostgresErrorCodes.UniqueViolation)
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
            catch (PostgresException e)
            {
                return StatusCode(500, e.Message);
            }
        }

    }
}

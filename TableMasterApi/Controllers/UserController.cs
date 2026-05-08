using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
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

        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<UserOut>> GetUserById(long id)
        {
            var user = await _userDAL.GetUserById(id);
            if (user == null)
            {
                return NotFound();
            }

            return Ok(user.ToPublicUser());
        }

        [Authorize]
        [HttpPut]
        public async Task<ActionResult<UserOut>> PutUser([FromBody] UserIn user)
        {
            try
            {
                var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers.Authorization);
                var idUserToken = _jwtService.ExtractUserIdFromToken(token);

                var userPut = await _userDAL.PutUser(idUserToken, user);
                return Ok(userPut);
            }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return StatusCode(403, "L'Email existe deja dans la base");
            }
        }

        [Authorize]
        [HttpPut("Password")]
        public async Task<ActionResult<bool>> PutPassword([FromBody] PasswordEntity passwordEntity)
        {
            var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers.Authorization);
            var idUserToken = _jwtService.ExtractUserIdFromToken(token);

            var user = await _userDAL.GetUserById(idUserToken);
            if (user == null)
            {
                return NotFound();
            }

            if (!_authDAL.VerifyPassword(user.Password, passwordEntity.OldPassword))
            {
                return NotFound("ancien Mot de passe incorrect");
            }

            var passwordChange = await _userDAL.PutPassword(user.Id, passwordEntity.NewPassword);
            if (!passwordChange)
            {
                return NotFound("Probleme lors du changement de mot de passe.");
            }

            return Ok(passwordChange);
        }

        [EnableRateLimiting("AuthPolicy")]
        [HttpPost]
        public async Task<ActionResult<LoginUserOut>> AddUser([FromBody] UserIn user)
        {
            try
            {
                var addedUser = await _userDAL.AddUser(user);
                var refreshToken = _jwtService.GenerateRefreshToken();
                var hashedRefreshToken = _jwtService.HashToken(refreshToken);

                await _userDAL.SaveRefreshToken(addedUser.Id, hashedRefreshToken, DateTime.UtcNow.AddDays(30));

                return Ok(new LoginUserOut
                {
                    User = addedUser,
                    AccessToken = _jwtService.GenerateAccessToken(addedUser.Id),
                    RefreshToken = refreshToken
                });
            }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return StatusCode(403, "L'Email existe deja dans la base");
            }
        }

        [Authorize]
        [HttpDelete]
        public async Task<ActionResult<UserOut>> DeleteUser()
        {
            var token = _jwtService.ExtractTokenFromAuthorization(HttpContext.Request.Headers.Authorization);
            var idUserToken = _jwtService.ExtractUserIdFromToken(token);

            var deleted = await _userDAL.DeletePassword(idUserToken);
            if (!deleted)
            {
                return NotFound("User not found.");
            }

            return Ok(deleted);
        }
    }
}

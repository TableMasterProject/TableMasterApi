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
        private readonly JwtService _jwtService;
        private readonly ICurrentUserService _currentUserService;

        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public UserController(
            IUserDAL userDAL,
            IAuthDAL authDAL,
            JwtService jwtService,
            ICurrentUserService currentUserService)
        {
            _userDAL = userDAL;
            _jwtService = jwtService;
            _currentUserService = currentUserService;
        }

        public UserController(IUserDAL userDAL, IAuthDAL authDAL, JwtService jwtService)
            : this(userDAL, authDAL, jwtService, new CurrentUserService())
        {
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<UserOut>> GetUserById(long id)
        {
            if (!TryGetCurrentUserId(out var idUserToken))
            {
                return Unauthorized();
            }
            if (id != idUserToken)
            {
                return Forbid();
            }

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
                if (!TryGetCurrentUserId(out var idUserToken))
                {
                    return Unauthorized();
                }

                var userPut = await _userDAL.PutUser(idUserToken, user);
                return Ok(userPut);
            }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return Conflict("L'Email existe deja dans la base");
            }
        }

        [Authorize]
        [HttpPut("Password")]
        public async Task<ActionResult<bool>> PutPassword([FromBody] PasswordEntity passwordEntity)
        {
            if (!TryGetCurrentUserId(out var idUserToken))
            {
                return Unauthorized();
            }

            var passwordChange = await _userDAL.ChangePassword(
                idUserToken,
                passwordEntity.OldPassword,
                passwordEntity.NewPassword);
            if (passwordChange == PasswordChangeResult.UserNotFound)
            {
                return NotFound();
            }
            if (passwordChange == PasswordChangeResult.InvalidOldPassword)
            {
                return BadRequest("Ancien mot de passe incorrect.");
            }
            if (passwordChange != PasswordChangeResult.Success)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Probleme lors du changement de mot de passe.");
            }

            return Ok(true);
        }

        [EnableRateLimiting("AuthPolicy")]
        [HttpPost]
        public async Task<ActionResult<LoginUserOut>> AddUser([FromBody] UserIn user)
        {
            try
            {
                var refreshToken = _jwtService.GenerateRefreshToken();
                var hashedRefreshToken = _jwtService.HashToken(refreshToken);
                var addedUser = await _userDAL.AddUserWithSession(
                    user,
                    hashedRefreshToken,
                    DateTime.UtcNow.AddDays(30));

                return Ok(new LoginUserOut
                {
                    User = addedUser,
                    AccessToken = _jwtService.GenerateAccessToken(addedUser.Id),
                    RefreshToken = refreshToken
                });
            }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return Conflict("L'Email existe deja dans la base");
            }
        }

        [Authorize]
        [HttpDelete]
        public async Task<ActionResult<UserOut>> DeleteUser()
        {
            if (!TryGetCurrentUserId(out var idUserToken))
            {
                return Unauthorized();
            }

            var deleted = await _userDAL.DeletePassword(idUserToken);
            if (!deleted)
            {
                return NotFound("User not found.");
            }

            return Ok(deleted);
        }

        private bool TryGetCurrentUserId(out long userId)
        {
            try
            {
                userId = _currentUserService.GetUserId(User);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                userId = default;
                return false;
            }
        }
    }
}

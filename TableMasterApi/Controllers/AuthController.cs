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
        private readonly IAppLinkService _appLinkService;
        private readonly IEmailNotificationService _emailNotificationService;

        public AuthController(
            IUserDAL userDAL,
            IAuthDAL authDAL,
            JwtService jwtService,
            IAppLinkService? appLinkService = null,
            IEmailNotificationService? emailNotificationService = null)
        {
            _jwtService = jwtService;
            _userDAL = userDAL;
            _authDAL = authDAL;
            _appLinkService = appLinkService ?? new AppLinkService(Microsoft.Extensions.Options.Options.Create(new AppLinksOptions()));
            _emailNotificationService = emailNotificationService ?? NullEmailNotificationService.Instance;
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
        [HttpPost("forgot-password")]
        public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest();
            }

            var user = await _userDAL.GetUserByEmail(request.Email.Trim());
            if (user != null)
            {
                await _userDAL.DeleteExpiredPasswordResetTokens();

                var resetToken = _jwtService.GenerateRefreshToken();
                var hashedResetToken = _jwtService.HashToken(resetToken);

                await _userDAL.SavePasswordResetToken(user.Id, hashedResetToken, DateTime.UtcNow.AddHours(1));
                var resetLink = _appLinkService.BuildPasswordResetLink(resetToken);

                await _emailNotificationService.SendPasswordResetAsync(user.ToPublicUser(), resetLink);
            }

            return Ok("Si un compte existe pour cet email, un lien de reinitialisation a ete envoye.");
        }

        [EnableRateLimiting("AuthPolicy")]
        [HttpPost("reset-password")]
        public async Task<ActionResult<bool>> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest();
            }

            var hashedToken = _jwtService.HashToken(request.Token);
            var userId = await _userDAL.GetUserIdByPasswordResetToken(hashedToken);
            if (userId == null)
            {
                return BadRequest("Lien invalide ou expire.");
            }

            var passwordChanged = await _userDAL.PutPassword(userId.Value, request.NewPassword);
            if (!passwordChanged)
            {
                return NotFound("Utilisateur introuvable.");
            }

            await _userDAL.DeletePasswordResetTokensForUser(userId.Value);
            await _userDAL.DeleteAllRefreshTokensForUser(userId.Value);

            return Ok(true);
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

        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest("Le refresh token est obligatoire.");
            }

            var hashedToken = _jwtService.HashToken(request.RefreshToken);
            await _userDAL.DeleteRefreshToken(hashedToken);
            return NoContent();
        }
    }
}

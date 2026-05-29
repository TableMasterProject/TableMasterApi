using Asp.Versioning;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
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
        private readonly GoogleAuthService _googleAuthService;

        public AuthController(IUserDAL userDAL, IAuthDAL authDAL, JwtService jwtService, GoogleAuthService googleAuthService)
        {
            _jwtService = jwtService;
            _userDAL = userDAL;
            _authDAL = authDAL;
            _googleAuthService = googleAuthService;
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

            if (user.AuthProvider == "Google" || string.IsNullOrWhiteSpace(user.Password))
            {
                return Unauthorized("Ce compte utilise la connexion Google.");
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
        [HttpPost("google/check")]
        public async Task<ActionResult<GoogleAuthCheckOut>> CheckGoogle([FromBody] GoogleAuthCheckIn model)
        {
            if (string.IsNullOrWhiteSpace(model.IdToken))
            {
                return BadRequest();
            }

            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await _googleAuthService.ValidateIdTokenAsync(model.IdToken);
            }
            catch (InvalidJwtException)
            {
                return Unauthorized("Token Google invalide.");
            }

            if (payload.EmailVerified != true)
            {
                return Unauthorized("Email Google non vérifié.");
            }

            var user = await _userDAL.GetUserByGoogleSubject(payload.Subject);
            user ??= await _userDAL.GetUserByEmail(payload.Email);

            if (user != null)
            {
                if (user.AuthProvider != "Google" || user.GoogleSubject != payload.Subject)
                {
                    return Conflict("Un compte existe déjà avec cet email.");
                }

                return Ok(await CreateGoogleLoginResponse(user));
            }

            return Ok(new GoogleAuthCheckOut
            {
                NeedsOnboarding = true,
                Email = payload.Email,
                FirstName = payload.GivenName ?? string.Empty,
                LastName = payload.FamilyName ?? string.Empty,
                GoogleRegistrationToken = _jwtService.GenerateGoogleRegistrationToken(
                    payload.Subject,
                    payload.Email,
                    payload.GivenName ?? string.Empty,
                    payload.FamilyName ?? string.Empty)
            });
        }

        [EnableRateLimiting("AuthPolicy")]
        [HttpPost("google/register")]
        public async Task<ActionResult<LoginUserOut>> RegisterGoogle([FromBody] GoogleRegisterIn model)
        {
            if (string.IsNullOrWhiteSpace(model.GoogleRegistrationToken) ||
                string.IsNullOrWhiteSpace(model.Email))
            {
                return BadRequest();
            }

            GoogleRegistrationClaims claims;
            try
            {
                claims = _jwtService.ValidateGoogleRegistrationToken(model.GoogleRegistrationToken);
            }
            catch (SecurityTokenException)
            {
                return Unauthorized("Session Google expirée.");
            }

            if (!string.Equals(claims.Email, model.Email, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Les informations Google ne correspondent pas.");
            }

            var existingBySubject = await _userDAL.GetUserByGoogleSubject(claims.GoogleSubject);
            if (existingBySubject != null)
            {
                return Ok(await CreateLoginResponse(existingBySubject));
            }

            var existingByEmail = await _userDAL.GetUserByEmail(claims.Email);
            if (existingByEmail != null)
            {
                return Conflict("Un compte existe déjà avec cet email.");
            }

            try
            {
                var user = await _userDAL.AddGoogleUser(model, claims.GoogleSubject);
                return Ok(await CreateLoginResponse(new UserDb
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    AccountType = user.AccountType,
                    CreatedAt = user.CreatedAt,
                    RestaurantId = user.RestaurantId,
                    AuthProvider = user.AuthProvider,
                    GoogleSubject = claims.GoogleSubject
                }));
            }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return Conflict("Un compte existe déjà avec cet email.");
            }
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

        private async Task<GoogleAuthCheckOut> CreateGoogleLoginResponse(UserDb user)
        {
            var login = await CreateLoginResponse(user);
            return new GoogleAuthCheckOut
            {
                NeedsOnboarding = false,
                AccessToken = login.AccessToken,
                RefreshToken = login.RefreshToken,
                User = login.User
            };
        }

        private async Task<LoginUserOut> CreateLoginResponse(UserDb user)
        {
            var refreshToken = _jwtService.GenerateRefreshToken();
            var hashedRefreshToken = _jwtService.HashToken(refreshToken);

            await _userDAL.SaveRefreshToken(user.Id, hashedRefreshToken, DateTime.UtcNow.AddDays(30));

            return new LoginUserOut
            {
                User = user.ToPublicUser(),
                AccessToken = _jwtService.GenerateAccessToken(user.Id),
                RefreshToken = refreshToken
            };
        }
    }
}

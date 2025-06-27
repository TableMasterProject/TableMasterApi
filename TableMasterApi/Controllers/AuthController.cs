using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TableMasterApi.DAL;
using TableMasterApi.Model;
using TableMasterApi.Service;
using Google.Apis.Auth;

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

        [HttpPost("google-login")]
        public async Task<ActionResult<LoginUserOut>> GoogleLogin([FromBody] string IdToken)
        {
            try
            {
                if (IdToken == null)
                {
                    return BadRequest("Token invalide");
                }

                // Vérifier le jeton Google
                var payload = await VerifyGoogleToken(IdToken);
                if (payload == null)
                {
                    return Unauthorized("Token Google invalide");
                }

                // Vérifier si l'utilisateur existe dans la base de données
                var user = await _userDAL.GetUserByEmail(payload.Email);
                if (user == null)
                {
                    // Ajouter un nouvel utilisateur si non trouvé
                    UserIn userIn = new UserIn()
                    {
                        Email = payload.Email,
                        FirstName = payload.GivenName,
                        LastName = payload.FamilyName,
                        AccountType = 0 // Type de compte standard
                    };

                    await _userDAL.AddUser(userIn); // Créer un nouvel utilisateur dans la base
                }

                var loginUserOut = new LoginUserOut
                {
                    User = user,
                    Token = _jwtService.GenerateToken(user.Id), // Générer un token JWT pour l'utilisateur
                };

                return Ok(loginUserOut);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        // Fonction pour vérifier le token Google
        private async Task<GoogleJsonWebSignature.Payload?> VerifyGoogleToken(string idToken)
        {
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new List<string> { "217738707730-ppb081uop2k7tegbq5br6ttc7ht09pdb.apps.googleusercontent.com" } // Remplacer par votre client ID
                };

                // Appel statique de la méthode ValidateAsync
                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
                return payload;
            }
            catch
            {
                return null;
            }
        }

    }
}

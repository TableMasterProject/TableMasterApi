using Azure.Core;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TableMasterApi.Model;

namespace TableMasterApi.Service
{
    

    public class JwtService
    {
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;

        public JwtService(IOptions<ConfigPerso> jwtSettings)
        {
            _secretKey = jwtSettings.Value.SecretKey;
            _issuer = jwtSettings.Value.Issuer;
            _audience = jwtSettings.Value.Audience;
        }

        public string ExtractTokenFromAuthorization(string authorizationHeader)
        {

            if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
            {
                return "";
            }

            var token = authorizationHeader.Substring("Bearer ".Length).Trim();
            return token;
        }
        public long ExtractUserIdFromToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jsonToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
            var userId = jsonToken?.Claims.First(claim => claim.Type == "UserId").Value;

            if (long.TryParse(userId, out var id))
            {
                return id;
            }

            throw new SecurityTokenException("Token invalide");
        }
        public string GenerateAccessToken(long userId)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),  // L'ID de l'utilisateur dans les claims
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Un identifiant unique pour chaque token
                new Claim("UserId", userId.ToString())  // Vous pouvez aussi ajouter des claims personnalisés
            };

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
}

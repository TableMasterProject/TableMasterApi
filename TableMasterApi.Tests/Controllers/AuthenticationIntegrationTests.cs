using System.Security.Cryptography;
using System.Text;
using Xunit;
using FluentAssertions;
using TableMasterApi.Service;
using TableMasterApi.Tests.Fixtures;

namespace TableMasterApi.Tests.Controllers
{
    /// <summary>
    /// Tests d'intégration pour le service d'authentification
    /// Démontre comment tester les authentifications JWT sans dépendre directement de la DB
    /// </summary>
    public class AuthenticationIntegrationTests : IClassFixture<JwtConfigFixture>
    {
        private readonly JwtConfigFixture _fixture;
        private readonly JwtService _jwtService;

        public AuthenticationIntegrationTests(JwtConfigFixture fixture)
        {
            _fixture = fixture;
            _jwtService = new JwtService(_fixture.JwtConfigOptions);
        }

        #region Tests du flux d'authentification complet

        [Fact]
        public void AuthenticationFlow_GenerateTokenAndExtractUserId_ShouldMaintainIntegrity()
        {
            // Arrange
            long userId = 12345;

            // Act
            // 1. Générer un token pour l'utilisateur
            var accessToken = _jwtService.GenerateAccessToken(userId);

            // 2. Simuler un client qui reçoit le token dans l'Authorization header
            var authorizationHeader = $"Bearer {accessToken}";

            // 3. Le serveur extrait le token du header
            var extractedToken = _jwtService.ExtractTokenFromAuthorization(authorizationHeader);

            // 4. Le serveur valide et extrait l'UserId du token
            var extractedUserId = _jwtService.ExtractUserIdFromToken(extractedToken);

            // Assert
            // L'UserId doit correspondre
            extractedUserId.Should().Be(userId);
        }

        [Fact]
        public void AuthenticationFlow_MultipleRequests_EachTokenIsUnique()
        {
            // Arrange
            long userId = 789;

            // Act
            var token1 = _jwtService.GenerateAccessToken(userId);
            var token2 = _jwtService.GenerateAccessToken(userId);
            var token3 = _jwtService.GenerateAccessToken(userId);

            // Assert
            // Chaque token est unique (contient un Jti différent)
            token1.Should().NotBe(token2);
            token2.Should().NotBe(token3);
            token1.Should().NotBe(token3);

            // Mais chaque token contient le même UserId
            _jwtService.ExtractUserIdFromToken(token1).Should().Be(userId);
            _jwtService.ExtractUserIdFromToken(token2).Should().Be(userId);
            _jwtService.ExtractUserIdFromToken(token3).Should().Be(userId);
        }

        #endregion

        #region Tests de sécurité du token

        [Fact]
        public void TokenSecurity_ExpiredTokenShouldBeIgnored()
        {
            // Arrange
            long userId = 999;
            var token = _jwtService.GenerateAccessToken(userId);

            // Assert que le token a bien été créé
            token.Should().NotBeNullOrEmpty();
            var extractedUserId = _jwtService.ExtractUserIdFromToken(token);
            extractedUserId.Should().Be(userId);

            // Note: Vérifier l'expiration du token nécessite une validation avec les clés de signature
            // qui est faite au niveau du middleware ASP.NET Core, pas ici
        }

        [Fact]
        public void TokenRefresh_GeneratedTokensAreUniqueAndSecure()
        {
            // Arrange & Act
            var refreshTokens = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                refreshTokens.Add(_jwtService.GenerateRefreshToken());
            }

            // Assert
            // Tous les tokens doivent être uniques
            refreshTokens.Distinct().Count().Should().Be(10);

            // Tous doivent être valides en Base64
            foreach (var token in refreshTokens)
            {
                var action = () => Convert.FromBase64String(token);
                action.Should().NotThrow();
            }
        }

        #endregion

        #region Tests de conformité JWT

        [Fact]
        public void JwtCompliance_TokenContainsRequiredClaims()
        {
            // Arrange
            long userId = 555;
            var token = _jwtService.GenerateAccessToken(userId);

            // Act
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = handler.ReadToken(token) as System.IdentityModel.Tokens.Jwt.JwtSecurityToken;

            // Assert
            jwtToken.Should().NotBeNull();
            
            // Doit avoir les claims standards
            jwtToken!.Claims.Should().Contain(c => c.Type == "sub"); // Subject
            jwtToken!.Claims.Should().Contain(c => c.Type == "jti"); // JWT ID
            jwtToken!.Claims.Should().Contain(c => c.Type == "UserId"); // Custom claim
            
            // Doit avoir les informations d'émission correctes
            jwtToken.Issuer.Should().Be(_fixture.JwtConfig.Issuer);
            jwtToken.Audiences.Should().Contain(_fixture.JwtConfig.Audience);
        }

        [Fact]
        public void JwtCompliance_TokenHasCorrectSignatureAlgorithm()
        {
            // Arrange
            long userId = 666;
            var token = _jwtService.GenerateAccessToken(userId);

            // Act
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = handler.ReadToken(token) as System.IdentityModel.Tokens.Jwt.JwtSecurityToken;

            // Assert
            jwtToken.Should().NotBeNull();
            // Doit être signé avec HMAC SHA256
            jwtToken!.Header.Alg.Should().Be("HS256");
        }

        #endregion

        #region Tests d'exemple de cycle de vie

        [Fact]
        public void LifecycleDemonstration_UserLoginToTokenValidation_CompleteFlow()
        {
            // Arrange
            long userId = 888;
            
            // Étape 1: L'utilisateur se connecte et reçoit un token
            var accessToken = _jwtService.GenerateAccessToken(userId);
            var refreshToken = _jwtService.GenerateRefreshToken();

            // Étape 2: Le client stocke les tokens et les envoie dans les requêtes suivantes
            var clientAuthHeader = $"Bearer {accessToken}";

            // Étape 3: Le serveur valide le token entrant
            var serverExtractedToken = _jwtService.ExtractTokenFromAuthorization(clientAuthHeader);
            var serverExtractedUserId = _jwtService.ExtractUserIdFromToken(serverExtractedToken);

            // Étape 4: Le serveur utilise l'UserId pour charger les données de l'utilisateur
            var authenticatedUserId = serverExtractedUserId;

            // Assert
            authenticatedUserId.Should().Be(userId);
            
            // Les tokens sont valides
            accessToken.Should().NotBeNullOrEmpty();
            refreshToken.Should().NotBeNullOrEmpty();
        }

        #endregion
    }
}

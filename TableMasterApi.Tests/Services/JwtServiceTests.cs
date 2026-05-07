using Xunit;
using FluentAssertions;
using System.IdentityModel.Tokens.Jwt;
using TableMasterApi.Service;
using TableMasterApi.Tests.Fixtures;

namespace TableMasterApi.Tests.Services
{
    /// <summary>
    /// Tests unitaires pour JwtService - Gestion des tokens JWT
    /// </summary>
    public class JwtServiceTests : IClassFixture<JwtConfigFixture>
    {
        private readonly JwtService _jwtService;
        private readonly JwtConfigFixture _fixture;

        public JwtServiceTests(JwtConfigFixture fixture)
        {
            _fixture = fixture;
            _jwtService = new JwtService(_fixture.JwtConfigOptions);
        }

        #region Tests d'extraction du token

        [Fact]
        public void ExtractTokenFromAuthorization_WithValidBearerToken_ShouldReturnToken()
        {
            // Arrange
            var authorizationHeader = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test.signature";

            // Act
            var result = _jwtService.ExtractTokenFromAuthorization(authorizationHeader);

            // Assert
            result.Should().Be("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test.signature");
        }

        [Theory]
        [InlineData("")]
        [InlineData("InvalidHeader")]
        [InlineData("Basic credentials")]
        public void ExtractTokenFromAuthorization_WithInvalidHeader_ShouldReturnEmpty(string authorizationHeader)
        {
            // Act
            var result = _jwtService.ExtractTokenFromAuthorization(authorizationHeader);

            // Assert
            result.Should().Be("");
        }

        [Fact]
        public void ExtractTokenFromAuthorization_WithWhitespace_ShouldHandleCorrectly()
        {
            // Arrange
            var authorizationHeader = "Bearer  token123  ";

            // Act
            var result = _jwtService.ExtractTokenFromAuthorization(authorizationHeader);

            // Assert
            result.Should().Be("token123");
        }

        #endregion

        #region Tests de génération de token

        [Fact]
        public void GenerateAccessToken_WithValidUserId_ShouldCreateValidJwt()
        {
            // Arrange
            long userId = 123;

            // Act
            var token = _jwtService.GenerateAccessToken(userId);

            // Assert
            token.Should().NotBeNullOrEmpty();

            // Vérifier que c'est un JWT valide
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

            jwtToken.Should().NotBeNull();
            jwtToken!.Issuer.Should().Be(_fixture.JwtConfig.Issuer);
            jwtToken.Audiences.Should().Contain(_fixture.JwtConfig.Audience);
        }

        [Fact]
        public void GenerateAccessToken_ShouldIncludeUserIdInClaims()
        {
            // Arrange
            long userId = 456;

            // Act
            var token = _jwtService.GenerateAccessToken(userId);

            // Assert
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

            var userIdClaim = jwtToken?.Claims.FirstOrDefault(c => c.Type == "UserId");
            userIdClaim.Should().NotBeNull();
            userIdClaim!.Value.Should().Be(userId.ToString());
        }

        [Fact]
        public void GenerateAccessToken_ShouldHaveExpirationInFuture()
        {
            // Arrange
            long userId = 123;
            var beforeGeneration = DateTime.UtcNow;

            // Act
            var token = _jwtService.GenerateAccessToken(userId);
            var afterGeneration = DateTime.UtcNow;

            // Assert
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

            jwtToken!.ValidTo.Should().BeAfter(afterGeneration);
            // Token expire en 5 minutes (avec marge de 1 seconde)
            (jwtToken.ValidTo - afterGeneration).TotalSeconds.Should().BeLessThan(301);
        }

        #endregion

        #region Tests d'extraction d'UserId du token

        [Fact]
        public void ExtractUserIdFromToken_WithValidToken_ShouldReturnCorrectUserId()
        {
            // Arrange
            long expectedUserId = 789;
            var token = _jwtService.GenerateAccessToken(expectedUserId);

            // Act
            var extractedUserId = _jwtService.ExtractUserIdFromToken(token);

            // Assert
            extractedUserId.Should().Be(expectedUserId);
        }

        [Fact]
        public void ExtractUserIdFromToken_WithInvalidToken_ShouldThrowException()
        {
            // Arrange
            var invalidToken = "invalid.token.here";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _jwtService.ExtractUserIdFromToken(invalidToken));
        }

        [Fact]
        public void ExtractUserIdFromToken_WithMultipleUsers_ShouldExtractCorrectId()
        {
            // Arrange
            var userIds = new[] { 1L, 100L, 9999L };
            var tokens = userIds.Select(id => _jwtService.GenerateAccessToken(id)).ToList();

            // Act & Assert
            for (int i = 0; i < userIds.Length; i++)
            {
                var extractedId = _jwtService.ExtractUserIdFromToken(tokens[i]);
                extractedId.Should().Be(userIds[i]);
            }
        }

        #endregion

        #region Tests de refresh token

        [Fact]
        public void GenerateRefreshToken_ShouldReturnValidBase64String()
        {
            // Act
            var refreshToken = _jwtService.GenerateRefreshToken();

            // Assert
            refreshToken.Should().NotBeNullOrEmpty();
            
            // Vérifier que c'est du Base64 valide
            var action = () => Convert.FromBase64String(refreshToken);
            action.Should().NotThrow();
        }

        [Fact]
        public void GenerateRefreshToken_ShouldGenerateDifferentTokensEachTime()
        {
            // Act
            var token1 = _jwtService.GenerateRefreshToken();
            var token2 = _jwtService.GenerateRefreshToken();
            var token3 = _jwtService.GenerateRefreshToken();

            // Assert
            token1.Should().NotBe(token2);
            token2.Should().NotBe(token3);
            token1.Should().NotBe(token3);
        }

        [Fact]
        public void GenerateRefreshToken_ShouldHaveSufficientLength()
        {
            // Act
            var refreshToken = _jwtService.GenerateRefreshToken();

            // Assert
            // 64 bytes en Base64 donnent environ 88 caractères
            var decodedBytes = Convert.FromBase64String(refreshToken);
            decodedBytes.Length.Should().Be(64);
        }

        #endregion
    }
}

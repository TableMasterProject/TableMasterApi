using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace TableMasterApi.Tests.Services
{
    public class CurrentUserServiceTests
    {
        private readonly CurrentUserService _service = new();

        [Fact]
        public void GetUserId_ShouldReturnUserIdClaim_WhenPresent()
        {
            var user = CreatePrincipal(new Claim("UserId", "42"));

            var result = _service.GetUserId(user);

            result.Should().Be(42);
        }

        [Fact]
        public void GetUserId_ShouldFallbackToNameIdentifier_WhenUserIdClaimIsMissing()
        {
            var user = CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "43"));

            var result = _service.GetUserId(user);

            result.Should().Be(43);
        }

        [Fact]
        public void GetUserId_ShouldFallbackToJwtSubject_WhenOtherClaimsAreMissing()
        {
            var user = CreatePrincipal(new Claim(JwtRegisteredClaimNames.Sub, "44"));

            var result = _service.GetUserId(user);

            result.Should().Be(44);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("")]
        public void GetUserId_ShouldThrowUnauthorized_WhenClaimIsInvalid(string rawUserId)
        {
            var user = CreatePrincipal(new Claim("UserId", rawUserId));

            var action = () => _service.GetUserId(user);

            action.Should().Throw<UnauthorizedAccessException>();
        }

        [Fact]
        public void GetUserId_ShouldThrowUnauthorized_WhenClaimIsMissing()
        {
            var user = CreatePrincipal();

            var action = () => _service.GetUserId(user);

            action.Should().Throw<UnauthorizedAccessException>();
        }

        private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        }
    }
}

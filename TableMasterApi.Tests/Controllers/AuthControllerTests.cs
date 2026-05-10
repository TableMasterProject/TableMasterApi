using Microsoft.AspNetCore.Mvc;
using TableMasterApi.Controllers;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Model;

namespace TableMasterApi.Tests.Controllers
{
    public class AuthControllerTests : IClassFixture<JwtConfigFixture>
    {
        private readonly JwtService _jwtService;

        public AuthControllerTests(JwtConfigFixture fixture)
        {
            _jwtService = new JwtService(fixture.JwtConfigOptions);
        }

        [Fact]
        public async Task Login_ShouldReturnOk_AndPersistRefreshToken_WhenCredentialsAreValid()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();

            var user = new UserDb
            {
                Id = 17,
                Email = "chef@example.com",
                Password = "hashed-password",
                FirstName = "Chef",
                LastName = "Tablemaster",
                AccountType = 1,
                CreatedAt = DateTime.UtcNow
            };

            var login = new LoginUserIn
            {
                Email = user.Email,
                Password = "plain-password"
            };

            string? savedHash = null;
            DateTime savedExpiry = default;

            userDal.Setup(x => x.GetUserByEmail(login.Email)).ReturnsAsync(user);
            authDal.Setup(x => x.VerifyPassword(user.Password, login.Password)).Returns(true);
            userDal.Setup(x => x.SaveRefreshToken(user.Id, It.IsAny<string>(), It.IsAny<DateTime>()))
                .Callback<long, string, DateTime>((_, hashedToken, expiry) =>
                {
                    savedHash = hashedToken;
                    savedExpiry = expiry;
                })
                .ReturnsAsync(true);

            var controller = new AuthController(userDal.Object, authDal.Object, _jwtService);

            var result = await controller.Login(login);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var payload = ok.Value.Should().BeOfType<LoginUserOut>().Subject;

            payload.User.Id.Should().Be(user.Id);
            payload.User.Email.Should().Be(user.Email);
            payload.AccessToken.Should().NotBeNullOrWhiteSpace();
            payload.RefreshToken.Should().NotBeNullOrWhiteSpace();
            _jwtService.ExtractUserIdFromToken(payload.AccessToken).Should().Be(user.Id);
            savedHash.Should().Be(_jwtService.HashToken(payload.RefreshToken));
            savedExpiry.Should().BeAfter(DateTime.UtcNow.AddDays(29));

            userDal.Verify(x => x.GetUserByEmail(login.Email), Times.Once);
            authDal.Verify(x => x.VerifyPassword(user.Password, login.Password), Times.Once);
            userDal.Verify(x => x.SaveRefreshToken(user.Id, It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public async Task Login_ShouldReturn404_WhenPasswordIsInvalid()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();

            var user = new UserDb
            {
                Id = 18,
                Email = "chef@example.com",
                Password = "hashed-password",
                FirstName = "Chef",
                LastName = "Tablemaster",
                AccountType = 1,
                CreatedAt = DateTime.UtcNow
            };

            userDal.Setup(x => x.GetUserByEmail(user.Email)).ReturnsAsync(user);
            authDal.Setup(x => x.VerifyPassword(user.Password, "wrong-password")).Returns(false);

            var controller = new AuthController(userDal.Object, authDal.Object, _jwtService);

            var result = await controller.Login(new LoginUserIn
            {
                Email = user.Email,
                Password = "wrong-password"
            });

            var error = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
            error.StatusCode.Should().Be(404);
            error.Value.Should().Be("Mot de passe incorecte");

            userDal.Verify(x => x.SaveRefreshToken(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task Login_ShouldReturnNotFound_WhenUserDoesNotExist()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();
            var login = new LoginUserIn { Email = "missing@example.com", Password = "Password123!" };

            userDal.Setup(x => x.GetUserByEmail(login.Email)).ReturnsAsync((UserDb?)null);

            var controller = new AuthController(userDal.Object, authDal.Object, _jwtService);

            var result = await controller.Login(login);

            result.Result.Should().BeOfType<NotFoundObjectResult>();
            authDal.Verify(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Login_ShouldReturnBadRequest_WhenCredentialsAreBlank()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();
            var controller = new AuthController(userDal.Object, authDal.Object, _jwtService);

            var result = await controller.Login(new LoginUserIn { Email = "", Password = " " });

            result.Result.Should().BeOfType<BadRequestResult>();
            userDal.Verify(x => x.GetUserByEmail(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Refresh_ShouldRotateTokens_WhenRefreshTokenIsValid()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();
            var refreshToken = _jwtService.GenerateRefreshToken();
            var hashedInput = _jwtService.HashToken(refreshToken);

            var user = new UserDb
            {
                Id = 27,
                Email = "refresh@example.com",
                Password = "hashed-password",
                FirstName = "Refresh",
                LastName = "User",
                AccountType = 1,
                CreatedAt = DateTime.UtcNow
            };

            string? deletedHash = null;
            string? savedHash = null;

            userDal.Setup(x => x.GetUserIdByRefreshToken(hashedInput)).ReturnsAsync(user.Id);
            userDal.Setup(x => x.GetUserById(user.Id)).ReturnsAsync(user);
            userDal.Setup(x => x.DeleteRefreshToken(It.IsAny<string>()))
                .Callback<string>(hash => deletedHash = hash)
                .ReturnsAsync(true);
            userDal.Setup(x => x.SaveRefreshToken(user.Id, It.IsAny<string>(), It.IsAny<DateTime>()))
                .Callback<long, string, DateTime>((_, hash, __) => savedHash = hash)
                .ReturnsAsync(true);

            var controller = new AuthController(userDal.Object, authDal.Object, _jwtService);

            var result = await controller.Refresh(new LoginTokenIn { RefreshToken = refreshToken });

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var payload = ok.Value.Should().BeOfType<LoginUserOut>().Subject;

            payload.User.Id.Should().Be(user.Id);
            payload.AccessToken.Should().NotBeNullOrWhiteSpace();
            payload.RefreshToken.Should().NotBeNullOrWhiteSpace();
            payload.RefreshToken.Should().NotBe(refreshToken);
            _jwtService.ExtractUserIdFromToken(payload.AccessToken).Should().Be(user.Id);
            deletedHash.Should().Be(hashedInput);
            savedHash.Should().Be(_jwtService.HashToken(payload.RefreshToken));

            userDal.Verify(x => x.GetUserIdByRefreshToken(hashedInput), Times.Once);
            userDal.Verify(x => x.DeleteRefreshToken(hashedInput), Times.Once);
            userDal.Verify(x => x.SaveRefreshToken(user.Id, It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public async Task Refresh_ShouldReturnUnauthorized_WhenRefreshTokenDoesNotExist()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();
            var refreshToken = _jwtService.GenerateRefreshToken();
            var hashedInput = _jwtService.HashToken(refreshToken);

            userDal.Setup(x => x.GetUserIdByRefreshToken(hashedInput)).ReturnsAsync((long?)null);

            var controller = new AuthController(userDal.Object, authDal.Object, _jwtService);

            var result = await controller.Refresh(new LoginTokenIn { RefreshToken = refreshToken });

            var unauthorized = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorized.Value.Should().Be("Session expirée");

            userDal.Verify(x => x.DeleteRefreshToken(It.IsAny<string>()), Times.Never);
            userDal.Verify(x => x.SaveRefreshToken(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task Refresh_ShouldReturnBadRequest_WhenRefreshTokenIsBlank()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();
            var controller = new AuthController(userDal.Object, authDal.Object, _jwtService);

            var result = await controller.Refresh(new LoginTokenIn { RefreshToken = "" });

            result.Result.Should().BeOfType<BadRequestResult>();
            userDal.Verify(x => x.GetUserIdByRefreshToken(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Refresh_ShouldReturnUnauthorized_WhenUserNoLongerExists()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();
            var refreshToken = _jwtService.GenerateRefreshToken();
            var hashedInput = _jwtService.HashToken(refreshToken);

            userDal.Setup(x => x.GetUserIdByRefreshToken(hashedInput)).ReturnsAsync(27L);
            userDal.Setup(x => x.GetUserById(27)).ReturnsAsync((UserDb?)null);

            var controller = new AuthController(userDal.Object, authDal.Object, _jwtService);

            var result = await controller.Refresh(new LoginTokenIn { RefreshToken = refreshToken });

            result.Result.Should().BeOfType<UnauthorizedObjectResult>();
            userDal.Verify(x => x.DeleteRefreshToken(It.IsAny<string>()), Times.Never);
            userDal.Verify(x => x.SaveRefreshToken(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }
    }
}

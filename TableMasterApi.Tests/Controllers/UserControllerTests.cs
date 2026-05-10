using Microsoft.AspNetCore.Mvc;
using TableMasterApi.Controllers;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Model;
using TableMasterApi.Tests.TestSupport;

namespace TableMasterApi.Tests.Controllers
{
    public class UserControllerTests : IClassFixture<JwtConfigFixture>
    {
        private readonly JwtService _jwtService;

        public UserControllerTests(JwtConfigFixture fixture)
        {
            _jwtService = new JwtService(fixture.JwtConfigOptions);
        }

        [Fact]
        public async Task GetUserById_ShouldReturnOkWithoutPassword_WhenUserExists()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();
            var user = TestData.UserDb(42);
            userDal.Setup(x => x.GetUserById(42)).ReturnsAsync(user);
            var controller = new UserController(userDal.Object, authDal.Object, _jwtService);

            var result = await controller.GetUserById(42);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(user.ToPublicUser());
        }

        [Fact]
        public async Task GetUserById_ShouldReturnNotFound_WhenUserDoesNotExist()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();
            userDal.Setup(x => x.GetUserById(42)).ReturnsAsync((UserDb?)null);
            var controller = new UserController(userDal.Object, authDal.Object, _jwtService);

            var result = await controller.GetUserById(42);

            result.Result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task PutUser_ShouldUseCurrentUserId()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();
            var controller = new UserController(userDal.Object, authDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);
            var input = TestData.UserIn();
            var updated = TestData.UserOut(42);

            userDal.Setup(x => x.PutUser(42, input)).ReturnsAsync(updated);

            var result = await controller.PutUser(input);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(updated);
            userDal.Verify(x => x.PutUser(42, input), Times.Once);
        }

        [Fact]
        public async Task AddUser_ShouldReturnOk_AndStoreRefreshTokenHash()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();

            var input = new UserIn
            {
                Email = "new-user@example.com",
                Password = "Password123!",
                FirstName = "New",
                LastName = "User",
                AccountType = 1
            };

            var createdUser = new UserOut
            {
                Id = 42,
                Email = input.Email,
                FirstName = input.FirstName,
                LastName = input.LastName,
                AccountType = input.AccountType,
                CreatedAt = DateTime.UtcNow
            };

            string? savedHash = null;

            userDal.Setup(x => x.AddUser(input)).ReturnsAsync(createdUser);
            userDal.Setup(x => x.SaveRefreshToken(createdUser.Id, It.IsAny<string>(), It.IsAny<DateTime>()))
                .Callback<long, string, DateTime>((_, hash, __) => savedHash = hash)
                .ReturnsAsync(true);

            var controller = new UserController(userDal.Object, authDal.Object, _jwtService);

            var result = await controller.AddUser(input);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var payload = ok.Value.Should().BeOfType<LoginUserOut>().Subject;

            payload.User.Id.Should().Be(createdUser.Id);
            payload.User.Email.Should().Be(createdUser.Email);
            payload.AccessToken.Should().NotBeNullOrWhiteSpace();
            payload.RefreshToken.Should().NotBeNullOrWhiteSpace();
            savedHash.Should().Be(_jwtService.HashToken(payload.RefreshToken));
            _jwtService.ExtractUserIdFromToken(payload.AccessToken).Should().Be(createdUser.Id);

            userDal.Verify(x => x.AddUser(input), Times.Once);
            userDal.Verify(x => x.SaveRefreshToken(createdUser.Id, It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public async Task PutPassword_ShouldReturnOk_WhenOldPasswordIsValid()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();
            var controller = new UserController(userDal.Object, authDal.Object, _jwtService);
            var userId = 99L;
            var token = _jwtService.GenerateAccessToken(userId);

            ControllerTestHelper.SetBearerToken(controller, token);

            var storedUser = new UserDb
            {
                Id = userId,
                Email = "password@example.com",
                Password = "stored-hash",
                FirstName = "Password",
                LastName = "Tester",
                AccountType = 1,
                CreatedAt = DateTime.UtcNow
            };

            userDal.Setup(x => x.GetUserById(userId)).ReturnsAsync(storedUser);
            authDal.Setup(x => x.VerifyPassword(storedUser.Password, "OldPassword!1")).Returns(true);
            userDal.Setup(x => x.PutPassword(storedUser.Id, "NewPassword!2")).ReturnsAsync(true);

            var result = await controller.PutPassword(new PasswordEntity
            {
                OldPassword = "OldPassword!1",
                NewPassword = "NewPassword!2"
            });

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().Be(true);

            userDal.Verify(x => x.GetUserById(userId), Times.Once);
            authDal.Verify(x => x.VerifyPassword(storedUser.Password, "OldPassword!1"), Times.Once);
            userDal.Verify(x => x.PutPassword(storedUser.Id, "NewPassword!2"), Times.Once);
        }

        [Fact]
        public async Task PutPassword_ShouldReturnNotFound_WhenCurrentUserDoesNotExist()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();
            var controller = new UserController(userDal.Object, authDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 99);

            userDal.Setup(x => x.GetUserById(99)).ReturnsAsync((UserDb?)null);

            var result = await controller.PutPassword(new PasswordEntity
            {
                OldPassword = "OldPassword!1",
                NewPassword = "NewPassword!2"
            });

            result.Result.Should().BeOfType<NotFoundResult>();
            authDal.Verify(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task PutPassword_ShouldReturnNotFound_WhenOldPasswordIsInvalid()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();
            var controller = new UserController(userDal.Object, authDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 99);
            var storedUser = TestData.UserDb(99);

            userDal.Setup(x => x.GetUserById(99)).ReturnsAsync(storedUser);
            authDal.Setup(x => x.VerifyPassword(storedUser.Password, "wrong")).Returns(false);

            var result = await controller.PutPassword(new PasswordEntity
            {
                OldPassword = "wrong",
                NewPassword = "NewPassword!2"
            });

            result.Result.Should().BeOfType<NotFoundObjectResult>();
            userDal.Verify(x => x.PutPassword(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task PutPassword_ShouldReturnNotFound_WhenDalDoesNotChangePassword()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();
            var controller = new UserController(userDal.Object, authDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 99);
            var storedUser = TestData.UserDb(99);

            userDal.Setup(x => x.GetUserById(99)).ReturnsAsync(storedUser);
            authDal.Setup(x => x.VerifyPassword(storedUser.Password, "OldPassword!1")).Returns(true);
            userDal.Setup(x => x.PutPassword(99, "NewPassword!2")).ReturnsAsync(false);

            var result = await controller.PutPassword(new PasswordEntity
            {
                OldPassword = "OldPassword!1",
                NewPassword = "NewPassword!2"
            });

            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task DeleteUser_ShouldReturnOk_WhenPasswordDeletionSucceeds()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();
            var controller = new UserController(userDal.Object, authDal.Object, _jwtService);
            var userId = 123L;
            var token = _jwtService.GenerateAccessToken(userId);

            ControllerTestHelper.SetBearerToken(controller, token);

            userDal.Setup(x => x.DeletePassword(userId)).ReturnsAsync(true);

            var result = await controller.DeleteUser();

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().Be(true);

            userDal.Verify(x => x.DeletePassword(userId), Times.Once);
        }

        [Fact]
        public async Task DeleteUser_ShouldReturnNotFound_WhenDeletionFails()
        {
            var userDal = new Mock<IUserDAL>();
            var authDal = new Mock<IAuthDAL>();
            var controller = new UserController(userDal.Object, authDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 123);

            userDal.Setup(x => x.DeletePassword(123)).ReturnsAsync(false);

            var result = await controller.DeleteUser();

            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }
    }
}

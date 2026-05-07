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
                Password = input.Password,
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
            savedHash.Should().Be(payload.RefreshToken);
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

            var storedUser = new UserOut
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
            userDal.Setup(x => x.PutPassword(storedUser, "NewPassword!2")).ReturnsAsync(true);

            var result = await controller.PutPassword(new PasswordEntity
            {
                OldPassword = "OldPassword!1",
                NewPassword = "NewPassword!2"
            });

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().Be(true);

            userDal.Verify(x => x.GetUserById(userId), Times.Once);
            authDal.Verify(x => x.VerifyPassword(storedUser.Password, "OldPassword!1"), Times.Once);
            userDal.Verify(x => x.PutPassword(storedUser, "NewPassword!2"), Times.Once);
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
    }
}
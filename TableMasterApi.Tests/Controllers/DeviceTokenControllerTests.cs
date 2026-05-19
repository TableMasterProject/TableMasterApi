using Microsoft.AspNetCore.Mvc;
using TableMasterApi.Controllers;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Tests.TestSupport;

namespace TableMasterApi.Tests.Controllers
{
    public class DeviceTokenControllerTests : IClassFixture<JwtConfigFixture>
    {
        private readonly JwtService _jwtService;

        public DeviceTokenControllerTests(JwtConfigFixture fixture)
        {
            _jwtService = new JwtService(fixture.JwtConfigOptions);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task RegisterDeviceToken_ShouldReturnBadRequest_WhenTokenIsMissing(string? token)
        {
            var dal = new Mock<IDeviceTokenDAL>();
            var controller = new DeviceTokenController(dal.Object, _jwtService);

            var result = await controller.RegisterDeviceToken(new DeviceTokenIn { DeviceToken = token!, DevicePlatform = "ios" });

            result.Should().BeOfType<BadRequestObjectResult>();
            dal.Verify(x => x.SaveDeviceTokenAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RegisterDeviceToken_ShouldSaveTokenWithUnknownPlatform_WhenPlatformIsNull()
        {
            var dal = new Mock<IDeviceTokenDAL>();
            var controller = new DeviceTokenController(dal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            var result = await controller.RegisterDeviceToken(new DeviceTokenIn { DeviceToken = "device-token", DevicePlatform = null! });

            result.Should().BeOfType<OkResult>();
            dal.Verify(x => x.SaveDeviceTokenAsync(42, "device-token", "unknown"), Times.Once);
        }

        [Fact]
        public async Task DeleteDeviceToken_ShouldReturnBadRequest_WhenTokenIsMissing()
        {
            var dal = new Mock<IDeviceTokenDAL>();
            var controller = new DeviceTokenController(dal.Object, _jwtService);

            var result = await controller.DeleteDeviceToken("");

            result.Should().BeOfType<BadRequestObjectResult>();
            dal.Verify(x => x.DeleteDeviceTokenAsync(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteDeviceToken_ShouldDeleteTokenForCurrentUser()
        {
            var dal = new Mock<IDeviceTokenDAL>();
            var controller = new DeviceTokenController(dal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            var result = await controller.DeleteDeviceToken("device-token");

            result.Should().BeOfType<OkResult>();
            dal.Verify(x => x.DeleteDeviceTokenAsync(42, "device-token"), Times.Once);
        }

        [Fact]
        public async Task RegisterDeviceToken_ShouldReturnBadRequest_WhenModelIsNull()
        {
            var dal = new Mock<IDeviceTokenDAL>();
            var controller = new DeviceTokenController(dal.Object, _jwtService);

            var result = await controller.RegisterDeviceToken(null!);

            result.Should().BeOfType<BadRequestObjectResult>();
            dal.Verify(x => x.SaveDeviceTokenAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("ios")]
        [InlineData("android")]
        public async Task RegisterDeviceToken_ShouldPersistExplicitPlatform(string platform)
        {
            var dal = new Mock<IDeviceTokenDAL>();
            var controller = new DeviceTokenController(dal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 17);

            var result = await controller.RegisterDeviceToken(new DeviceTokenIn { DeviceToken = "tok", DevicePlatform = platform });

            result.Should().BeOfType<OkResult>();
            dal.Verify(x => x.SaveDeviceTokenAsync(17, "tok", platform), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task DeleteDeviceToken_ShouldReturnBadRequest_WhenTokenIsNullOrWhitespace(string? token)
        {
            var dal = new Mock<IDeviceTokenDAL>();
            var controller = new DeviceTokenController(dal.Object, _jwtService);

            var result = await controller.DeleteDeviceToken(token!);

            result.Should().BeOfType<BadRequestObjectResult>();
            dal.Verify(x => x.DeleteDeviceTokenAsync(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
        }
    }
}

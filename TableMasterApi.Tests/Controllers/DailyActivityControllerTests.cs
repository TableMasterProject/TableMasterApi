using Microsoft.AspNetCore.Mvc;
using TableMasterApi.Controllers;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Tests.TestSupport;

namespace TableMasterApi.Tests.Controllers
{
    public class DailyActivityControllerTests : IClassFixture<JwtConfigFixture>
    {
        private readonly JwtService _jwtService;

        public DailyActivityControllerTests(JwtConfigFixture fixture)
        {
            _jwtService = new JwtService(fixture.JwtConfigOptions);
        }

        [Fact]
        public async Task GetByRestaurant_ShouldReturnOkWithActivities()
        {
            var dal = new Mock<IDailyActivityDAL>();
            var controller = CreateController(dal);
            var activities = new[] { TestData.DailyActivityOut() };

            dal.Setup(x => x.GetByRestaurantAsync(10)).ReturnsAsync(activities);

            var result = await controller.GetByRestaurant(10);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(activities);
        }

        [Fact]
        public async Task Post_ShouldReturnBadRequest_WhenInputIsNull()
        {
            var dal = new Mock<IDailyActivityDAL>();
            var controller = CreateController(dal);

            var result = await controller.Post(null!);

            result.Should().BeOfType<BadRequestResult>();
            dal.Verify(x => x.InsertAsync(It.IsAny<DailyActivityIn>()), Times.Never);
        }

        [Fact]
        public async Task Put_ShouldReturnUnauthorized_WhenRestaurantBelongsToAnotherUser()
        {
            var dal = new Mock<IDailyActivityDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(dal, restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            dal.Setup(x => x.GetByIdAsync(7)).ReturnsAsync(TestData.DailyActivityOut());
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 99));

            var result = await controller.Put(7, TestData.DailyActivityIn());

            result.Should().BeOfType<UnauthorizedObjectResult>();
            dal.Verify(x => x.UpdateAsync(It.IsAny<long>(), It.IsAny<DailyActivityIn>()), Times.Never);
        }

        [Fact]
        public async Task Put_ShouldReturnOk_WhenUserOwnsRestaurant()
        {
            var dal = new Mock<IDailyActivityDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(dal, restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);
            var input = TestData.DailyActivityIn();
            var updated = TestData.DailyActivityOut();

            dal.Setup(x => x.GetByIdAsync(7)).ReturnsAsync(TestData.DailyActivityOut());
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 42));
            dal.Setup(x => x.UpdateAsync(7, input)).ReturnsAsync(updated);

            var result = await controller.Put(7, input);

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(updated);
        }

        [Fact]
        public async Task Delete_ShouldReturnNotFound_WhenActivityDoesNotExist()
        {
            var dal = new Mock<IDailyActivityDAL>();
            var controller = CreateController(dal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            dal.Setup(x => x.GetByIdAsync(7)).ReturnsAsync((DailyActivityOut?)null);

            var result = await controller.Delete(7);

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task Delete_ShouldReturnOk_WhenUserOwnsRestaurant()
        {
            var dal = new Mock<IDailyActivityDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(dal, restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            dal.Setup(x => x.GetByIdAsync(7)).ReturnsAsync(TestData.DailyActivityOut());
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 42));
            dal.Setup(x => x.DeleteAsync(7)).ReturnsAsync(true);

            var result = await controller.Delete(7);

            result.Should().BeOfType<OkResult>();
        }

        private DailyActivityController CreateController(
            Mock<IDailyActivityDAL>? dal = null,
            Mock<IRestaurantDAL>? restaurantDal = null)
        {
            return new DailyActivityController(
                (dal ?? new Mock<IDailyActivityDAL>()).Object,
                (restaurantDal ?? new Mock<IRestaurantDAL>()).Object,
                _jwtService);
        }
    }
}

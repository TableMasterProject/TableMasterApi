using Microsoft.AspNetCore.Mvc;
using TableMasterApi.Controllers;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Tests.TestSupport;

namespace TableMasterApi.Tests.Controllers
{
    public class ClosedDayExceptionControllerTests : IClassFixture<JwtConfigFixture>
    {
        private readonly JwtService _jwtService;

        public ClosedDayExceptionControllerTests(JwtConfigFixture fixture)
        {
            _jwtService = new JwtService(fixture.JwtConfigOptions);
        }

        [Fact]
        public async Task GetByRestaurant_ShouldReturnOkWithExceptions()
        {
            var dal = new Mock<IClosedDayExceptionDAL>();
            var controller = CreateController(dal);
            var exceptions = new[] { TestData.ClosedDayExceptionOut() };

            dal.Setup(x => x.GetByRestaurantAsync(10)).ReturnsAsync(exceptions);

            var result = await controller.GetByRestaurant(10);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(exceptions);
        }

        [Fact]
        public async Task Post_ShouldReturnBadRequest_WhenInputIsNull()
        {
            var dal = new Mock<IClosedDayExceptionDAL>();
            var controller = CreateController(dal);

            var result = await controller.Post(null!);

            result.Result.Should().BeOfType<BadRequestResult>();
            dal.Verify(x => x.InsertAsync(It.IsAny<ClosedDayExceptionIn>()), Times.Never);
        }

        [Fact]
        public async Task Post_ShouldReturnOk_WhenInputIsValid()
        {
            var dal = new Mock<IClosedDayExceptionDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(dal, restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);
            var input = TestData.ClosedDayExceptionIn();
            var created = TestData.ClosedDayExceptionOut();

            restaurantDal.Setup(x => x.GetRestaurantById(input.RestaurantId)).ReturnsAsync(TestData.RestaurantOut(userId: 42));
            dal.Setup(x => x.InsertAsync(input)).ReturnsAsync(created);

            var result = await controller.Post(input);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(created);
        }

        [Fact]
        public async Task Post_ShouldReturnForbidden_WhenRestaurantBelongsToAnotherUser()
        {
            var dal = new Mock<IClosedDayExceptionDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(dal, restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);
            var input = TestData.ClosedDayExceptionIn();

            restaurantDal.Setup(x => x.GetRestaurantById(input.RestaurantId))
                .ReturnsAsync(TestData.RestaurantOut(userId: 99));

            var result = await controller.Post(input);

            result.Result.Should().BeOfType<ForbidResult>();
            dal.Verify(x => x.InsertAsync(It.IsAny<ClosedDayExceptionIn>()), Times.Never);
        }

        [Fact]
        public async Task Put_ShouldReturnForbidden_WhenRestaurantBelongsToAnotherUser()
        {
            var dal = new Mock<IClosedDayExceptionDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(dal, restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            dal.Setup(x => x.GetByIdAsync(8)).ReturnsAsync(TestData.ClosedDayExceptionOut());
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 99));

            var result = await controller.Put(8, TestData.ClosedDayExceptionIn());

            result.Result.Should().BeOfType<ForbidResult>();
            dal.Verify(x => x.UpdateAsync(It.IsAny<long>(), It.IsAny<ClosedDayExceptionIn>()), Times.Never);
        }

        [Fact]
        public async Task Put_ShouldReturnOk_WhenUserOwnsRestaurant()
        {
            var dal = new Mock<IClosedDayExceptionDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(dal, restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);
            var input = TestData.ClosedDayExceptionIn();
            var updated = TestData.ClosedDayExceptionOut();

            dal.Setup(x => x.GetByIdAsync(8)).ReturnsAsync(TestData.ClosedDayExceptionOut());
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 42));
            dal.Setup(x => x.UpdateAsync(8, input)).ReturnsAsync(updated);

            var result = await controller.Put(8, input);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(updated);
        }

        [Fact]
        public async Task Delete_ShouldReturnOk_WhenUserOwnsRestaurant()
        {
            var dal = new Mock<IClosedDayExceptionDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(dal, restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            dal.Setup(x => x.GetByIdAsync(8)).ReturnsAsync(TestData.ClosedDayExceptionOut());
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 42));
            dal.Setup(x => x.DeleteAsync(8)).ReturnsAsync(true);

            var result = await controller.Delete(8);

            result.Should().BeOfType<OkResult>();
        }

        private ClosedDayExceptionController CreateController(
            Mock<IClosedDayExceptionDAL>? dal = null,
            Mock<IRestaurantDAL>? restaurantDal = null)
        {
            return new ClosedDayExceptionController(
                (dal ?? new Mock<IClosedDayExceptionDAL>()).Object,
                (restaurantDal ?? new Mock<IRestaurantDAL>()).Object,
                _jwtService);
        }
    }
}

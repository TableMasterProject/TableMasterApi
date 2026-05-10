using Microsoft.AspNetCore.Mvc;
using TableMasterApi.Controllers;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Tests.TestSupport;

namespace TableMasterApi.Tests.Controllers
{
    public class MenuControllerTests : IClassFixture<JwtConfigFixture>
    {
        private readonly JwtService _jwtService;

        public MenuControllerTests(JwtConfigFixture fixture)
        {
            _jwtService = new JwtService(fixture.JwtConfigOptions);
        }

        [Fact]
        public async Task GetByRestaurant_ShouldReturnOkWithMenus()
        {
            var menuDal = new Mock<IMenuDAL>();
            var controller = CreateController(menuDal);
            var menus = new[] { TestData.MenuOut() };

            menuDal.Setup(x => x.GetByRestaurantAsync(10)).ReturnsAsync(menus);

            var result = await controller.GetByRestaurant(10);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(menus);
        }

        [Fact]
        public async Task Post_ShouldReturnBadRequest_WhenInputIsNull()
        {
            var menuDal = new Mock<IMenuDAL>();
            var controller = CreateController(menuDal);

            var result = await controller.Post(null!);

            result.Should().BeOfType<BadRequestResult>();
            menuDal.Verify(x => x.InsertAsync(It.IsAny<MenuIn>()), Times.Never);
        }

        [Fact]
        public async Task Post_ShouldReturnOk_WhenInputIsValid()
        {
            var menuDal = new Mock<IMenuDAL>();
            var controller = CreateController(menuDal);
            var input = TestData.MenuIn();
            var created = TestData.MenuOut();

            menuDal.Setup(x => x.InsertAsync(input)).ReturnsAsync(created);

            var result = await controller.Post(input);

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(created);
        }

        [Fact]
        public async Task Delete_ShouldReturnUnauthorized_WhenRestaurantBelongsToAnotherUser()
        {
            var menuDal = new Mock<IMenuDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(menuDal, restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            menuDal.Setup(x => x.GetByIdAsync(3)).ReturnsAsync(TestData.MenuOut());
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 99));

            var result = await controller.Delete(3);

            result.Should().BeOfType<UnauthorizedObjectResult>();
            menuDal.Verify(x => x.DeleteAsync(It.IsAny<long>()), Times.Never);
        }

        [Fact]
        public async Task Delete_ShouldReturnOk_WhenUserOwnsRestaurant()
        {
            var menuDal = new Mock<IMenuDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(menuDal, restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            menuDal.Setup(x => x.GetByIdAsync(3)).ReturnsAsync(TestData.MenuOut());
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 42));
            menuDal.Setup(x => x.DeleteAsync(3)).ReturnsAsync(true);

            var result = await controller.Delete(3);

            result.Should().BeOfType<OkResult>();
        }

        private MenuController CreateController(
            Mock<IMenuDAL>? menuDal = null,
            Mock<IRestaurantDAL>? restaurantDal = null)
        {
            return new MenuController(
                (menuDal ?? new Mock<IMenuDAL>()).Object,
                (restaurantDal ?? new Mock<IRestaurantDAL>()).Object,
                _jwtService);
        }
    }
}

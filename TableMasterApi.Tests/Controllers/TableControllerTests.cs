using Microsoft.AspNetCore.Mvc;
using TableMasterApi.Controllers;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Tests.TestSupport;

namespace TableMasterApi.Tests.Controllers
{
    public class TableControllerTests : IClassFixture<JwtConfigFixture>
    {
        private readonly JwtService _jwtService;

        public TableControllerTests(JwtConfigFixture fixture)
        {
            _jwtService = new JwtService(fixture.JwtConfigOptions);
        }

        [Fact]
        public async Task GetTables_ShouldReturnOk_WhenTablesExist()
        {
            var tableDal = new Mock<ITableDAL>();
            var controller = CreateController(tableDal: tableDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);
            var tables = new[] { TestData.TableOut() };

            tableDal.Setup(x => x.GetTablesByRestaurantAsync(10)).ReturnsAsync(tables);

            var result = await controller.GetTables(10);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(tables);
        }

        [Fact]
        public async Task GetTables_ShouldReturnNotFound_WhenDalReturnsNull()
        {
            var tableDal = new Mock<ITableDAL>();
            var controller = CreateController(tableDal: tableDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            tableDal.Setup(x => x.GetTablesByRestaurantAsync(10)).ReturnsAsync((IEnumerable<TableEntityOut>)null!);

            var result = await controller.GetTables(10);

            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task CreateTable_ShouldSetRestaurantIdAndReturnOk_WhenUserOwnsRestaurant()
        {
            var tableDal = new Mock<ITableDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(tableDal, restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);
            var input = TestData.TableIn();
            var created = TestData.TableOut();

            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 42));
            tableDal.Setup(x => x.CreateTable(It.Is<TableEntityIn>(t => t.RestaurantId == 10))).ReturnsAsync(created);

            var result = await controller.CreateTable(10, input);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(created);
            tableDal.Verify(x => x.CreateTable(It.Is<TableEntityIn>(t => t.RestaurantId == 10)), Times.Once);
        }

        [Fact]
        public async Task CreateTable_ShouldReturnForbidden_WhenRestaurantBelongsToAnotherUser()
        {
            var tableDal = new Mock<ITableDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(tableDal, restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 99));

            var result = await controller.CreateTable(10, TestData.TableIn());

            result.Result.Should().BeOfType<ForbidResult>();
            tableDal.Verify(x => x.CreateTable(It.IsAny<TableEntityIn>()), Times.Never);
        }

        [Fact]
        public async Task UpdateTable_ShouldReturnNotFound_WhenTableDoesNotExist()
        {
            var tableDal = new Mock<ITableDAL>();
            var controller = CreateController(tableDal: tableDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            tableDal.Setup(x => x.GetTablesById(4)).ReturnsAsync((TableEntityOut?)null);

            var result = await controller.UpdateTable(4, TestData.TableIn());

            result.Result.Should().BeOfType<NotFoundObjectResult>();
            tableDal.Verify(x => x.UpdateTable(It.IsAny<long>(), It.IsAny<TableEntityIn>()), Times.Never);
        }

        [Fact]
        public async Task Delete_ShouldReturnOk_WhenUserOwnsRestaurant()
        {
            var tableDal = new Mock<ITableDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(tableDal, restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            tableDal.Setup(x => x.GetTablesById(4)).ReturnsAsync(TestData.TableOut());
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 42));
            tableDal.Setup(x => x.DeleteTable(4)).ReturnsAsync(true);

            var result = await controller.Delete(4);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().Be(true);
        }

        [Fact]
        public async Task ReplaceTables_ShouldReturnBadRequest_WhenListIsEmpty()
        {
            var tableDal = new Mock<ITableDAL>();
            var controller = CreateController(tableDal: tableDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            var result = await controller.ReplaceTables(10, []);

            result.Result.Should().BeOfType<BadRequestObjectResult>();
            tableDal.Verify(x => x.ReplaceTablesAsync(It.IsAny<long>(), It.IsAny<IEnumerable<TableEntityIn>>()), Times.Never);
        }

        [Fact]
        public async Task ReplaceTables_ShouldDelegateToDal_WhenUserOwnsRestaurant()
        {
            var tableDal = new Mock<ITableDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(tableDal, restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);
            var input = new List<TableEntityIn> { TestData.TableIn() };
            var output = new[] { TestData.TableOut() };

            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 42));
            tableDal.Setup(x => x.ReplaceTablesAsync(10, input)).ReturnsAsync(output);

            var result = await controller.ReplaceTables(10, input);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(output);
        }

        private TableController CreateController(
            Mock<ITableDAL>? tableDal = null,
            Mock<IRestaurantDAL>? restaurantDal = null)
        {
            return new TableController(
                (tableDal ?? new Mock<ITableDAL>()).Object,
                (restaurantDal ?? new Mock<IRestaurantDAL>()).Object,
                _jwtService);
        }
    }
}

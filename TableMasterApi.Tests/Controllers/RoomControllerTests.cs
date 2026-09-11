using Microsoft.AspNetCore.Mvc;
using TableMasterApi.Controllers;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Tests.TestSupport;

namespace TableMasterApi.Tests.Controllers
{
    public class RoomControllerTests : IClassFixture<JwtConfigFixture>
    {
        private readonly JwtService _jwtService;

        public RoomControllerTests(JwtConfigFixture fixture)
        {
            _jwtService = new JwtService(fixture.JwtConfigOptions);
        }

        [Fact]
        public async Task GetRooms_ShouldReturnOk_WhenRestaurantExists()
        {
            var roomDal = new Mock<IRoomDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RoomController(roomDal.Object, restaurantDal.Object, _jwtService);
            var rooms = new[] { TestData.RoomOut() };

            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut());
            roomDal.Setup(x => x.GetRoomsByRestaurantAsync(10)).ReturnsAsync(rooms);

            var result = await controller.GetRooms(10);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(rooms);
        }

        [Fact]
        public async Task GetRooms_ShouldReturnNotFound_WhenRestaurantDoesNotExist()
        {
            var roomDal = new Mock<IRoomDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RoomController(roomDal.Object, restaurantDal.Object, _jwtService);

            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync((RestaurantOut?)null);

            var result = await controller.GetRooms(10);

            result.Result.Should().BeOfType<NotFoundObjectResult>();
            roomDal.Verify(x => x.GetRoomsByRestaurantAsync(It.IsAny<long>()), Times.Never);
        }

        [Fact]
        public async Task CreateRoom_ShouldReturnOk_WhenRestaurantBelongsToUser()
        {
            var roomDal = new Mock<IRoomDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RoomController(roomDal.Object, restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService.GenerateAccessToken(42));

            var room = new RestaurantRoomIn
            {
                Name = "Terrasse",
                BoundaryPoints = RestaurantRoomDefaults.DefaultBoundary()
            };
            var restaurant = new RestaurantOut { Id = 10, UserId = 42, RestaurantName = "Test" };
            var created = new RestaurantRoomOut { Id = 7, RestaurantId = 10, Name = room.Name };

            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(restaurant);
            roomDal.Setup(x => x.CreateRoomAsync(It.Is<RestaurantRoomIn>(r => r.RestaurantId == 10)))
                .ReturnsAsync(created);

            var result = await controller.CreateRoom(10, room);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(created);
            roomDal.Verify(x => x.CreateRoomAsync(It.Is<RestaurantRoomIn>(r => r.RestaurantId == 10)), Times.Once);
        }

        [Fact]
        public async Task CreateRoom_ShouldReturnForbidden_WhenRestaurantBelongsToAnotherUser()
        {
            var roomDal = new Mock<IRoomDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RoomController(roomDal.Object, restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService.GenerateAccessToken(42));

            restaurantDal.Setup(x => x.GetRestaurantById(10))
                .ReturnsAsync(new RestaurantOut { Id = 10, UserId = 99, RestaurantName = "Test" });

            var result = await controller.CreateRoom(10, new RestaurantRoomIn
            {
                Name = "Salle",
                BoundaryPoints = RestaurantRoomDefaults.DefaultBoundary()
            });

            result.Result.Should().BeOfType<ForbidResult>();
            roomDal.Verify(x => x.CreateRoomAsync(It.IsAny<RestaurantRoomIn>()), Times.Never);
        }

        [Fact]
        public async Task DeleteRoom_ShouldReturnConflict_WhenRoomContainsTables()
        {
            var roomDal = new Mock<IRoomDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RoomController(roomDal.Object, restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService.GenerateAccessToken(42));

            roomDal.Setup(x => x.GetRoomByIdAsync(5))
                .ReturnsAsync(new RestaurantRoomOut { Id = 5, RestaurantId = 10, Name = "Salle" });
            restaurantDal.Setup(x => x.GetRestaurantById(10))
                .ReturnsAsync(new RestaurantOut { Id = 10, UserId = 42, RestaurantName = "Test" });
            roomDal.Setup(x => x.RoomHasTablesAsync(5)).ReturnsAsync(true);

            var result = await controller.DeleteRoom(5);

            result.Result.Should().BeOfType<ConflictObjectResult>();
            roomDal.Verify(x => x.DeleteRoomAsync(It.IsAny<long>()), Times.Never);
        }

        [Fact]
        public async Task UpdateRoom_ShouldReturnOk_WhenUserOwnsRoom()
        {
            var roomDal = new Mock<IRoomDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RoomController(roomDal.Object, restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);
            var input = TestData.RoomIn("Nouvelle salle");
            var updated = TestData.RoomOut();

            roomDal.Setup(x => x.GetRoomByIdAsync(5)).ReturnsAsync(TestData.RoomOut());
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 42));
            roomDal.Setup(x => x.UpdateRoomAsync(5, input)).ReturnsAsync(updated);

            var result = await controller.UpdateRoom(5, input);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(updated);
            input.RestaurantId.Should().Be(10);
        }

        [Fact]
        public async Task UpdateRoom_ShouldReturnBadRequest_WhenNameIsMissing()
        {
            var roomDal = new Mock<IRoomDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RoomController(roomDal.Object, restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            roomDal.Setup(x => x.GetRoomByIdAsync(5)).ReturnsAsync(TestData.RoomOut());
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 42));

            var result = await controller.UpdateRoom(5, TestData.RoomIn(""));

            result.Result.Should().BeOfType<BadRequestObjectResult>();
            roomDal.Verify(x => x.UpdateRoomAsync(It.IsAny<long>(), It.IsAny<RestaurantRoomIn>()), Times.Never);
        }

        [Fact]
        public async Task DeleteRoom_ShouldReturnOk_WhenRoomIsEmptyAndOwned()
        {
            var roomDal = new Mock<IRoomDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RoomController(roomDal.Object, restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            roomDal.Setup(x => x.GetRoomByIdAsync(5)).ReturnsAsync(TestData.RoomOut());
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 42));
            roomDal.Setup(x => x.RoomHasTablesAsync(5)).ReturnsAsync(false);
            roomDal.Setup(x => x.DeleteRoomAsync(5)).ReturnsAsync(true);

            var result = await controller.DeleteRoom(5);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().Be(true);
        }

        [Fact]
        public async Task SaveLayout_ShouldDelegateAtomicLayoutPayload_WhenUserOwnsRoom()
        {
            var roomDal = new Mock<IRoomDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RoomController(roomDal.Object, restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService.GenerateAccessToken(42));

            var layout = new RestaurantRoomLayoutIn
            {
                Room = new RestaurantRoomIn
                {
                    Name = "Salle principale",
                    BoundaryPoints = RestaurantRoomDefaults.DefaultBoundary()
                },
                TablesToAdd =
                [
                    new TableEntityIn { TableNumber = 1, NumberOfSeats = 2 }
                ],
                TablesToUpdate =
                [
                    new TableEntityOut { Id = 8, TableNumber = 2, NumberOfSeats = 4 }
                ],
                TableIdsToDelete = [9]
            };
            var saved = new RestaurantRoomLayoutOut
            {
                Room = new RestaurantRoomOut { Id = 5, RestaurantId = 10, Name = "Salle principale" },
                Tables = []
            };

            roomDal.Setup(x => x.GetRoomByIdAsync(5))
                .ReturnsAsync(new RestaurantRoomOut { Id = 5, RestaurantId = 10, Name = "Salle" });
            restaurantDal.Setup(x => x.GetRestaurantById(10))
                .ReturnsAsync(new RestaurantOut { Id = 10, UserId = 42, RestaurantName = "Test" });
            roomDal.Setup(x => x.SaveLayoutAsync(5, layout)).ReturnsAsync(saved);

            var result = await controller.SaveLayout(5, layout);

            result.Result.Should().BeOfType<OkObjectResult>();
            layout.Room.RestaurantId.Should().Be(10);
            roomDal.Verify(x => x.SaveLayoutAsync(5, layout), Times.Once);
        }

        [Fact]
        public async Task SaveLayout_ShouldReturnForbidden_WhenRestaurantBelongsToAnotherUser()
        {
            var roomDal = new Mock<IRoomDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RoomController(roomDal.Object, restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            roomDal.Setup(x => x.GetRoomByIdAsync(5)).ReturnsAsync(TestData.RoomOut());
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 99));

            var result = await controller.SaveLayout(5, new RestaurantRoomLayoutIn { Room = TestData.RoomIn() });

            result.Result.Should().BeOfType<ForbidResult>();
            roomDal.Verify(x => x.SaveLayoutAsync(It.IsAny<long>(), It.IsAny<RestaurantRoomLayoutIn>()), Times.Never);
        }

        [Fact]
        public async Task SaveLayout_ShouldReturnNotFound_WhenRoomDoesNotExist()
        {
            var roomDal = new Mock<IRoomDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RoomController(roomDal.Object, restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 42);

            roomDal.Setup(x => x.GetRoomByIdAsync(5)).ReturnsAsync((RestaurantRoomOut?)null);

            var result = await controller.SaveLayout(5, new RestaurantRoomLayoutIn { Room = TestData.RoomIn() });

            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }
    }
}

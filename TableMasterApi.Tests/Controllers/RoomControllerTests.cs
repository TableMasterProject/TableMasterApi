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
        public async Task CreateRoom_ShouldReturnUnauthorized_WhenRestaurantBelongsToAnotherUser()
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

            result.Result.Should().BeOfType<UnauthorizedObjectResult>();
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
    }
}

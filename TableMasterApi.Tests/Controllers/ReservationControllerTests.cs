using Microsoft.AspNetCore.Mvc;
using TableMasterApi.Controllers;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Hubs;
using TableMasterApi.Tests.TestSupport;

namespace TableMasterApi.Tests.Controllers
{
    public class ReservationControllerTests : IClassFixture<JwtConfigFixture>
    {
        private readonly JwtService _jwtService;

        public ReservationControllerTests(JwtConfigFixture fixture)
        {
            _jwtService = new JwtService(fixture.JwtConfigOptions);
        }

        [Fact]
        public async Task GetReservations_ShouldReturnOk_WhenReservationsExist()
        {
            var reservationDal = new Mock<IReservationDAL>();
            var controller = CreateController(reservationDal: reservationDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 12);
            var search = new SearchReservations();
            var reservations = new[] { TestData.ReservationOut() };

            reservationDal.Setup(x => x.GetReservations(search)).ReturnsAsync(reservations);

            var result = await controller.GetReservations(search);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(reservations);
        }

        [Fact]
        public async Task GetReservations_ShouldReturnBadRequest_WhenSearchIsNull()
        {
            var reservationDal = new Mock<IReservationDAL>();
            var controller = CreateController(reservationDal: reservationDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 12);

            var result = await controller.GetReservations(null!);

            result.Result.Should().BeOfType<BadRequestResult>();
            reservationDal.Verify(x => x.GetReservations(It.IsAny<SearchReservations>()), Times.Never);
        }

        [Fact]
        public async Task GetMyReservations_ShouldForceCurrentUserIdOnSearch()
        {
            var reservationDal = new Mock<IReservationDAL>();
            var controller = CreateController(reservationDal: reservationDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 12);
            var search = new SearchReservations();
            var reservations = new[] { TestData.ReservationOut(userId: 12) };

            reservationDal.Setup(x => x.GetReservations(search)).ReturnsAsync(reservations);

            var result = await controller.GetMyReservations(search);

            result.Result.Should().BeOfType<OkObjectResult>();
            search.IdUser.Should().Be(12);
            reservationDal.Verify(x => x.GetReservations(It.Is<SearchReservations>(s => s.IdUser == 12)), Times.Once);
        }

        [Fact]
        public async Task CreateReservation_ShouldReturnBadRequest_WhenTableBelongsToAnotherRestaurant()
        {
            var tableDal = new Mock<ITableDAL>();
            var controller = CreateController(tableDal: tableDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService.GenerateAccessToken(12));

            tableDal.Setup(x => x.GetTablesById(4))
                .ReturnsAsync(new TableEntityOut { Id = 4, RestaurantId = 99, TableNumber = 1, NumberOfSeats = 4 });

            var result = await controller.CreateReservation(new ReservationIn
            {
                TableId = 4,
                RestaurantId = 10,
                ReservationDate = DateTime.UtcNow.AddDays(1),
                NumberOfPeople = 2
            });

            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task CreateReservation_ShouldReturnBadRequest_WhenTableCapacityIsTooSmall()
        {
            var tableDal = new Mock<ITableDAL>();
            var controller = CreateController(tableDal: tableDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService.GenerateAccessToken(12));

            tableDal.Setup(x => x.GetTablesById(4))
                .ReturnsAsync(new TableEntityOut { Id = 4, RestaurantId = 10, TableNumber = 1, NumberOfSeats = 2 });

            var result = await controller.CreateReservation(new ReservationIn
            {
                TableId = 4,
                RestaurantId = 10,
                ReservationDate = DateTime.UtcNow.AddDays(1),
                NumberOfPeople = 3
            });

            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task CreateReservation_ShouldReturnConflict_WhenValidatedReservationIsInsideSafetyMargin()
        {
            var reservationDal = new Mock<IReservationDAL>();
            var tableDal = new Mock<ITableDAL>();
            var controller = CreateController(reservationDal, tableDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService.GenerateAccessToken(12));

            var requestedDate = new DateTime(2026, 5, 12, 20, 0, 0, DateTimeKind.Utc);
            tableDal.Setup(x => x.GetTablesById(4))
                .ReturnsAsync(new TableEntityOut { Id = 4, RestaurantId = 10, TableNumber = 1, NumberOfSeats = 4 });
            reservationDal.Setup(x => x.GetReservations(It.IsAny<SearchReservations>()))
                .ReturnsAsync(
                [
                    new ReservationOut
                    {
                        Id = 1,
                        TableId = 4,
                        RestaurantId = 10,
                        ReservationDate = requestedDate.AddMinutes(60),
                        NumberOfPeople = 2,
                        Status = ReservationStatus.Validee
                    }
                ]);

            var result = await controller.CreateReservation(new ReservationIn
            {
                TableId = 4,
                RestaurantId = 10,
                ReservationDate = requestedDate,
                NumberOfPeople = 2
            });

            result.Result.Should().BeOfType<ConflictObjectResult>();
            reservationDal.Verify(x => x.CreateReservationAsync(It.IsAny<ReservationIn>()), Times.Never);
        }

        [Fact]
        public async Task CreateReservation_ShouldReturnNotFound_WhenTableDoesNotExist()
        {
            var reservationDal = new Mock<IReservationDAL>();
            var tableDal = new Mock<ITableDAL>();
            var controller = CreateController(reservationDal: reservationDal, tableDal: tableDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 12);

            tableDal.Setup(x => x.GetTablesById(4)).ReturnsAsync((TableEntityOut?)null);

            var result = await controller.CreateReservation(TestData.ReservationIn());

            result.Result.Should().BeOfType<NotFoundObjectResult>();
            reservationDal.Verify(x => x.CreateReservationAsync(It.IsAny<ReservationIn>()), Times.Never);
        }

        [Fact]
        public async Task CreateReservation_ShouldReturnNotFound_WhenRestaurantDoesNotExist()
        {
            var reservationDal = new Mock<IReservationDAL>();
            var tableDal = new Mock<ITableDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(reservationDal, tableDal, restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 12);

            tableDal.Setup(x => x.GetTablesById(4)).ReturnsAsync(TestData.TableOut());
            reservationDal.Setup(x => x.GetReservations(It.IsAny<SearchReservations>())).ReturnsAsync([]);
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync((RestaurantOut?)null);

            var result = await controller.CreateReservation(TestData.ReservationIn());

            result.Result.Should().BeOfType<NotFoundObjectResult>();
            reservationDal.Verify(x => x.CreateReservationAsync(It.IsAny<ReservationIn>()), Times.Never);
        }

        [Fact]
        public async Task CreateReservation_ShouldReturnOkAndSendCreatedMessages_WhenReservationIsValid()
        {
            var reservationDal = new Mock<IReservationDAL>();
            var tableDal = new Mock<ITableDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var hub = ExternalServiceTestHelper.CreateReservationHub();
            var controller = CreateController(reservationDal, tableDal, restaurantDal, hubDouble: hub);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 12);
            var input = TestData.ReservationIn();
            var created = TestData.ReservationOut(userId: 12);

            tableDal.Setup(x => x.GetTablesById(4)).ReturnsAsync(TestData.TableOut());
            reservationDal.Setup(x => x.GetReservations(It.IsAny<SearchReservations>())).ReturnsAsync([]);
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 99));
            reservationDal.Setup(x => x.CreateReservationAsync(input)).ReturnsAsync(created);

            var result = await controller.CreateReservation(input);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(created);
            input.UserId.Should().Be(12);
            hub.Clients.Verify(x => x.Group(ReservationHub.RESTAURANT_GROUP_PREFIX + 10), Times.Once);
            hub.Clients.Verify(x => x.Group(ReservationHub.USER_GROUP_PREFIX + 12), Times.Once);
            hub.ClientProxy.Verify(
                x => x.SendCoreAsync(ReservationHub.SEND_AT_ReceiveReservationCreated, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task CreateReservation_ShouldAutoValidate_WhenRestaurantRequiresAutoValidation()
        {
            var reservationDal = new Mock<IReservationDAL>();
            var tableDal = new Mock<ITableDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var hub = ExternalServiceTestHelper.CreateReservationHub();
            var controller = CreateController(reservationDal, tableDal, restaurantDal, hubDouble: hub);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 12);
            var input = TestData.ReservationIn();
            var created = TestData.ReservationOut(userId: 12);
            created.Status = ReservationStatus.Validee;

            tableDal.Setup(x => x.GetTablesById(4)).ReturnsAsync(TestData.TableOut());
            reservationDal.Setup(x => x.GetReservations(It.IsAny<SearchReservations>())).ReturnsAsync([]);
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(id: 10, userId: 99, autoValidate: true));
            reservationDal.Setup(x => x.CreateReservationAsync(input)).ReturnsAsync(created);

            var result = await controller.CreateReservation(input);

            result.Result.Should().BeOfType<OkObjectResult>();
            input.Status.Should().Be(ReservationStatus.Validee);
            hub.ClientProxy.Verify(
                x => x.SendCoreAsync(ReservationHub.SEND_AT_ReceiveReservationUpdateStatus, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task UpdateReservationStatus_ShouldReturnUnauthorized_WhenCurrentUserIsNotRestaurantOwnerOrReservationUser()
        {
            var reservationDal = new Mock<IReservationDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = CreateController(reservationDal: reservationDal, restaurantDal: restaurantDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 12);

            reservationDal.Setup(x => x.GetMyReservationById(1)).ReturnsAsync(TestData.ReservationOut(userId: 77));
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 99));

            var result = await controller.UpdateReservationStatus(1, ReservationStatus.Validee);

            result.Result.Should().BeOfType<UnauthorizedResult>();
            reservationDal.Verify(x => x.UpdateReservationStatus(It.IsAny<long>(), It.IsAny<ReservationStatus>()), Times.Never);
        }

        [Fact]
        public async Task UpdateReservationStatus_ShouldReturnOkAndNotifyGroups_WhenCurrentUserCanUpdate()
        {
            var reservationDal = new Mock<IReservationDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var hub = ExternalServiceTestHelper.CreateReservationHub();
            var controller = CreateController(reservationDal: reservationDal, restaurantDal: restaurantDal, hubDouble: hub);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 99);
            var existing = TestData.ReservationOut(id: 1, userId: 12);
            var updated = TestData.ReservationOut(id: 1, userId: 12);
            updated.Status = ReservationStatus.Validee;

            reservationDal.Setup(x => x.GetMyReservationById(1)).ReturnsAsync(existing);
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 99));
            reservationDal.Setup(x => x.UpdateReservationStatus(1, ReservationStatus.Validee)).ReturnsAsync(updated);

            var result = await controller.UpdateReservationStatus(1, ReservationStatus.Validee);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(updated);
            hub.ClientProxy.Verify(
                x => x.SendCoreAsync(ReservationHub.SEND_AT_ReceiveReservationUpdateStatus, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task Delete_ShouldReturnNotFound_WhenReservationDoesNotExist()
        {
            var reservationDal = new Mock<IReservationDAL>();
            var controller = CreateController(reservationDal: reservationDal);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 12);

            reservationDal.Setup(x => x.Delete(1)).ReturnsAsync((ReservationOut?)null);

            var result = await controller.Delete(1);

            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task Delete_ShouldReturnOkAndNotifyGroups_WhenReservationExists()
        {
            var reservationDal = new Mock<IReservationDAL>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var hub = ExternalServiceTestHelper.CreateReservationHub();
            var controller = CreateController(reservationDal: reservationDal, restaurantDal: restaurantDal, hubDouble: hub);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 12);
            var deleted = TestData.ReservationOut(id: 1, userId: 12);

            reservationDal.Setup(x => x.Delete(1)).ReturnsAsync(deleted);
            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 99));

            var result = await controller.Delete(1);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().Be(true);
            hub.ClientProxy.Verify(
                x => x.SendCoreAsync(ReservationHub.SEND_AT_ReceiveReservationDeleted, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        private ReservationController CreateController(
            Mock<IReservationDAL>? reservationDal = null,
            Mock<ITableDAL>? tableDal = null,
            Mock<IRestaurantDAL>? restaurantDal = null,
            Mock<IDeviceTokenDAL>? deviceTokenDal = null,
            ReservationHubTestDouble? hubDouble = null)
        {
            hubDouble ??= ExternalServiceTestHelper.CreateReservationHub();
            deviceTokenDal ??= new Mock<IDeviceTokenDAL>();
            deviceTokenDal.Setup(x => x.GetDeviceTokensForUserIdsAsync(It.IsAny<IEnumerable<long>>()))
                .ReturnsAsync([]);
            var fcmService = ExternalServiceTestHelper.CreateUninitializedFcmService();

            return new ReservationController(
                (reservationDal ?? new Mock<IReservationDAL>()).Object,
                (restaurantDal ?? new Mock<IRestaurantDAL>()).Object,
                (tableDal ?? new Mock<ITableDAL>()).Object,
                deviceTokenDal.Object,
                _jwtService,
                hubDouble.HubContext.Object,
                fcmService);
        }
    }
}

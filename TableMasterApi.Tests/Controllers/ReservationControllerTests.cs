using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Runtime.Serialization;
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

        private ReservationController CreateController(
            Mock<IReservationDAL>? reservationDal = null,
            Mock<ITableDAL>? tableDal = null)
        {
            var deviceTokenDal = new Mock<IDeviceTokenDAL>();
            var hubContext = new Mock<IHubContext<ReservationHub>>();
            var restaurantDal = new Mock<IRestaurantDAL>();
            var fcmService = (FcmService)FormatterServices.GetUninitializedObject(typeof(FcmService));

            return new ReservationController(
                (reservationDal ?? new Mock<IReservationDAL>()).Object,
                restaurantDal.Object,
                (tableDal ?? new Mock<ITableDAL>()).Object,
                deviceTokenDal.Object,
                _jwtService,
                hubContext.Object,
                fcmService);
        }
    }
}

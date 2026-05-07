using Microsoft.AspNetCore.Mvc;
using TableMasterApi.Controllers;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Model;
using TableMasterApi.Tests.TestSupport;

namespace TableMasterApi.Tests.Controllers
{
    public class RestaurantControllerTests : IClassFixture<JwtConfigFixture>
    {
        private readonly JwtService _jwtService;

        public RestaurantControllerTests(JwtConfigFixture fixture)
        {
            _jwtService = new JwtService(fixture.JwtConfigOptions);
        }

        [Fact]
        public async Task Post_ShouldReturnCreatedRestaurant_WhenInputIsValid()
        {
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RestaurantController(restaurantDal.Object, _jwtService);
            var userId = 55L;
            ControllerTestHelper.SetBearerToken(controller, _jwtService.GenerateAccessToken(userId));

            var input = new RestaurantIn
            {
                RestaurantName = "Test Bistro",
                StreetNumber = "12",
                StreetName = "Rue des Tests",
                PostalCode = "75001",
                City = "Paris",
                Phone = "0102030405",
                CuisineType = "Francaise",
                PaymentMethods = "CB",
                Description = "Restaurant de test",
                IsAutoValidateReservation = true
            };

            var created = new RestaurantOut
            {
                Id = 88,
                UserId = userId,
                RestaurantName = input.RestaurantName,
                StreetNumber = input.StreetNumber,
                StreetName = input.StreetName,
                PostalCode = input.PostalCode,
                City = input.City,
                Phone = input.Phone,
                CuisineType = input.CuisineType,
                PaymentMethods = input.PaymentMethods,
                Description = input.Description,
                IsAutoValidateReservation = input.IsAutoValidateReservation,
                CreatedAt = DateTime.UtcNow
            };

            restaurantDal.Setup(x => x.PostRestaurantAsync(userId, input)).ReturnsAsync(created);

            var result = await controller.Post(input);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(created);

            restaurantDal.Verify(x => x.PostRestaurantAsync(userId, input), Times.Once);
        }

        [Fact]
        public async Task Put_ShouldReturnOk_WhenRestaurantUpdateSucceeds()
        {
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RestaurantController(restaurantDal.Object, _jwtService);
            var userId = 61L;
            ControllerTestHelper.SetBearerToken(controller, _jwtService.GenerateAccessToken(userId));

            var input = new RestaurantIn
            {
                RestaurantName = "Updated Bistro",
                StreetNumber = "20",
                StreetName = "Avenue des Tests",
                PostalCode = "69000",
                City = "Lyon",
                Phone = "0607080910",
                CuisineType = "Italienne",
                PaymentMethods = "CB,Espèces",
                Description = "Restaurant mis à jour",
                IsAutoValidateReservation = false
            };

            var updated = new RestaurantOut
            {
                Id = 91,
                UserId = userId,
                RestaurantName = input.RestaurantName,
                StreetNumber = input.StreetNumber,
                StreetName = input.StreetName,
                PostalCode = input.PostalCode,
                City = input.City,
                Phone = input.Phone,
                CuisineType = input.CuisineType,
                PaymentMethods = input.PaymentMethods,
                Description = input.Description,
                IsAutoValidateReservation = input.IsAutoValidateReservation,
                CreatedAt = DateTime.UtcNow
            };

            restaurantDal.Setup(x => x.PutRestaurantAsync(userId, 91, input)).ReturnsAsync(updated);

            var result = await controller.Put(91, input);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(updated);

            restaurantDal.Verify(x => x.PutRestaurantAsync(userId, 91, input), Times.Once);
        }

        [Fact]
        public async Task Delete_ShouldReturnOk_WhenRestaurantDeletionSucceeds()
        {
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RestaurantController(restaurantDal.Object, _jwtService);
            var userId = 70L;
            ControllerTestHelper.SetBearerToken(controller, _jwtService.GenerateAccessToken(userId));

            restaurantDal.Setup(x => x.DeleteRestaurant(userId, 33)).ReturnsAsync(true);

            var result = await controller.Delete(33);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().Be(true);

            restaurantDal.Verify(x => x.DeleteRestaurant(userId, 33), Times.Once);
        }
    }
}
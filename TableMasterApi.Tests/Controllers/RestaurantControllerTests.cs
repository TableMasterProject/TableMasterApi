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
        public async Task GetAll_ShouldReturnOkWithRestaurants()
        {
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RestaurantController(restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 55);
            var search = new SearchRestaurant();
            var restaurants = new[] { TestData.RestaurantOut(userId: 55) };

            restaurantDal.Setup(x => x.GetRestaurants(search)).ReturnsAsync(restaurants);

            var result = await controller.GetAll(search);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(restaurants);
        }

        [Fact]
        public async Task GetAll_ShouldReturnStatus500_WhenDalThrows()
        {
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RestaurantController(restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 55);

            restaurantDal.Setup(x => x.GetRestaurants(It.IsAny<SearchRestaurant>()))
                .ThrowsAsync(new InvalidOperationException("db down"));

            var result = await controller.GetAll(new SearchRestaurant());

            var error = result.Result.Should().BeOfType<ObjectResult>().Subject;
            error.StatusCode.Should().Be(500);
        }

        [Fact]
        public async Task Get_ShouldReturnOk_WhenRestaurantExists()
        {
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RestaurantController(restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 55);
            var restaurant = TestData.RestaurantOut(userId: 55);

            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(restaurant);

            var result = await controller.Get(10);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(restaurant);
        }

        [Fact]
        public async Task Get_ShouldReturnNotFound_WhenRestaurantDoesNotExist()
        {
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RestaurantController(restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 55);

            restaurantDal.Setup(x => x.GetRestaurantById(10)).ReturnsAsync((RestaurantOut?)null);

            var result = await controller.Get(10);

            result.Result.Should().BeOfType<NotFoundObjectResult>();
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
        public async Task Post_ShouldReturnBadRequest_WhenInputIsNull()
        {
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RestaurantController(restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 55);

            var result = await controller.Post(null!);

            result.Result.Should().BeOfType<BadRequestObjectResult>();
            restaurantDal.Verify(x => x.PostRestaurantAsync(It.IsAny<long>(), It.IsAny<RestaurantIn>()), Times.Never);
        }

        [Fact]
        public async Task Post_ShouldReturnGoogleMapsStatus_WhenGeocodingFails()
        {
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RestaurantController(restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 55);
            var input = TestData.RestaurantIn();

            restaurantDal.Setup(x => x.PostRestaurantAsync(55, input))
                .ThrowsAsync(new GoogleMapsException(400, "Adresse introuvable."));

            var result = await controller.Post(input);

            var error = result.Result.Should().BeOfType<ObjectResult>().Subject;
            error.StatusCode.Should().Be(400);
            error.Value.Should().Be("Adresse introuvable.");
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
        public async Task Put_ShouldReturnNotFound_WhenDalReturnsNull()
        {
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RestaurantController(restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 61);
            var input = TestData.RestaurantIn();

            restaurantDal.Setup(x => x.PutRestaurantAsync(61, 91, input)).ReturnsAsync((RestaurantOut?)null);

            var result = await controller.Put(91, input);

            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task Put_ShouldReturnGoogleMapsStatus_WhenGeocodingFails()
        {
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RestaurantController(restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 61);
            var input = TestData.RestaurantIn();

            restaurantDal.Setup(x => x.PutRestaurantAsync(61, 91, input))
                .ThrowsAsync(new GoogleMapsException(503, "Google Maps a refuse la requete."));

            var result = await controller.Put(91, input);

            var error = result.Result.Should().BeOfType<ObjectResult>().Subject;
            error.StatusCode.Should().Be(503);
            error.Value.Should().Be("Google Maps a refuse la requete.");
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

        [Fact]
        public async Task Delete_ShouldReturnNotFound_WhenDeletionFails()
        {
            var restaurantDal = new Mock<IRestaurantDAL>();
            var controller = new RestaurantController(restaurantDal.Object, _jwtService);
            ControllerTestHelper.SetBearerToken(controller, _jwtService, 70);

            restaurantDal.Setup(x => x.DeleteRestaurant(70, 33)).ReturnsAsync(false);

            var result = await controller.Delete(33);

            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }
    }
}

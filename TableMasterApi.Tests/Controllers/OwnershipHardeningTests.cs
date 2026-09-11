using Microsoft.AspNetCore.Mvc;
using TableMasterApi.Controllers;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Tests.TestSupport;

namespace TableMasterApi.Tests.Controllers;

public class OwnershipHardeningTests : IClassFixture<JwtConfigFixture>
{
    private readonly JwtService _jwt;
    public OwnershipHardeningTests(JwtConfigFixture fixture) => _jwt = new(fixture.JwtConfigOptions);

    [Fact]
    public async Task ClosedDay_CannotBeTransferredToAnotherRestaurant()
    {
        var dal = new Mock<IClosedDayExceptionDAL>();
        var restaurants = new Mock<IRestaurantDAL>();
        dal.Setup(x => x.GetByIdAsync(8)).ReturnsAsync(TestData.ClosedDayExceptionOut());
        restaurants.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 42));
        var controller = new ClosedDayExceptionController(dal.Object, restaurants.Object, _jwt);
        ControllerTestHelper.SetBearerToken(controller, _jwt, 42);
        var input = TestData.ClosedDayExceptionIn();
        input.RestaurantId = 999;
        var result = await controller.Put(8, input);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        dal.Verify(x => x.UpdateAsync(It.IsAny<long>(), It.IsAny<ClosedDayExceptionIn>()), Times.Never);
    }

    [Fact]
    public async Task DailyActivity_CannotBeTransferredToAnotherRestaurant()
    {
        var dal = new Mock<IDailyActivityDAL>();
        var restaurants = new Mock<IRestaurantDAL>();
        dal.Setup(x => x.GetByIdAsync(8)).ReturnsAsync(new DailyActivityOut { Id = 8, RestaurantId = 10 });
        restaurants.Setup(x => x.GetRestaurantById(10)).ReturnsAsync(TestData.RestaurantOut(userId: 42));
        var controller = new DailyActivityController(dal.Object, restaurants.Object, _jwt);
        ControllerTestHelper.SetBearerToken(controller, _jwt, 42);
        var result = await controller.Put(8, new DailyActivityIn { RestaurantId = 999 });
        result.Should().BeOfType<BadRequestObjectResult>();
        dal.Verify(x => x.UpdateAsync(It.IsAny<long>(), It.IsAny<DailyActivityIn>()), Times.Never);
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using TableMasterApi.Hubs;
using TableMasterApi.DAL.Interfaces;

namespace TableMasterApi.Tests.Controllers;

public class ReservationHubTests
{
    private static (ReservationHub Hub, Mock<IGroupManager> Groups, Mock<IRestaurantDAL> Restaurants) CreateHub(long? userId)
    {
        var groups = new Mock<IGroupManager>();
        var restaurants = new Mock<IRestaurantDAL>();
        var context = new Mock<HubCallerContext>();
        context.SetupGet(x => x.ConnectionId).Returns("test-connection");
        context.SetupGet(x => x.User).Returns(new ClaimsPrincipal(userId.HasValue
            ? new ClaimsIdentity([new Claim("UserId", userId.Value.ToString())], "TestBearer")
            : new ClaimsIdentity()));
        var hub = new ReservationHub(
            NullLogger<ReservationHub>.Instance,
            restaurants.Object,
            new CurrentUserService())
        {
            Context = context.Object,
            Groups = groups.Object
        };
        return (hub, groups, restaurants);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(42L)]
    public async Task CannotJoinAnotherUsersPrivateGroup(long? userId)
    {
        var (hub, groups, _) = CreateHub(userId);
        var act = () => hub.JoinUserGroup("99");
        await act.Should().ThrowAsync<HubException>();
        groups.Verify(x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OwnerCanJoinOnlyTheirRestaurantPrivateGroup()
    {
        var (hub, groups, restaurants) = CreateHub(42);
        restaurants.Setup(x => x.GetRestaurantById(7))
            .ReturnsAsync(new RestaurantOut { Id = 7, UserId = 42 });
        restaurants.Setup(x => x.GetRestaurantById(8))
            .ReturnsAsync(new RestaurantOut { Id = 8, UserId = 99 });

        await hub.JoinRestaurantGroup("7");
        var forbidden = () => hub.JoinRestaurantGroup("8");

        await forbidden.Should().ThrowAsync<HubException>();
        groups.Verify(x => x.AddToGroupAsync(
            "test-connection",
            "restaurant_7",
            It.IsAny<CancellationToken>()), Times.Once);
        groups.Verify(x => x.AddToGroupAsync(
            "test-connection",
            "restaurant_8",
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticatedUserCanJoinExistingRestaurantAvailabilityGroup()
    {
        var (hub, groups, restaurants) = CreateHub(42);
        restaurants.Setup(x => x.GetRestaurantById(7))
            .ReturnsAsync(new RestaurantOut { Id = 7, UserId = 99 });

        await hub.JoinAvailabilityGroup("7");

        groups.Verify(x => x.AddToGroupAsync(
            "test-connection",
            "availability_7",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

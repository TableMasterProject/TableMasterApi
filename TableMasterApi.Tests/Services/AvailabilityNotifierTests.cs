using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using TableMasterApi.Hubs;

namespace TableMasterApi.Tests.Services;

public class AvailabilityNotifierTests
{
    [Fact]
    public async Task EventContainsOnlyRestaurantIdentifier()
    {
        var proxy = new Mock<IClientProxy>();
        object? payload = null;
        proxy.Setup(x => x.SendCoreAsync(
                ReservationHub.SEND_AT_ReceiveAvailabilityChanged,
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((_, arguments, _) => payload = arguments.Single())
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        clients.Setup(x => x.Group("availability_17")).Returns(proxy.Object);
        var hub = new Mock<IHubContext<ReservationHub>>();
        hub.SetupGet(x => x.Clients).Returns(clients.Object);
        var notifier = new AvailabilityNotifier(hub.Object, NullLogger<AvailabilityNotifier>.Instance);

        await notifier.NotifyAsync(17);

        payload.Should().NotBeNull();
        var properties = payload!.GetType().GetProperties();
        properties.Should().ContainSingle();
        properties[0].Name.Should().Be("restaurantId");
        properties[0].GetValue(payload).Should().Be(17L);
    }
}

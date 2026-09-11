using System.Text.Json;
using TableMasterApi.DAL.Interfaces;

namespace TableMasterApi.Tests.Services;

public class ReservationNotificationSenderTests
{
    [Fact]
    public async Task PartialPushRetriesOnlyUnsuccessfulDevicesAndPurgesPermanentTokens()
    {
        var devices = new Mock<IDeviceTokenDAL>();
        devices.Setup(x => x.GetDeviceTokensForUserIdsAsync(It.IsAny<IEnumerable<long>>())).ReturnsAsync(["success", "invalid", "temporary"]);
        var fcm = new Mock<IFcmDeviceSender>();
        fcm.Setup(x => x.SendToDeviceAsync("success", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>())).ReturnsAsync(PushDeliveryResult.Success);
        fcm.Setup(x => x.SendToDeviceAsync("invalid", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>())).ReturnsAsync(PushDeliveryResult.PermanentFailure);
        fcm.SetupSequence(x => x.SendToDeviceAsync("temporary", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PushDeliveryResult.Retry).ReturnsAsync(PushDeliveryResult.Success);
        var sender = new ReservationNotificationSender(devices.Object, fcm.Object, Mock.Of<IUserDAL>(), Mock.Of<IEmailService>(), Mock.Of<IAppLinkService>());
        var eventId = Guid.NewGuid();
        var task = new OutboxTask { UserId = 5, Channel = "push", Payload = JsonSerializer.Serialize(new ReservationNotification(eventId, 1, 1, new(2026, 7, 20), "reservation_created", "Validee")) };
        Task Checkpoint(IEnumerable<string> hashes, CancellationToken ct) { task.CompletedRecipients = JsonSerializer.Serialize(hashes); return Task.CompletedTask; }
        Func<Task> first = () => sender.SendAsync(task, Checkpoint, default);
        await first.Should().ThrowAsync<NotificationDeliveryException>();
        task.CompletedRecipients.Should().NotContain("success").And.NotContain("invalid");
        await sender.SendAsync(task, Checkpoint, default);
        fcm.Verify(x => x.SendToDeviceAsync("success", It.IsAny<string>(), It.IsAny<string>(), It.Is<object>(data => data.GetType().GetProperty("eventId")!.GetValue(data)!.Equals(eventId)), It.IsAny<CancellationToken>()), Times.Once);
        fcm.Verify(x => x.SendToDeviceAsync("temporary", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        devices.Verify(x => x.DeleteDeviceTokenAsync(5, "invalid"), Times.Once);
    }
}

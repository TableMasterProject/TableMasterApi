using System.Runtime.Serialization;
using Microsoft.AspNetCore.SignalR;
using TableMasterApi.Hubs;

namespace TableMasterApi.Tests.TestSupport
{
    internal sealed class ReservationHubTestDouble
    {
        public Mock<IHubContext<ReservationHub>> HubContext { get; } = new();
        public Mock<IHubClients> Clients { get; } = new();
        public Mock<IClientProxy> ClientProxy { get; } = new();

        public ReservationHubTestDouble()
        {
            Clients.Setup(x => x.Group(It.IsAny<string>())).Returns(ClientProxy.Object);
            HubContext.Setup(x => x.Clients).Returns(Clients.Object);
        }

        public void VerifyGroupMessage(string groupName, string methodName, Times times)
        {
            Clients.Verify(x => x.Group(groupName), times);
            ClientProxy.Verify(
                x => x.SendCoreAsync(
                    methodName,
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()),
                times);
        }
    }

    internal static class ExternalServiceTestHelper
    {
        public static ReservationHubTestDouble CreateReservationHub() => new();

        public static FcmService CreateUninitializedFcmService()
        {
#pragma warning disable SYSLIB0050
            return (FcmService)FormatterServices.GetUninitializedObject(typeof(FcmService));
#pragma warning restore SYSLIB0050
        }
    }
}

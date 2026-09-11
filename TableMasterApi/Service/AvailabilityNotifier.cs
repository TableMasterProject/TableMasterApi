using Microsoft.AspNetCore.SignalR;
using TableMasterApi.Hubs;

namespace TableMasterApi.Service;

public interface IAvailabilityNotifier
{
    Task NotifyAsync(long restaurantId);
}

public sealed class AvailabilityNotifier(IHubContext<ReservationHub> hub, ILogger<AvailabilityNotifier> logger) : IAvailabilityNotifier
{
    public async Task NotifyAsync(long restaurantId)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await hub.Clients.Group(ReservationHub.AVAILABILITY_GROUP_PREFIX + restaurantId)
                .SendAsync(ReservationHub.SEND_AT_ReceiveAvailabilityChanged, new { restaurantId }, timeout.Token);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Impossible d'annoncer le changement de disponibilités du restaurant {RestaurantId}.", restaurantId);
        }
    }
}

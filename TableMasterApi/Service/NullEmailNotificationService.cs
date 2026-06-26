using TableMasterApi.Model;

namespace TableMasterApi.Service
{
    public sealed class NullEmailNotificationService : IEmailNotificationService
    {
        public static NullEmailNotificationService Instance { get; } = new();

        private NullEmailNotificationService()
        {
        }

        public Task SendPasswordResetAsync(UserOut user, string resetLink, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendReservationCreatedAsync(ReservationOut reservation, RestaurantOut restaurant, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendReservationStatusUpdatedAsync(ReservationOut reservation, RestaurantOut restaurant, ReservationStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendReservationCancelledAsync(ReservationOut reservation, RestaurantOut? restaurant, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

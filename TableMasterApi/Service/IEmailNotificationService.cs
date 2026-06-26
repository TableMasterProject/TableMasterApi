using TableMasterApi.Model;

namespace TableMasterApi.Service
{
    public interface IEmailNotificationService
    {
        Task SendPasswordResetAsync(UserOut user, string resetLink, CancellationToken cancellationToken = default);
        Task SendReservationCreatedAsync(ReservationOut reservation, RestaurantOut restaurant, CancellationToken cancellationToken = default);
        Task SendReservationStatusUpdatedAsync(ReservationOut reservation, RestaurantOut restaurant, ReservationStatus status, CancellationToken cancellationToken = default);
        Task SendReservationCancelledAsync(ReservationOut reservation, RestaurantOut? restaurant, CancellationToken cancellationToken = default);
    }
}

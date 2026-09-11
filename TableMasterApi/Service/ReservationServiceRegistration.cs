using Microsoft.Extensions.DependencyInjection.Extensions;
namespace TableMasterApi.Service;

public static class ReservationServiceRegistration
{
    public static IServiceCollection AddReservationServices(this IServiceCollection services, bool enableWorker = true)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<ReservationRules>();
        services.AddScoped<ReservationOutbox>();
        services.AddSingleton<IFcmDeviceSender>(provider => provider.GetRequiredService<FcmService>());
        services.AddScoped<IReservationNotificationSender, ReservationNotificationSender>();
        if (enableWorker) services.AddHostedService<ReservationNotificationWorker>();
        return services;
    }
}

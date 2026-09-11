using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Model;

namespace TableMasterApi.Service;

public sealed class NotificationDeliveryException(string code, bool permanent = false) : Exception(code)
{
    public bool Permanent { get; } = permanent;
}

public interface IReservationNotificationSender
{
    Task SendAsync(OutboxTask task, Func<IEnumerable<string>, CancellationToken, Task> checkpoint, CancellationToken cancellationToken);
}

public sealed class ReservationNotificationSender(IDeviceTokenDAL devices, IFcmDeviceSender fcm, IUserDAL users, IEmailService email, IAppLinkService links) : IReservationNotificationSender
{
    public async Task SendAsync(OutboxTask task, Func<IEnumerable<string>, CancellationToken, Task> checkpoint, CancellationToken ct)
    {
        var notification = JsonSerializer.Deserialize<ReservationNotification>(task.Payload) ?? throw new NotificationDeliveryException("payload_invalid", true);
        var title = notification.Type switch { "reservation_created" => "Nouvelle réservation", "reservation_cancelled" => "Réservation annulée", _ => "Statut de réservation mis à jour" };
        var body = $"Réservation du {notification.ReservationDate:dd/MM/yyyy HH:mm} : {notification.Status}.";
        if (task.Channel == "email")
        {
            var user = await users.GetUserById(task.UserId);
            if (user == null) return;
            var link = links.BuildReservationLink(notification.ReservationId);
            var sent = await email.SendEmailAsync(new EmailMessage { ToEmail = user.Email, ToName = user.FirstName, Subject = title,
                TextContent = body + "\n" + link, HtmlContent = $"<p>{System.Net.WebUtility.HtmlEncode(body)}</p><a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Ouvrir la réservation</a>" }, ct);
            if (!sent) throw new NotificationDeliveryException("email_unavailable");
            return;
        }
        var completed = JsonSerializer.Deserialize<HashSet<string>>(task.CompletedRecipients) ?? [];
        var tokens = (await devices.GetDeviceTokensForUserIdsAsync([task.UserId])).Distinct();
        foreach (var token in tokens)
        {
            ct.ThrowIfCancellationRequested();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
            if (completed.Contains(hash)) continue;
            var result = await fcm.SendToDeviceAsync(token, title, body, new { eventId = notification.EventId, type = notification.Type, reservationId = notification.ReservationId, status = notification.Status }, ct);
            if (result == PushDeliveryResult.PermanentFailure) await devices.DeleteDeviceTokenAsync(task.UserId, token);
            else if (result == PushDeliveryResult.Retry) throw new NotificationDeliveryException("push_temporary");
            completed.Add(hash);
            await checkpoint(completed, ct);
        }
    }
}

public sealed class ReservationNotificationWorker(IServiceScopeFactory scopes, ILogger<ReservationNotificationWorker> logger) : BackgroundService
{
    private static readonly System.Diagnostics.Metrics.Meter Meter = new("TableMaster.ReservationNotifications");
    private static readonly System.Diagnostics.Metrics.Counter<long> Outcomes = Meter.CreateCounter<long>("reservation_notification_outcomes");

    public static async Task<bool> ProcessOneAsync(ReservationOutbox outbox, IReservationNotificationSender sender, CancellationToken stoppingToken, ILogger? logger = null)
    {
        var task = await outbox.LeaseAsync(stoppingToken);
        if (task == null) return false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            await sender.SendAsync(task, (hashes, ct) => outbox.SaveRecipientsAsync(task, hashes, ct), timeout.Token);
            await outbox.CompleteAsync(task, stoppingToken);
            Outcomes.Add(1, new KeyValuePair<string, object?>("state", "completed"), new KeyValuePair<string, object?>("channel", task.Channel));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
        catch (NotificationDeliveryException ex)
        {
            await outbox.FailAsync(task, ex.Permanent, ex.Message, stoppingToken);
            ReportFailure(task, ex.Permanent, logger);
        }
        catch (Exception)
        {
            await outbox.FailAsync(task, false, "delivery_failed", stoppingToken);
            ReportFailure(task, false, logger);
        }
        return true;
    }

    private static void ReportFailure(OutboxTask task, bool permanent, ILogger? logger)
    {
        var state = permanent || task.Attempts + 1 >= 6 ? "dead" : "retry";
        Outcomes.Add(1, new KeyValuePair<string, object?>("state", state), new KeyValuePair<string, object?>("channel", task.Channel));
        logger?.LogWarning("Notification {TaskId}: {State}, attempt {Attempt}", task.Id, state, task.Attempts + 1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var handled = await ProcessOneAsync(scope.ServiceProvider.GetRequiredService<ReservationOutbox>(), scope.ServiceProvider.GetRequiredService<IReservationNotificationSender>(), stoppingToken, logger);
                if (handled) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Notification worker failed: {ErrorType}", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

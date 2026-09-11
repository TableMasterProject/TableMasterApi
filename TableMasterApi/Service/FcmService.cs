using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using TableMasterApi.Model;

namespace TableMasterApi.Service;

public enum PushDeliveryResult { Success, PermanentFailure, Retry }

public interface IFcmDeviceSender
{
    Task<PushDeliveryResult> SendToDeviceAsync(string token, string title, string body, object? data, CancellationToken cancellationToken);
}

public class FcmService : IFcmDeviceSender
{
    private readonly ConfigPerso _config;
    private static readonly object InitializationLock = new();

    public FcmService(ConfigPerso config) => _config = config;

    private void EnsureInitialized()
    {
        lock (InitializationLock)
        {
        if (FirebaseApp.DefaultInstance == null)
        {
            using var credentialStream = File.OpenRead(_config.FirebaseServiceAccountPath);
            var credential = CredentialFactory
                .FromStream<ServiceAccountCredential>(credentialStream)
                .ToGoogleCredential();

            FirebaseApp.Create(new AppOptions
            {
                Credential = credential
            });
        }
    }

    }

    public virtual async Task<PushDeliveryResult> SendToDeviceAsync(string token, string title, string body, object? data, CancellationToken cancellationToken)
    {
        try
        {
            EnsureInitialized();
            await FirebaseMessaging.DefaultInstance.SendAsync(new Message { Token = token, Data = new Dictionary<string, string>(BuildPayload(title, body, data)) }, cancellationToken);
            return PushDeliveryResult.Success;
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument or MessagingErrorCode.SenderIdMismatch)
        {
            return PushDeliveryResult.PermanentFailure;
        }
        catch (FirebaseMessagingException) { return PushDeliveryResult.Retry; }
    }

    public async Task<bool> SendNotificationAsync(IEnumerable<string> tokens, string title, string body, object? data = null)
    {
        var tokenList = tokens?.ToList() ?? [];
        if (tokenList.Count == 0)
        {
            return false;
        }

        var payload = BuildPayload(title, body, data);

        var message = new MulticastMessage
        {
            Tokens = tokenList,
            Data = new Dictionary<string, string>(payload)
        };

        EnsureInitialized();
        var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
        return response.SuccessCount > 0;
    }

    public static IReadOnlyDictionary<string, string> BuildPayload(string title, string body, object? data)
    {
        var payload = data?.GetType()
            .GetProperties()
            .ToDictionary(property => property.Name, property => property.GetValue(data)?.ToString() ?? string.Empty)
            ?? new Dictionary<string, string>();

        payload["title"] = title;
        payload["body"] = body;
        return payload;
    }
}

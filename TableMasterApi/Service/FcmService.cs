using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using TableMasterApi.Model;

namespace TableMasterApi.Service;

public class FcmService
{
    public FcmService(ConfigPerso config)
    {
        if (FirebaseApp.DefaultInstance == null)
        {
            using var credentialStream = File.OpenRead(config.FirebaseServiceAccountPath);
            var credential = CredentialFactory
                .FromStream<ServiceAccountCredential>(credentialStream)
                .ToGoogleCredential();

            FirebaseApp.Create(new AppOptions
            {
                Credential = credential
            });
        }
    }

    public async Task<bool> SendNotificationAsync(IEnumerable<string> tokens, string title, string body, object? data = null)
    {
        var tokenList = tokens?.ToList() ?? [];
        if (tokenList.Count == 0)
        {
            return false;
        }

        var payload = data?.GetType()
            .GetProperties()
            .ToDictionary(property => property.Name, property => property.GetValue(data)?.ToString() ?? string.Empty)
            ?? new Dictionary<string, string>();

        payload["title"] = title;
        payload["body"] = body;

        var message = new MulticastMessage
        {
            Tokens = tokenList,
            Data = payload
        };

        var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
        return response.SuccessCount > 0;
    }
}

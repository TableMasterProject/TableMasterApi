using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using System.IO;
using System.Text.Json;

public class FcmService
{
    public FcmService()
    {
        if (FirebaseApp.DefaultInstance == null)
        {
            FirebaseApp.Create(new AppOptions()
            {
                Credential = GoogleCredential.FromFile("tablemaster-firebase.json")
            });
        }
    }

    public async Task<bool> SendNotificationAsync(IEnumerable<string> tokens, string title, string body, object? data = null)
    {
        if (tokens == null || !tokens.Any()) return false;

        // On prépare le message
        var message = new MulticastMessage()
        {
            Tokens = tokens.ToList(),
            Data = new Dictionary<string, string>
            {
                { "title", title },
                { "body", body },
                { "type", "reservation_created" }
            }
        };

        // REMPLACER SendMulticastAsync PAR SendEachForMulticastAsync
        var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
    
        // On vérifie s'il y a au moins un succès
        return response.SuccessCount > 0;
    }
}
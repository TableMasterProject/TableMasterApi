using Google.Apis.Auth;

namespace TableMasterApi.Service
{
    public class GoogleAuthService
    {
        private readonly IConfiguration _configuration;

        public GoogleAuthService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<GoogleJsonWebSignature.Payload> ValidateIdTokenAsync(string idToken)
        {
            var clientId = _configuration["GoogleAuth:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new InvalidOperationException("GoogleAuth:ClientId est manquant.");
            }

            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId]
            };

            return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        }
    }
}

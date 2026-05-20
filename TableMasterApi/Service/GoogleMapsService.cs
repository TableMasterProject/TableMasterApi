using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using TableMasterApi.Model;

namespace TableMasterApi.Service
{
    public class GoogleMapsService
    {
        private readonly ConfigPerso _config;
        private readonly HttpClient _httpClient;
        private readonly ILogger<GoogleMapsService> _logger;

        public GoogleMapsService(ConfigPerso config, HttpClient httpClient, ILogger<GoogleMapsService> logger)
        {
            _config = config;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<RestaurantIn> FillLatLongAsync(RestaurantIn restaurant)
        {
            if (string.IsNullOrWhiteSpace(restaurant.StreetNumber) ||
                string.IsNullOrWhiteSpace(restaurant.StreetName) ||
                string.IsNullOrWhiteSpace(restaurant.City) ||
                string.IsNullOrWhiteSpace(restaurant.PostalCode))
            {
                throw new GoogleMapsException(
                    StatusCodes.Status400BadRequest,
                    "L'adresse doit etre complete (numero, rue, code postal, ville).");
            }

            if (string.IsNullOrWhiteSpace(_config.KeyApiGoogleMaps))
            {
                throw new GoogleMapsException(
                    StatusCodes.Status503ServiceUnavailable,
                    "La configuration Google Maps est manquante cote serveur.");
            }

            var address = $"{restaurant.StreetNumber} {restaurant.StreetName} {restaurant.PostalCode} {restaurant.City}";
            var requestUrl = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={_config.KeyApiGoogleMaps}";

            var response = await _httpClient.GetAsync(requestUrl);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Google Maps geocoding HTTP error {StatusCode}. Response: {ResponseContent}",
                    (int)response.StatusCode,
                    responseContent);

                throw new GoogleMapsException(
                    StatusCodes.Status503ServiceUnavailable,
                    "Google Maps n'a pas repondu correctement.");
            }

            GoogleGeocodeResponse? geocodeResponse;
            try
            {
                geocodeResponse = JsonSerializer.Deserialize<GoogleGeocodeResponse>(responseContent);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Google Maps geocoding returned invalid JSON.");
                throw new GoogleMapsException(
                    StatusCodes.Status503ServiceUnavailable,
                    "Reponse invalide de l'API Google Maps.",
                    innerException: exception);
            }

            if (geocodeResponse?.Status == "OK" && geocodeResponse.Results.Length > 0)
            {
                var location = geocodeResponse.Results[0].Geometry.Location;
                restaurant.Latitude = location.Lat;
                restaurant.Longitude = location.Lng;

                return restaurant;
            }

            var googleStatus = geocodeResponse?.Status ?? "EMPTY_RESPONSE";
            _logger.LogWarning(
                "Google Maps geocoding failed with status {GoogleStatus}. Error message: {GoogleErrorMessage}",
                googleStatus,
                geocodeResponse?.ErrorMessage);

            throw googleStatus switch
            {
                "ZERO_RESULTS" => new GoogleMapsException(
                    StatusCodes.Status400BadRequest,
                    "Adresse introuvable. Verifiez le numero, la rue, le code postal et la ville.",
                    googleStatus),
                "REQUEST_DENIED" => new GoogleMapsException(
                    StatusCodes.Status503ServiceUnavailable,
                    "Google Maps a refuse la requete. Verifiez la cle API et ses restrictions.",
                    googleStatus),
                "OVER_QUERY_LIMIT" or "OVER_DAILY_LIMIT" => new GoogleMapsException(
                    StatusCodes.Status503ServiceUnavailable,
                    "Le quota Google Maps est atteint.",
                    googleStatus),
                _ => new GoogleMapsException(
                    StatusCodes.Status503ServiceUnavailable,
                    "Adresse introuvable ou reponse invalide de l'API Google Maps.",
                    googleStatus)
            };
        }
    }

    public class GoogleGeocodeResponse
    {
        [JsonPropertyName("results")]
        public GeocodeResult[] Results { get; set; } = [];

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }
    }

    public class GeocodeResult
    {
        [JsonPropertyName("geometry")]
        public Geometry Geometry { get; set; } = new();
    }

    public class Geometry
    {
        [JsonPropertyName("location")]
        public Location Location { get; set; } = new();
    }

    public class Location
    {
        [JsonPropertyName("lat")]
        public decimal Lat { get; set; }

        [JsonPropertyName("lng")]
        public decimal Lng { get; set; }
    }
}

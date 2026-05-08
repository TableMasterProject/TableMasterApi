using System.Text.Json;
using System.Text.Json.Serialization;
using TableMasterApi.Model;

namespace TableMasterApi.Service
{
    public class GoogleMapsService
    {
        private readonly ConfigPerso _config;
        private readonly HttpClient _httpClient;

        public GoogleMapsService(ConfigPerso config, HttpClient httpClient)
        {
            _config = config;
            _httpClient = httpClient;
        }

        public async Task<RestaurantIn> FillLatLongAsync(RestaurantIn restaurant)
        {
            if (string.IsNullOrWhiteSpace(restaurant.StreetNumber) ||
                string.IsNullOrWhiteSpace(restaurant.StreetName) ||
                string.IsNullOrWhiteSpace(restaurant.City) ||
                string.IsNullOrWhiteSpace(restaurant.PostalCode))
            {
                throw new ArgumentException("L'adresse doit être complète (numéro, rue, code postal, ville).");
            }

            if (string.IsNullOrWhiteSpace(_config.KeyApiGoogleMaps))
            {
                throw new InvalidOperationException("ConfigPerso:KeyApiGoogleMaps est manquante.");
            }

            var address = $"{restaurant.StreetNumber} {restaurant.StreetName} {restaurant.PostalCode} {restaurant.City}";
            var requestUrl = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={_config.KeyApiGoogleMaps}";

            var response = await _httpClient.GetAsync(requestUrl);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Échec de la récupération des données depuis Google Maps.");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var geocodeResponse = JsonSerializer.Deserialize<GoogleGeocodeResponse>(responseContent);

            if (geocodeResponse?.Status != "OK" || geocodeResponse.Results.Length == 0)
            {
                throw new Exception("Adresse introuvable ou réponse invalide de l'API Google Maps.");
            }

            var location = geocodeResponse.Results[0].Geometry.Location;
            restaurant.Latitude = location.Lat;
            restaurant.Longitude = location.Lng;

            return restaurant;
        }
    }

    public class GoogleGeocodeResponse
    {
        [JsonPropertyName("results")]
        public GeocodeResult[] Results { get; set; } = [];

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
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

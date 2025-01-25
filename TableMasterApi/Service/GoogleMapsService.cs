using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TableMasterApi.Model;


namespace TableMasterApi.Service
{    
    public class GoogleMapsService
    {
        private readonly ConfigPerso _config;

        public GoogleMapsService(ConfigPerso config)
        {
            _config = config;
        }

        public async Task<RestaurantIn> FillLatLongAsync(RestaurantIn restaurant)
        {
            if (restaurant == null)
                throw new ArgumentNullException(nameof(restaurant));

            if (string.IsNullOrWhiteSpace(restaurant.StreetNumber) ||
                string.IsNullOrWhiteSpace(restaurant.StreetName) ||
                string.IsNullOrWhiteSpace(restaurant.City) ||
                string.IsNullOrWhiteSpace(restaurant.PostalCode))
            {
                throw new ArgumentException("L'adresse doit être complète (numéro, rue, code postal, ville).");
            }

            var address = $"{restaurant.StreetNumber} {restaurant.StreetName} {restaurant.PostalCode} {restaurant.City}";
            var requestUrl = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={_config.KeyApiGoogleMaps}";

            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
                throw new Exception("Échec de la récupération des données depuis Google Maps.");

            var responseContent = response.Content.ReadAsStringAsync().Result;
            var geocodeResponse = JsonSerializer.Deserialize<GoogleGeocodeResponse>(responseContent);

            if (geocodeResponse?.Status != "OK" || geocodeResponse.Results == null || geocodeResponse.Results.Length == 0)
                throw new Exception("Adresse introuvable ou réponse invalide de l'API Google Maps.");

            var location = geocodeResponse.Results[0].Geometry.Location;

            // Mise à jour des coordonnées dans le restaurant
            restaurant.Latitude = location.Lat;
            restaurant.Longitude = location.Lng;

            return restaurant;
        }
    }
    public class GoogleGeocodeResponse
    {
        [JsonPropertyName("results")]
        public GeocodeResult[] Results { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }
    }

    public class GeocodeResult
    {
        [JsonPropertyName("geometry")]
        public Geometry Geometry { get; set; }
    }

    public class Geometry
    {
        [JsonPropertyName("location")]
        public Location Location { get; set; }
    }

    public class Location
    {
        [JsonPropertyName("lat")]
        public decimal Lat { get; set; }

        [JsonPropertyName("lng")]
        public decimal Lng { get; set; }
    }


}

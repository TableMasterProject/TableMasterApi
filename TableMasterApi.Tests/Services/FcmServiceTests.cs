using TableMasterApi.Service;
using TableMasterApi.Tests.TestSupport;

namespace TableMasterApi.Tests.Services
{
    public class FcmServiceTests
    {
        [Fact]
        public void BuildPayload_ContainsTitleAndBody_WhenDataIsNull()
        {
            var payload = FcmService.BuildPayload("Reservation", "Confirmee", data: null);

            payload.Should().Contain(new KeyValuePair<string, string>("title", "Reservation"));
            payload.Should().Contain(new KeyValuePair<string, string>("body", "Confirmee"));
            payload.Should().HaveCount(2);
        }

        [Fact]
        public void BuildPayload_AddsDataPropertiesAsStrings()
        {
            var payload = FcmService.BuildPayload(
                "Reservation",
                "Confirmee",
                new { ReservationId = 42L, RestaurantId = 10L });

            payload["ReservationId"].Should().Be("42");
            payload["RestaurantId"].Should().Be("10");
            payload["title"].Should().Be("Reservation");
            payload["body"].Should().Be("Confirmee");
        }

        [Fact]
        public void BuildPayload_TitleAndBody_OverrideDataPropertiesOfSameName()
        {
            var payload = FcmService.BuildPayload(
                "OfficialTitle",
                "OfficialBody",
                new { title = "from-data", body = "from-data" });

            payload["title"].Should().Be("OfficialTitle");
            payload["body"].Should().Be("OfficialBody");
        }

        [Fact]
        public void BuildPayload_NullPropertyValues_BecomeEmptyStrings()
        {
            var payload = FcmService.BuildPayload(
                "T",
                "B",
                new { Comment = (string?)null });

            payload["Comment"].Should().Be(string.Empty);
        }

        [Fact]
        public async Task SendNotificationAsync_ReturnsFalse_WhenTokenCollectionIsNull()
        {
            var service = ExternalServiceTestHelper.CreateUninitializedFcmService();

            var sent = await service.SendNotificationAsync(null!, "Titre", "Body");

            sent.Should().BeFalse();
        }

        [Fact]
        public async Task SendNotificationAsync_ReturnsFalse_WhenNoTokens()
        {
            var service = ExternalServiceTestHelper.CreateUninitializedFcmService();

            var sent = await service.SendNotificationAsync(Array.Empty<string>(), "Titre", "Body");

            sent.Should().BeFalse();
        }
    }
}

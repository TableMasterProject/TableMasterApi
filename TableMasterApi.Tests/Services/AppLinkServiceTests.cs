using Microsoft.Extensions.Options;

namespace TableMasterApi.Tests.Services
{
    public class AppLinkServiceTests
    {
        [Fact]
        public void BuildPasswordResetLink_ShouldUseConfiguredWebBaseUrl()
        {
            var service = new AppLinkService(Options.Create(new AppLinksOptions
            {
                BaseUrl = "https://app.example.com/",
                PasswordResetPath = "/reset-password"
            }));

            var link = service.BuildPasswordResetLink("a+b/c=");

            link.Should().Be("https://app.example.com/reset-password?token=a%2Bb%2Fc%3D");
        }

        [Fact]
        public void BuildReservationLink_ShouldFallbackToCustomScheme_WhenBaseUrlIsMissing()
        {
            var service = new AppLinkService(Options.Create(new AppLinksOptions
            {
                FallbackScheme = "tablemaster",
                ReservationPath = "/reservations/{reservationId}"
            }));

            var link = service.BuildReservationLink(123);

            link.Should().Be("tablemaster://reservations/123");
        }
    }
}

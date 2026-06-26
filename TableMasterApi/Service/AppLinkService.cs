using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using TableMasterApi.Model;

namespace TableMasterApi.Service
{
    public class AppLinkService : IAppLinkService
    {
        private readonly AppLinksOptions _options;

        public AppLinkService(IOptions<AppLinksOptions> options)
        {
            _options = options.Value;
        }

        public string BuildPasswordResetLink(string token)
        {
            var url = BuildLink(_options.PasswordResetPath);
            return QueryHelpers.AddQueryString(url, "token", token);
        }

        public string BuildReservationLink(long reservationId)
        {
            return BuildLink(_options.ReservationPath.Replace("{reservationId}", reservationId.ToString()));
        }

        private string BuildLink(string path)
        {
            path = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();

            if (!path.StartsWith('/'))
            {
                path = "/" + path;
            }

            if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
            {
                return $"{_options.BaseUrl.TrimEnd('/')}{path}";
            }

            var scheme = string.IsNullOrWhiteSpace(_options.FallbackScheme)
                ? "tablemaster"
                : _options.FallbackScheme.Trim().TrimEnd(':', '/', '\\');

            return $"{scheme}://{path.TrimStart('/')}";
        }
    }
}

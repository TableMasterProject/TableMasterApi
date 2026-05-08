using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace TableMasterApi.Service
{
    public interface ICurrentUserService
    {
        long GetUserId(ClaimsPrincipal user);
    }

    public class CurrentUserService : ICurrentUserService
    {
        public long GetUserId(ClaimsPrincipal user)
        {
            var rawUserId = user.FindFirstValue("UserId")
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (!long.TryParse(rawUserId, out var userId))
            {
                throw new UnauthorizedAccessException("Utilisateur non authentifie.");
            }

            return userId;
        }
    }
}

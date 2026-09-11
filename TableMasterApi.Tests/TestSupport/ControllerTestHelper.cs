using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;

namespace TableMasterApi.Tests.TestSupport
{
    internal static class ControllerTestHelper
    {
        public static void SetBearerToken(ControllerBase controller, string token)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims, "TestBearer"));
            controller.ControllerContext.HttpContext.Request.Headers["Authorization"] = $"Bearer {token}";
        }

        public static void SetBearerToken(ControllerBase controller, JwtService jwtService, long userId)
        {
            SetBearerToken(controller, jwtService.GenerateAccessToken(userId));
        }
    }
}

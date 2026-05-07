using Microsoft.AspNetCore.Http;
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

            controller.ControllerContext.HttpContext.Request.Headers["Authorization"] = $"Bearer {token}";
        }
    }
}
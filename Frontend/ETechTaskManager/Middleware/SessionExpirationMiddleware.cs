using System.Text;
using Newtonsoft.Json;
using Microsoft.Extensions.Options;

namespace ETechTaskManager.Middleware
{
    public class SessionExpirationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _sessionCookieName; // The actual cookie name from SessionOptions

        public SessionExpirationMiddleware(
            RequestDelegate next,
            IHttpClientFactory httpClientFactory,
            IOptions<SessionOptions> sessionOptions // <--- NEW
        )
        {
            _next = next;
            _httpClientFactory = httpClientFactory;
            // Grab the name that was set in AddSession(...)
            _sessionCookieName = sessionOptions.Value.Cookie.Name;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Exclude paths related to login and logout
            var excludedPaths = new[] { "/Home/Login", "/Home/Logout" };
            if (excludedPaths.Any(path => context.Request.Path.StartsWithSegments(path)))
            {
                await _next(context);
                return;
            }

            var userId = context.Session.GetString("UserId");

            // Skip if session is not initialized yet (no user)
            if (string.IsNullOrEmpty(userId))
            {
                await _next(context);
                return;
            }

            // Check if the cookie named _sessionCookieName exists
            if (!context.Request.Cookies.ContainsKey(_sessionCookieName))
            {
                // Session has expired
                var client = _httpClientFactory.CreateClient();
                await client.PutAsync(
                    $"https://localhost:7013/api/Users/UpdateOnlineStatus/{userId}/online-status",
                    new StringContent(JsonConvert.SerializeObject(false), Encoding.UTF8, "application/json")
                );

                context.Session.Clear();
            }

            // Continue down the pipeline
            await _next(context);
        }
    }
}

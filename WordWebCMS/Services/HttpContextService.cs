using Microsoft.AspNetCore.Http;

namespace WordWebCMS.Services
{
    /// <summary>
    /// Service to access HttpContext in static classes
    /// </summary>
    public class HttpContextService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpContextService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public HttpContext? Current => _httpContextAccessor.HttpContext;

        public string GetUserHostAddress()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return "unknown";

            // Try to get the real IP from forwarded headers first
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                var ips = forwardedFor.Split(',');
                if (ips.Length > 0)
                {
                    return ips[0].Trim();
                }
            }

            // Fallback to connection remote IP
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        public Uri? GetRequestUrl()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            var request = context.Request;
            var scheme = request.Scheme;
            var host = request.Host.Value;
            var pathBase = request.PathBase.Value;
            var path = request.Path.Value;
            var queryString = request.QueryString.Value;

            return new Uri($"{scheme}://{host}{pathBase}{path}{queryString}");
        }

        public Uri? GetRequestUrlAuthority()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            var request = context.Request;
            var scheme = request.Scheme;
            var host = request.Host.Value;

            return new Uri($"{scheme}://{host}");
        }
    }
}

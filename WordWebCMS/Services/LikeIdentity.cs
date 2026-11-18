using System.Security.Cryptography;
using System.Text;

namespace WordWebCMS.Services;

public static class LikeIdentity
{
    public static string Anonymous(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress;
        if (ip == null) return "";
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        // Setup owns a persistent random secret. Do not store raw IPs or trust arbitrary forwarded headers.
        var secret = context.RequestServices.GetRequiredService<IConfiguration>()["Setup:Token"];
        if (string.IsNullOrEmpty(secret)) throw new InvalidOperationException("Site identity key is unavailable.");
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), ip.GetAddressBytes())).ToLowerInvariant();
    }
}

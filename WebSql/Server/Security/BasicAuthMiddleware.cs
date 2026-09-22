using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace WebSql.Server.Security
{
    /// <summary>
    /// Puts a login in front of the ENTIRE site (pages, static files and API). Fails closed:
    /// if no credentials are configured, nothing is served. Wrong attempts are throttled per IP.
    /// Uses HTTP Basic auth, so it must only ever be served over HTTPS.
    /// </summary>
    public class BasicAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly AuthSettings _settings;
        private readonly ILogger<BasicAuthMiddleware> _logger;
        private readonly ConcurrentDictionary<string, Attempts> _attempts = new();
        private readonly HashSet<IPAddress> _allowedIps;

        public BasicAuthMiddleware(RequestDelegate next, AuthSettings settings, ILogger<BasicAuthMiddleware> logger)
        {
            _next = next;
            _settings = settings;
            _logger = logger;
            _allowedIps = settings.AllowedIps
                .Select(s => IPAddress.TryParse(s.Trim(), out var ip) ? ip : null)
                .Where(ip => ip is not null)
                .Select(ip => ip!.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip)
                .ToHashSet();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var remote = context.Connection.RemoteIpAddress;
            var ipKey = remote?.ToString() ?? "unknown";

            if (_settings.AllowedIps.Length > 0)
            {
                var normalized = remote is { IsIPv4MappedToIPv6: true } ? remote.MapToIPv4() : remote;
                if (normalized is null || !_allowedIps.Contains(normalized))
                {
                    _logger.LogWarning("Blocked request from non-allowlisted IP {Ip}", ipKey);
                    await Refuse(context, StatusCodes.Status403Forbidden, "Forbidden");
                    return;
                }
            }

            if (!_settings.IsConfigured)
            {
                await Refuse(context, StatusCodes.Status503ServiceUnavailable,
                    "WebSql is not configured. Set Auth:Username and Auth:PasswordHash " +
                    "(generate a hash with: dotnet run --project WebSql/Server -- hash-password).");
                return;
            }

            // Entries are only created for failures (and dropped on success), so the table can't be
            // inflated by ordinary traffic.
            if (_attempts.TryGetValue(ipKey, out var existing) && existing.LockedUntil > DateTime.UtcNow)
            {
                var wait = (int)Math.Ceiling((existing.LockedUntil - DateTime.UtcNow).TotalSeconds);
                context.Response.Headers.RetryAfter = wait.ToString();
                await Refuse(context, StatusCodes.Status429TooManyRequests, "Too many failed sign-in attempts. Try again later.");
                return;
            }

            if (TryReadCredentials(context, out var user, out var password))
            {
                // Always run the PBKDF2 verification so the timing does not reveal whether the username was right.
                var passwordOk = PasswordHasher.Verify(password, _settings.PasswordHash);
                var userOk = FixedEquals(user, _settings.Username);

                if (passwordOk && userOk)
                {
                    _attempts.TryRemove(ipKey, out _);
                    await _next(context);
                    return;
                }

                RegisterFailure(_attempts.GetOrAdd(ipKey, _ => new Attempts()), ipKey);
            }

            context.Response.Headers.WWWAuthenticate = "Basic realm=\"WebSql\", charset=\"UTF-8\"";
            await Refuse(context, StatusCodes.Status401Unauthorized, "Authentication required.");
        }

        private void RegisterFailure(Attempts state, string ipKey)
        {
            PurgeStale();
            lock (state)
            {
                state.Failures++;
                state.LastFailure = DateTime.UtcNow;
                if (state.Failures >= _settings.MaxFailedAttempts)
                {
                    state.LockedUntil = DateTime.UtcNow.AddMinutes(_settings.LockoutMinutes);
                    state.Failures = 0;
                    _logger.LogWarning("Locked out {Ip} for {Minutes} minutes after repeated failed sign-ins", ipKey, _settings.LockoutMinutes);
                }
            }
        }

        // Bounds memory if someone sprays failures from many addresses.
        private void PurgeStale()
        {
            if (_attempts.Count < 10_000) return;

            var cutoff = DateTime.UtcNow.AddMinutes(-Math.Max(_settings.LockoutMinutes, 60));
            foreach (var (ip, a) in _attempts)
                if (a.LockedUntil < DateTime.UtcNow && a.LastFailure < cutoff)
                    _attempts.TryRemove(ip, out _);
        }

        private static bool TryReadCredentials(HttpContext context, out string user, out string password)
        {
            user = password = string.Empty;
            var header = context.Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..].Trim()));
                var idx = decoded.IndexOf(':');
                if (idx < 0) return false;
                user = decoded[..idx];
                password = decoded[(idx + 1)..];
                return true;
            }
            catch (FormatException) { return false; }
        }

        private static bool FixedEquals(string a, string b) =>
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

        private static async Task Refuse(HttpContext context, int status, string message)
        {
            context.Response.StatusCode = status;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(message);
        }

        private sealed class Attempts
        {
            public int Failures;
            public DateTime LockedUntil = DateTime.MinValue;
            public DateTime LastFailure = DateTime.MinValue;
        }
    }
}

using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using WebSql.Server.Configuration;
using WebSql.Server.Security;
using WebSql.Server.Services;

// `dotnet run --project WebSql/Server -- hash-password` prints a PBKDF2 hash for Auth:PasswordHash.
if (args.Length > 0 && args[0].Equals("hash-password", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.Write("Password: ");
    var password = ReadPassword();
    Console.Error.WriteLine();
    if (string.IsNullOrEmpty(password) || password.Length < 12)
    {
        Console.Error.WriteLine("Use a password of at least 12 characters.");
        return 1;
    }
    Console.WriteLine(PasswordHasher.Hash(password));
    return 0;
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ---- Security settings -------------------------------------------------------------------------
var authSettings = new AuthSettings();
builder.Configuration.GetSection("Auth").Bind(authSettings);
builder.Services.AddSingleton(authSettings);

var securitySettings = new SecuritySettings();
builder.Configuration.GetSection("Security").Bind(securitySettings);
builder.Services.AddSingleton(securitySettings);

// JWT: the token is only a pointer to an in-memory session, and sessions do not survive a restart,
// so no key needs to be stored anywhere. If none is configured, generate a random one per process.
// A configured key must be strong - the old committed placeholder is refused.
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JwtSettings").Bind(jwtSettings);
if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey))
{
    jwtSettings.SecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
else if (jwtSettings.SecretKey.Length < 32 || jwtSettings.SecretKey.Contains("ChangeThisInProduction", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "JwtSettings:SecretKey is a placeholder or shorter than 32 characters. Remove it (a random key is generated at startup) or set a strong one.");
}
builder.Services.AddSingleton(jwtSettings);

// Register custom services
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();

// Add logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

// Configure CORS (the hosted client is same-origin; this only matters if you deliberately add other origins)
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "https://localhost:5001" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebSqlPolicy", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Rate limiting, partitioned per client IP so one caller cannot exhaust everyone's allowance.
// "connect" is deliberately tight: it is the endpoint that can be used to guess SQL logins.
static string ClientKey(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("fixed", ctx => RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:PermitLimit", 100),
        Window = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("RateLimiting:Window", 60)),
        QueueLimit = 0
    }));

    options.AddPolicy("connect", ctx => RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:ConnectPermitLimit", 10),
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Please try again later.", token);
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Only believe X-Forwarded-* from proxies the operator listed; otherwise anyone could spoof their IP.
if (securitySettings.TrustedProxies.Length > 0)
{
    var forwarded = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        ForwardLimit = 1
    };
    forwarded.KnownProxies.Clear();
    forwarded.KnownNetworks.Clear();
    foreach (var p in securitySettings.TrustedProxies)
    {
        if (IPAddress.TryParse(p.Trim(), out var proxyIp)) forwarded.KnownProxies.Add(proxyIp);
        else app.Logger.LogWarning("Security:TrustedProxies entry '{Entry}' is not an IP address and was ignored", p);
    }
    app.UseForwardedHeaders(forwarded);
}

app.UseHttpsRedirection();

// Response hardening. (No CSP: the Monaco editor needs inline/eval/blob workers; nosniff + framing
// protection + no-store on the API cover the cheap wins.)
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (context.Request.Path.StartsWithSegments("/api"))
        headers.CacheControl = "no-store";
    await next();
});

// The login wall. Everything below - pages, framework files, API - sits behind it.
app.UseMiddleware<BasicAuthMiddleware>();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

// Enable CORS
app.UseCors("WebSqlPolicy");

// Enable Rate Limiting
app.UseRateLimiter();

app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

if (!authSettings.IsConfigured)
{
    app.Logger.LogWarning(
        "Auth:Username / Auth:PasswordHash are not configured - every request will be refused. " +
        "Generate a hash with: dotnet run --project WebSql/Server -- hash-password");
}

app.Run();
return 0;

static string ReadPassword()
{
    if (Console.IsInputRedirected) return Console.ReadLine() ?? string.Empty;

    var sb = new System.Text.StringBuilder();
    ConsoleKeyInfo key;
    while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
    {
        if (key.Key == ConsoleKey.Backspace) { if (sb.Length > 0) sb.Length--; }
        else if (!char.IsControl(key.KeyChar)) sb.Append(key.KeyChar);
    }
    return sb.ToString();
}

// Exposes the entry point to the test project.
public partial class Program { }

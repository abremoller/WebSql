using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WebSql.Server.Security;

namespace WebSql.Tests;

[TestClass]
public class PasswordHasherTests
{
    private const int Fast = 100_000; // the minimum accepted; keeps tests quick

    [TestMethod]
    public void Roundtrip_Verifies()
    {
        var hash = PasswordHasher.Hash("correct horse battery", Fast);
        Assert.IsTrue(PasswordHasher.Verify("correct horse battery", hash));
        Assert.IsFalse(PasswordHasher.Verify("wrong password!!", hash));
    }

    [TestMethod]
    public void SameSaltIsNeverReused()
    {
        Assert.AreNotEqual(PasswordHasher.Hash("same password 123", Fast), PasswordHasher.Hash("same password 123", Fast));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("plaintext-password")]
    [DataRow("pbkdf2-sha256$5$c2FsdA==$aGFzaA==")]      // iteration count far too low
    [DataRow("pbkdf2-sha256$210000$not-base64$xx")]
    [DataRow("md5$1$a$b")]
    public void MalformedHashes_AreRejected_NotThrown(string? stored)
    {
        Assert.IsFalse(PasswordHasher.IsWellFormed(stored));
        Assert.IsFalse(PasswordHasher.Verify("anything", stored));
    }
}

[TestClass]
public class BasicAuthMiddlewareTests
{
    private const string Password = "correct horse battery";
    private static readonly string Hash = PasswordHasher.Hash(Password, 100_000);

    private static AuthSettings Configured(Action<AuthSettings>? tweak = null)
    {
        var s = new AuthSettings { Username = "admin", PasswordHash = Hash, MaxFailedAttempts = 3, LockoutMinutes = 15 };
        tweak?.Invoke(s);
        return s;
    }

    private static async Task<(int Status, string Body, IHeaderDictionary Headers, bool NextCalled)> Send(
        AuthSettings settings, string? user = null, string? pass = null, string ip = "10.0.0.1",
        BasicAuthMiddleware? reuse = null)
    {
        var nextCalled = false;
        var mw = reuse ?? new BasicAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; },
            settings, NullLogger<BasicAuthMiddleware>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        ctx.Response.Body = new MemoryStream();
        if (user is not null)
            ctx.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));

        await mw.InvokeAsync(ctx);

        ctx.Response.Body.Position = 0;
        return (ctx.Response.StatusCode, await new StreamReader(ctx.Response.Body).ReadToEndAsync(), ctx.Response.Headers, nextCalled);
    }

    [TestMethod]
    public async Task NotConfigured_FailsClosed_With503()
    {
        var r = await Send(new AuthSettings());
        Assert.AreEqual(503, r.Status);
        Assert.IsFalse(r.NextCalled);
        StringAssert.Contains(r.Body, "not configured");
    }

    [TestMethod]
    public async Task NotConfigured_EvenWithAnyCredentials_Refuses()
    {
        var r = await Send(new AuthSettings(), "admin", "anything");
        Assert.AreEqual(503, r.Status);
        Assert.IsFalse(r.NextCalled);
    }

    [TestMethod]
    public async Task MissingCredentials_Gets401_WithChallenge()
    {
        var r = await Send(Configured());
        Assert.AreEqual(401, r.Status);
        Assert.IsFalse(r.NextCalled);
        StringAssert.Contains(r.Headers.WWWAuthenticate.ToString(), "Basic");
    }

    [TestMethod]
    public async Task WrongPassword_Or_WrongUser_Gets401()
    {
        Assert.AreEqual(401, (await Send(Configured(), "admin", "nope-nope-nope")).Status);
        Assert.AreEqual(401, (await Send(Configured(), "root", Password)).Status);
        Assert.AreEqual(401, (await Send(Configured(), "ADMIN", Password)).Status); // username is case-sensitive
    }

    [TestMethod]
    public async Task CorrectCredentials_PassThrough()
    {
        var r = await Send(Configured(), "admin", Password);
        Assert.IsTrue(r.NextCalled);
        Assert.AreEqual(200, r.Status);
    }

    [TestMethod]
    public async Task RepeatedFailures_LockOutTheIp_EvenForCorrectPassword_ButNotOthers()
    {
        var settings = Configured();
        var mw = new BasicAuthMiddleware(_ => Task.CompletedTask, settings, NullLogger<BasicAuthMiddleware>.Instance);

        for (var i = 0; i < 3; i++)
            Assert.AreEqual(401, (await Send(settings, "admin", "bad-guess-" + i, "10.0.0.9", mw)).Status);

        var locked = await Send(settings, "admin", Password, "10.0.0.9", mw);
        Assert.AreEqual(429, locked.Status);
        Assert.IsTrue(int.Parse(locked.Headers.RetryAfter.ToString()) > 0);

        // A different address is unaffected.
        Assert.AreEqual(200, (await Send(settings, "admin", Password, "10.0.0.10", mw)).Status);
    }

    [TestMethod]
    public async Task SuccessfulLogin_ResetsEarlierFailures()
    {
        var settings = Configured();
        var mw = new BasicAuthMiddleware(_ => Task.CompletedTask, settings, NullLogger<BasicAuthMiddleware>.Instance);

        // Two failures, a success, then two more failures must not add up to a lockout (limit is 3).
        for (var i = 0; i < 2; i++) await Send(settings, "admin", "bad-guess-" + i, "10.0.0.20", mw);
        Assert.AreEqual(200, (await Send(settings, "admin", Password, "10.0.0.20", mw)).Status);
        for (var i = 0; i < 2; i++) Assert.AreEqual(401, (await Send(settings, "admin", "bad-guess-" + i, "10.0.0.20", mw)).Status);
        Assert.AreEqual(200, (await Send(settings, "admin", Password, "10.0.0.20", mw)).Status);
    }

    [TestMethod]
    public async Task IpAllowlist_BlocksOthers_AndAllowsListed()
    {
        var settings = Configured(s => s.AllowedIps = ["203.0.113.5"]);
        Assert.AreEqual(403, (await Send(settings, "admin", Password, "198.51.100.7")).Status);
        Assert.AreEqual(200, (await Send(settings, "admin", Password, "203.0.113.5")).Status);
    }

    [TestMethod]
    public async Task IpAllowlist_HandlesIPv4MappedAddresses()
    {
        var settings = Configured(s => s.AllowedIps = ["203.0.113.5"]);
        var r = await Send(settings, "admin", Password, "::ffff:203.0.113.5");
        Assert.AreEqual(200, r.Status);
    }

    [TestMethod]
    public async Task MalformedAuthorizationHeader_Gets401_NotAnException()
    {
        var settings = Configured();
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        ctx.Response.Body = new MemoryStream();
        ctx.Request.Headers.Authorization = "Basic !!!not-base64!!!";

        var mw = new BasicAuthMiddleware(_ => Task.CompletedTask, settings, NullLogger<BasicAuthMiddleware>.Instance);
        await mw.InvokeAsync(ctx);

        Assert.AreEqual(401, ctx.Response.StatusCode);
    }
}

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using WebSql.Server.Configuration;
using WebSql.Server.Security;
using WebSql.Server.Services;
using WebSql.Shared;

namespace WebSql.Tests;

[TestClass]
public class ConnectionManagerTests
{
    private static ConnectionManager Create(SecuritySettings? settings = null)
    {
        var jwt = new JwtSettings { SecretKey = "test-secret-key-that-is-long-enough-for-hs256-0123456789" };
        return new ConnectionManager(
            NullLogger<ConnectionManager>.Instance,
            new JwtService(jwt, NullLogger<JwtService>.Instance),
            settings ?? new SecuritySettings());
    }

    private static ConnectionDetails Sql(string server = "db1", string login = "reader", string password = "pw", string? database = null) =>
        new() { ServerName = server, Login = login, Password = password, SelectedDatabase = database };

    private static ConnectionDetails MySql(string server = "db1", string login = "root", string password = "pw", string? database = null) =>
        new() { Engine = DatabaseEngine.MySql, ServerName = server, Login = login, Password = password, SelectedDatabase = database };

    [TestMethod]
    public async Task MySql_SessionsRememberTheirEngine()
    {
        var mgr = Create();
        var my = await mgr.CreateConnectionAsync(MySql());
        var ms = await mgr.CreateConnectionAsync(Sql());

        Assert.AreEqual(DatabaseEngine.MySql, mgr.GetEngine(my));
        Assert.AreEqual(DatabaseEngine.SqlServer, mgr.GetEngine(ms));
        Assert.AreEqual(DatabaseEngine.SqlServer, mgr.GetEngine("not-a-token"));
    }

    [TestMethod]
    [DataRow("db1", "db1", 3306u)]
    [DataRow("db1:3307", "db1", 3307u)]
    [DataRow("::1", "::1", 3306u)] // IPv6 literal is not host:port
    public async Task MySql_ServerNameIsSplitIntoHostAndPort(string server, string host, uint port)
    {
        var mgr = Create();
        var token = await mgr.CreateConnectionAsync(MySql(server: server));

        var b = new MySqlConnectionStringBuilder(mgr.GetConnectionString(token)!);
        Assert.AreEqual(host, b.Server);
        Assert.AreEqual(port, b.Port);
    }

    [TestMethod]
    public async Task MySql_ConnectionStringInjection_IsNeutralised()
    {
        var mgr = Create();
        var token = await mgr.CreateConnectionAsync(MySql(password: "x;Server=evil.example.com", login: "u;Database=mysql"));
        mgr.UpdateDatabase(token, "sales;Password=hacked");

        var b = new MySqlConnectionStringBuilder(mgr.GetConnectionString(token)!);
        Assert.AreEqual("db1", b.Server);
        Assert.AreEqual("x;Server=evil.example.com", b.Password);
        Assert.AreEqual("u;Database=mysql", b.UserID);
        Assert.AreEqual("sales;Password=hacked", b.Database);
    }

    [TestMethod]
    public async Task MySql_RefusesIntegratedSecurity()
    {
        var mgr = Create(new SecuritySettings { AllowIntegratedSecurity = true });
        var details = MySql();
        details.IntegratedSecurity = true;
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => mgr.CreateConnectionAsync(details));
    }

    [TestMethod]
    public async Task MySql_CertificateIsValidatedByDefault_AndOptInToTrust()
    {
        var strict = Create();
        var t1 = await strict.CreateConnectionAsync(MySql());
        Assert.AreEqual(MySqlSslMode.VerifyFull, new MySqlConnectionStringBuilder(strict.GetConnectionString(t1)!).SslMode);

        var lax = Create(new SecuritySettings { TrustServerCertificate = true });
        var t2 = await lax.CreateConnectionAsync(MySql());
        Assert.AreEqual(MySqlSslMode.Required, new MySqlConnectionStringBuilder(lax.GetConnectionString(t2)!).SslMode);
    }

    [TestMethod]
    public async Task IntegratedSecurity_IsRefusedByDefault()
    {
        var mgr = Create();
        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            mgr.CreateConnectionAsync(new ConnectionDetails { ServerName = "db1", IntegratedSecurity = true }));
        StringAssert.Contains(ex.Message, "Integrated security is disabled");
    }

    [TestMethod]
    public async Task IntegratedSecurity_WorksWhenExplicitlyEnabled()
    {
        var mgr = Create(new SecuritySettings { AllowIntegratedSecurity = true });
        var token = await mgr.CreateConnectionAsync(new ConnectionDetails { ServerName = "db1", IntegratedSecurity = true });
        Assert.IsTrue(new SqlConnectionStringBuilder(mgr.GetConnectionString(token)!).IntegratedSecurity);
    }

    [TestMethod]
    public async Task ConnectionStringInjection_IsNeutralised()
    {
        var mgr = Create();
        var token = await mgr.CreateConnectionAsync(
            Sql(password: "x;Integrated Security=True;Data Source=evil.example.com", login: "u;Initial Catalog=master"));

        var b = new SqlConnectionStringBuilder(mgr.GetConnectionString(token)!);
        Assert.AreEqual("db1", b.DataSource);
        Assert.IsFalse(b.IntegratedSecurity);
        Assert.AreEqual("x;Integrated Security=True;Data Source=evil.example.com", b.Password);
        Assert.AreEqual("u;Initial Catalog=master", b.UserID);
        Assert.AreEqual(string.Empty, b.InitialCatalog);
    }

    [TestMethod]
    public async Task DatabaseName_CannotInjectKeywords()
    {
        var mgr = Create();
        var token = await mgr.CreateConnectionAsync(Sql());
        mgr.UpdateDatabase(token, "Sales;Integrated Security=True");

        var b = new SqlConnectionStringBuilder(mgr.GetConnectionString(token)!);
        Assert.AreEqual("Sales;Integrated Security=True", b.InitialCatalog);
        Assert.IsFalse(b.IntegratedSecurity);
    }

    [TestMethod]
    [DataRow("db1", "db1")]                    // unchanged: no port
    [DataRow("db1,1433", "db1,1433")]           // unchanged: SQL Server's own comma syntax
    [DataRow("127.0.0.1:14330", "127.0.0.1,14330")] // "host:port", as typed in the dialog, is accepted too
    [DataRow("::1", "::1")]                     // IPv6 literal is not host:port
    public async Task Sql_ColonPortSyntax_IsAcceptedLikeComma(string server, string expectedDataSource)
    {
        var mgr = Create();
        var token = await mgr.CreateConnectionAsync(Sql(server: server));

        var b = new SqlConnectionStringBuilder(mgr.GetConnectionString(token)!);
        Assert.AreEqual(expectedDataSource, b.DataSource);
    }

    [TestMethod]
    public async Task ServerCertificate_IsValidatedByDefault_AndOptInToTrust()
    {
        var strict = Create();
        var t1 = await strict.CreateConnectionAsync(Sql());
        Assert.IsFalse(new SqlConnectionStringBuilder(strict.GetConnectionString(t1)!).TrustServerCertificate);

        var lax = Create(new SecuritySettings { TrustServerCertificate = true });
        var t2 = await lax.CreateConnectionAsync(Sql());
        Assert.IsTrue(new SqlConnectionStringBuilder(lax.GetConnectionString(t2)!).TrustServerCertificate);
    }

    [TestMethod]
    public async Task ServerAllowlist_IsEnforced_CaseInsensitively()
    {
        var mgr = Create(new SecuritySettings { AllowedServers = ["Prod-SQL01"] });

        await mgr.CreateConnectionAsync(Sql(server: "prod-sql01"));
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => mgr.CreateConnectionAsync(Sql(server: "internal-db.corp")));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("db1\r\nextra")]
    public async Task InvalidServerNames_AreRejected(string server)
    {
        var mgr = Create();
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => mgr.CreateConnectionAsync(Sql(server: server)));
    }

    [TestMethod]
    public async Task SqlLoginRequiresALogin()
    {
        var mgr = Create();
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => mgr.CreateConnectionAsync(Sql(login: "")));
    }

    [TestMethod]
    public async Task IdleSessions_Expire_AndTheirCredentialsAreForgotten()
    {
        var mgr = Create(new SecuritySettings { SessionIdleMinutes = 0.0005 }); // ~30ms
        var token = await mgr.CreateConnectionAsync(Sql());
        Assert.IsTrue(mgr.ValidateSession(token));

        await Task.Delay(120);

        Assert.IsFalse(mgr.ValidateSession(token));
        Assert.IsNull(mgr.GetConnectionString(token));
    }

    [TestMethod]
    public async Task ActiveUse_KeepsASessionAlive()
    {
        var mgr = Create(new SecuritySettings { SessionIdleMinutes = 0.004 }); // ~240ms
        var token = await mgr.CreateConnectionAsync(Sql());

        for (var i = 0; i < 4; i++)
        {
            await Task.Delay(100);
            Assert.IsNotNull(mgr.GetConnectionString(token), "session should stay alive while used");
        }
    }

    [TestMethod]
    public async Task SessionCap_IsEnforced()
    {
        var mgr = Create(new SecuritySettings { MaxSessions = 2 });
        await mgr.CreateConnectionAsync(Sql());
        await mgr.CreateConnectionAsync(Sql());
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => mgr.CreateConnectionAsync(Sql()));
    }

    [TestMethod]
    public async Task Disconnect_ForgetsTheSession()
    {
        var mgr = Create();
        var token = await mgr.CreateConnectionAsync(Sql());
        await mgr.DisconnectAsync(token);
        Assert.IsFalse(mgr.ValidateSession(token));
    }

    [TestMethod]
    public void ForgedOrGarbageTokens_AreRejected()
    {
        var mgr = Create();
        Assert.IsFalse(mgr.ValidateSession(""));
        Assert.IsFalse(mgr.ValidateSession("not-a-jwt"));

        // A token signed with a different key must not validate.
        var other = new JwtService(new JwtSettings { SecretKey = "another-secret-key-that-is-long-enough-for-hs256-9876543210" },
            NullLogger<JwtService>.Instance);
        Assert.IsFalse(mgr.ValidateSession(other.GenerateToken(Guid.NewGuid().ToString(), "db1")));
    }
}

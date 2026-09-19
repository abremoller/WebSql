# WebSql

A modern, web-based database management tool built with Blazor WebAssembly and ASP.NET Core.

## 🎯 Project Vision

WebSql is designed to be a powerful, browser-based SQL client that can be self-hosted on any web server, giving you secure access to your databases from anywhere. Think of it as a self-hosted alternative to database management tools, accessible through your browser.

## ✨ Current Features (MVP)

- 🔐 **Login-protected** - The whole site sits behind a login (it refuses all requests until you configure one)
- 🔒 **Secure Connection Management** - Connection strings live server-side; sessions expire when idle
- 🗄️ **SQL Server Support** - Connect with a SQL login (integrated security exists but is off by default)
- 🌳 **Object Explorer** - Browse databases, tables, columns, and schema information
- ⚡ **Query Execution** - Write and execute SQL queries with real-time results
- 📊 **Results Grid** - View query results in a clean, tabular format
- 🔄 **Database Switching** - Quickly switch between databases on the same server
- ⏱️ **Performance Metrics** - Track query execution time and rows affected

## 🏗️ Architecture

- **Frontend:** Blazor WebAssembly (C#)
- **Backend:** ASP.NET Core Web API
- **UI Framework:** Havit Blazor Components
- **Database:** SQL Server with Microsoft.Data.SqlClient
- **Target Platform:** .NET 8.0 LTS
- **Authentication:** HTTP Basic login in front of everything (PBKDF2-hashed password, per-IP lockout, optional IP allowlist); JWT session tokens for database sessions
- **Security:** see [Security](#-security) below

## 🚀 Getting Started

### Prerequisites
- .NET 8.0 SDK or later
- SQL Server (for database connections)
- Visual Studio 2022 or VS Code with C# extension

### Running Locally

```powershell
# Clone the repository
git clone https://github.com/abremoller/WebSql.git
cd WebSql

# Restore dependencies
dotnet restore

# Configure the login (see Security below) - the site refuses every request until you do
# Run the server project
cd WebSql/Server
dotnet run
```

Navigate to `https://localhost:7xxx` in your browser.

## 📚 Documentation

Comprehensive documentation is available in the [Documentation](./Documentation) folder:

- **[Development Roadmap](./Documentation/DevelopmentList1.md)** - Current and planned features
- **[Change Log](./Documentation/ChangeLog.md)** - Project history and changes
- **[Implementation Summary](./Documentation/ImplementationSummary.md)** - Recent refactoring details
- **[Migration Guide](./Documentation/MigrationGuide.md)** - API changes and upgrade instructions

## 🔐 Security

WebSql can run arbitrary SQL against whatever server you point it at, so treat it like the database itself. **Do not expose it to the internet without HTTPS, a strong password and (ideally) an IP allowlist.**

### First-time setup (required)

The site **refuses every request until a login is configured**.

1. Generate a password hash (minimum 12 characters; input is not echoed):

   `dotnet run --project WebSql/Server -- hash-password`

2. Store it with the username. In development use user-secrets; in production use environment variables (`Auth__Username`, `Auth__PasswordHash`) or your host's secret store. Never commit them:

   ```
   cd WebSql/Server
   dotnet user-secrets set "Auth:Username" "your-username"
   dotnet user-secrets set "Auth:PasswordHash" "<the hash from step 1>"
   ```

### Settings

| Setting | Default | Meaning |
|---|---|---|
| `Auth:AllowedIps` | `[]` | If set, only these client IPs can reach the site at all |
| `Auth:MaxFailedAttempts` / `Auth:LockoutMinutes` | 5 / 15 | Failed-login lockout, per IP |
| `Security:AllowIntegratedSecurity` | `false` | Allow connecting as the Windows account the app runs under. Leave off unless you understand that every WebSql user then inherits that account's SQL access |
| `Security:TrustServerCertificate` | `false` | Skip SQL Server certificate validation. Turn on only for self-signed certificates you trust |
| `Security:AllowedServers` | `[]` | If set, users can only connect to these servers |
| `Security:SessionIdleMinutes` / `Security:MaxSessions` | 30 / 25 | Idle sessions are dropped and their credentials forgotten |
| `Security:DangerousOperationsMode` | `Prompt` | `Disabled`, `Prompt` or `Enabled` for DROP, TRUNCATE, EXEC, UPDATE/DELETE without WHERE and similar |
| `RateLimiting:ConnectPermitLimit` | 10 per minute per IP | Throttles connection attempts (guessing SQL logins) |
| `JwtSettings:SecretKey` | empty | Leave empty: a random key is generated at startup (sessions do not survive restarts anyway). A configured key must be 32+ characters |

### What is protected

- ✅ Everything (pages, framework files, API) is behind the login; wrong passwords lock out the IP
- ✅ Connection strings are built with `SqlConnectionStringBuilder` (no injection through server, login, password or database name) and never leave the server
- ✅ Integrated security and certificate trust are opt-in; optional server allowlist
- ✅ Idle sessions expire; per-IP rate limits (tighter on `connect`); no secrets committed to git
- ✅ The old unauthenticated `SQL/RunQuery/...` and `SQL/GetServerExplorer/...` GET endpoints (connection string in the URL) were removed

### Notes

- The dangerous-operation prompt is a guard against **accidents**, not a security boundary. The real boundary is the permissions of the SQL login you connect with: **use a least-privilege (ideally read-only) login.**
- The login uses HTTP Basic auth, so it is only safe over HTTPS.
- Behind a reverse proxy, the client IP used for lockout and the allowlist is the address the app sees; make sure the real client address is passed through before relying on `Auth:AllowedIps`.
- Run `dotnet test` for the 68 tests covering the login, connection policy and query guard.

## 🛣️ Roadmap

### Current Phase: MVP Security & Core Features
- [x] Secure connection management
- [x] Async/await architecture
- [x] Session-based authentication
- [x] Login protection, rate limiting, session expiry
- [ ] Monaco code editor integration
- [ ] Advanced results display

### Future Phases:
- **Phase 2:** Advanced features, collaboration, multi-database support
- **Phase 3:** AI assistance, real-time collaboration, advanced visualization

See [Development Lists](./Documentation) for detailed roadmap.

## 🤝 Contributing

This is currently a personal project, but contributions are welcome! Please:

1. Check the [Development List](./Documentation/DevelopmentList1.md) for available tasks
2. Create a feature branch
3. Make your changes with appropriate tests
4. Update documentation (ChangeLog.md, etc.)
5. Submit a pull request

## 📝 Project Status

- **Current Version:** 0.3.0-alpha (unreleased)
- **Framework:** .NET 8.0 LTS
- **Status:** Active Development
- **Branch:** MVP
- **Last Updated:** December 13, 2025

## 🎓 Learning Journey

This project was created as a practical learning exercise in:
- Blazor WebAssembly development
- ASP.NET Core Web API design
- Secure authentication patterns
- Async/await best practices
- Modern C# development

## 📄 License

[MIT](LICENSE)

## 🙏 Acknowledgments

- [Havit Blazor Components](https://havit.blazor.eu/) - Excellent UI component library
- [Blazor Community](https://blazor.net/) - Great resources and support

---

**Note:** This is an MVP in active development. Features are being added regularly, and the API may change. See [ChangeLog](./Documentation/ChangeLog.md) for recent updates.
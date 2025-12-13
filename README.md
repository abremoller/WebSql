# WebSql

A modern, web-based database management tool built with Blazor WebAssembly and ASP.NET Core.

## 🎯 Project Vision

WebSql is designed to be a powerful, browser-based SQL client that can be hosted on any web server (like Plesk), giving you secure access to your databases from anywhere. Think of it as a self-hosted alternative to database management tools, accessible through your browser.

## ✨ Current Features (MVP)

- 🔐 **Secure Connection Management** - Session-based authentication with server-side connection storage
- 🗄️ **SQL Server Support** - Connect to SQL Server databases with integrated or SQL authentication
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
- **Authentication:** JWT (JSON Web Tokens)
- **Security:** Rate limiting, CORS policies

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

**Recent Security Improvements:**
- ✅ Connection strings no longer exposed in URLs
- ✅ Session-based authentication with secure token management
- ✅ Server-side connection string storage
- ✅ Automatic session expiration (24 hours)
- ✅ All API endpoints use POST requests with request validation

**Planned Security Enhancements:**
- JWT authentication
- Rate limiting
- CORS policies
- Parameterized query enforcement

## 🛣️ Roadmap

### Current Phase: MVP Security & Core Features
- [x] Secure connection management
- [x] Async/await architecture
- [x] Session-based authentication
- [ ] JWT authentication
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

[Add your license here]

## 🙏 Acknowledgments

- [Havit Blazor Components](https://havit.blazor.eu/) - Excellent UI component library
- [Blazor Community](https://blazor.net/) - Great resources and support

---

**Note:** This is an MVP in active development. Features are being added regularly, and the API may change. See [ChangeLog](./Documentation/ChangeLog.md) for recent updates.
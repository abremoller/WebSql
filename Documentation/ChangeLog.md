# WebSql Change Log

## Current Sprint - Security & Architecture Refactoring

### 🎯 Sprint Goal
Implement critical security fixes and modernize the architecture to prepare for production deployment.

---

## [Unreleased] - In Progress

### Added
- 📝 Created comprehensive development roadmaps (Lists 1, 2, 3)
- 📝 Created change log tracking document
- ✅ **IConnectionManager** interface for secure connection management
- ✅ **ConnectionManager** service with session-based token system
- ✅ **New API Controllers**: ConnectionController and QueryController
- ✅ **DTOs for API requests/responses** (ConnectRequest, QueryRequest, etc.)
- ✅ Session expiration (24-hour timeout)
- ✅ Connection validation on every API call
- ✅ Execution time metrics in query responses

### Changed
- ♻️ **REMOVED connection strings from URLs** - Now using POST requests with session tokens
- ♻️ **Converted all API endpoints to POST** - No more sensitive data in URLs
- ⚡ **Added async/await throughout** - MSSQL.RunQueryAsync(), ServerLogic async methods
- 🔒 **ConnectionService now manages session tokens** - Client stores token, not connection string
- ♻️ Updated ConnectionDetails model with SelectedDatabase property
- ⚡ Query execution now returns metrics (execution time, rows affected)
- ♻️ Improved null safety with nullable reference types

### Security
- 🔒 **CRITICAL FIX: Connection strings no longer exposed in URLs**
- 🔒 Session-based authentication with unique tokens per connection
- 🔒 Server-side connection string management
- 🔒 Session validation on every API request
- 🔒 Automatic session expiration (24 hours)
- 🔒 Password no longer saved to localStorage (only if explicitly requested)

### Technical Improvements
- Better error handling with structured responses
- Logging infrastructure added to server
- ConnectionManager registered as singleton service
- Async database operations for better performance
- Null-safe string handling throughout

### Known Remaining Issues
- ⚠️ Still need parameterized query support
- ⚠️ No JWT/proper authentication yet
- ⚠️ No rate limiting
- ⚠️ Error messages could be more user-friendly in UI

---

## [0.1.0] - Initial MVP - 2024

### Added
- ✅ Blazor WebAssembly client application
- ✅ ASP.NET Core Web API server
- ✅ SQL Server database connectivity
- ✅ Basic object explorer (databases, tables, columns)
- ✅ Simple query editor (text area)
- ✅ Results grid display
- ✅ Connection dialog with auth options
- ✅ Database switching dropdown
- ✅ Local storage for connection details
- ✅ Havit Blazor UI components integration

### Known Issues
- ⚠️ Connection strings exposed in URLs (SECURITY RISK)
- ⚠️ No API authentication/authorization
- ⚠️ Synchronous database operations only
- ⚠️ No SQL injection prevention
- ⚠️ Limited error handling
- ⚠️ No connection pooling management
- ⚠️ Basic UI components (no Monaco editor)

---

## Legend
- ✅ Completed
- 🚧 In Progress
- ⚠️ Known Issue
- 🔴 Critical
- 🟠 High Priority
- 🟡 Medium Priority
- 🟢 Low Priority
- 📝 Documentation
- 🔒 Security
- 🐛 Bug Fix
- ✨ New Feature
- ♻️ Refactoring
- ⚡ Performance
- 🎨 UI/UX

---

## Update Instructions
When implementing changes:
1. Mark items in DevelopmentList1.md with [x]
2. Add detailed changes to this ChangeLog.md
3. Use appropriate emoji/category markers
4. Include before/after examples for significant changes
5. Note any breaking changes prominently

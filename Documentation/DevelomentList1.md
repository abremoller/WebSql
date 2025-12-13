# WebSql Development Roadmap

## 🔴 Critical Security Fixes (Priority 1)

### 1. Secure Connection String Handling
- [x] **Remove connection strings from URLs** - Currently passing sensitive data in GET route parameters
- [x] **Implement server-side connection management** - Store connections with session/token IDs
- [x] **Add JWT authentication** - Secure API endpoints with bearer tokens
- [x] **Encrypt stored credentials** - Credentials not persisted; handled via JWT tokens and server-side connection management
- [x] **Add CORS policies** - Restrict API access to authorized origins only

### 2. SQL Injection Prevention
- [N/A] **Implement parameterized queries** - Not applicable: This is a SQL query tool where users write raw SQL (like SSMS). Query validation provides appropriate protection.
- [x] **Add query validation** - Sanitize and validate all user input
- [x] **Implement query allow/deny lists** - Restrict dangerous SQL commands with 3-state mode (Disabled/Prompt/Enabled)

### 3. API Security
- [x] **Add authentication middleware** - Protect all API endpoints
- [x] **Implement rate limiting** - Prevent API abuse
- [DEFERRED] **Add authorization policies** - Role-based access control (Post-MVP: requires user management system)
- [x] **Secure error handling** - Don't expose stack traces/connection details to client

---

## 🟠 High Priority Architecture Improvements (Priority 2)

### 4. Connection Management
- [x] **Create ConnectionManager service** - Centralized connection lifecycle management
- [DEFERRED] **Implement connection pooling** - SQL Server handles pooling automatically; additional pooling would add complexity without benefit
- [x] **Add connection profiles** - Save/load named connection configurations  
- [x] **Add server connection history** - Dropdown with last 10 successful server connections cached in browser
- [x] **Support connection timeouts** - Configurable timeout settings
- [x] **Add connection health checks** - Verify connections before use

### 5. Data Access Layer Refactoring
- [x] **Convert to async/await** - Use async database operations throughout
- [ ] **Implement repository pattern** - Abstract data access logic
- [x] **Add proper error handling** - Structured exception handling and logging
- [ ] **Use IDbConnection abstraction** - Prepare for multi-database support
- [ ] **Add transaction support** - Enable multi-statement transactions

### 6. API Design Improvements
- [x] **Change to POST requests** - Move sensitive data from URLs to request bodies
- [x] **Implement proper DTOs** - Request/Response models with validation
- [ ] **Add API versioning** - Support v1, v2 endpoints
- [x] **Implement structured logging** - Use Serilog or similar
- [x] **Add comprehensive error responses** - Standardized error format

---

## 🟡 Medium Priority Features (Priority 3)

### 7. Query Editor Enhancements
- [ ] **Integrate Monaco Editor** - VS Code-like SQL editing experience
- [ ] **Add syntax highlighting** - SQL keyword highlighting
- [ ] **Implement IntelliSense/autocomplete** - Auto-completion for tables/columns/SQL keywords
- [ ] **Add query formatting** - Auto-format SQL queries
- [ ] **Enable multiple query tabs** - Work on multiple queries simultaneously

### 8. Results Display Improvements
- [ ] **Add pagination** - Handle large result sets efficiently
- [ ] **Implement column sorting** - Click-to-sort functionality
- [ ] **Add column filtering** - Quick filter per column
- [ ] **Enable column resizing** - Adjustable column widths
- [ ] **Add row selection** - Select and copy multiple rows
- [ ] **Export results** - CSV, JSON, Excel export options

### 9. Object Explorer Enhancements
- [ ] **Add views support** - Display database views
- [ ] **Add stored procedures** - Browse and execute sprocs
- [ ] **Add functions support** - Scalar and table-valued functions
- [ ] **Show indexes** - Display table indexes
- [ ] **Add schema information** - Show column types, constraints, defaults
- [ ] **Implement right-click context menu** - Quick actions (Script table, etc.)
- [ ] **Add search/filter** - Find objects by name

### 10. Query Execution Features
- [ ] **Add query history** - Save and recall previous queries
- [ ] **Show execution time** - Display query duration
- [ ] **Implement Messages tab** - Display query execution messages like SSMS (errors, warnings, rows affected, etc.)
- [ ] **Add execution plans** - View query execution plans
- [ ] **Implement query cancellation** - Stop long-running queries
- [ ] **Add transaction controls** - BEGIN, COMMIT, ROLLBACK buttons
- [ ] **Show rows affected** - Display modification counts
- [ ] **Add query templates** - Common query snippets

---

## 🟢 Lower Priority / Nice-to-Have (Priority 4)

### 11. Multi-Database Support
- [ ] **Abstract database provider interface** - IDbProvider pattern
- [ ] **Add PostgreSQL support** - Implement Postgres provider
- [ ] **Add MySQL support** - Implement MySQL provider
- [ ] **Add SQLite support** - Implement SQLite provider
- [ ] **Add MongoDB support** - NoSQL document database
- [ ] **Database-specific features** - Handle vendor-specific SQL

### 12. Advanced Features
- [ ] **Add backup/restore** - Database backup management
- [ ] **Implement schema compare** - Compare database schemas
- [ ] **Add data compare** - Compare table data
- [ ] **Generate scripts** - DDL script generation
- [ ] **Add diagram designer** - Visual schema designer
- [ ] **Implement migration tools** - Schema migration support

### 13. User Experience
- [ ] **Add dark/light theme toggle** - Theme customization
- [ ] **Implement keyboard shortcuts** - F5 to execute, Ctrl+S to save, etc.
- [ ] **Add query bookmarks** - Save favorite queries
- [ ] **Implement workspace saves** - Save entire work session
- [ ] **Add collaborative features** - Share queries with team
- [ ] **Mobile responsive design** - Optimize for tablets/phones

### 14. Configuration & Settings
- [ ] **Add appsettings configuration** - Move hardcoded values to config
- [ ] **Implement user preferences** - Persistent user settings
- [ ] **Add connection string encryption** - Encrypted config storage
- [ ] **Environment-specific configs** - Dev/Staging/Prod settings
- [ ] **Add feature flags** - Toggle features on/off

### 15. Testing & Quality
- [ ] **Add unit tests** - Test business logic
- [ ] **Add integration tests** - Test API endpoints
- [ ] **Add UI tests** - Blazor component tests
- [ ] **Implement CI/CD pipeline** - Automated build/test/deploy
- [ ] **Add code coverage** - Track test coverage metrics

### 16. Documentation
- [ ] **Add API documentation** - Swagger/OpenAPI docs
- [ ] **Create user guide** - How-to documentation
- [ ] **Add deployment guide** - Plesk deployment instructions
- [ ] **Create developer docs** - Architecture and contribution guide
- [ ] **Add inline code comments** - Improve code documentation

---

## 🔮 Post-MVP Advanced Features
See [AdvancedFeatures.md](AdvancedFeatures.md) for detailed documentation on:
- **Data Editing Interface** - No-code SQL editing with role-based access
- **Audit Logging System** - Comprehensive change tracking and compliance features

---

## 📊 Current Status
- **Project Phase**: MVP - Security Hardened
- **Framework**: .NET 8.0 LTS / Blazor WebAssembly
- **Database Support**: SQL Server (Microsoft.Data.SqlClient 5.1.5)
- **Deployment Target**: Plesk hosting
- **Authentication**: JWT with HMAC-SHA256 signing
- **Security**: Rate limiting, CORS, secure token management, dangerous operations protection
- **Code Editor**: Monaco Editor with SQL syntax highlighting and IntelliSense
- **Query Feedback**: Messages panel with execution time and rows affected

## 🎯 Recommended Next Steps
1. Start with **Critical Security Fixes** (items 1-3)
2. Move to **Connection Management & API improvements** (items 4-6)
3. Enhance **Query Editor** with Monaco integration (item 7)
4. Improve **Results Display** and **Object Explorer** (items 8-9)
5. Plan for **Multi-Database Support** architecture (item 11)

---

## 🔒 Security Features

### Dangerous Operations Protection (3-State Mode)

WebSql includes a `QueryValidator` service that protects against dangerous SQL operations with configurable security levels.

#### Configuration

Set the mode in `appsettings.json`:

```json
{
  "Security": {
    "DangerousOperationsMode": "Prompt"
  }
}
```

#### Security Modes

**1. `Disabled` (Most Secure)**
- All dangerous operations are blocked immediately
- Error returned without execution
- Recommended for production environments
- No bypass available

**2. `Prompt` (Balanced - Default)**
- Dangerous operations trigger confirmation dialog
- User must explicitly approve execution
- Shows specific dangerous operations detected
- Best for development/testing environments
- Provides safety with flexibility

**3. `Enabled` (Least Secure)**
- All operations allowed without prompting
- No validation interruptions
- Use only in controlled environments
- Suitable for automated scripts or trusted admins

#### Protected Operations

The validator blocks/prompts for:
- `DROP` statements (DATABASE, TABLE, VIEW, PROCEDURE, FUNCTION)
- `TRUNCATE` operations
- System commands (`SHUTDOWN`, `DBCC`, `xp_cmdshell`, `sp_configure`)
- `BACKUP` / `RESTORE` operations
- `DELETE` without `WHERE` clause
- `UPDATE` without `WHERE` clause

#### How It Works

1. User submits SQL query
2. `QueryValidator` analyzes query for dangerous patterns
3. Based on configured mode:
   - **Disabled**: Returns error immediately
   - **Prompt**: Returns `RequiresConfirmation=true` to client
   - **Enabled**: Executes without interruption
4. If confirmation required, JavaScript dialog shows warning
5. User confirms or cancels
6. If confirmed, query resubmitted with `ConfirmedDangerous=true`
7. Validator allows execution of confirmed dangerous queries

#### Logging

All dangerous query attempts are logged regardless of mode:
```
LogWarning("Dangerous query blocked: {DangerousOp} - Query: {Query}")
```

#### Files Modified
- `WebSql/Server/Services/QueryValidator.cs` - Validation logic with mode support
- `WebSql/Server/Controllers/QueryController.cs` - Injects configuration, uses validator
- `WebSql/Shared/DTOs/ApiDTOs.cs` - Added `ConfirmedDangerous` and `RequiresConfirmation` fields
- `WebSql/Client/Services/ConnectionService.cs` - Handles confirmation parameter
- `WebSql/Client/Pages/Index.razor` - Shows confirmation dialog when needed
- `WebSql/Server/appsettings.json` - Configuration setting
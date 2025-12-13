# Migration Guide - Security Refactoring

## Overview
This guide explains the breaking changes from the initial MVP to the secured version.

---

## API Changes

### ❌ OLD API (Deprecated - Remove These)

```csharp
// WebSql/Server/Controllers/SQLController.cs - DELETE THIS FILE

[HttpGet]
[Route("SQL/GetServerExplorer/{connectionString}")]
public IActionResult GetServerExplorer(string connectionString)

[HttpGet]
[Route("SQL/RunQuery/{connectionString}&{query}")]
public IActionResult RunQuery(string connectionString, string query)
```

### ✅ NEW API (Use These Instead)

```csharp
// WebSql/Server/Controllers/ConnectionController.cs
[HttpPost("api/Connection/connect")]
[HttpPost("api/Connection/disconnect")]
[HttpPost("api/Connection/change-database")]

// WebSql/Server/Controllers/QueryController.cs
[HttpPost("api/Query/execute")]
[HttpPost("api/Query/object-explorer")]
```

---

## Client Code Migration

### Connection Flow

**OLD:**
```csharp
// Built connection string on client
var connectionString = $"Data Source={server};User Id={login};Password={password};";

// Passed in URL (INSECURE!)
var result = await http.GetFromJsonAsync<ObjectExplorer>($"SQL/GetServerExplorer/{connectionString}");
```

**NEW:**
```csharp
// Connect first to get session token
var request = new ConnectRequest 
{
    ServerName = server,
    Login = login,
    Password = password,
    IntegratedSecurity = false
};

var response = await http.PostAsJsonAsync("api/Connection/connect", request);
var result = await response.Content.ReadFromJsonAsync<ConnectResponse>();
string sessionToken = result.SessionToken; // Store this!

// Use token for subsequent requests
var explorerRequest = new ObjectExplorerRequest { SessionToken = sessionToken };
var explorerResponse = await http.PostAsJsonAsync("api/Query/object-explorer", explorerRequest);
```

### Query Execution

**OLD:**
```csharp
var table = await http.GetFromJsonAsync<Table>($"SQL/RunQuery/{connectionString}&{query}");
```

**NEW:**
```csharp
var request = new QueryRequest 
{
    SessionToken = sessionToken,
    Query = query
};

var response = await http.PostAsJsonAsync("api/Query/execute", request);
var result = await response.Content.ReadFromJsonAsync<QueryResponse>();

if (result.Success)
{
    var table = result.ResultTable;
    var executionTime = result.ExecutionTimeMs;
    var rowsAffected = result.RowsAffected;
}
```

---

## Service Layer Changes

### IConnectionService Interface

**Removed Properties:**
- `string ConnectionString` - No longer exposed to client

**Added Properties:**
- `string? SessionToken` - Client-side session identifier
- `double LastQueryExecutionTimeMs` - Query performance metric

**Changed Methods:**
```csharp
// OLD
Task<int> RunQuery(string query)

// NEW
Task<(int RowCount, string? ErrorMessage)> RunQuery(string query)
```

**Added Methods:**
- `Task<bool> ConnectAsync()` - Establish connection
- `Task<bool> ChangeDatabaseAsync(string databaseName)` - Switch databases
- `Task DisconnectAsync()` - Clean up session

---

## Data Access Layer Changes

### MSSQL Class

**Added:**
```csharp
public async Task<(DataTable Result, double ExecutionTimeMs, int RowsAffected)> 
    RunQueryAsync(string query)

public async Task<bool> TestConnectionAsync()
```

**Deprecated:**
```csharp
[Obsolete("Use RunQueryAsync instead")]
public DataTable RunQuery(string query)
```

---

## Configuration Changes

### Program.cs (Server)

**Add this registration:**
```csharp
builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
```

---

## Breaking Changes Checklist

If you have custom code, check these:

- [ ] Replace all `GetFromJsonAsync` to SQL endpoints with `PostAsJsonAsync`
- [ ] Update to use session tokens instead of connection strings
- [ ] Handle new response types (ConnectResponse, QueryResponse, etc.)
- [ ] Update error handling for structured error messages
- [ ] Switch from sync to async methods (RunQuery → RunQueryAsync)
- [ ] Delete old SQLController.cs file
- [ ] Remove any code that builds connection strings on client side

---

## Testing Your Migration

### 1. Check Network Tab
Open browser DevTools → Network tab
- ❌ Should NOT see connection strings in any URLs
- ✅ Should see only session tokens in request bodies

### 2. Verify Session Management
```csharp
// After connect
Assert.IsNotNull(ConnectionService.SessionToken);

// After disconnect
await ConnectionService.DisconnectAsync();
Assert.IsNull(ConnectionService.SessionToken);
```

### 3. Test API Directly (Postman/curl)

```bash
# 1. Connect
curl -X POST http://localhost:5000/api/Connection/connect \
  -H "Content-Type: application/json" \
  -d '{
    "serverName": "localhost",
    "login": "sa",
    "password": "password",
    "integratedSecurity": false
  }'

# Response: {"sessionToken":"guid-here","success":true}

# 2. Query with token
curl -X POST http://localhost:5000/api/Query/execute \
  -H "Content-Type: application/json" \
  -d '{
    "sessionToken": "guid-from-above",
    "query": "SELECT * FROM sys.databases"
  }'
```

---

## Rollback Plan (If Needed)

If issues arise, old code is still in git history:

```bash
# View old SQLController
git show HEAD~1:WebSql/Server/Controllers/SQLController.cs

# Revert specific file
git checkout HEAD~1 -- WebSql/Server/Controllers/SQLController.cs
```

However, recommend fixing forward instead of rolling back.

---

## Common Issues & Solutions

### Issue: "Invalid or expired session"
**Cause:** Session token not stored or expired (24hr limit)  
**Solution:** Call `ConnectAsync()` again to get new token

### Issue: "Connection not found"
**Cause:** Session token invalid or server restarted  
**Solution:** Re-authenticate with connection dialog

### Issue: Compilation errors about `required` keyword
**Cause:** .NET 6 doesn't support C# 11 required keyword  
**Solution:** Use default values instead: `= string.Empty`

### Issue: Async methods not awaited
**Cause:** Forgot to await async calls  
**Solution:** Add `await` keyword and make calling method async

---

## Future Enhancements

This migration sets the foundation for:

1. **JWT Authentication** - Replace simple tokens with signed JWTs
2. **Refresh Tokens** - Extend sessions beyond 24 hours
3. **Role-Based Access** - Different permissions per user
4. **Rate Limiting** - Prevent abuse
5. **Connection Pooling** - Better resource management

---

## Questions?

See full details in:
- `Documentation/ImplementationSummary.md`
- `Documentation/ChangeLog.md`
- `Documentation/DevelopmentList1.md`

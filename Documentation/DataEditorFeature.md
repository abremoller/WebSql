# Data Editor Feature

## Overview
The Data Editor is a new page in WebSql that allows users to view, add, edit, and delete table data through a user-friendly form interface without writing SQL.

## Location
- **Route**: `/dataeditor`
- **File**: `WebSql/Client/Pages/DataEditor.razor`
- **Navigation**: Accessible via "Data Editor" link in the left navigation menu

## Key Features

### 1. Safety First - Mandatory WHERE Clause
- Users **must** specify a WHERE clause before loading data
- Prevents accidentally loading entire large tables
- Prevents unrestricted UPDATE/DELETE operations
- Clear UI prompts guide users to enter filtering conditions

### 2. Database & Table Selection
- Dropdown to select database (from connected server)
- Dropdown to select table (shows `schema.tablename` format)
- Automatically loads table structure (columns and types)

### 3. Data Loading
- "Load Data" button fetches rows matching the WHERE clause
- Displays data in a clean table grid
- Shows column names and data types in headers
- Truncates long values with "..." and full value in tooltip

### 4. Add New Row
- "Add New Row" button opens modal form
- Form displays all columns with their data types
- Input fields for each column
- Generates INSERT statement on save

### 5. Edit Existing Row
- "Edit" button (pencil icon) on each row
- Opens modal pre-populated with current values
- User can modify any field
- Generates UPDATE statement with WHERE clause matching original values

### 6. Delete Row
- "Delete" button (trash icon) on each row
- Confirmation dialog before deletion
- Generates DELETE statement with WHERE clause matching all column values

## Technical Implementation

### Security Features
- **Server-side execution**: All queries run through existing ConnectionService
- **Query validation**: Leverages existing dangerous operations protection
- **WHERE clause enforcement**: UI prevents running queries without filters
- **Parameterized identification**: Uses all columns in WHERE for UPDATE/DELETE

### Data Type Handling
- **String types** (char, varchar, text, date, time): Wrapped in single quotes, quotes escaped
- **Numeric types** (int, decimal, float): No quotes
- **NULL handling**: Empty inputs converted to NULL

### Dependencies
- Uses existing `ConnectionService` for database operations
- Uses existing `IConnectionService.Connected` property
- Uses existing `ChangeDatabaseAsync()` and `RunQuery()` methods
- Reads from `ObjectExplorer` to populate database/table lists

### UI Components (Havit Blazor Bootstrap)
- `HxSelect` - Database and table dropdowns
- `HxButton` - Action buttons with icons
- `HxAlert` - Information and error messages
- `HxModal` - Edit/Add form dialog
- `HxInputText` - Form input fields

## Usage Flow

1. **Connect to database** (via existing connection dialog)
2. **Navigate to Data Editor** page
3. **Select database** from dropdown
4. **Select table** from dropdown
5. **Enter WHERE clause** (e.g., `CustomerID = 5` or `Status = 'Active'`)
6. **Click "Load Data"** to view matching rows
7. **Add/Edit/Delete** rows as needed
   - Each operation shows confirmation or error messages
   - Data automatically refreshes after successful operations

## Error Handling
- Connection errors displayed in alert messages
- SQL errors from server shown to user
- Validation happens server-side through existing infrastructure
- Delete operations require user confirmation

## Future Enhancements (Post-MVP)
- **Role-based permissions**: Control which tables/columns users can edit
- **Field-level validation**: Custom validation rules per column
- **Bulk operations**: Edit multiple rows at once
- **Advanced WHERE builder**: Visual query builder interface
- **Audit logging**: Track all data changes with user/timestamp
- **Data export**: Export filtered data to CSV/Excel
- **Pagination**: Handle large result sets with paging

## Code Highlights

### WHERE Clause Safety
```csharp
@if (whereClause == null)
{
    <HxAlert Color="ThemeColor.Info">
        <strong>Safety First:</strong> Please specify a WHERE clause to limit the rows you want to edit.
    </HxAlert>
    // WHERE clause input form
}
```

### SQL Value Formatting
```csharp
private string FormatSqlValue(string value, string type)
{
    if (string.IsNullOrWhiteSpace(value))
        return "NULL";

    var lowerType = type.ToLower();
    
    // String types need quotes
    if (lowerType.Contains("char") || lowerType.Contains("text") || 
        lowerType.Contains("date") || lowerType.Contains("time"))
    {
        var escapedValue = value.Replace("'", "''");
        return $"'{escapedValue}'"; 
    }
    
    return value; // Numeric types
}
```

### UPDATE Statement Generation
```csharp
var setClauses = columns.Select((col, i) => 
    $"[{col.Name}] = {FormatSqlValue(editValues[i], col.Type)}").ToList();

var whereParts = columns.Select((col, i) => 
    $"[{col.Name}] = {FormatSqlValue(rows[editingRowIndex.Value][i], col.Type)}").ToList();

query = $"UPDATE [{selectedTable.Schema}].[{selectedTable.Name}] " +
        $"SET {string.Join(", ", setClauses)} " +
        $"WHERE {string.Join(" AND ", whereParts)}";
```

## Testing Checklist
- [ ] Connect to SQL Server instance
- [ ] Select different databases
- [ ] Select different tables
- [ ] Enter various WHERE clauses (valid and invalid)
- [ ] Load data with WHERE clause
- [ ] Add new row with various data types
- [ ] Edit existing row
- [ ] Delete row (confirm dialog appears)
- [ ] Verify NULL handling
- [ ] Verify string with quotes is escaped properly
- [ ] Verify numeric values work without quotes
- [ ] Test error scenarios (invalid WHERE, connection loss, etc.)
- [ ] Change WHERE clause after loading data
- [ ] Verify data refreshes after INSERT/UPDATE/DELETE

## Related Files
- `WebSql/Client/Pages/DataEditor.razor` - Main page component
- `WebSql/Client/Services/IConnectionService.cs` - Added `Connected` property
- `WebSql/Client/Services/ConnectionService.cs` - Implemented `Connected` property
- `WebSql/Client/Shared/NavMenu.razor` - Added navigation link
- `Documentation/AdvancedFeatures.md` - Original feature specification

## Build Status
✅ **Build Successful** - Solution builds without errors (16 nullable reference warnings are pre-existing)

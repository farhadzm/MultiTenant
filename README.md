# Multi-Tenant API Documentation

## Overview

This project implements a comprehensive Multi-Tenant architecture using Entity Framework Core Query Filters. The system automatically filters data based on the current tenant context, ensuring complete data isolation between different tenants while maintaining a single database schema.

## 🏗️ Architecture Components

### 1. Multi-Tenant Implementation with EF Core Query Filters

The core of this multi-tenant system relies on Entity Framework Core's `HasQueryFilter` functionality. Unlike traditional approaches that require separate databases or complex data partitioning, this implementation uses a single database with automatic query filtering.

#### Key Benefits:
- **Single Database**: All tenants share the same database schema
- **Automatic Filtering**: Data is automatically filtered based on tenant context
- **Performance**: No complex joins or manual filtering required
- **Scalability**: Easy to add new tenants without schema changes

#### Implementation Details:

```csharp
// In ApplicationDbContext.OnModelCreating()
modelBuilder.Entity<Organization>()
    .AddQueryFilter(e => _currentTenant.Id == null || e.TenantId == _currentTenant.Id);

modelBuilder.Entity<Employee>()
    .AddQueryFilter(e => _currentTenant.Id == null || e.Organization.TenantId == _currentTenant.Id);
```

### 2. AddQueryFilter Extension Method

**Problem Solved**: Entity Framework Core doesn't support multiple `HasQueryFilter` calls on the same entity. This limitation prevents combining soft delete filters with tenant-specific filters.

**Solution**: Custom `AddQueryFilter` extension method that combines multiple query filters into a single expression.

```csharp
public static void AddQueryFilter<T>(this EntityTypeBuilder<T> entityTypeBuilder, Expression<Func<T, bool>> expression) where T : class
{
    ParameterExpression parameterExpression = Expression.Parameter(entityTypeBuilder.Metadata.ClrType);
    Expression expression2 = ReplacingExpressionVisitor.Replace(expression.Parameters.Single(), parameterExpression, expression.Body);
    LambdaExpression queryFilter = entityTypeBuilder.Metadata.GetQueryFilter();
    
    if (queryFilter != null)
    {
        expression2 = Expression.AndAlso(
            ReplacingExpressionVisitor.Replace(queryFilter.Parameters.Single(), parameterExpression, queryFilter.Body), 
            expression2);
    }

    LambdaExpression filter = Expression.Lambda(expression2, parameterExpression);
    entityTypeBuilder.HasQueryFilter(filter);
}
```

**How it works**:
1. Takes the new filter expression
2. Combines it with existing filters using `Expression.AndAlso`
3. Applies the combined filter to the entity

### 3. ICurrentTenant Interface and Implementation

The `ICurrentTenant` interface provides a thread-safe way to manage tenant context throughout the application lifecycle.

#### Interface Definition:
```csharp
public interface ICurrentTenant
{
    int? Id { get; }
    IDisposable Change(int? id);
}
```

#### CurrentTenant Implementation:
```csharp
public class CurrentTenant : ICurrentTenant
{
    private readonly AsyncLocal<CurrentTenantInfo> _currentScope = new AsyncLocal<CurrentTenantInfo>();

    public int? Id => _currentScope.Value?.Id;

    public IDisposable Change(int? id)
    {
        var parentScope = _currentScope.Value;
        _currentScope.Value = new CurrentTenantInfo(id);
        
        return new DisposeAction(() =>
        {
            _currentScope.Value = parentScope;
        });
    }
}
```

#### Key Features:
- **Thread-Safe**: Uses `AsyncLocal<T>` for thread-safe tenant context
- **Scoped Changes**: `Change()` method returns `IDisposable` for automatic cleanup
- **Null Support**: Can set tenant to `null` for global operations
- **Nested Scopes**: Supports nested tenant changes with proper restoration

### 4. DisposeAction Class - Critical for Tenant Context Management

The `DisposeAction` class is essential for proper tenant context management and preventing memory leaks.

```csharp
public class DisposeAction : IDisposable
{
    private readonly Action _action;

    public DisposeAction(Action action)
    {
        _action = action;
    }

    public void Dispose()
    {
        _action();
    }
}
```

#### How DisposeAction Works:

1. **Context Preservation**: When `Change()` is called, it stores the current tenant context
2. **Temporary Override**: Sets the new tenant context
3. **Automatic Restoration**: When `Dispose()` is called, it restores the previous context
4. **Memory Safety**: Ensures tenant context is properly cleaned up

#### Example Usage:
```csharp
// Original tenant context is preserved
using (currentTenant.Change(5))
{
    // All queries here will filter by tenant ID 5
    var employees = await context.Employees.ToListAsync();
} // Previous tenant context is automatically restored
```

### 5. TenantMiddleware - Mandatory Request Processing

The `TenantMiddleware` is **mandatory** and must be registered early in the pipeline to ensure all requests have proper tenant context.

```csharp
public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentTenant currentTenant)
    {
        int tenantId = ExtractTenantId(context.Request);

        using (currentTenant.Change(tenantId))
        {
            await _next(context);
        }
    }

    private static int ExtractTenantId(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Tenant-Id", out var tenantId))
            return Convert.ToInt32(tenantId);

        throw new ArgumentException("X-Tenant-Id Not found");
    }
}
```

#### Registration in Program.cs:
```csharp
app.UseMiddleware<TenantMiddleware>(); // Must be before other middleware
```

#### Why It's Mandatory:
- **Request Context**: Every HTTP request must have a tenant context
- **Data Isolation**: Ensures queries are automatically filtered
- **Security**: Prevents cross-tenant data access
- **Consistency**: Maintains tenant context throughout request lifecycle

### 6. CreateEmployeeAsync - Tenant Validation

The `CreateEmployeeAsync` method includes important tenant validation logic:

```csharp
public async Task<Employee> CreateEmployeeAsync(Employee employee)
{
    var organizationExists = await _context.Organizations.AnyAsync(a => a.Id == employee.OrganizationId);

    if (!organizationExists)
        throw new Exception("OrganizationId Not Found");

    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();
    return employee;
}
```

#### Important Tenant Behavior:
- **Automatic Filtering**: The `AnyAsync` query is automatically filtered by the current tenant
- **Data Isolation**: If the current tenant's ID doesn't match the organization's `TenantId`, no data will be returned
- **Security**: Prevents creating employees for organizations belonging to other tenants
- **Validation**: Ensures data integrity within tenant boundaries

### 7. GetAllOrganizationsAsync - Bypassing Tenant Filters

The `GetAllOrganizationsAsync` method demonstrates how to bypass tenant filtering for administrative operations:

```csharp
public async Task<List<Organization>> GetAllOrganizationsAsync()
{
    using (_currentTenant.Change(null))
    {
        return await GetOrganizationsAsync();
    }
}
```

#### How It Works:
1. **Temporary Override**: `Change(null)` temporarily sets tenant context to `null`
2. **Filter Bypass**: Query filters check `_currentTenant.Id == null` and allow all data
3. **Automatic Cleanup**: `using` statement ensures previous tenant context is restored
4. **Global Access**: Returns data from all tenants for administrative purposes

## 🗄️ Database Schema

### Entities

#### Tenant
```csharp
public class Tenant : IBaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}
```

#### Organization
```csharp
public class Organization : IBaseEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public Tenant Tenant { get; set; } = null!;
}
```

#### Employee
```csharp
public class Employee : IBaseEntity
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public Organization Organization { get; set; } = null!;
}
```

### Relationships
- **Tenant** → **Organization**: One-to-Many (Cascade Delete)
- **Organization** → **Employee**: One-to-Many (Cascade Delete)

## 🔧 Configuration

### Connection String
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=127.0.0.1,1433; Initial Catalog=MultiTenantDB;User Id = sa; Password=Admin_123;TrustServerCertificate=true"
  }
}
```

### Service Registration
```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<DataService>();
builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();
```

## 📡 API Endpoints

### Organizations Controller
- `GET /api/organizations` - Get organizations for current tenant
- `GET /api/organizations/all` - Get all organizations (admin only)
- `POST /api/organizations` - Create new organization
- `GET /api/organizations/employees` - Get employees for current tenant
- `POST /api/organizations/employees` - Create new employee

### Request Headers
All requests must include:
```
X-Tenant-Id: {tenantId}
```

## 🚀 Usage Examples

### Creating an Organization
```http
POST /api/organizations
X-Tenant-Id: 1
Content-Type: application/json

{
  "tenantId": 1,
  "name": "Tech Company"
}
```

### Creating an Employee
```http
POST /api/organizations/employees
X-Tenant-Id: 1
Content-Type: application/json

{
  "organizationId": 1,
  "name": "John Doe",
  "code": "EMP001"
}
```

### Getting All Organizations (Admin)
```http
GET /api/organizations/all
X-Tenant-Id: 1
```

## 🔒 Security Considerations

1. **Tenant Isolation**: Data is automatically filtered by tenant context
2. **Header Validation**: Tenant ID is extracted from request headers
3. **Query Filtering**: All database queries are automatically scoped to current tenant
4. **Soft Delete**: Deleted records are filtered out automatically
5. **Context Management**: Proper disposal of tenant context prevents data leaks

## 🎯 Key Benefits

1. **Automatic Data Isolation**: No manual filtering required
2. **Single Database**: Cost-effective and easy to maintain
3. **Thread-Safe**: Proper async context management
4. **Flexible**: Easy to bypass filters when needed
5. **Performance**: Efficient query filtering at database level
6. **Scalable**: Easy to add new tenants and entities

## 🔧 Development Setup

1. **Database**: SQL Server with the provided connection string
2. **Migrations**: Run `dotnet ef database update`
3. **Headers**: Include `X-Tenant-Id` in all API requests
4. **Middleware**: Ensure `TenantMiddleware` is registered early in pipeline

This multi-tenant implementation provides a robust, secure, and scalable solution for managing multi-tenant data with automatic isolation and filtering.

# مستندات کامل پروژه Multi-Tenant با Entity Framework Core

## مقدمه و هدف پروژه

### هدف اصلی
این پروژه با هدف پیاده‌سازی یک معماری **Multi-Tenant** (چند مستأجری) کامل و بهینه طراحی شده است. در این معماری، یک سیستم واحد قادر است داده‌های چندین مستأجر (Tenant) مختلف را به صورت کاملاً مجزا و امن مدیریت کند.

### چرا Multi-Tenant؟
- **صرفه‌جویی در هزینه**: یک سیستم واحد برای چندین مشتری
- **مدیریت آسان**: نگهداری و به‌روزرسانی یک کدبیس
- **مقیاس‌پذیری**: امکان اضافه کردن مستأجران جدید بدون تغییر در کد
- **ایزولاسیون داده**: هر مستأجر فقط به داده‌های خود دسترسی دارد

### چالش‌های موجود
1. **ایزولاسیون داده**: چگونه اطمینان حاصل کنیم که هر مستأجر فقط داده‌های خود را می‌بیند؟
2. **عملکرد**: چگونه بدون تأثیر بر عملکرد، فیلتر کردن داده‌ها را انجام دهیم؟
3. **امنیت**: چگونه از دسترسی غیرمجاز به داده‌های سایر مستأجران جلوگیری کنیم؟
4. **انعطاف**: چگونه امکان دسترسی به همه داده‌ها برای ادمین فراهم کنیم؟

## نیازمندی‌های پروژه

### 1. نیازمندی‌های فنی
- **پایگاه داده واحد**: استفاده از یک دیتابیس برای همه مستأجران
- **فیلتر خودکار**: فیلتر کردن خودکار داده‌ها بر اساس مستأجر فعلی
- **Soft Delete**: امکان حذف منطقی رکوردها
- **Thread Safety**: پشتیبانی از عملیات همزمان
- **Context Management**: مدیریت صحیح context مستأجر

### 2. نیازمندی‌های امنیتی
- **ایزولاسیون کامل**: هیچ مستأجری نباید به داده‌های مستأجر دیگر دسترسی داشته باشد
- **اعتبارسنجی**: بررسی صحت شناسه مستأجر در هر درخواست
- **Context Security**: اطمینان از عدم نشت context بین درخواست‌ها

### 3. نیازمندی‌های عملکردی
- **عملکرد بالا**: فیلتر کردن در سطح دیتابیس نه در کد
- **مقیاس‌پذیری**: امکان اضافه کردن مستأجران جدید بدون تغییر کد
- **انعطاف**: امکان دسترسی به همه داده‌ها در صورت نیاز

## معماری و پیاده‌سازی

### 1. پیاده‌سازی Multi-Tenant با EF Core Query Filters

#### مشکل اصلی
Entity Framework Core به طور پیش‌فرض از چندین `HasQueryFilter` روی یک entity پشتیبانی نمی‌کند. این محدودیت مانع از ترکیب فیلترهای soft delete با فیلترهای tenant-specific می‌شود.

#### راه‌حل: AddQueryFilter Extension Method

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

#### نحوه کارکرد:
1. **دریافت فیلتر جدید**: expression جدید را دریافت می‌کند
2. **ترکیب فیلترها**: با استفاده از `Expression.AndAlso` فیلترها را ترکیب می‌کند
3. **اعمال فیلتر ترکیبی**: فیلتر نهایی را روی entity اعمال می‌کند

#### مزایا:
- **فیلتر خودکار**: همه کوئری‌ها به صورت خودکار فیلتر می‌شوند
- **عملکرد بالا**: فیلتر کردن در سطح دیتابیس
- **امنیت**: جلوگیری از دسترسی غیرمجاز به داده‌ها

### 2. ICurrentTenant Interface و پیاده‌سازی

#### هدف
مدیریت thread-safe context مستأجر در طول چرخه حیات اپلیکیشن.

#### تعریف Interface:
```csharp
public interface ICurrentTenant
{
    int? Id { get; }
    IDisposable Change(int? id);
}
```

#### پیاده‌سازی CurrentTenant:
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

#### ویژگی‌های کلیدی:
- **Thread-Safe**: استفاده از `AsyncLocal<T>` برای thread safety
- **تغییرات Scoped**: متد `Change()` یک `IDisposable` برمی‌گرداند
- **پشتیبانی از Null**: امکان تنظیم tenant به null برای عملیات global
- **Nested Scopes**: پشتیبانی از تغییرات nested با بازگردانی صحیح

### 3. کلاس DisposeAction - حیاتی برای مدیریت Context

#### اهمیت
کلاس `DisposeAction` برای مدیریت صحیح context مستأجر و جلوگیری از memory leak ضروری است.

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

#### نحوه کارکرد DisposeAction:

1. **حفظ Context**: هنگام فراخوانی `Change()`، context فعلی مستأجر ذخیره می‌شود
2. **Override موقت**: context جدید مستأجر تنظیم می‌شود
3. **بازگردانی خودکار**: هنگام فراخوانی `Dispose()`، context قبلی بازگردانده می‌شود
4. **Memory Safety**: اطمینان از پاکسازی صحیح context مستأجر

#### مثال استفاده:
```csharp
// Context اصلی مستأجر حفظ می‌شود
using (currentTenant.Change(5))
{
    // همه کوئری‌ها در اینجا بر اساس tenant ID 5 فیلتر می‌شوند
    var employees = await context.Employees.ToListAsync();
} // Context قبلی مستأجر به صورت خودکار بازگردانده می‌شود
```

### 4. TenantMiddleware - اجباری برای پردازش درخواست‌ها

#### اهمیت
`TenantMiddleware` **اجباری** است و باید در ابتدای pipeline ثبت شود تا همه درخواست‌ها context مستأجر مناسب داشته باشند.

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

#### ثبت در Program.cs:
```csharp
app.UseMiddleware<TenantMiddleware>(); // باید قبل از سایر middleware ها باشد
```

#### چرا اجباری است:
- **Context درخواست**: هر درخواست HTTP باید context مستأجر داشته باشد
- **ایزولاسیون داده**: اطمینان از فیلتر خودکار کوئری‌ها
- **امنیت**: جلوگیری از دسترسی cross-tenant
- **سازگاری**: حفظ context مستأجر در طول چرخه حیات درخواست

### 5. CreateEmployeeAsync - اعتبارسنجی Tenant

متد `CreateEmployeeAsync` شامل منطق مهم اعتبارسنجی tenant است:

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

#### رفتار مهم Tenant:
- **فیلتر خودکار**: کوئری `AnyAsync` به صورت خودکار بر اساس مستأجر فعلی فیلتر می‌شود
- **ایزولاسیون داده**: اگر ID مستأجر فعلی با `TenantId` سازمان مطابقت نداشته باشد، هیچ داده‌ای برگردانده نمی‌شود
- **امنیت**: جلوگیری از ایجاد کارمند برای سازمان‌های متعلق به مستأجران دیگر
- **اعتبارسنجی**: اطمینان از یکپارچگی داده در محدوده مستأجر

### 6. GetAllOrganizationsAsync - دور زدن فیلترهای Tenant

متد `GetAllOrganizationsAsync` نشان می‌دهد چگونه می‌توان فیلترهای tenant را برای عملیات مدیریتی دور زد:

```csharp
public async Task<List<Organization>> GetAllOrganizationsAsync()
{
    using (_currentTenant.Change(null))
    {
        return await GetOrganizationsAsync();
    }
}
```

#### نحوه کارکرد:
1. **Override موقت**: `Change(null)` به صورت موقت context مستأجر را به null تنظیم می‌کند
2. **دور زدن فیلتر**: فیلترهای کوئری `_currentTenant.Id == null` را بررسی کرده و همه داده‌ها را مجاز می‌دانند
3. **پاکسازی خودکار**: دستور `using` اطمینان می‌دهد که context قبلی مستأجر بازگردانده شود
4. **دسترسی Global**: داده‌های همه مستأجران را برای اهداف مدیریتی برمی‌گرداند

## ساختار پایگاه داده

### موجودیت‌ها

#### Tenant (مستأجر)
```csharp
public class Tenant : IBaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}
```

#### Organization (سازمان)
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

#### Employee (کارمند)
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

### روابط
- **Tenant** → **Organization**: یک به چند (حذف آبشاری)
- **Organization** → **Employee**: یک به چند (حذف آبشاری)

## پیکربندی

### Connection String
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=127.0.0.1,1433; Initial Catalog=MultiTenantDB;User Id = sa; Password=Admin_123;TrustServerCertificate=true"
  }
}
```

### ثبت سرویس‌ها
```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<DataService>();
builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();
```

## API Endpoints

### Organizations Controller
- `GET /api/organizations` - دریافت سازمان‌های مستأجر فعلی
- `GET /api/organizations/all` - دریافت همه سازمان‌ها (فقط ادمین)
- `POST /api/organizations` - ایجاد سازمان جدید
- `GET /api/organizations/employees` - دریافت کارمندان مستأجر فعلی
- `POST /api/organizations/employees` - ایجاد کارمند جدید

### هدرهای درخواست
همه درخواست‌ها باید شامل موارد زیر باشند:
```
X-Tenant-Id: {tenantId}
```

## مثال‌های استفاده

### ایجاد یک سازمان
```http
POST /api/organizations
X-Tenant-Id: 1
Content-Type: application/json

{
  "tenantId": 1,
  "name": "شرکت فناوری"
}
```

### ایجاد یک کارمند
```http
POST /api/organizations/employees
X-Tenant-Id: 1
Content-Type: application/json

{
  "organizationId": 1,
  "name": "علی احمدی",
  "code": "EMP001"
}
```

### دریافت همه سازمان‌ها (ادمین)
```http
GET /api/organizations/all
X-Tenant-Id: 1
```

## ملاحظات امنیتی

1. **ایزولاسیون مستأجر**: داده‌ها به صورت خودکار بر اساس context مستأجر فیلتر می‌شوند
2. **اعتبارسنجی هدر**: شناسه مستأجر از هدرهای درخواست استخراج می‌شود
3. **فیلتر کوئری**: همه کوئری‌های دیتابیس به صورت خودکار محدود به مستأجر فعلی می‌شوند
4. **Soft Delete**: رکوردهای حذف شده به صورت خودکار فیلتر می‌شوند
5. **مدیریت Context**: دفع صحیح context مستأجر از نشت داده جلوگیری می‌کند

## مزایای کلیدی

1. **ایزولاسیون خودکار داده**: نیازی به فیلتر دستی نیست
2. **پایگاه داده واحد**: مقرون به صرفه و آسان برای نگهداری
3. **Thread-Safe**: مدیریت صحیح context async
4. **انعطاف**: امکان دور زدن فیلترها در صورت نیاز
5. **عملکرد**: فیلتر کردن کارآمد در سطح دیتابیس
6. **مقیاس‌پذیری**: آسان برای اضافه کردن مستأجران و موجودیت‌های جدید

## راه‌اندازی توسعه

1. **پایگاه داده**: SQL Server با connection string ارائه شده
2. **Migration**: اجرای `dotnet ef database update`
3. **هدرها**: شامل کردن `X-Tenant-Id` در همه درخواست‌های API
4. **Middleware**: اطمینان از ثبت `TenantMiddleware` در ابتدای pipeline

## نتیجه‌گیری

این پیاده‌سازی Multi-Tenant راه‌حلی قوی، امن و مقیاس‌پذیر برای مدیریت داده‌های چند مستأجری با ایزولاسیون خودکار و فیلتر کردن ارائه می‌دهد. استفاده از Entity Framework Core Query Filters امکان فیلتر کردن خودکار در سطح دیتابیس را فراهم می‌کند که منجر به عملکرد بالا و امنیت بهتر می‌شود.

### نکات مهم:
- **TenantMiddleware اجباری است** و باید در ابتدای pipeline ثبت شود
- **Context Management** با `DisposeAction` برای جلوگیری از memory leak ضروری است
- **Query Filters** به صورت خودکار همه کوئری‌ها را فیلتر می‌کنند
- **Security** از طریق ایزولاسیون کامل داده‌ها تضمین می‌شود

این معماری امکان توسعه و نگهداری آسان سیستم‌های چند مستأجری را فراهم می‌کند و در عین حال امنیت و عملکرد بالایی را تضمین می‌کند.

using MultiTenant.Api.Contracts;

namespace MultiTenant.Api.Middlewares;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenant currentTenant)
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

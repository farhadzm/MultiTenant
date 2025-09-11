using MultiTenant.Api.Contracts;
using MultiTenant.Api.Models;

namespace MultiTenant.Api.Services;

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

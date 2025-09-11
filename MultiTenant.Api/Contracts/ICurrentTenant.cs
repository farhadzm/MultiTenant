namespace MultiTenant.Api.Contracts;

public interface ICurrentTenant
{
    int? Id { get; }

    IDisposable Change(int? id);
}

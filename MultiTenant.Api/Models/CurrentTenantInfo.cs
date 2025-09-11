namespace MultiTenant.Api.Models;

public class CurrentTenantInfo
{
    public int? Id { get; }

    public CurrentTenantInfo(int? id)
    {
        Id = id;
    }

}

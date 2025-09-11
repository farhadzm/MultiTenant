namespace MultiTenant.Api.Entities;

public class Organization : IBaseEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDeleted { get; set; } 
    public Tenant Tenant { get; set; } = null!;
}

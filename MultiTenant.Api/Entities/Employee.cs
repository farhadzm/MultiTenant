namespace MultiTenant.Api.Entities;

public class Employee : IBaseEntity
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }

    public Organization Organization { get; set; } = null!;
}

namespace MultiTenant.Api.Entities;

public interface IBaseEntity
{
    bool IsDeleted { get; set; }
}

using Microsoft.EntityFrameworkCore;
using MultiTenant.Api.Contracts;
using MultiTenant.Api.Entities;
using MultiTenant.Api.Extension;

namespace MultiTenant.Api.Data;

public class ApplicationDbContext : DbContext
{
    private readonly ICurrentTenant _currentTenant;
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentTenant currentTenant) : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<Employee> Employees { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Organization>()
            .HasOne(o => o.Tenant)
            .WithMany()
            .HasForeignKey(o => o.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Organization)
            .WithMany()
            .HasForeignKey(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);


        GlobalFiltersExtension.ApplySoftDeleteQueryFilters(modelBuilder);

        modelBuilder.Entity<Organization>()
            .AddQueryFilter(e => _currentTenant.Id == null || e.TenantId == _currentTenant.Id);

        modelBuilder.Entity<Employee>()
            .AddQueryFilter(e => _currentTenant.Id == null || e.Organization.TenantId == _currentTenant.Id);
    }
}

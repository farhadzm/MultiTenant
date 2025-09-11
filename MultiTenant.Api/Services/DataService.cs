using Microsoft.EntityFrameworkCore;
using MultiTenant.Api.Contracts;
using MultiTenant.Api.Data;
using MultiTenant.Api.Entities;

namespace MultiTenant.Api.Services;

public class DataService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    public DataService(
        ApplicationDbContext context, 
        ICurrentTenant currentTenant)
    {
        _context = context;
        _currentTenant = currentTenant;
    }

    public async Task<List<Employee>> GetEmployeesAsync()
    {
        return await _context.Employees
            .ToListAsync();
    }

    public async Task<List<Organization>> GetOrganizationsAsync()
    {
        return await _context.Organizations
            .ToListAsync();
    }

    public async Task<Employee> CreateEmployeeAsync(Employee employee)
    {
        var organizationExists = await _context.Organizations.AnyAsync(a => a.Id == employee.OrganizationId);

        if (!organizationExists)
            throw new Exception("OrganizationId Not Found");

        _context.Employees.Add(employee);

        await _context.SaveChangesAsync();

        return employee;
    }

    public async Task<Organization> CreateOrganizationAsync(Organization organization)
    {
        _context.Organizations.Add(organization);

        await _context.SaveChangesAsync();

        return organization;
    }

    public async Task<List<Organization>> GetAllOrganizationsAsync()
    {
        using (_currentTenant.Change(null))
        {
            return await GetOrganizationsAsync();
        }
    }

    public async Task<List<Employee>> GetEmployeesAsync(int tenantId)
    {
        using (_currentTenant.Change(tenantId))
        {
            return await _context
                .Employees
                .ToListAsync();
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using MultiTenant.Api.Entities;
using MultiTenant.Api.Services;

namespace MultiTenant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrganizationsController : ControllerBase
{
    private readonly DataService _dataService;

    public OrganizationsController(DataService dataService)
    {
        _dataService = dataService;
    }

    [HttpGet]
    public async Task<IActionResult> GetOrganizations()
    {
        var organizations = await _dataService.GetOrganizationsAsync();

        return Ok(organizations);
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllOrganizations()
    {
        var organizations = await _dataService.GetAllOrganizationsAsync();

        return Ok(organizations);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrganization(Organization organization)
    {
        var createdOrganization = await _dataService.CreateOrganizationAsync(organization);

        return Ok(createdOrganization);
    }

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees()
    {
        var employees = await _dataService.GetEmployeesAsync();

        return Ok(employees);
    }

    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee(Employee employee)
    {
        var createdEmployee = await _dataService.CreateEmployeeAsync(employee);

        return Ok(createdEmployee);
    }
}

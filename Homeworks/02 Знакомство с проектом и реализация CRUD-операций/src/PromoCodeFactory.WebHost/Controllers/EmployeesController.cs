using Microsoft.AspNetCore.Mvc;
using PromoCodeFactory.WebHost.Mapping;
using PromoCodeFactory.WebHost.Models;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace PromoCodeFactory.WebHost.Controllers;

/// <summary>
/// Сотрудники
/// </summary>
public class EmployeesController(
    IRepository<Employee> employeeRepository,
    IRepository<Role> roleRepository
    ) : BaseController
{
    /// <summary>
    /// Получить данные всех сотрудников
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EmployeeShortResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeShortResponse>>> Get(CancellationToken ct)
    {
        var employees = await employeeRepository.GetAll(ct);

        var employeesModels = employees.Select(Mapper.ToEmployeeShortResponse).ToList();

        return Ok(employeesModels);
    }

    /// <summary>
    /// Получить данные сотрудника по Id
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeResponse>> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var employee = await employeeRepository.GetById(id, ct);
        return employee is null ? NotFound() : Ok(Mapper.ToEmployeeShortResponse(employee));
    }

    /// <summary>
    /// Создать сотрудника
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeResponse>> Create([FromBody] EmployeeCreateRequest request, CancellationToken ct)
    {
        var roleId = await roleRepository.GetById(request.RoleId, ct);
        if (roleId is null) return BadRequest();

        var employee = new EmployeeResponse(
            Id: Guid.NewGuid(),
            FullName: request.FirstName + " " + request.LastName,
            Email: request.Email,
            Role: Mapper.ToRoleResponse(roleId),
            AppliedPromocodesCount: 0
        );
        await employeeRepository.Add(Mapper.ToEmployee(request, roleId), ct);
        return Ok(employee);
    }

    /// <summary>
    /// Обновить сотрудника
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeResponse>> Update(
        [FromRoute] Guid id,
        [FromBody] EmployeeUpdateRequest request,
        CancellationToken ct)
    {
        var employee = await employeeRepository.GetById(id, ct);
        if (employee is null) return NotFound();

        var roleId = await roleRepository.GetById(request.RoleId, ct);
        if (roleId is null) return BadRequest();

        employee.FirstName = request.FirstName;
        employee.LastName = request.LastName;
        employee.Email = request.Email;
        employee.Role = roleId;

        try
        {
            await employeeRepository.Update(employee, ct);
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
        var response = Mapper.ToEmployeeResponse(employee);
        return Ok(response);
    }

    /// <summary>
    /// Удалить сотрудника
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var employee = await employeeRepository.GetById(id, ct);
        if (employee is null) return NotFound();

        try
        {
            await employeeRepository.Delete(id, ct);
        } catch(EntityNotFoundException)
        {
            return NotFound();
        }
        return NoContent();
    }
}

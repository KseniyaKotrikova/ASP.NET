using Microsoft.AspNetCore.Mvc;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;
using PromoCodeFactory.WebHost.Mapping;
using PromoCodeFactory.WebHost.Models.Customers;
using PromoCodeFactory.WebHost.Models.Preferences;
using PromoCodeFactory.WebHost.Models.PromoCodes;

namespace PromoCodeFactory.WebHost.Controllers;

/// <summary>
/// Клиенты
/// </summary>
public class CustomersController(IRepository<Customer> customerRepository,
    IRepository<PromoCode> promoCodeRepository,
    IRepository<Preference> preferenceRepository) : BaseController
{

    /// <summary>
    /// Получить данные всех клиентов
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CustomerShortResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CustomerShortResponse>>> Get(CancellationToken ct)
    {
        var customers = await customerRepository.GetAll(withIncludes: true, ct);

        var response = customers.Select(CustomersMapper.ToCustomerShortResponse).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Получить данные клиента по Id
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> GetById(Guid id, CancellationToken ct)
    {
        // Здесь обязательно withIncludes: true для получения Preferences и PromoCodes
        var customer = await customerRepository.GetById(id, withIncludes: true, ct);

        if (customer == null)
            return NotFound();

        // Собираем все ID промокодов, которые есть у клиента
        var promoIds = customer.CustomerPromoCodes.Select(x => x.PromoCodeId).ToList();

        // Загружаем сами промокоды одним запросом через GetByRangeId
        var promoCodes = await promoCodeRepository.GetByRangeId(promoIds, withIncludes: false, ct);

        var promoCodeResponses = customer.CustomerPromoCodes.Select(cp => {
            // Находим данные промокода в загруженном списке
            var promo = promoCodes.FirstOrDefault(p => p.Id == cp.PromoCodeId);

            return new CustomerPromoCodeResponse(
                cp.PromoCodeId,
                promo?.Code ?? "N/A",
                promo?.ServiceInfo ?? "",
                promo?.PartnerName ?? "",
                promo?.BeginDate ?? DateTimeOffset.MinValue,
                promo?.EndDate ?? DateTimeOffset.MaxValue,
                promo?.PartnerManager?.Id ?? Guid.Empty, // Менеджер
                promo?.Preference?.Id ?? Guid.Empty,      // Предпочтение
                cp.CreatedAt,
                cp.AppliedAt
            );
        }).ToList();

        var response = new CustomerResponse(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.Preferences.Select(p => new PreferenceShortResponse(p.Id, p.Name)).ToList(),
            promoCodeResponses
        );

        return Ok(response);
    }

    /// <summary>
    /// Создать клиента
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerShortResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerShortResponse>> Create([FromBody] CustomerCreateRequest request, CancellationToken ct)
    {
        // 1. Получаем реальные объекты предпочтений из базы по списку ID
        var preferences = await preferenceRepository.GetByRangeId(request.PreferenceIds, ct: ct);

        // 2. Создаем сущность
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Preferences = preferences.ToList(),
            CustomerPromoCodes = new List<CustomerPromoCode>()
        };

        // 3. Сохраняем
        await customerRepository.Add(customer, ct);

        var response = CustomersMapper.ToCustomerShortResponse(customer);
        return StatusCode(201, response);
    }

    /// <summary>
    /// Обновить клиента
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerShortResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerShortResponse>> Update(
        [FromRoute] Guid id,
        [FromBody] CustomerUpdateRequest request,
        CancellationToken ct)
    {
        // Загружаем клиента вместе с текущими предпочтениями
        var customer = await customerRepository.GetById(id, withIncludes: true, ct);
        if (customer == null) return NotFound();

        // Обновляем простые поля
        customer.FirstName = request.FirstName;
        customer.LastName = request.LastName;
        customer.Email = request.Email;

        // Обновляем связи: загружаем новые предпочтения
        var newPreferences = await preferenceRepository.GetByRangeId(request.PreferenceIds, ct: ct);

        customer.Preferences.Clear(); // Очищаем старые

        foreach (var preference in newPreferences)
        {
            customer.Preferences.Add(preference); // Добавляем новые
        }

        await customerRepository.Update(customer, ct);

        var response = CustomersMapper.ToCustomerShortResponse(customer);
        return StatusCode(200, response);
    }

    /// <summary>
    /// Удалить клиента
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var customer = await customerRepository.GetById(id, ct: ct);
        if (customer == null) return NotFound();

        await customerRepository.Delete(id, ct);

        return NoContent();
    }
}

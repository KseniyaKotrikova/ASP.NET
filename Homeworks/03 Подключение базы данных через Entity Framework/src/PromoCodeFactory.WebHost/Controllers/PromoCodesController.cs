using Microsoft.AspNetCore.Mvc;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;
using PromoCodeFactory.WebHost.Models.PromoCodes;

namespace PromoCodeFactory.WebHost.Controllers;

/// <summary>
/// Промокоды
/// </summary>
public class PromoCodesController(IRepository<PromoCode> promoCodeRepository,
    IRepository<Customer> customerRepository,
    IRepository<Preference> preferenceRepository,
    IRepository<Employee> employeeRepository) : BaseController
{

    /// <summary>
    /// Получить все промокоды
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PromoCodeShortResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PromoCodeShortResponse>>> Get(CancellationToken ct)
    {
        // Получаем все промокоды (для краткого списка обычно includes не нужны)
        var promoCodes = await promoCodeRepository.GetAll(withIncludes: true, ct);

        var response = promoCodes.Select(x => new PromoCodeShortResponse(
            x.Id,
            x.Code,
            x.ServiceInfo,
            x.PartnerName,
            x.BeginDate,
            x.EndDate,
            x.PartnerManager.Id,
            x.Preference.Id
        )).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Получить промокод по id
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PromoCodeShortResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromoCodeShortResponse>> GetById(Guid id, CancellationToken ct)
    {
        var promoCode = await promoCodeRepository.GetById(id, withIncludes: true, ct);

        if (promoCode == null)
            return NotFound();

        var response = new PromoCodeShortResponse(
            promoCode.Id,
            promoCode.Code,
            promoCode.ServiceInfo,
            promoCode.PartnerName,
            promoCode.BeginDate,
            promoCode.EndDate,
            promoCode.PartnerManager.Id,
            promoCode.Preference.Id
        );

        return Ok(response);
    }

    /// <summary>
    /// Создать промокод и выдать его клиентам с указанным предпочтением
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PromoCodeShortResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(PromoCodeCreateRequest request, CancellationToken ct)
    {
    // 1. Получаем предпочтение, для которого создается промокод
        var preference = await preferenceRepository.GetById(request.PreferenceId, ct: ct);
        if (preference == null) return BadRequest("Предпочтение не найдено");

        var partnerManager = await employeeRepository.GetById(request.PartnerManagerId, ct: ct);
        if (partnerManager == null)
            return BadRequest("Сотрудник (менеджер) не найден");

        // 3. Находим клиентов с этим предпочтением
        var customers = await customerRepository.GetWhere(
            c => c.Preferences.Any(p => p.Id == request.PreferenceId),
            withIncludes: true, // Нужно, чтобы обновить их коллекцию PromoCodes
            ct: ct
        );

        // 3. Создаем новую сущность PromoCode
        var promoCode = new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            ServiceInfo = request.ServiceInfo,
            PartnerName = request.PartnerName,
            BeginDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddMonths(1), // Пример срока действия
            Preference = preference,
            PartnerManager = partnerManager, // Поле обязательно по модели
            CustomerPromoCodes = new List<CustomerPromoCode>()
        };

        // 4. Раздаем промокод клиентам
        foreach (var customer in customers)
        {
            promoCode.CustomerPromoCodes.Add(new CustomerPromoCode
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                PromoCodeId = promoCode.Id,
                CreatedAt = promoCode.BeginDate
            });
        }

        // 5. Сохраняем промокод.
        await promoCodeRepository.Add(promoCode, ct);

        return CreatedAtAction(nameof(GetById), new { id = promoCode.Id }, null);
    }


    /// <summary>
    /// Применить промокод (отметить, что клиент использовал промокод)
    /// </summary>
    [HttpPost("{id:guid}/apply")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Apply(
        [FromRoute] Guid id,
        [FromBody] PromoCodeApplyRequest request,
        CancellationToken ct)
    {
        // 1. Находим клиента и загружаем его коллекцию промокодов
        var customer = await customerRepository.GetById(request.CustomerId, withIncludes: true, ct);

        if (customer == null)
            return NotFound(); // Возвращает 404 согласно атрибуту

        // 2. Ищем в коллекции клиента запись о конкретном промокоде
        var customerPromo = customer.CustomerPromoCodes
            .FirstOrDefault(x => x.PromoCodeId == id);

        if (customerPromo == null)
            return BadRequest(); // У клиента нет такого промокода

        if (customerPromo.AppliedAt.HasValue)
            return BadRequest(); // Промокод уже применен

        // 3. Устанавливаем дату использования
        customerPromo.AppliedAt = DateTimeOffset.UtcNow;

        // 4. Сохраняем изменения в базе через репозиторий клиента
        await customerRepository.Update(customer, ct);

        // 5. Возвращаем 204 согласно атрибуту ProducesResponseType
        return NoContent();
    }
}

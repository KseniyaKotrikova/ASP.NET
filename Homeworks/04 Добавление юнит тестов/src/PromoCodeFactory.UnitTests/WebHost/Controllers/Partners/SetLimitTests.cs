using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PromoCodeFactory.Core.Abstractions.Repositories;
using PromoCodeFactory.Core.Domain.Administration;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;
using PromoCodeFactory.Core.Exceptions;
using PromoCodeFactory.WebHost.Controllers;
using PromoCodeFactory.WebHost.Models.Partners;
using Soenneker.Utils.AutoBogus;

namespace PromoCodeFactory.UnitTests.WebHost.Controllers.Partners;

public class SetLimitTests
{
    private readonly Mock<IRepository<Partner>> _partnersRepositoryMock;
    private readonly Mock<IRepository<PartnerPromoCodeLimit>> _partnerLimitsRepositoryMock;
    //private readonly Mock<IRepository<PartnersLi>>
    private readonly PartnersController _sut;
    public SetLimitTests()
    {
        _partnersRepositoryMock = new Mock<IRepository<Partner>>();
        _partnerLimitsRepositoryMock = new Mock<IRepository<PartnerPromoCodeLimit>>();
        _sut = new PartnersController(_partnersRepositoryMock.Object, _partnerLimitsRepositoryMock.Object);
    }

    private PartnerPromoCodeLimitCreateRequest CreateValidRequest() =>
        new AutoFaker<PartnerPromoCodeLimitCreateRequest>()
            .RuleFor(x => x.EndAt, f => f.Date.FutureOffset()) // Гарантируем валидную дату
            .Generate();

    [Fact]
    public async Task CreateLimit_WhenPartnerNotFound_ReturnsNotFound()
    {
        // Arrange
        var partnerId = Guid.NewGuid();
        _partnersRepositoryMock.Setup(r => r.GetById(partnerId))
            .ReturnsAsync((Partner)null);

        // Act
        var result = await _sut.CreateLimit(partnerId, CreateValidRequest(), CancellationToken.None);

        // Assert
        var actionResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject; //404
        var details = actionResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        details.Title.Should().Be("Partner not found");
    }

    [Fact]
    public async Task CreateLimit_WhenPartnerBlocked_ReturnsUnprocessableEntity()
    {
        // Arrange
        var partnerId = Guid.NewGuid();
        var partner = new AutoFaker<Partner>()
            .RuleFor(x => x.Id, partnerId)
            .RuleFor(x => x.IsActive, false) // Блокируем партнера
            .Generate();

        var request = CreateValidRequest();

        _partnersRepositoryMock
            .Setup(r => r.GetById(partnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        // Act
        var result = await _sut.CreateLimit(partnerId, request, CancellationToken.None);

        // Assert
        // 1. Проверяем тип результата (422 с объектом)
        var actionResult = result.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;

        // 2. Проверяем, что внутри именно ProblemDetails
        var details = actionResult.Value.Should().BeOfType<ProblemDetails>().Subject;

        // 3. Проверяем поля
        details.Title.Should().Be("Partner blocked");
        details.Detail.Should().Be("Cannot create limit for a blocked partner.");
    }

    [Fact]
    public async Task CreateLimit_WhenValidRequest_ReturnsCreatedAndAddsLimit()
    {
        // Arrange
        var partnerId = Guid.NewGuid();
        var partner = new AutoFaker<Partner>()
            .RuleFor(x => x.Id, partnerId)
            .RuleFor(x => x.IsActive, true)
            .RuleFor(x => x.PartnerLimits, new List<PartnerPromoCodeLimit>())
            .Generate();

        var request = CreateValidRequest();

        _partnersRepositoryMock
            .Setup(r => r.GetById(partnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        // Act
        var result = await _sut.CreateLimit(partnerId, request, CancellationToken.None);

        // Assert
        // Проверяем, что вернулся статус 201
        result.Result.Should().BeOfType<CreatedAtActionResult>();

        // ГЛАВНОЕ: Проверяем, что репозиторий лимитов получил команду Add с правильными данными
        _partnerLimitsRepositoryMock.Verify(r => r.Add(
                It.Is<PartnerPromoCodeLimit>(l =>
                    l.Limit == request.Limit &&
                    l.Partner == partner &&
                    l.IssuedCount == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateLimit_WhenValidRequestWithActiveLimits_CancelsOldLimitsAndAddsNew()
    {
        // Arrange
        var partnerId = Guid.NewGuid();
        // Сначала создаем партнера, чтобы передать его в лимит
        var partner = new Partner
        {
            Id = partnerId,
            Name = "Test Partner",
            IsActive = true,
            Manager = new AutoFaker<Employee>().Generate(),
            PartnerLimits = new List<PartnerPromoCodeLimit>()
        };

        // Теперь создаем старый лимит и привязываем его к партнеру
        var oldLimit = new PartnerPromoCodeLimit
        {
            Id = Guid.NewGuid(),
            Partner = partner,
            Limit = 100,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
            EndAt = DateTimeOffset.UtcNow.AddDays(-1),
            CanceledAt = null
        };

        // Добавляем лимит в коллекцию партнера
        partner.PartnerLimits.Add(oldLimit);

        var request = CreateValidRequest();

        _partnersRepositoryMock
            .Setup(r => r.GetById(partnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        // Act
        await _sut.CreateLimit(partnerId, request, CancellationToken.None);

        // Assert
        // 1. Проверяем, что у старого лимита заполнилась дата отмены
        oldLimit.CanceledAt.Should().NotBeNull();
       // oldLimit.CanceledAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));

        // 2. Проверяем, что был вызван Update для партнера (чтобы сохранить отмену лимитов)
        _partnersRepositoryMock.Verify(r => r.Update(
                It.Is<Partner>(p => p.Id == partnerId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // 3. Проверяем, что новый лимит всё равно был добавлен
        _partnerLimitsRepositoryMock.Verify(r => r.Add(
                It.Is<PartnerPromoCodeLimit>(l => l.Limit == request.Limit),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateLimit_WhenUpdateThrowsEntityNotFoundException_ReturnsNotFound()
    {
        // Arrange
        var partnerId = Guid.NewGuid();

        // Создаем активный лимит, чтобы контроллер зашел в блок IF и вызвал Update
        var activeLimit = new PartnerPromoCodeLimit
        {
            Partner = null!, // В данном тесте ссылка не критична для логики Exception
            Limit = 10,
            CreatedAt = DateTimeOffset.UtcNow,
            EndAt = DateTimeOffset.UtcNow.AddDays(1),
            CanceledAt = null
        };

        var partner = new Partner
        {
            Id = partnerId,
            Name = "Test Partner",
            IsActive = true,
            Manager = new AutoFaker<Employee>().Generate(),
            PartnerLimits = new List<PartnerPromoCodeLimit> { activeLimit }
        };

        _partnersRepositoryMock
            .Setup(r => r.GetById(partnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        // Настраиваем мок так, чтобы при вызове Update вылетало исключение
        _partnersRepositoryMock
            .Setup(r => r.Update(partner, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException(typeof(Partner), partner.Id));

        // Act
        var result = await _sut.CreateLimit(partnerId, CreateValidRequest(), CancellationToken.None);

        // Assert
        // В вашем коде в блоке catch возвращается просто return NotFound(); без ProblemDetails
        result.Result.Should().BeOfType<NotFoundResult>();
    }
}

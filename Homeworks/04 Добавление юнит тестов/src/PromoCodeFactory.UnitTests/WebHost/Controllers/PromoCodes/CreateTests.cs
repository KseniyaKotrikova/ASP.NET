using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PromoCodeFactory.Core.Abstractions.Repositories;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;
using PromoCodeFactory.WebHost.Controllers;
using PromoCodeFactory.WebHost.Models.PromoCodes;
using Soenneker.Utils.AutoBogus;
using Xunit;

namespace PromoCodeFactory.UnitTests.WebHost.Controllers.PromoCodes;

public class CreateTests
{
    private readonly Mock<IRepository<PromoCode>> _promoCodesRepositoryMock;
    private readonly Mock<IRepository<Customer>> _customersRepositoryMock;
    private readonly Mock<IRepository<CustomerPromoCode>> _customerPromoCodesRepositoryMock;
    private readonly Mock<IRepository<Partner>> _partnersRepositoryMock;
    private readonly Mock<IRepository<Preference>> _preferencesRepositoryMock;
    private readonly PromoCodesController _sut;

    public CreateTests()
    {
        _promoCodesRepositoryMock = new Mock<IRepository<PromoCode>>();
        _customersRepositoryMock = new Mock<IRepository<Customer>>();
        _customerPromoCodesRepositoryMock = new Mock<IRepository<CustomerPromoCode>>();
        _partnersRepositoryMock = new Mock<IRepository<Partner>>();
        _preferencesRepositoryMock = new Mock<IRepository<Preference>>();

        _sut = new PromoCodesController(
            _promoCodesRepositoryMock.Object,
            _customersRepositoryMock.Object,
            _customerPromoCodesRepositoryMock.Object,
            _partnersRepositoryMock.Object,
            _preferencesRepositoryMock.Object);
    }

    [Fact]
    public async Task Create_WhenLimitExceeded_ReturnsUnprocessableEntity()
    {
        // Arrange
        var request = new AutoFaker<PromoCodeCreateRequest>().Generate();
        var partner = new Partner { Id = request.PartnerId, Name = "Test", IsActive = true, Manager = null! };

        var activeLimit = new PartnerPromoCodeLimit
        {
            Partner = partner, // Required
            Limit = 10,
            IssuedCount = 10, // Лимит исчерпан
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            EndAt = DateTimeOffset.UtcNow.AddDays(1), // Важно: EndAt в будущем
            CanceledAt = null
        };
        partner.PartnerLimits = new List<PartnerPromoCodeLimit> { activeLimit };

        _partnersRepositoryMock.Setup(r => r.GetById(request.PartnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);
        _preferencesRepositoryMock
            .Setup(r => r.GetById(request.PreferenceId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Preference
            {
                Id = request.PreferenceId,
                Name = "Test Preference" // Указываем обязательное имя
            });

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        var actionResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        actionResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        var details = actionResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        details.Title.Should().Be("Limit exceeded");
    }

    [Fact]
    public async Task Create_WhenValidRequest_ReturnsCreatedAndIncrementsIssuedCount()
    {
        // Arrange
        var request = new AutoFaker<PromoCodeCreateRequest>().Generate();
        var partner = new Partner { Id = request.PartnerId, Name = "Test", IsActive = true, Manager = null! };

        var activeLimit = new PartnerPromoCodeLimit
        {
            Partner = partner,
            Limit = 10,
            IssuedCount = 0,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            EndAt = DateTimeOffset.UtcNow.AddDays(1),
            CanceledAt = null
        };
        partner.PartnerLimits = new List<PartnerPromoCodeLimit> { activeLimit };

        _partnersRepositoryMock.Setup(r => r.GetById(request.PartnerId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);
        _preferencesRepositoryMock.Setup(r => r.GetById(request.PreferenceId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Preference
            {
                Id = request.PreferenceId,
                Name = "Test Preference" // Указываем обязательное имя
            });
        _customersRepositoryMock.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Customer, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Customer>());

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        activeLimit.IssuedCount.Should().Be(1); // Счетчик увеличился

        _partnersRepositoryMock.Verify(r => r.Update(partner, It.IsAny<CancellationToken>()), Times.Once);
        _promoCodesRepositoryMock.Verify(r => r.Add(It.IsAny<PromoCode>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

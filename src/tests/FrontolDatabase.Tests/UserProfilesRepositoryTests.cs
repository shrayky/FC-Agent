using Domain.Frontol.Dto;
using Domain.Frontol.Interfaces;
using FrontolDatabase.Entitys;
using FrontolDatabase.Repositories;
using FrontolDatabase.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class UserProfilesRepositoryTests
{
    private UserProfilesRepository _repository;
    private MainDbCtx _dbContext;

    [SetUp]
    public void SetUp()
    {
        // Создаем InMemory базу данных для MainDbCtx
        var options = new DbContextOptionsBuilder<MainDbCtx>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _dbContext = new MainDbCtx(options);
        
        var loggerMock = new Mock<ILogger<UserProfilesRepository>>();
        var defaultSecurityServiceLoggerMock = new Mock<ILogger<UserProfileDefaultSecurityService>>();
        
        // Создаем реальный экземпляр UserProfileDefaultSecurityService с моком логгера
        // Для тестирования SecuritiesToUpdate этот сервис не используется
        var defaultSecurityService = new UserProfileDefaultSecurityService(defaultSecurityServiceLoggerMock.Object);
        
        var mainDbMock = new Mock<IFrontolMainDb>();
        
        _repository = new UserProfilesRepository(
            loggerMock.Object,
            _dbContext,
            defaultSecurityService,
            mainDbMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext?.Dispose();
    }

    [Test]
    public void SecuritiesToUpdate_WhenValueChangedFrom0To1_ReturnsSecurity()
    {
        // Arrange
        var profileSecurities = new List<UserProfileSecurity>
        {
            new() { Id = 1, Value = 1 }
        };
        var existSecurities = new List<Security>
        {
            new() { SecurityCode = 1, Value = 0 }
        };
        
        // Act
        var result = _repository.SecuritiesToUpdate(profileSecurities, existSecurities);
        
        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].SecurityCode, Is.EqualTo(1));
    }

    [Test]
    public void SecuritiesToUpdate_WhenValueChangedFrom1To0_ReturnsSecurity()
    {
        // Arrange
        var profileSecurities = new List<UserProfileSecurity>
        {
            new() { Id = 1, Value = 0 }
        };
        var existSecurities = new List<Security>
        {
            new() { SecurityCode = 1, Value = 1 }
        };
        
        // Act
        var result = _repository.SecuritiesToUpdate(profileSecurities, existSecurities);
        
        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].SecurityCode, Is.EqualTo(1));
    }

    [Test]
    public void SecuritiesToUpdate_WhenValueNotChanged_ReturnsEmpty()
    {
        // Arrange
        var profileSecurities = new List<UserProfileSecurity>
        {
            new() { Id = 1, Value = 0 },
            new() { Id = 2, Value = 1 }
        };
        var existSecurities = new List<Security>
        {
            new() { SecurityCode = 1, Value = 0 },
            new() { SecurityCode = 2, Value = 1 }
        };
        
        // Act
        var result = _repository.SecuritiesToUpdate(profileSecurities, existSecurities);
        
        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void SecuritiesToUpdate_WhenValueExistsInNewButNotInDatabase_ReturnsOnlyMatching()
    {
        // Arrange: значение есть в новом профиле, но отсутствует в базе
        var profileSecurities = new List<UserProfileSecurity>
        {
            new() { Id = 1, Value = 1 },
            new() { Id = 2, Value = 0 } // этого нет в базе
        };
        var existSecurities = new List<Security>
        {
            new() { SecurityCode = 1, Value = 0 }
        };
        
        // Act
        var result = _repository.SecuritiesToUpdate(profileSecurities, existSecurities);
        
        // Assert: метод не должен возвращать записи, которых нет в базе
        // (они обрабатываются отдельно как newSecuritiesToAdd)
        Assert.That(result, Has.Count.EqualTo(1)); // только SecurityCode=1, который есть в обоих списках
        Assert.That(result[0].SecurityCode, Is.EqualTo(1));
    }

    [Test]
    public void SecuritiesToUpdate_WhenValueExistsInDatabaseButNotInNew_ReturnsOnlyMatching()
    {
        // Arrange: значение есть в базе, но отсутствует в новом профиле
        var profileSecurities = new List<UserProfileSecurity>
        {
            new() { Id = 1, Value = 1 }
        };
        var existSecurities = new List<Security>
        {
            new() { SecurityCode = 1, Value = 0 },
            new() { SecurityCode = 2, Value = 1 } // этого нет в новом профиле
        };
        
        // Act
        var result = _repository.SecuritiesToUpdate(profileSecurities, existSecurities);
        
        // Assert: метод должен возвращать записи, которых нет в новом профиле со значением 0
        // (Join делает внутреннее соединение, поэтому SecurityCode=2 не попадет в результат)
        Assert.That(result, Has.Count.EqualTo(2)); // только SecurityCode=1, который есть в обоих списках
        Assert.That(result[0].SecurityCode, Is.EqualTo(1));
        Assert.That(result[1].SecurityCode, Is.EqualTo(2));
        Assert.That(result[1].Value, Is.EqualTo(0));
    }

    [Test]
    public void SecuritiesToUpdate_WhenMixedScenario_ReturnsOnlyChangedValues()
    {
        // Arrange: смешанный сценарий
        var profileSecurities = new List<UserProfileSecurity>
        {
            new() { Id = 1, Value = 1 }, // изменилось: 0 -> 1
            new() { Id = 2, Value = 0 }, // изменилось: 1 -> 0
            new() { Id = 3, Value = 0 }, // не изменилось: 0 -> 0
            new() { Id = 4, Value = 1 }, // не изменилось: 1 -> 1
            new() { Id = 5, Value = 1 }  // есть только в новом профиле
        };
        var existSecurities = new List<Security>
        {
            new() { SecurityCode = 1, Value = 0 },
            new() { SecurityCode = 2, Value = 1 },
            new() { SecurityCode = 3, Value = 0 },
            new() { SecurityCode = 4, Value = 1 },
            new() { SecurityCode = 6, Value = 0 } // есть только в базе
        };
        
        // Act
        var result = _repository.SecuritiesToUpdate(profileSecurities, existSecurities);
        
        // Assert: должны вернуться только SecurityCode=1 и SecurityCode=2 (значения изменились)
        Assert.That(result, Has.Count.EqualTo(2));
        var securityCodes = result.Select(s => s.SecurityCode).ToList();
        Assert.That(securityCodes, Contains.Item(1));
        Assert.That(securityCodes, Contains.Item(2));
        // SecurityCode=5 и SecurityCode=6 не должны попасть в результат (нет в обоих списках)
    }
}
using Application.Frontol;
using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Cashiers;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class CashierApplyServiceTests
{
    private Mock<IFrontolCashiers> _cashiers = null!;
    private Mock<IFrontolMainDb> _mainDb = null!;
    private CashierApplyService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _cashiers = new Mock<IFrontolCashiers>();
        _mainDb = new Mock<IFrontolMainDb>();
        _service = new CashierApplyService(_cashiers.Object, _mainDb.Object);
    }

    [Test]
    public async Task Apply_успех_вызывает_Restart()
    {
        _cashiers.Setup(c => c.Apply(It.IsAny<int>(), It.IsAny<IReadOnlyList<CashierItem>>()))
            .ReturnsAsync(Result.Success());
        _mainDb.Setup(m => m.Restart()).ReturnsAsync(Result.Success());

        var result = await _service.Apply(100, [new CashierItem { Code = 5 }]);

        Assert.That(result.IsSuccess, Is.True);
        _mainDb.Verify(m => m.Restart(), Times.Once);
    }

    [Test]
    public async Task Apply_пустой_пакет_вызывает_Restart()
    {
        _cashiers.Setup(c => c.Apply(0, It.IsAny<IReadOnlyList<CashierItem>>()))
            .ReturnsAsync(Result.Success());
        _mainDb.Setup(m => m.Restart()).ReturnsAsync(Result.Success());

        var result = await _service.Apply(0, []);

        Assert.That(result.IsSuccess, Is.True);
        _mainDb.Verify(m => m.Restart(), Times.Once);
    }

    [Test]
    public async Task Apply_ошибка_записи_без_Restart()
    {
        _cashiers.Setup(c => c.Apply(It.IsAny<int>(), It.IsAny<IReadOnlyList<CashierItem>>()))
            .ReturnsAsync(Result.Failure("USER"));

        var result = await _service.Apply(100, [new CashierItem { Code = 5 }]);

        Assert.That(result.IsFailure, Is.True);
        _mainDb.Verify(m => m.Restart(), Times.Never);
    }
}

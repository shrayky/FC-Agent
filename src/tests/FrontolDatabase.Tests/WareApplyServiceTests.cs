using Application.Frontol;
using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Wares;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class WareApplyServiceTests
{
    private Mock<IFrontolWares> _wares = null!;
    private Mock<IFrontolMainDb> _mainDb = null!;
    private WareApplyService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _wares = new Mock<IFrontolWares>();
        _mainDb = new Mock<IFrontolMainDb>();
        _service = new WareApplyService(_wares.Object, _mainDb.Object);
    }

    [Test]
    public async Task Apply_успех_вызывает_Restart()
    {
        _wares.Setup(w => w.Apply(It.IsAny<IReadOnlyList<WareGroup>>(), It.IsAny<IReadOnlyList<WareItem>>()))
            .ReturnsAsync(Result.Success());
        _mainDb.Setup(m => m.Restart()).ReturnsAsync(Result.Success());

        var result = await _service.Apply([new WareGroup { GroupCode = "1" }], []);

        Assert.That(result.IsSuccess, Is.True);
        _mainDb.Verify(m => m.Restart(), Times.Once);
    }

    [Test]
    public async Task Apply_ошибка_групп_без_Restart()
    {
        _wares.Setup(w => w.Apply(It.IsAny<IReadOnlyList<WareGroup>>(), It.IsAny<IReadOnlyList<WareItem>>()))
            .ReturnsAsync(Result.Failure("группа"));

        var result = await _service.Apply([new WareGroup { GroupCode = "1" }], []);

        Assert.That(result.IsFailure, Is.True);
        _mainDb.Verify(m => m.Restart(), Times.Never);
    }
}

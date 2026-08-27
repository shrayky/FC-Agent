using Application.Frontol;
using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using Domain.Messages.Enums;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class PaySystemModeServiceTests
{
    private Mock<IFrontolDevices> _devices = null!;
    private Mock<IPaySystemEmulatorSnapshotStore> _snapshot = null!;
    private Mock<IFrontolMainDb> _mainDb = null!;
    private PaySystemModeService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _devices = new Mock<IFrontolDevices>();
        _snapshot = new Mock<IPaySystemEmulatorSnapshotStore>();
        _mainDb = new Mock<IFrontolMainDb>();
        _service = new PaySystemModeService(_devices.Object, _snapshot.Object, _mainDb.Object);
    }

    /// <summary>
    /// В эмулятор: меняем устройства, дописываем снимок, ставим перезапуск.
    /// </summary>
    [Test]
    public async Task Apply_эмулятор_сохраняет_изменённые_устройства_и_перезапускает()
    {
        _devices.Setup(d => d.SwitchPaySystemsToEmulator())
            .ReturnsAsync(Result.Success(new List<int> { 2 }));
        _snapshot.Setup(s => s.Load())
            .ReturnsAsync(Result.Success(new List<int> { 1 }));
        _snapshot.Setup(s => s.Save(It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(Result.Success());
        _mainDb.Setup(m => m.Restart()).ReturnsAsync(Result.Success());

        var result = await _service.ChangeMode(PaySystemMode.Emulator);

        Assert.That(result.IsSuccess, Is.True);
        _snapshot.Verify(s => s.Save(It.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 1, 2 }))), Times.Once);
        _mainDb.Verify(m => m.Restart(), Times.Once);
    }

    /// <summary>
    /// Нет устройств для эмулятора — файл и Restart не трогаем.
    /// </summary>
    [Test]
    public async Task Apply_эмулятор_без_устройств_не_перезапускает()
    {
        _devices.Setup(d => d.SwitchPaySystemsToEmulator())
            .ReturnsAsync(Result.Success(new List<int>()));

        var result = await _service.ChangeMode(PaySystemMode.Emulator);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo("Нет банковских систем для перевода в эмулятор"));
        _snapshot.Verify(s => s.Save(It.IsAny<IReadOnlyList<int>>()), Times.Never);
        _mainDb.Verify(m => m.Restart(), Times.Never);
    }

    /// <summary>
    /// В рабочий режим: читаем файл, включаем устройства, удаляем файл, Restart.
    /// </summary>
    [Test]
    public async Task Apply_рабочий_режим_включает_устройства_из_файла()
    {
        _snapshot.Setup(s => s.Load()).ReturnsAsync(Result.Success(new List<int> { 5, 7 }));
        _devices.Setup(d => d.SwitchPaySystemsToWorkMode(It.IsAny<IReadOnlyList<int>>()))
            .ReturnsAsync(Result.Success());
        _snapshot.Setup(s => s.Clear()).ReturnsAsync(Result.Success());
        _mainDb.Setup(m => m.Restart()).ReturnsAsync(Result.Success());

        var result = await _service.ChangeMode(PaySystemMode.Connected);

        Assert.That(result.IsSuccess, Is.True);
        _devices.Verify(d => d.SwitchPaySystemsToWorkMode(It.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 5, 7 }))), Times.Once);
        _snapshot.Verify(s => s.Clear(), Times.Once);
        _mainDb.Verify(m => m.Restart(), Times.Once);
    }

    /// <summary>
    /// Нет файла — устройства и Restart не трогаем.
    /// </summary>
    [Test]
    public async Task Apply_рабочий_режим_без_файла_не_перезапускает()
    {
        _snapshot.Setup(s => s.Load())
            .ReturnsAsync(Result.Failure<List<int>>("Файл отключенных банковских систем не найден"));

        var result = await _service.ChangeMode(PaySystemMode.Connected);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo("Файл отключенных банковских систем не найден"));
        _devices.Verify(d => d.SwitchPaySystemsToWorkMode(It.IsAny<IReadOnlyList<int>>()), Times.Never);
        _mainDb.Verify(m => m.Restart(), Times.Never);
    }
}

using Domain.Frontol.Enums;
using FrontolDatabase.Entitys;
using FrontolDatabase.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class DevicesRepositoryTests
{
    private MainDbCtx _dbContext = null!;
    private DevicesRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<MainDbCtx>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MainDbCtx(options);
        _repository = new DevicesRepository(new Mock<ILogger<DevicesRepository>>().Object, _dbContext);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    /// <summary>
    /// В эмулятор переводим только платёжные системы, не папки и не другие типы.
    /// </summary>
    [Test]
    public async Task SwitchPaySystemsToEmulator_меняет_только_платёжные_системы()
    {
        _dbContext.Devices!.AddRange(
            PaySystem(1, DeviceConnectionStateEnum.Connected),
            PaySystem(2, DeviceConnectionStateEnum.Connected, isFolder: true),
            Device(3, DeviceTypeEnum.CashRegister, DeviceConnectionStateEnum.Connected),
            PaySystem(4, DeviceConnectionStateEnum.Emulator));
        await _dbContext.SaveChangesAsync();

        var result = await _repository.SwitchPaySystemsToEmulator();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(new[] { 1 }));
        Assert.That((await DeviceById(1)).ConnectionState, Is.EqualTo(DeviceConnectionStateEnum.Emulator));
        Assert.That((await DeviceById(2)).ConnectionState, Is.EqualTo(DeviceConnectionStateEnum.Connected));
        Assert.That((await DeviceById(3)).ConnectionState, Is.EqualTo(DeviceConnectionStateEnum.Connected));
        Assert.That((await DeviceById(4)).ConnectionState, Is.EqualTo(DeviceConnectionStateEnum.Emulator));
    }

    /// <summary>
    /// В рабочий режим возвращаем только устройства из переданного списка.
    /// </summary>
    [Test]
    public async Task SwitchPaySystemsToConnected_меняет_только_указанные_устройства()
    {
        _dbContext.Devices!.AddRange(
            PaySystem(1, DeviceConnectionStateEnum.Emulator),
            PaySystem(2, DeviceConnectionStateEnum.Emulator),
            PaySystem(3, DeviceConnectionStateEnum.Emulator));
        await _dbContext.SaveChangesAsync();

        var result = await _repository.SwitchPaySystemsToWorkMode([1, 3, 99]);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That((await DeviceById(1)).ConnectionState, Is.EqualTo(DeviceConnectionStateEnum.Connected));
        Assert.That((await DeviceById(2)).ConnectionState, Is.EqualTo(DeviceConnectionStateEnum.Emulator));
        Assert.That((await DeviceById(3)).ConnectionState, Is.EqualTo(DeviceConnectionStateEnum.Connected));
    }

    private async Task<Devices> DeviceById(int id) =>
        await _dbContext.Devices!.AsNoTracking().SingleAsync(d => d.Id == id);

    private static Devices PaySystem(int id, DeviceConnectionStateEnum state, bool isFolder = false) =>
        Device(id, DeviceTypeEnum.PaySystem, state, isFolder);

    private static Devices Device(int id, DeviceTypeEnum type, DeviceConnectionStateEnum state, bool isFolder = false) =>
        new()
        {
            Id = id,
            Code = id,
            Name = $"dev-{id}",
            DeviceType = type,
            ConnectionState = state,
            IsFolder = isFolder
        };
}

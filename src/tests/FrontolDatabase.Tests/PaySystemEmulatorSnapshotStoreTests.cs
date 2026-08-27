using Application.Frontol;

namespace FrontolDatabase.Tests;

[TestFixture]
public class PaySystemEmulatorSnapshotStoreTests
{
    private string _tempDir = null!;
    private string _filePath = null!;
    private PaySystemEmulatorSnapshotStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fc-pay-systems-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "pay-systems-emulator.json");
        _store = new PaySystemEmulatorSnapshotStore(_filePath);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>
    /// Сохранённый список читается обратно.
    /// </summary>
    [Test]
    public async Task Save_затем_Load_возвращает_сохранённые_идентификаторы()
    {
        var save = await _store.Save([10, 20]);
        var load = await _store.Load();

        Assert.That(save.IsSuccess, Is.True);
        Assert.That(load.IsSuccess, Is.True);
        Assert.That(load.Value, Is.EqualTo(new[] { 10, 20 }));
        Assert.That(File.Exists(_filePath), Is.True);
    }

    /// <summary>
    /// Без файла загрузка — ошибка.
    /// </summary>
    [Test]
    public async Task Load_ошибка_если_файла_нет()
    {
        var load = await _store.Load();

        Assert.That(load.IsFailure, Is.True);
        Assert.That(load.Error, Is.EqualTo("Файл отключенных банковских систем не найден"));
    }

    /// <summary>
    /// Clear удаляет файл.
    /// </summary>
    [Test]
    public async Task Clear_удаляет_файл()
    {
        await _store.Save([1]);

        var clear = await _store.Clear();
        var load = await _store.Load();

        Assert.That(clear.IsSuccess, Is.True);
        Assert.That(File.Exists(_filePath), Is.False);
        Assert.That(load.IsFailure, Is.True);
    }
}

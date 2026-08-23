using HostApp.Services;

namespace HostApp.Tests;

[TestFixture]
public class VersionFolderAllocatorTests
{
    /// <summary>
    /// Нет установленных папок линии 2.1 — имя остаётся major.minor.
    /// </summary>
    [Test]
    public void Allocate_без_папок_возвращает_major_minor()
    {
        var productDir = CreateTempProductDir();
        try
        {
            var name = VersionFolderAllocator.Allocate(productDir, new Version(2, 1));

            Assert.That(name, Is.EqualTo("2.1"));
        }
        finally
        {
            DeleteTempRoot(productDir);
        }
    }

    /// <summary>
    /// Занятый 2.1 даёт новую папку 2.1.1, чтобы Latest указывал на установку.
    /// </summary>
    [Test]
    public void Allocate_при_занятом_2_1_возвращает_2_1_1()
    {
        var productDir = CreateTempProductDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(productDir, "2.1"));

            var name = VersionFolderAllocator.Allocate(productDir, new Version(2, 1));

            Assert.That(name, Is.EqualTo("2.1.1"));
        }
        finally
        {
            DeleteTempRoot(productDir);
        }
    }

    /// <summary>
    /// Свободный 2.1 при существующем 2.1.1 не используется: Latest иначе остался бы на 2.1.1.
    /// </summary>
    [Test]
    public void Allocate_при_существующем_2_1_1_возвращает_2_1_2()
    {
        var productDir = CreateTempProductDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(productDir, "2.1.1"));

            var name = VersionFolderAllocator.Allocate(productDir, new Version(2, 1));

            Assert.That(name, Is.EqualTo("2.1.2"));
        }
        finally
        {
            DeleteTempRoot(productDir);
        }
    }

    /// <summary>
    /// Берётся максимум линии major.minor, а не первое свободное имя.
    /// </summary>
    [Test]
    public void Allocate_при_2_1_и_2_1_1_возвращает_2_1_2()
    {
        var productDir = CreateTempProductDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(productDir, "2.1"));
            Directory.CreateDirectory(Path.Combine(productDir, "2.1.1"));

            var name = VersionFolderAllocator.Allocate(productDir, new Version(2, 1));

            Assert.That(name, Is.EqualTo("2.1.2"));
        }
        finally
        {
            DeleteTempRoot(productDir);
        }
    }

    /// <summary>
    /// Папки другой линии (2.0) не влияют на имя 2.1.
    /// </summary>
    [Test]
    public void Allocate_игнорирует_другую_линию_версий()
    {
        var productDir = CreateTempProductDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(productDir, "2.0"));

            var name = VersionFolderAllocator.Allocate(productDir, new Version(2, 1));

            Assert.That(name, Is.EqualTo("2.1"));
        }
        finally
        {
            DeleteTempRoot(productDir);
        }
    }

    /// <summary>
    /// Несуществующий каталог продукта даёт базовое имя без исключения.
    /// </summary>
    [Test]
    public void Allocate_без_каталога_продукта_возвращает_major_minor()
    {
        var missing = Path.Combine(Path.GetTempPath(), "fc-alloc-missing-" + Guid.NewGuid());

        var name = VersionFolderAllocator.Allocate(missing, new Version(2, 1, 0));

        Assert.That(name, Is.EqualTo("2.1"));
    }

    /// <summary>
    /// Создаёт пустой временный каталог продукта.
    /// </summary>
    private static string CreateTempProductDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fc-alloc-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Удаляет временный каталог, если он был создан.
    /// </summary>
    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }
}

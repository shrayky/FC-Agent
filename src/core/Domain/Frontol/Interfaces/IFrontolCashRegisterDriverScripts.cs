using CSharpFunctionalExtensions;
using Domain.Frontol.Models.Settings;

namespace Domain.Frontol.Interfaces;

public interface IFrontolCashRegisterDriverScripts
{
    /// <summary>
    /// Читает JSON-скрипты драйвера ККТ из каталога json_scripts.
    /// </summary>
    Task<Result<List<AtolCashRegisterDriver10Srcipt>>> FromFiles();

    /// <summary>
    /// Записывает JSON-скрипты драйвера ККТ в каталог json_scripts.
    /// </summary>
    Task<Result> ToFiles(IReadOnlyList<AtolCashRegisterDriver10Srcipt> scripts, bool upload);
}

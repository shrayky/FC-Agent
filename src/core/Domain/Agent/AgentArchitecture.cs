using System.Runtime.InteropServices;

namespace Domain.Agent;

/// <summary>
/// Имя архитектуры процесса для метаданных пакета (RID, не ОС).
/// </summary>
public static class AgentArchitecture
{
    /// <summary>
    /// x86 или x64 — как в UniqId файлов обновления.
    /// </summary>
    public static string Name(Architecture architecture) =>
        architecture == Architecture.X86 ? "x86" : "x64";
}

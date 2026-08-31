namespace Domain.Agent;

/// <summary>
/// Размер диска, свободное место, буква и метка тома.
/// </summary>
public readonly record struct DriveMetrics(long TotalBytes, long FreeBytes, string Letter = "", string Name = "");

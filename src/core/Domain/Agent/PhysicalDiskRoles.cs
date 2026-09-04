using Domain.Agent.Dto;

namespace Domain.Agent;

public static class PhysicalDiskRoles
{
    public static void Apply(
        IReadOnlyList<DiskPartition> partitions,
        string osLetter,
        string dbLetter)
    {
        var os = Normalize(osLetter);
        var db = Normalize(dbLetter);

        foreach (var partition in partitions)
        {
            var letter = Normalize(partition.Letter);
            partition.Letter = letter;
            partition.IsOs = os.Length > 0 && letter == os;
            partition.IsDatabase = db.Length > 0 && letter == db;
        }
    }

    private static string Normalize(string letter)
    {
        if (string.IsNullOrWhiteSpace(letter) || !char.IsLetter(letter[0]))
            return string.Empty;

        return char.ToUpperInvariant(letter[0]).ToString();
    }
}

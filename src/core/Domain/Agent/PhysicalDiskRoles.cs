using Domain.Agent.Dto;

namespace Domain.Agent;

public static class PhysicalDiskRoles
{
    public static void Apply(
        PhysicalDiskHealth disk,
        IReadOnlyList<string> volumeLetters,
        string osLetter,
        string dbLetter)
    {
        var letters = volumeLetters
            .Select(Normalize)
            .Where(letter => letter.Length > 0)
            .Distinct()
            .ToList();

        var os = Normalize(osLetter);
        var db = Normalize(dbLetter);

        disk.IsOs = os.Length > 0 && letters.Contains(os);
        disk.IsDatabase = db.Length > 0 && letters.Contains(db);
        disk.Letter = Prefer(letters, os, db);
    }

    private static string Prefer(IReadOnlyList<string> letters, string os, string db)
    {
        if (os.Length > 0 && letters.Contains(os))
            return os;

        if (db.Length > 0 && letters.Contains(db))
            return db;

        return letters.Count > 0 ? letters[0] : string.Empty;
    }

    private static string Normalize(string letter)
    {
        if (string.IsNullOrWhiteSpace(letter) || !char.IsLetter(letter[0]))
            return string.Empty;

        return char.ToUpperInvariant(letter[0]).ToString();
    }
}

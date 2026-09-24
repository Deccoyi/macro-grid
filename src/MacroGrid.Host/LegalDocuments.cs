using System.Text;

namespace MacroGrid.Host;

/// <summary>
/// Reads the legal texts that ship next to the exe (the user agreement, the project license, the third-party notice index
/// and the original license text of every bundled library) so the editor's Help window can show them inside the app instead
/// of pointing at a repository. A development build has none of these files; every method then returns null or an empty list.
/// </summary>
internal sealed class LegalDocuments(string baseDirectory)
{
    public sealed record Overview(string? Agreement, string? ProjectLicense, string? Notices, IReadOnlyList<string> Libraries);

    private string LicensesDirectory => Path.Combine(baseDirectory, "licenses");

    public Overview GetOverview() => new(
        ReadText("license-agreement.txt"),
        ReadText("LICENSE"),
        ReadText("THIRD_PARTY_NOTICES.md"),
        ListLibraries());

    /// <summary>The license text(s) of one library, or null when no library of that name is installed. The name is only
    /// accepted when it is exactly one of the folders in the licenses folder, so it can never point anywhere else.</summary>
    public string? GetLibrary(string name)
    {
        if (!ListLibraries().Contains(name, StringComparer.Ordinal)) return null;

        var directory = Path.Combine(LicensesDirectory, name);
        var files = Directory.GetFiles(directory).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        var text = new StringBuilder();
        foreach (var file in files)
        {
            if (files.Count > 1) text.AppendLine($"----- {Path.GetFileName(file)} -----");
            text.AppendLine(File.ReadAllText(file));
            text.AppendLine();
        }
        return text.ToString().TrimEnd();
    }

    private IReadOnlyList<string> ListLibraries() =>
        Directory.Exists(LicensesDirectory)
            ? Directory.GetDirectories(LicensesDirectory).Select(d => Path.GetFileName(d)!).Order(StringComparer.OrdinalIgnoreCase).ToList()
            : [];

    private string? ReadText(string fileName)
    {
        var path = Path.Combine(baseDirectory, fileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}

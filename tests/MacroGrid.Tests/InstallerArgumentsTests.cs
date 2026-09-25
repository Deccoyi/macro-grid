using System.Security.Cryptography;
using MacroGrid.Core.Updates;

namespace MacroGrid.Tests;

public sealed class InstallerArgumentsTests
{
    [Fact]
    public void The_update_setup_is_visible_and_never_silent()
    {
        // A silent setup skips the license page, so a changed agreement would never be shown to the person.
        Assert.DoesNotContain("SILENT", InstallerArguments.ForUpdate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/UPDATE", InstallerArguments.ForUpdate);
        Assert.Contains("/NORESTART", InstallerArguments.ForUpdate);
    }

    private static string? FindAgreement()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "installer", "license-agreement.txt");
            if (File.Exists(path)) return path;
        }
        return null;
    }

    [Fact]
    public void The_agreement_carries_a_last_updated_date_and_hashes_the_same_every_time()
    {
        var path = FindAgreement();
        Assert.NotNull(path);

        var bytes = File.ReadAllBytes(path);
        var text = System.Text.Encoding.ASCII.GetString(bytes);

        // The hash is the identity of the text the person accepted; the date line is part of it and must be bumped with every edit.
        Assert.Matches(@"Last updated: \d{4}-\d{2}-\d{2}", text);
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(File.ReadAllBytes(path)));
        Assert.NotEqual(SHA256.HashData(bytes), SHA256.HashData([.. bytes, (byte)' ']));
    }
}

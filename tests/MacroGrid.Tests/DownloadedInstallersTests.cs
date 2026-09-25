using MacroGrid.Core.Updates;

namespace MacroGrid.Tests;

public sealed class DownloadedInstallersTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"), "updates");

    public void Dispose()
    {
        var parent = Path.GetDirectoryName(_root)!;
        if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
    }

    private static ReleaseVersion V(string text) => ReleaseVersion.TryParse(text, out var v) ? v : throw new FormatException(text);

    private string Add(string version, string file = "MacroGrid-Setup-x.exe")
    {
        var folder = Path.Combine(_root, version);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, file), "x");
        return folder;
    }

    [Fact]
    public void A_missing_folder_is_fine()
    {
        Assert.Empty(DownloadedInstallers.CleanUp(_root, V("0.2.1")));
    }

    [Fact]
    public void Only_the_newest_installer_newer_than_the_running_version_stays()
    {
        var old = Add("0.2.1-alpha");
        var mid = Add("0.3.0-alpha");
        var newest = Add("0.3.1-alpha");

        var deleted = DownloadedInstallers.CleanUp(_root, V("0.2.1"));

        Assert.True(Directory.Exists(newest));
        Assert.False(Directory.Exists(mid));
        Assert.False(Directory.Exists(old));
        Assert.Equal(2, deleted.Count);
    }

    [Fact]
    public void The_version_that_was_just_downloaded_stays_even_when_a_newer_leftover_exists()
    {
        var leftover = Add("0.9.0-alpha");
        var fresh = Add("0.3.0-alpha");
        var older = Add("0.2.5");

        DownloadedInstallers.CleanUp(_root, V("0.2.1"), keep: V("0.3.0-alpha"));

        Assert.True(Directory.Exists(fresh));
        Assert.False(Directory.Exists(leftover));
        Assert.False(Directory.Exists(older));
    }

    [Fact]
    public void Everything_goes_when_the_running_version_is_as_new_as_all_of_them()
    {
        Add("0.2.1");
        Add("0.3.0-alpha");

        DownloadedInstallers.CleanUp(_root, V("0.3.0"));

        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public void The_kept_folder_loses_its_half_finished_download_but_keeps_the_installer()
    {
        var kept = Add("0.3.0", "MacroGrid-Setup-0.3.0.exe.part");
        File.WriteAllText(Path.Combine(kept, "MacroGrid-Setup-0.3.0.exe"), "done");

        DownloadedInstallers.CleanUp(_root, V("0.2.1"));

        Assert.True(File.Exists(Path.Combine(kept, "MacroGrid-Setup-0.3.0.exe")));
        Assert.False(File.Exists(Path.Combine(kept, "MacroGrid-Setup-0.3.0.exe.part")));
    }

    [Fact]
    public void Folders_that_are_not_versions_and_loose_files_are_left_alone()
    {
        Add("0.2.1");
        var other = Path.Combine(_root, "keep-me");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "x");

        DownloadedInstallers.CleanUp(_root, V("0.3.0"));

        Assert.True(Directory.Exists(other));
        Assert.True(File.Exists(Path.Combine(_root, "notes.txt")));
        Assert.False(Directory.Exists(Path.Combine(_root, "0.2.1")));
        Assert.True(Directory.Exists(_root)); // not empty, so it stays
    }

    [Fact]
    public void A_folder_that_cannot_be_deleted_is_reported_and_the_rest_still_goes()
    {
        var locked = Add("0.2.0");
        Add("0.2.1");
        using var hold = File.Open(Path.Combine(locked, "MacroGrid-Setup-x.exe"), FileMode.Open, FileAccess.Read, FileShare.None);
        var errors = new List<string>();

        var deleted = DownloadedInstallers.CleanUp(_root, V("0.3.0"), (path, _) => errors.Add(path));

        Assert.Contains(locked, errors);
        Assert.DoesNotContain(locked, deleted);
        Assert.False(Directory.Exists(Path.Combine(_root, "0.2.1")));
    }
}

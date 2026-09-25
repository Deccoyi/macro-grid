using System.IO.Compression;
using MacroGrid.Core.Plugins.Distribution;

namespace MacroGrid.Tests;

public sealed class PluginZipTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static byte[] Zip(Action<ZipArchive> write)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true)) write(zip);
        return buffer.ToArray();
    }

    private static void AddEntry(ZipArchive zip, string name, string content = "x")
    {
        using var stream = zip.CreateEntry(name).Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }

    [Fact]
    public void Extracts_a_well_formed_archive()
    {
        var bytes = Zip(zip =>
        {
            AddEntry(zip, "plugin.json", "{}");
            AddEntry(zip, "sub/file.txt", "hello");
        });

        PluginZip.ExtractSafely(bytes, _root);

        Assert.Equal("{}", File.ReadAllText(Path.Combine(_root, "plugin.json")));
        Assert.Equal("hello", File.ReadAllText(Path.Combine(_root, "sub", "file.txt")));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("sub/../../escape.txt")]
    [InlineData("..\\escape.txt")]
    public void Refuses_an_entry_that_escapes_the_destination(string entryName)
    {
        var bytes = Zip(zip => AddEntry(zip, entryName));

        var ex = Assert.Throws<PluginDownloadException>(() => PluginZip.ExtractSafely(bytes, _root));
        Assert.Equal(PluginDownloadException.Verify, ex.Code);
    }
}

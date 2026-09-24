using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Model;
using MacroGrid.Core.Profiles;
using MacroGrid.Protocol;

namespace MacroGrid.Tests;

public class ProfilePackageTests
{
    private static Profile Sample() => new()
    {
        Id = "p1",
        Name = "Streaming",
        Pages =
        [
            new Page
            {
                Id = "a",
                Widgets =
                [
                    new Widget
                    {
                        Id = "w1",
                        Text = "Scene",
                        Style = new WidgetStyle { Icon = "data:image/svg+xml;base64," + new string('Q', 300) },
                        Actions = { [WidgetEvents.Press] = [new ActionBinding("obs.switchScene", new JsonObject { ["scene"] = "Main" }), new ActionBinding("core.hotkey", new JsonObject())] },
                    },
                    new Widget { Id = "w2", X = 1, Actions = { [WidgetEvents.Press] = [new ActionBinding("obs.switchScene", new JsonObject())] } },
                ],
            },
        ],
    };

    private static ProfilePackageManifest Manifest(int version = ProfilePackage.CurrentFormatVersion) =>
        new(version, "Streaming", DateTimeOffset.UtcNow, "0.2.0", [new PackagePluginRef("obs", "OBS", "0.2.0", ["obs.switchScene"])]);

    [Fact]
    public void A_package_round_trips_the_profile_and_the_manifest()
    {
        var bytes = ProfilePackage.Write(Sample(), Manifest());

        var content = ProfilePackage.Read(bytes);

        Assert.Equal("Streaming", content.Profile.Name);
        Assert.Equal(Sample().Pages[0].Widgets[0].Style.Icon, content.Profile.Pages[0].Widgets[0].Style.Icon);
        Assert.Equal("obs", Assert.Single(content.Manifest!.RequiredPlugins).Id);
    }

    [Fact]
    public void A_package_is_a_zip_with_the_two_known_entries()
    {
        using var zip = new ZipArchive(new MemoryStream(ProfilePackage.Write(Sample(), Manifest())));

        Assert.Equal(["manifest.json", "profile.json"], zip.Entries.Select(e => e.FullName).Order());
    }

    [Fact]
    public void A_plain_profile_json_is_still_accepted_without_a_manifest()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(Sample(), ProtocolJson.Options);

        var content = ProfilePackage.Read(json);

        Assert.Equal("Streaming", content.Profile.Name);
        Assert.Null(content.Manifest);
    }

    [Fact]
    public void A_package_from_a_newer_format_is_refused_with_a_clear_message()
    {
        var bytes = ProfilePackage.Write(Sample(), Manifest(version: ProfilePackage.CurrentFormatVersion + 1));

        var ex = Assert.Throws<InvalidDataException>(() => ProfilePackage.Read(bytes));

        Assert.Contains("newer version", ex.Message);
    }

    [Fact]
    public void A_zip_without_a_profile_is_refused()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            zip.CreateEntry("readme.txt").Open().Write("hi"u8);

        Assert.Throws<InvalidDataException>(() => ProfilePackage.Read(buffer.ToArray()));
    }

    [Fact]
    public void Garbage_is_refused_instead_of_crashing()
    {
        Assert.Throws<InvalidDataException>(() => ProfilePackage.Read(Encoding.UTF8.GetBytes("not a profile")));
        Assert.Throws<InvalidDataException>(() => ProfilePackage.Read("PK\u0003\u0004 damaged"u8.ToArray()));
    }

    [Fact]
    public void A_profile_that_fails_validation_is_refused()
    {
        var broken = Sample();
        broken.Pages.Clear();

        Assert.Throws<InvalidDataException>(() => ProfilePackage.Read(ProfilePackage.Write(broken, Manifest())));
    }

    [Fact]
    public void ActionTypes_lists_each_bound_type_once()
    {
        Assert.Equal(["core.hotkey", "obs.switchScene"], ProfilePackage.ActionTypes(Sample()).Order());
    }
}

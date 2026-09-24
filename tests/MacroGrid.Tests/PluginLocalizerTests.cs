using MacroGrid.Core.Plugins;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Tests;

public sealed class PluginLocalizerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mg-localizer-" + Guid.NewGuid().ToString("N"));
    private string _language = "tr";

    public PluginLocalizerTests()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "locales"));
        File.WriteAllText(Path.Combine(_dir, "locales", "tr.json"), """{ "Audio source": "Ses kaynağı", "Retry in {0}s ({1})": "{1} için {0}s sonra dene" }""");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private PluginLocalizer Create(string? defaultLanguage = null)
    {
        var localizer = new PluginLocalizer(() => _language);
        localizer.Register("demo", _dir, defaultLanguage);
        return localizer;
    }

    [Fact]
    public void Translates_a_known_text_into_the_current_language()
    {
        Assert.Equal("Ses kaynağı", Create().Translate("demo", "Audio source"));
    }

    [Fact]
    public void Fills_the_values_of_a_run_time_text_into_the_translated_template()
    {
        var localizer = Create();
        Assert.Equal("obs için 5s sonra dene", localizer.Translate("demo", "Retry in 5s (obs)"));
        Assert.Equal("obs için 5s sonra dene", localizer.TranslateAny("Retry in 5s (obs)"));
    }

    [Fact]
    public void Falls_back_to_the_written_text_when_the_entry_is_missing()
    {
        Assert.Equal("Something else", Create().Translate("demo", "Something else"));
    }

    [Fact]
    public void Falls_back_to_the_written_text_when_the_language_has_no_file()
    {
        _language = "de";
        Assert.Equal("Audio source", Create().Translate("demo", "Audio source"));
    }

    [Fact]
    public void Keeps_the_text_when_the_language_is_the_default_language()
    {
        Assert.Equal("Audio source", Create(defaultLanguage: "tr").Translate("demo", "Audio source"));
    }

    [Fact]
    public void Accepts_a_regional_language_code()
    {
        _language = "tr-TR";
        Assert.Equal("Ses kaynağı", Create().Translate("demo", "Audio source"));
    }

    [Fact]
    public void Leaves_texts_of_an_unknown_plugin_and_of_the_host_alone()
    {
        var localizer = Create();
        Assert.Equal("Audio source", localizer.Translate("other", "Audio source"));
        Assert.Equal("Audio source", localizer.Translate(null, "Audio source"));
    }

    [Fact]
    public void Ignores_a_broken_locale_file()
    {
        File.WriteAllText(Path.Combine(_dir, "locales", "tr.json"), "{ not json");
        Assert.Equal("Audio source", Create().Translate("demo", "Audio source"));
    }

    [Fact]
    public void Translates_the_label_description_and_options_of_a_form_field()
    {
        var field = new SettingField("source", "Audio source", SettingFieldKind.Select)
        {
            Description = "Audio source",
            Options = [new SettingOption("a", "Audio source")],
        };

        var localized = Create().Localize("demo", field);

        Assert.Equal("Ses kaynağı", localized.Label);
        Assert.Equal("Ses kaynağı", localized.Description);
        Assert.Equal("Ses kaynağı", localized.Options![0].Label);
        Assert.Equal("a", localized.Options[0].Value);
    }
}

using System.Text.Json;
using MacroStation.Core.Actions;
using MacroStation.Core.Model;
using MacroStation.Protocol;

namespace MacroStation.Core.Profiles;

/// <summary>Persists profiles as one JSON file each under <c>{dataDir}/profiles</c>.</summary>
public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions FileJson = new(ProtocolJson.Options) { WriteIndented = true };

    private readonly string _dir;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Profile> _profiles = [];

    public ProfileStore(string dataDir)
    {
        _dir = Path.Combine(dataDir, "profiles");
        Directory.CreateDirectory(_dir);
        Load();
        if (_profiles.Count == 0)
            Save(CreateDefaultProfile());
    }

    public static string DefaultDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacroStation");

    public IReadOnlyList<Profile> All
    {
        get { lock (_lock) return _profiles.Values.OrderBy(p => p.Name).ToList(); }
    }

    public Profile? Get(string id)
    {
        lock (_lock) return _profiles.GetValueOrDefault(id);
    }

    public Profile First() => All[0];

    /// <returns>false if this was the last remaining profile and nothing was deleted (the app always needs at least one).</returns>
    public bool Delete(string id)
    {
        lock (_lock)
        {
            if (_profiles.Count <= 1 || !_profiles.ContainsKey(id))
                return false;

            _profiles.Remove(id);
            var path = PathFor(id);
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
    }

    public void Save(Profile profile)
    {
        var json = JsonSerializer.Serialize(profile, FileJson);
        var path = PathFor(profile.Id);
        var tmp = path + ".tmp";
        lock (_lock)
        {
            // Write-then-rename so a crash never leaves a half written profile.
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
            _profiles[profile.Id] = profile;
        }
    }

    private void Load()
    {
        foreach (var file in Directory.EnumerateFiles(_dir, "*.json"))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<Profile>(File.ReadAllText(file), FileJson);
                if (profile is not null)
                    _profiles[profile.Id] = profile;
            }
            catch (JsonException)
            {
                // Keep the broken file for manual recovery instead of overwriting it.
                File.Move(file, file + ".broken", overwrite: true);
            }
        }
    }

    private string PathFor(string id)
    {
        if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || id.Contains(".."))
            throw new ArgumentException($"Invalid profile id '{id}'.", nameof(id));
        return Path.Combine(_dir, id + ".json");
    }

    public static Profile CreateDefaultProfile()
    {
        static Widget Label(string text, int x, int y, int w, int h, string bg, string fg, double fontSize = 18) => new()
        {
            Type = WidgetTypes.Label,
            Text = text,
            X = x, Y = y, W = w, H = h,
            Style = new WidgetStyle { Background = bg, Foreground = fg, FontSize = fontSize },
        };

        var page2 = new Page
        {
            Name = "Sayfa 2",
            Cols = 4,
            Rows = 1,
            Widgets =
            [
                new Widget
                {
                    Text = "← Geri",
                    X = 0, Y = 0,
                    Style = new WidgetStyle { Background = "#334155", Foreground = "#ffffff" },
                    Actions = { [WidgetEvents.Press] = [new ActionBinding(PageAction.TypeId, PageAction.Back())] },
                },
                new Widget
                {
                    Text = "Metin editörü aç",
                    X = 1, Y = 0,
                    Style = new WidgetStyle { Background = "#0284c7", Foreground = "#ffffff" },
                    Actions = { [WidgetEvents.Press] = [new ActionBinding(OpenAction.TypeId, OpenAction.Settings("notepad.exe"))] },
                },
                new Widget
                {
                    Text = "Tümünü kopyala",
                    X = 2, Y = 0, W = 2,
                    Style = new WidgetStyle { Background = "#7c3aed", Foreground = "#ffffff" },
                    Actions =
                    {
                        [WidgetEvents.Press] =
                        [
                            new ActionBinding(HotkeyAction.TypeId, HotkeyAction.Settings("ctrl+a")),
                            new ActionBinding(DelayAction.TypeId, DelayAction.Settings(50)),
                            new ActionBinding(HotkeyAction.TypeId, HotkeyAction.Settings("ctrl+c")),
                        ],
                    },
                },
            ],
        };

        var page1 = new Page
        {
            Name = "Ana sayfa",
            Cols = 4,
            Rows = 2,
            Widgets =
            [
                Label("{system.time|HH:mm:ss}\n{system.time|dd.MM.yyyy}", 0, 0, 2, 2, "#0f172a", "#facc15", 22),
                Label("CPU\n{system.cpu|0}%", 2, 0, 1, 1, "#1f2937", "#38bdf8"),
                Label("RAM\n{system.ram|0}%", 3, 0, 1, 1, "#1f2937", "#4ade80"),
                new Widget
                {
                    Type = WidgetTypes.Toggle,
                    Text = "Sessiz",
                    X = 2, Y = 1,
                    Style = new WidgetStyle { Background = "#334155", Foreground = "#ffffff" },
                    Actions =
                    {
                        [WidgetEvents.ToggleOn] = [new ActionBinding(HotkeyAction.TypeId, HotkeyAction.Settings("volumemute"))],
                        [WidgetEvents.ToggleOff] = [new ActionBinding(HotkeyAction.TypeId, HotkeyAction.Settings("volumemute"))],
                    },
                },
                new Widget
                {
                    Text = "Sayfa 2 →",
                    X = 3, Y = 1,
                    Style = new WidgetStyle { Background = "#334155", Foreground = "#ffffff" },
                    Actions = { [WidgetEvents.Press] = [new ActionBinding(PageAction.TypeId, PageAction.Goto(page2.Id))] },
                },
            ],
        };

        return new Profile { Name = "Varsayılan", Pages = [page1, page2] };
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Devices;
using MacroGrid.Core.Languages;
using MacroGrid.Core.Model;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Automation;
using MacroGrid.Core.Variables;
using MacroGrid.Protocol;

namespace MacroGrid.Core.Backup;

public static class RestoreItemKind
{
    public const string Profile = "profile";
    public const string ProfileTree = "profileTree";
    public const string Preferences = "preferences";
    public const string Variables = "variables";
    public const string Automation = "automation";
    public const string Device = "device";
    public const string PluginSettings = "pluginSettings";
    public const string LanguagePack = "languagePack";
}

public static class RestoreState
{
    public const string New = "new";
    public const string Different = "different";
    public const string Same = "same";
}

/// <summary>One thing in a backup, compared with what is here. The editor translates <see cref="Kind"/> and the warning codes.</summary>
public sealed record RestoreItem(string Kind, string Key, string Name, string State)
{
    /// <summary>What differs by name: the pages of a profile, the settings of the preferences, the fields of a device.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];
    public int? HerePages { get; init; }
    public int? HereWidgets { get; init; }
    public int? BackupPages { get; init; }
    public int? BackupWidgets { get; init; }
    /// <summary>Variables and automation rules: how many would be added, changed, and (variables only) left out because their type differs.</summary>
    public int? Added { get; init; }
    public int? Changed { get; init; }
    public int? Skipped { get; init; }
    public int? HereVersion { get; init; }
    public int? BackupVersion { get; init; }
}

public sealed record RestoreWarning(string Code, IReadOnlyList<string> Args);

public sealed record InspectResult(string Id, BackupManifest Manifest, IReadOnlyList<RestoreItem> Items, IReadOnlyList<RestoreWarning> Warnings);

public sealed record RestoreRequestItem(string Kind, string Key);

public sealed record RestoreItemResult(string Kind, string Key, bool Ok, string? Error, IReadOnlyList<RestoreWarning> Warnings);

/// <summary>The backup a person asked to restore was not found (expired, replaced, or never inspected).</summary>
public sealed class RestoreSessionException(string message) : Exception(message);

/// <summary>
/// Compares a backup with what is here and applies the items the person ticked. The parsed backup is held in one in-memory slot between
/// the two steps, so exactly what was shown is what is restored. Every item goes through its store's own method, a restore point is made first
/// (and nothing is applied when it cannot be), and nothing is ever deleted.
/// </summary>
public sealed class BackupRestorer(
    BackupService backups,
    BackupCollector collector,
    ProfileStore profiles,
    ProfileTreeStore tree,
    PreferencesStore preferences,
    UserVariableService variables,
    AutomationStore automation,
    DeviceStore devices,
    PluginManager plugins,
    LanguagePackStore languages,
    ActionDispatcher dispatcher,
    Func<Profile, Task> profileChanged,
    TimeProvider? clock = null)
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions Json = new(ProtocolJson.Options);

    private sealed record Session(string Id, BackupFileContent Backup, DateTimeOffset Expires);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Session? _session;

    /// <summary>Reads a backup file's bytes, keeps them in the one session slot and answers what differs.</summary>
    /// <exception cref="InvalidDataException">The file is not a usable backup.</exception>
    public InspectResult Inspect(byte[] bytes)
    {
        var backup = BackupFile.Read(bytes);
        var session = new Session(Guid.NewGuid().ToString("N"), backup, _clock.GetUtcNow() + SessionLifetime);
        lock (this) _session = session;
        var (items, warnings) = Compare(backup);
        return new InspectResult(session.Id, backup.Manifest, items, warnings);
    }

    /// <exception cref="RestoreSessionException">The id is unknown or expired.</exception>
    /// <exception cref="BackupException">The restore point could not be made; nothing was applied.</exception>
    public async Task<IReadOnlyList<RestoreItemResult>> RestoreAsync(string id, IReadOnlyList<RestoreRequestItem> requested)
    {
        await _gate.WaitAsync();
        try
        {
            Session? session;
            lock (this) session = _session;
            if (session is null || session.Id != id || session.Expires < _clock.GetUtcNow())
                throw new RestoreSessionException("This restore session has ended. Choose the file again.");

            var (items, _) = Compare(session.Backup);
            var wanted = requested.Select(r => (r.Kind, r.Key)).ToHashSet();
            var apply = items.Where(i => i.State != RestoreState.Same && wanted.Contains((i.Kind, i.Key))).ToList();
            if (apply.Count == 0) return [];

            backups.CreateRestorePoint(BackupService.ReasonRestore);

            var content = session.Backup.Content;
            var results = new List<RestoreItemResult>();
            foreach (var kind in ApplyOrder)
            {
                foreach (var item in apply.Where(i => i.Kind == kind))
                    results.Add(await ApplyAsync(item, content));
            }

            lock (this) _session = null;
            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static readonly string[] ApplyOrder =
    [
        RestoreItemKind.LanguagePack, RestoreItemKind.Variables, RestoreItemKind.Automation, RestoreItemKind.Profile, RestoreItemKind.ProfileTree,
        RestoreItemKind.Preferences, RestoreItemKind.Device, RestoreItemKind.PluginSettings,
    ];

    // ---- compare ----------------------------------------------------------------------------------------------------------------

    private (List<RestoreItem> Items, List<RestoreWarning> Warnings) Compare(BackupFileContent backup)
    {
        var content = backup.Content;
        var here = collector.Collect();
        var items = new List<RestoreItem>();
        var warnings = new List<RestoreWarning>();

        foreach (var profile in content.Profiles.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
            items.Add(CompareProfile(profile, profiles.Get(profile.Id)));

        if (content.ProfileTree.Count > 0)
            items.Add(new RestoreItem(RestoreItemKind.ProfileTree, "", "", Equal(content.ProfileTree, here.ProfileTree) ? RestoreState.Same : RestoreState.Different));

        if (content.Preferences is { } prefs)
        {
            var differing = DifferingSettings(prefs, here.Preferences!);
            items.Add(new RestoreItem(RestoreItemKind.Preferences, "", "", differing.Count == 0 ? RestoreState.Same : RestoreState.Different) { Names = differing });
            if (prefs.AllowUnencrypted && !here.Preferences!.AllowUnencrypted) warnings.Add(new RestoreWarning("unencryptedKept", []));
            if (!IsBuiltInLanguage(prefs.Language) && !here.LanguagePacks.ContainsKey(prefs.Language) && !content.LanguagePacks.ContainsKey(prefs.Language))
                warnings.Add(new RestoreWarning("languagePackMissing", [prefs.Language]));
        }

        if (content.Variables.Count > 0)
            items.Add(CompareVariables(content.Variables, here.Variables, warnings));

        if (content.AutomationRules.Count > 0)
            items.Add(CompareAutomation(content.AutomationRules, here.AutomationRules));

        var paired = here.Devices.ToDictionary(d => d.Id, StringComparer.Ordinal);
        foreach (var device in content.Devices)
        {
            if (!paired.TryGetValue(device.Id, out var current)) { warnings.Add(new RestoreWarning("deviceNotPaired", [device.Name])); continue; }
            var fields = new List<string>();
            if (device.AssignedProfileId != current.AssignedProfileId) fields.Add("assignedProfileId");
            if (device.FollowActiveWindow != current.FollowActiveWindow) fields.Add("followActiveWindow");
            if (device.AutoSwitchLocked != current.AutoSwitchLocked) fields.Add("autoSwitchLocked");
            items.Add(new RestoreItem(RestoreItemKind.Device, device.Id, current.Name, fields.Count == 0 ? RestoreState.Same : RestoreState.Different) { Names = fields });
        }

        foreach (var (pluginId, settings) in content.PluginSettings.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!here.PluginSettings.TryGetValue(pluginId, out var current)) { warnings.Add(new RestoreWarning("pluginSettingsNotRunning", [pluginId])); continue; }
            items.Add(new RestoreItem(RestoreItemKind.PluginSettings, pluginId, pluginId, JsonNode.DeepEquals(settings, current) ? RestoreState.Same : RestoreState.Different));
        }

        foreach (var (tag, text) in content.LanguagePacks.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var existing = here.LanguagePacks.GetValueOrDefault(tag);
            var state = existing is null ? RestoreState.New : SameJson(existing, text) ? RestoreState.Same : RestoreState.Different;
            items.Add(new RestoreItem(RestoreItemKind.LanguagePack, tag, PackName(text) ?? tag, state) { HereVersion = existing is null ? null : PackVersion(existing), BackupVersion = PackVersion(text) });
        }

        foreach (var plugin in plugins.MissingPlugins(new ProfilePackageManifest(1, "", backup.Manifest.CreatedAt, backup.Manifest.ServerVersion, backup.Manifest.RequiredPlugins)))
            warnings.Add(new RestoreWarning("missingPlugin", [plugin.Name]));

        var known = dispatcher.Handlers.Select(h => h.Type).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var type in content.Profiles.SelectMany(ProfilePackage.ActionTypes).Distinct(StringComparer.OrdinalIgnoreCase).Where(t => !known.Contains(t)))
            warnings.Add(new RestoreWarning("unknownActionType", [type]));

        warnings.AddRange(ProfileReferences.MissingFiles(content.Profiles, dispatcher.Handlers)
            .Select(f => new RestoreWarning("missingFile", [f.Page, f.Widget, f.Path])));

        return (items, warnings);
    }

    private RestoreItem CompareProfile(Profile backup, Profile? current)
    {
        var backupWidgets = backup.Pages.Sum(p => p.Widgets.Count);
        if (current is null)
            return new RestoreItem(RestoreItemKind.Profile, backup.Id, backup.Name, RestoreState.New) { BackupPages = backup.Pages.Count, BackupWidgets = backupWidgets };

        // The stored profile goes through the same checks as the one in the file, so a difference is a real one.
        var normalized = ProfilePackage.ParseProfile(JsonSerializer.SerializeToUtf8Bytes(current, Json));
        var same = Equal(backup, normalized);
        var changedPages = new List<string>();
        if (!same)
        {
            var hereById = normalized.Pages.ToDictionary(p => p.Id, StringComparer.Ordinal);
            foreach (var page in backup.Pages)
                if (!hereById.TryGetValue(page.Id, out var other) || !Equal(page, other)) changedPages.Add(page.Name);
            if (changedPages.Count == 0 && backup.Name != normalized.Name) changedPages.Add(backup.Name);
        }
        return new RestoreItem(RestoreItemKind.Profile, backup.Id, backup.Name, same ? RestoreState.Same : RestoreState.Different)
        {
            Names = [.. changedPages.Take(10)],
            HerePages = current.Pages.Count,
            HereWidgets = current.Pages.Sum(p => p.Widgets.Count),
            BackupPages = backup.Pages.Count,
            BackupWidgets = backupWidgets,
        };
    }

    private static RestoreItem CompareVariables(List<UserVariable> backup, List<UserVariable> here, List<RestoreWarning> warnings)
    {
        int added = 0, changed = 0, skipped = 0;
        foreach (var variable in backup)
        {
            var existing = here.FirstOrDefault(v => v.Name.Equals(variable.Name, StringComparison.OrdinalIgnoreCase));
            if (existing is null) added++;
            else if (existing.Type != variable.Type) { skipped++; warnings.Add(new RestoreWarning("variableTypeConflict", [variable.Name])); }
            else if (Describe(existing) != Describe(variable)) changed++;
        }
        var state = added + changed == 0 ? RestoreState.Same : RestoreState.Different;
        return new RestoreItem(RestoreItemKind.Variables, "", "", state) { Added = added, Changed = changed, Skipped = skipped };
    }

    /// <summary>A rule counts as changed when anything but its on/off switch differs, since a restored rule always comes back switched off.</summary>
    private static RestoreItem CompareAutomation(List<AutomationRule> backup, List<AutomationRule> here)
    {
        int added = 0, changed = 0;
        foreach (var rule in backup)
        {
            var existing = here.FirstOrDefault(r => r.Id == rule.Id);
            if (existing is null) added++;
            else if (!Equal(SwitchedOff(rule), SwitchedOff(existing))) changed++;
        }
        return new RestoreItem(RestoreItemKind.Automation, "", "", added + changed == 0 ? RestoreState.Same : RestoreState.Different) { Added = added, Changed = changed };
    }

    private static AutomationRule SwitchedOff(AutomationRule rule)
    {
        var copy = JsonSerializer.Deserialize<AutomationRule>(JsonSerializer.SerializeToUtf8Bytes(rule, Json), Json) ?? new AutomationRule();
        copy.Enabled = false;
        return copy;
    }

    private static string Describe(UserVariable v) =>
        JsonSerializer.Serialize(new { v.Type, Initial = UserVariables.TryConvert(v.Type, v.Initial, out var value) ? value : null, v.Keep, v.Description }, Json);

    private static List<string> DifferingSettings(AppPreferences backup, AppPreferences here)
    {
        var a = JsonSerializer.SerializeToNode(backup, Json) as JsonObject ?? [];
        var b = JsonSerializer.SerializeToNode(here, Json) as JsonObject ?? [];
        return [.. a.Select(p => p.Key).Union(b.Select(p => p.Key)).Where(k => !JsonNode.DeepEquals(a[k], b[k]))];
    }

    private static bool Equal<T>(T a, T b) => JsonNode.DeepEquals(JsonSerializer.SerializeToNode(a, Json), JsonSerializer.SerializeToNode(b, Json));

    private static bool IsBuiltInLanguage(string language) => language is "tr" or "en";

    private static bool SameJson(string a, string b)
    {
        try { return JsonNode.DeepEquals(JsonNode.Parse(a), JsonNode.Parse(b)); }
        catch (JsonException) { return false; }
    }

    private static string? PackName(string json) => PackMeta(json)?["name"]?.GetValue<string>();

    private static int? PackVersion(string json) => PackMeta(json)?["version"] is JsonValue v && v.TryGetValue<int>(out var n) ? n : null;

    private static JsonNode? PackMeta(string json)
    {
        try { return JsonNode.Parse(json)?["meta"]; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return null; }
    }

    // ---- apply ------------------------------------------------------------------------------------------------------------------

    private async Task<RestoreItemResult> ApplyAsync(RestoreItem item, BackupContent content)
    {
        var warnings = new List<RestoreWarning>();
        try
        {
            switch (item.Kind)
            {
                case RestoreItemKind.LanguagePack:
                    languages.Save(item.Key, content.LanguagePacks[item.Key]);
                    break;
                case RestoreItemKind.Variables:
                    ApplyVariables(content.Variables, warnings);
                    break;
                case RestoreItemKind.Automation:
                    ApplyAutomation(content.AutomationRules);
                    break;
                case RestoreItemKind.Profile:
                    var profile = content.Profiles.First(p => p.Id == item.Key);
                    profiles.Save(profile);
                    await profileChanged(profile);
                    break;
                case RestoreItemKind.ProfileTree:
                    tree.Save([.. content.ProfileTree]);
                    break;
                case RestoreItemKind.Preferences:
                    ApplyPreferences(content.Preferences!, warnings);
                    break;
                case RestoreItemKind.Device:
                    ApplyDevice(content.Devices.First(d => d.Id == item.Key), warnings);
                    break;
                case RestoreItemKind.PluginSettings:
                    ApplyPluginSettings(item.Key, content.PluginSettings[item.Key]);
                    break;
            }
            return new RestoreItemResult(item.Kind, item.Key, true, null, warnings);
        }
        catch (Exception ex) when (ex is InvalidOperationException or LanguagePackException or IOException or ArgumentException or UnauthorizedAccessException)
        {
            return new RestoreItemResult(item.Kind, item.Key, false, ex.Message, warnings);
        }
    }

    /// <summary>Variables are merged, never removed: a backup variable is added or replaces the definition of the same name and type; one with another type is skipped.</summary>
    private void ApplyVariables(List<UserVariable> backup, List<RestoreWarning> warnings)
    {
        var merged = variables.List().ToList();
        foreach (var variable in backup)
        {
            var index = merged.FindIndex(v => v.Name.Equals(variable.Name, StringComparison.OrdinalIgnoreCase));
            if (index < 0) merged.Add(variable);
            else if (merged[index].Type == variable.Type) merged[index] = variable with { Name = merged[index].Name };
            else warnings.Add(new RestoreWarning("variableTypeConflict", [variable.Name]));
        }
        if (!variables.TryReplace(merged, out var error)) throw new InvalidOperationException(error);
    }

    /// <summary>Rules are merged by id, never removed, and every restored rule is saved switched off: a restore must not start actions by itself. The pause switch keeps its value.</summary>
    private void ApplyAutomation(List<AutomationRule> backup)
    {
        var merged = automation.List().ToList();
        foreach (var rule in backup)
        {
            var off = SwitchedOff(rule);
            var index = merged.FindIndex(r => r.Id == rule.Id);
            if (index < 0) merged.Add(off);
            else merged[index] = off;
        }
        if (!automation.TryReplace(merged, automation.Paused, out var error)) throw new InvalidOperationException(error);
    }

    /// <summary>A restore never turns on unencrypted connections: that setting keeps its current value when the backup would switch it on.</summary>
    private void ApplyPreferences(AppPreferences backup, List<RestoreWarning> warnings)
    {
        var current = preferences.Get();
        var restored = JsonSerializer.Deserialize<AppPreferences>(JsonSerializer.SerializeToUtf8Bytes(backup, Json), Json) ?? new AppPreferences();
        if (restored.AllowUnencrypted && !current.AllowUnencrypted)
        {
            restored.AllowUnencrypted = false;
            warnings.Add(new RestoreWarning("unencryptedKept", []));
        }
        preferences.Save(restored);
    }

    private void ApplyDevice(BackupDevice device, List<RestoreWarning> warnings)
    {
        var profileId = device.AssignedProfileId;
        if (profileId is not null && profiles.Get(profileId) is null)
        {
            profileId = null;
            warnings.Add(new RestoreWarning("deviceProfileMissing", [device.Name]));
        }
        devices.AssignProfile(device.Id, profileId);
        devices.SetFollowActiveWindow(device.Id, device.FollowActiveWindow);
        devices.SetAutoSwitchLocked(device.Id, device.AutoSwitchLocked);
    }

    private void ApplyPluginSettings(string pluginId, JsonObject settings)
    {
        var page = plugins.GetSettingsPage(pluginId) ?? throw new InvalidOperationException("The plugin is not running here.");
        var values = (JsonObject)settings.DeepClone();
        PluginSettingsSecrets.KeepStoredPasswords(page.Fields, values, page.Load());
        page.Save(values);
    }
}

using System.Text.Json;
using MacroGrid.Core.Model;
using MacroGrid.Protocol;

namespace MacroGrid.Core.Profiles;

/// <summary>Persists the root profile tree (docs/plans/hierarchy-tree-and-folders-plan.md) as one JSON
/// file, write-then-rename like PreferencesStore/ProfileStore. Profiles are one file each and never all
/// loaded together outside this store's own normalization pass, so their arrangement has to live
/// separately rather than on each Profile.</summary>
public sealed class ProfileTreeStore
{
    private static readonly JsonSerializerOptions FileJson = new(ProtocolJson.Options) { WriteIndented = true };

    private readonly string _path;
    private readonly Lock _lock = new();
    private List<ProfileTreeNode> _nodes = [];

    public ProfileTreeStore(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "profile-tree.json");
        Load();
    }

    /// <summary>The tree, repaired against the current profile list: a profile missing from the tree is
    /// appended at the root (sorted by name, after everything already placed); a node naming a profile
    /// that no longer exists is dropped, recursively. Self-heals the stored file when it had drifted, so a
    /// later read (or another process' copy of it) never re-derives a different repair.</summary>
    public List<ProfileTreeNode> GetNormalized(IReadOnlyList<Profile> profiles)
    {
        lock (_lock)
        {
            var knownIds = new HashSet<string>(profiles.Select(p => p.Id));
            var seen = new HashSet<string>();
            var repaired = Prune(_nodes, knownIds, seen);

            var missing = profiles.Where(p => !seen.Contains(p.Id)).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
            repaired.AddRange(missing.Select(p => new ProfileTreeNode { Type = "profile", Id = p.Id }));

            if (!Same(repaired, _nodes)) SaveLocked(repaired);
            return repaired;
        }
    }

    public void Save(List<ProfileTreeNode> nodes)
    {
        lock (_lock) SaveLocked(nodes);
    }

    private void SaveLocked(List<ProfileTreeNode> nodes)
    {
        var json = JsonSerializer.Serialize(nodes, FileJson);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _path, overwrite: true);
        _nodes = nodes;
    }

    private static List<ProfileTreeNode> Prune(List<ProfileTreeNode> nodes, HashSet<string> knownIds, HashSet<string> seen)
    {
        var result = new List<ProfileTreeNode>();
        foreach (var node in nodes)
        {
            if (node.Type == "folder")
            {
                var children = Prune(node.Children, knownIds, seen);
                result.Add(new ProfileTreeNode { Type = "folder", Id = node.Id, Name = node.Name, Children = children });
            }
            else if (knownIds.Contains(node.Id) && seen.Add(node.Id))
            {
                result.Add(node);
            }
        }
        return result;
    }

    private static bool Same(List<ProfileTreeNode> a, List<ProfileTreeNode> b) =>
        JsonSerializer.Serialize(a, FileJson) == JsonSerializer.Serialize(b, FileJson);

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var nodes = JsonSerializer.Deserialize<List<ProfileTreeNode>>(File.ReadAllText(_path), FileJson);
            if (nodes is not null) _nodes = nodes;
        }
        catch (JsonException)
        {
            File.Move(_path, _path + ".broken", overwrite: true);
        }
    }
}

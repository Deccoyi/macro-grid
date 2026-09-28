namespace MacroGrid.Core.Model;

/// <summary>One node of a profile's page tree (docs/plans/hierarchy-tree-and-folders-plan.md): how the
/// Hierarchy panel arranges and orders this profile's pages into folders. `Pages` stays the one place page
/// data lives — this only arranges it. A single class with a string discriminator (rather than two
/// polymorphic subtypes) keeps the JSON shape simple: a "page" node only ever sets `Id`; a "folder" node
/// sets `Id`, `Name` and `Children`. The editor is responsible for normalizing this against the profile's
/// actual `Pages` on every load and mutation (a page missing from the tree, an unknown id, or a duplicate
/// is repaired there) — the server stores and returns it as-is.</summary>
public sealed class PageTreeNode
{
    /// <summary>"page" or "folder".</summary>
    public string Type { get; set; } = "page";
    public string Id { get; set; } = "";
    /// <summary>Folder nodes only.</summary>
    public string? Name { get; set; }
    /// <summary>Folder nodes only.</summary>
    public List<PageTreeNode> Children { get; set; } = [];
}

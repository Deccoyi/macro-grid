namespace MacroGrid.Core.Profiles;

/// <summary>One node of the root profile tree (docs/plans/hierarchy-tree-and-folders-plan.md): how the
/// Hierarchy panel arranges profiles into folders, stored separately from any one profile (profiles are
/// loaded one at a time; this arrangement needs all of them at once). Same shape convention as
/// MacroGrid.Core.Model.PageTreeNode: a string discriminator instead of polymorphic subtypes.</summary>
public sealed class ProfileTreeNode
{
    /// <summary>"profile" or "folder".</summary>
    public string Type { get; set; } = "profile";
    public string Id { get; set; } = "";
    /// <summary>Folder nodes only.</summary>
    public string? Name { get; set; }
    /// <summary>Folder nodes only.</summary>
    public List<ProfileTreeNode> Children { get; set; } = [];
}

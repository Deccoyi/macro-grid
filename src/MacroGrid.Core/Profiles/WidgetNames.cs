using MacroGrid.Core.Model;

namespace MacroGrid.Core.Profiles;

/// <summary>
/// The instance name of a widget (<see cref="Widget.Name"/>): 1 to 64 characters, unique on its page (compared without case), shown in messages and
/// pickers and never used as a reference. Old profiles have no names or repeated ones; <see cref="Ensure"/> fills and repairs them the same way
/// every time (it depends only on page and widget order). The editor has a mirror of these rules in <c>widgetNames.ts</c>; both read
/// <c>tests/shared/widget-name-cases.json</c>.
/// </summary>
public static class WidgetNames
{
    public const int MaxLength = 64;

    /// <summary>Control characters removed, trimmed, cut to <see cref="MaxLength"/>; null when nothing is left.</summary>
    public static string? Normalize(string? name)
    {
        if (name is null) return null;
        var cleaned = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (cleaned.Length > MaxLength) cleaned = cleaned[..MaxLength].TrimEnd();
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>The default name for a new widget: <c>Prefix_n</c> with the smallest n of 1 or more that is not in <paramref name="taken"/>.</summary>
    public static string Default(string type, IEnumerable<string?> taken)
    {
        var used = new HashSet<string>(taken.Where(n => n is not null).Select(n => n!), StringComparer.OrdinalIgnoreCase);
        var prefix = Prefix(type);
        for (var n = 1; ; n++)
        {
            var candidate = $"{prefix}_{n}";
            if (!used.Contains(candidate)) return candidate;
        }
    }

    /// <summary>The wanted name made unique: itself when free, else with <c>_2</c>, <c>_3</c>, ... appended (the base is cut so the result fits).</summary>
    public static string Unique(string name, IEnumerable<string?> taken)
    {
        var used = new HashSet<string>(taken.Where(n => n is not null).Select(n => n!), StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(name)) return name;
        for (var n = 2; ; n++)
        {
            var suffix = $"_{n}";
            var baseName = name.Length + suffix.Length > MaxLength ? name[..(MaxLength - suffix.Length)] : name;
            var candidate = baseName + suffix;
            if (!used.Contains(candidate)) return candidate;
        }
    }

    /// <summary>Gives every widget of every page a valid, page-unique name. Existing valid names are never changed; the first of two equal
    /// names keeps it. Returns true when anything changed.</summary>
    public static bool Ensure(Profile profile)
    {
        var changed = false;
        foreach (var page in profile.Pages) changed |= Ensure(page);
        return changed;
    }

    public static bool Ensure(Page page)
    {
        var changed = false;
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First the names that exist: the first widget with a name keeps it (normalized); every later widget with the same name is a repeat.
        var repeats = new List<(Widget Widget, string Wanted)>();
        foreach (var widget in page.Widgets)
        {
            var wanted = Normalize(widget.Name);
            if (wanted is null) continue;
            if (taken.Add(wanted))
            {
                if (wanted != widget.Name) { widget.Name = wanted; changed = true; }
            }
            else repeats.Add((widget, wanted));
        }
        // A repeat gets a suffix, and never one that another widget already uses.
        foreach (var (widget, wanted) in repeats)
        {
            var name = Unique(wanted, taken);
            taken.Add(name);
            widget.Name = name;
            changed = true;
        }

        // Then the widgets without a name, so a default never takes a name another widget already has.
        foreach (var widget in page.Widgets.Where(w => Normalize(w.Name) is null))
        {
            var name = Default(widget.Type, taken);
            taken.Add(name);
            widget.Name = name;
            changed = true;
        }
        return changed;
    }

    private static string Prefix(string type) => type switch
    {
        WidgetTypes.Button => "Button",
        WidgetTypes.Toggle => "Toggle",
        WidgetTypes.Label => "Label",
        WidgetTypes.Slider => "Slider",
        WidgetTypes.Knob => "Knob",
        WidgetTypes.Image => "Image",
        WidgetTypes.Web => "Web",
        _ => "Widget",
    };
}

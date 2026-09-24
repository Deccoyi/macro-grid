using MacroGrid.Core.Model;

namespace MacroGrid.Core.Profiles;

/// <summary>
/// Validates a profile edited in the editor before it is persisted: grid bounds and widget
/// overlap. The editor also checks this client-side for instant feedback while dragging, but the
/// server re-checks on save since it is the only thing that can actually reject a bad write.
/// </summary>
public static class ProfileValidator
{
    private const int MaxGridSize = 24;

    public static bool Validate(Profile profile, out string? error)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            error = "The profile name cannot be empty.";
            return false;
        }

        if (profile.Pages.Count == 0)
        {
            error = "A profile needs at least one page.";
            return false;
        }

        if (!ValidateAppMatches(profile, out error))
            return false;

        var pageIds = new HashSet<string>();
        foreach (var page in profile.Pages)
        {
            if (!pageIds.Add(page.Id))
            {
                error = $"Duplicate page id: {page.Id}";
                return false;
            }

            if (page.Cols < 1 || page.Cols > MaxGridSize || page.Rows < 1 || page.Rows > MaxGridSize)
            {
                error = $"The grid size of page '{page.Name}' must be between 1 and {MaxGridSize}.";
                return false;
            }

            if (!ValidatePlacement(page, out error))
                return false;
        }

        error = null;
        return true;
    }

    private static bool ValidateAppMatches(Profile profile, out string? error)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var match in profile.AppMatches)
        {
            if (string.IsNullOrWhiteSpace(match.ProcessName))
            {
                error = "An auto-switch rule needs an application name (for example \"Player.exe\").";
                return false;
            }

            var key = match.ProcessName.Trim() + "\u0000" + (match.TitleContains?.Trim() ?? "");
            if (!seen.Add(key))
            {
                error = $"Duplicate auto-switch rule for '{match.ProcessName}'.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool ValidatePlacement(Page page, out string? error)
    {
        var widgetIds = new HashSet<string>();
        var occupied = new bool[page.Cols, page.Rows];

        foreach (var widget in page.Widgets)
        {
            if (!widgetIds.Add(widget.Id))
            {
                error = $"Duplicate widget id: {widget.Id}";
                return false;
            }

            if (widget.W < 1 || widget.H < 1)
            {
                error = $"Widget '{widget.Id}' must be at least 1 wide and 1 high.";
                return false;
            }

            if (widget.X < 0 || widget.Y < 0 || widget.X + widget.W > page.Cols || widget.Y + widget.H > page.Rows)
            {
                error = $"Widget '{widget.Id}' extends beyond the page.";
                return false;
            }

            for (var x = widget.X; x < widget.X + widget.W; x++)
            {
                for (var y = widget.Y; y < widget.Y + widget.H; y++)
                {
                    if (occupied[x, y])
                    {
                        error = $"Widget '{widget.Id}' overlaps another widget at ({x}, {y}).";
                        return false;
                    }
                    occupied[x, y] = true;
                }
            }
        }

        error = null;
        return true;
    }
}

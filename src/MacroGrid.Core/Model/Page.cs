namespace MacroGrid.Core.Model;

public sealed class Page
{
    public string Id { get; set; } = Profile.NewId();
    public string Name { get; set; } = "Page";
    public int Cols { get; set; } = 4;
    public int Rows { get; set; } = 3;
    /// <summary>Gap between grid cells, in CSS px. Matches the renderer's own default so old profiles without this field still look the same.</summary>
    public int Gap { get; set; } = 10;
    /// <summary>Padding around the grid, in CSS px.</summary>
    public int Padding { get; set; } = 0;
    /// <summary>How the grid is placed within the page when it doesn't fill the available space: "start" | "center" | "end".</summary>
    public string Alignment { get; set; } = "center";
    public List<Widget> Widgets { get; set; } = [];

    public Widget? FindWidget(string widgetId) => Widgets.FirstOrDefault(w => w.Id == widgetId);
}

namespace MacroStation.Protocol;

public static class MessageTypes
{
    // client -> server
    public const string Hello = "hello";
    public const string WidgetDown = "widget.down";
    public const string WidgetUp = "widget.up";
    public const string WidgetLongPress = "widget.longPress";
    public const string WidgetDoubleTap = "widget.doubleTap";
    public const string WidgetValue = "widget.value";
    public const string PageChange = "page.change";
    public const string ProfileChange = "profile.change";

    // server -> client
    public const string Welcome = "welcome";
    public const string LayoutFull = "layout.full";
    public const string PageShow = "page.show";
    public const string WidgetState = "widget.state";
    public const string Error = "error";
}

namespace MacroGrid.Protocol;

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
    public const string PageNext = "page.next";
    public const string PagePrev = "page.prev";
    public const string ProfileChange = "profile.change";
    public const string ProfileLock = "profile.lock";
    public const string AssetGet = "asset.get";
    public const string PluginWidgetRequest = "plugin.widget.request";
    public const string PluginWidgetError = "plugin.widget.error";

    // server -> client
    public const string Welcome = "welcome";
    public const string LayoutFull = "layout.full";
    public const string LayoutPatch = "layout.patch";
    public const string Asset = "asset";
    public const string PageShow = "page.show";
    public const string WidgetState = "widget.state";
    public const string ProfilesList = "profiles.list";
    public const string PluginWidgetReply = "plugin.widget.reply";
    public const string PluginWidgetVars = "plugin.widget.vars";
    public const string PluginWidgetEvent = "plugin.widget.event";
    public const string Error = "error";
}

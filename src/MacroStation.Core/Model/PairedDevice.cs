namespace MacroStation.Core.Model;

/// <summary>A device that has completed PIN pairing at least once. <see cref="Token"/> is what it sends
/// in every future `hello` instead of the PIN — losing it (uninstall, cache clear) means pairing again.</summary>
public sealed class PairedDevice
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Token { get; set; }
    public DateTimeOffset PairedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}

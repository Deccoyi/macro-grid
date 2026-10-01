using System.Text;
using System.Text.Json;
using MacroGrid.Core.Plugins.Distribution;

namespace MacroGrid.Tests;

public sealed class SignedCatalogFileTests
{
    private static string Payload(string kind = "revoked", string sequence = "3") =>
        $$"""{ "kind": "{{kind}}", "sequence": {{sequence}}, "issuedAt": "2026-10-01T12:00:00Z", "formatVersion": 1, "plugins": [] }""";

    private static bool Read(byte[] envelope, string kind, out SignedCatalogDocument doc, long max = 2 * 1024 * 1024) =>
        SignedCatalogFile.TryRead(envelope, kind, TestPluginSigning.PublicKey, out doc, max);

    [Fact]
    public void A_valid_envelope_is_read_with_its_header()
    {
        Assert.True(Read(TestPluginSigning.SignEnvelope(Payload()), "revoked", out var doc));
        Assert.Equal("revoked", doc.Kind);
        Assert.Equal(3, doc.Sequence);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero), doc.IssuedAt);
        Assert.Contains("\"plugins\"", Encoding.UTF8.GetString(doc.Payload));
    }

    [Fact]
    public void A_changed_payload_byte_fails()
    {
        var envelope = TestPluginSigning.SignEnvelope(Payload());
        using var json = JsonDocument.Parse(envelope);
        var payload = Convert.FromBase64String(json.RootElement.GetProperty("payload").GetString()!);
        payload[^3] ^= 1;
        var tampered = JsonSerializer.SerializeToUtf8Bytes(new { payload = Convert.ToBase64String(payload), signature = json.RootElement.GetProperty("signature").GetString() });

        Assert.False(Read(tampered, "revoked", out _));
    }

    [Fact]
    public void A_signature_by_another_key_fails()
    {
        using var other = TestPluginSigning.NewOtherKey();
        Assert.False(Read(TestPluginSigning.SignEnvelope(Payload(), other), "revoked", out _));
    }

    [Fact]
    public void The_wrong_kind_fails()
    {
        Assert.False(Read(TestPluginSigning.SignEnvelope(Payload("index")), "revoked", out _));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-4")]
    [InlineData("\"3\"")]
    [InlineData("1.5")]
    public void A_missing_or_bad_sequence_fails(string sequence)
    {
        Assert.False(Read(TestPluginSigning.SignEnvelope(Payload(sequence: sequence)), "revoked", out _));
        Assert.False(Read(TestPluginSigning.SignEnvelope("""{ "kind": "revoked" }"""), "revoked", out _));
    }

    [Fact]
    public void Garbage_is_refused_without_throwing()
    {
        Assert.False(Read("not json"u8.ToArray(), "revoked", out _));
        Assert.False(Read("[]"u8.ToArray(), "revoked", out _));
        Assert.False(Read("""{ "payload": "***", "signature": "x" }"""u8.ToArray(), "revoked", out _));
        Assert.False(Read("""{ "payload": 5, "signature": 6 }"""u8.ToArray(), "revoked", out _));
        Assert.False(Read([], "revoked", out _));
    }

    [Fact]
    public void An_oversized_input_is_refused()
    {
        var envelope = TestPluginSigning.SignEnvelope(Payload());
        Assert.True(Read(envelope, "revoked", out _, max: envelope.Length));
        Assert.False(Read(envelope, "revoked", out _, max: envelope.Length - 1));
    }
}

public sealed class PluginRevocationListTests
{
    private static PluginRevocationList? Parse(string json) => PluginRevocationList.Parse(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Versions_are_matched_as_exact_text_and_the_id_ignores_case()
    {
        var list = Parse("""{ "formatVersion": 1, "plugins": [ { "id": "Example", "versions": ["1.2.0"], "reason": "Bad." } ] }""")!;

        Assert.Equal("Bad.", list.Find("example", "1.2.0")!.Reason);
        Assert.Null(list.Find("example", "1.2.1"));
        Assert.Null(list.Find("other", "1.2.0"));
    }

    [Fact]
    public void An_entry_without_versions_covers_every_version_and_gets_the_default_reason()
    {
        var list = Parse("""{ "formatVersion": 1, "plugins": [ { "id": "x" } ] }""")!;

        Assert.Equal(PluginRevocationList.DefaultReason, list.Find("x", "9.9.9")!.Reason);
    }

    [Fact]
    public void An_odd_entry_is_skipped_and_the_rest_kept()
    {
        var list = Parse("""{ "formatVersion": 1, "plugins": [ 5, { "id": 3 }, { "id": "a", "versions": "1.0" }, { "id": "b", "versions": [] }, { "id": "ok" } ] }""")!;

        Assert.Equal("ok", Assert.Single(list.Entries).Id);
    }

    [Fact]
    public void The_reason_is_cleaned_and_cut()
    {
        var list = Parse($$"""{ "formatVersion": 1, "plugins": [ { "id": "a", "reason": "  hi\u0001 {{new string('x', 300)}}" } ] }""")!;

        var reason = list.Entries[0].Reason;
        Assert.Equal(PluginRevocationList.MaxReasonLength, reason.Length);
        Assert.StartsWith("hi x", reason);
    }

    [Fact]
    public void Another_format_version_is_refused_as_a_whole()
    {
        Assert.Null(Parse("""{ "formatVersion": 2, "plugins": [] }"""));
        Assert.Null(Parse("""{ "plugins": [] }"""));
        Assert.Null(Parse("nope"));
    }

    [Fact]
    public void At_most_500_entries_are_kept()
    {
        var entries = string.Join(",", Enumerable.Range(0, 600).Select(i => $$"""{ "id": "p{{i}}" }"""));
        Assert.Equal(PluginRevocationList.MaxEntries, Parse($$"""{ "formatVersion": 1, "plugins": [ {{entries}} ] }""")!.Entries.Count);
    }
}

using System.Text;
using MacroGrid.Core.Preferences;

namespace MacroGrid.Tests;

public sealed class AgreementAcceptanceTests
{
    private static string Hash(string text) => AgreementAcceptance.HashOf(Encoding.ASCII.GetBytes(text));

    [Fact]
    public void The_hash_is_lower_case_sha256_hex_and_stable()
    {
        var hash = Hash("agreement v1");

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash.ToLowerInvariant(), hash);
        Assert.Equal(hash, Hash("agreement v1"));
    }

    [Fact]
    public void Any_edit_of_the_text_changes_the_hash()
    {
        Assert.NotEqual(Hash("agreement v1"), Hash("agreement v1 "));
        Assert.NotEqual(Hash("agreement v1"), Hash("Agreement v1"));
    }

    [Fact]
    public void The_same_hash_is_accepted_whatever_its_letter_case()
    {
        var shipped = Hash("agreement v1");

        Assert.True(AgreementAcceptance.IsAccepted(shipped, shipped));
        Assert.True(AgreementAcceptance.IsAccepted(shipped.ToUpperInvariant(), shipped));
        Assert.True(AgreementAcceptance.IsAccepted("  " + shipped + " ", shipped));
    }

    [Fact]
    public void A_changed_text_or_no_record_is_not_accepted()
    {
        var shipped = Hash("agreement v2");

        Assert.False(AgreementAcceptance.IsAccepted(Hash("agreement v1"), shipped));
        Assert.False(AgreementAcceptance.IsAccepted(null, shipped));
        Assert.False(AgreementAcceptance.IsAccepted("", shipped));
        Assert.False(AgreementAcceptance.IsAccepted("   ", shipped));
    }
}

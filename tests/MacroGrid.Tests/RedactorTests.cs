using System.Diagnostics;
using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Tests;

public class RedactorTests
{
    private static Redactor Make() => new(new RedactionContext(
        HomeFolder: @"C:\Users\Jane Doe",
        DataFolder: @"C:\Users\Jane Doe\AppData\Roaming\MacroGrid",
        UserName: "janedoe",
        MachineName: "OFFICE-PC",
        DeviceNames: ["Jane's Phone", "Tab (2)", "x"]));

    [Theory]
    [InlineData(@"Loaded C:\Users\Jane Doe\AppData\Roaming\MacroGrid\profiles\a.json", @"Loaded <data>\profiles\a.json")]
    [InlineData(@"Opened c:/users/jane doe/Documents/x.txt", @"Opened <home>/Documents/x.txt")]
    [InlineData(@"Opened D:\Users\Someone\Desktop\x.txt", @"Opened <home>\Desktop\x.txt")]
    [InlineData(@"Opened C:\Program Files\Tool\x.dll", @"Opened C:\Program Files\Tool\x.dll")]
    public void Known_and_user_folders_are_replaced(string input, string expected) => Assert.Equal(expected, Make().Line(input));

    [Theory]
    [InlineData("user janedoe on OFFICE-PC", "user <user> on <pc>")]
    [InlineData("Janedoes is not the user", "Janedoes is not the user")]
    [InlineData("host office-pc.local", "host <pc>.local")]
    public void User_and_machine_names_are_whole_words(string input, string expected) => Assert.Equal(expected, Make().Line(input));

    [Fact]
    public void Device_names_become_numbered_labels_and_may_hold_regex_characters()
    {
        var r = Make();
        Assert.Equal("paired <device 1> and <device 2>", r.Line("paired Jane's Phone and Tab (2)"));
        Assert.Equal("a x b", r.Line("a x b"));
    }

    [Theory]
    [InlineData("GET https://user:pw@host.example/p?id=7&key=abc", "GET https://host.example/p?id=<redacted>&key=<redacted>")]
    [InlineData("see https://example.com/docs", "see https://example.com/docs")]
    public void Addresses_lose_credentials_and_query_values(string input, string expected) => Assert.Equal(expected, Make().Line(input));

    [Theory]
    [InlineData("password=hunter2", "password=<redacted>")]
    [InlineData("apiKey: \"abc def\"", "apiKey: <redacted>")]
    [InlineData("settings.api_key = zzz", "settings.api_key = <redacted>")]
    [InlineData("Authorization: Bearer abc.def-123", "Authorization: <redacted> <redacted>")]
    [InlineData("Bearer abc123", "Bearer <redacted>")]
    [InlineData("pin=1234", "pin=<redacted>")]
    [InlineData("user_pin: 99", "user_pin: <redacted>")]
    [InlineData("mapping = 5", "mapping = 5")]
    [InlineData("Sound file not found: sound.wav", "Sound file not found: sound.wav")]
    public void Secrets_by_name_are_removed(string input, string expected) => Assert.Equal(expected, Make().Line(input));

    [Theory]
    [InlineData("from 192.168.1.20 to 10.0.0.5", "from <ip> to <ip>")]
    [InlineData("from 127.0.0.1", "from 127.0.0.1")]
    [InlineData("version 10.0.26200.1", "version 10.0.26200.1")]
    [InlineData("addr fe80::1ff:fe23:4567:890a", "addr <ip>")]
    [InlineData("loopback ::1", "loopback ::1")]
    [InlineData("at 12:30:45 today", "at 12:30:45 today")]
    [InlineData("call Foo::Bar now", "call Foo::Bar now")]
    [InlineData("mail jane@example.com now", "mail <email> now")]
    public void Ip_and_email_addresses_are_replaced_but_not_look_alikes(string input, string expected) => Assert.Equal(expected, Make().Line(input));

    [Fact]
    public void Long_key_like_runs_are_replaced_and_short_ids_stay()
    {
        var token = new string('a', 40);
        Assert.Equal("id <redacted:40>", Make().Line("id " + token));
        Assert.Equal("id a1b2c3d4e5f6", Make().Line("id a1b2c3d4e5f6"));
        Assert.Equal("2026-10-01T12:30:45.123Z ok", Make().Line("2026-10-01T12:30:45.123Z ok"));
    }

    [Fact]
    public void The_same_input_gives_the_same_output()
    {
        var line = @"C:\Users\Jane Doe\x janedoe 10.1.2.3 token=abc";
        Assert.Equal(Make().Line(line), Make().Line(line));
        Assert.Equal(Make().Line(line), Make().Line(Make().Line(line)));
    }

    [Fact]
    public void A_huge_line_returns_fast_and_is_cut()
    {
        var line = new string('a', 100_000) + " " + string.Concat(Enumerable.Repeat("1.2.3.", 20_000));
        var clock = Stopwatch.StartNew();
        var result = Make().Line(line);
        clock.Stop();
        Assert.True(clock.ElapsedMilliseconds < 2000, $"took {clock.ElapsedMilliseconds} ms");
        Assert.True(result.Length < Redactor.MaxLineLength + 20);
    }

    [Fact]
    public void Empty_context_still_applies_the_pattern_rules()
    {
        var r = new Redactor(new RedactionContext());
        Assert.Equal("password=<redacted> <ip>", r.Line("password=x 8.8.8.8"));
        Assert.Equal("", r.Line(""));
    }
}

using MacroGrid.Core.Plugins;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.PluginTool;

/// <summary>The plugin author's command line: check a plugin folder, package it, start it and try it out. Exit codes: 0 done, 1 the plugin has
/// errors or the command failed, 2 the command line was not understood.</summary>
public static class PluginToolApp
{
    public const int Ok = 0;
    public const int Failed = 1;
    public const int Usage = 2;

    private const string UsageText = """
        Usage: macrogrid-plugin <command> [options]

          validate <folder>     check a plugin folder (nothing is run)
        """;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            (args.Length == 0 ? error : output).WriteLine(UsageText);
            return args.Length == 0 ? Usage : Ok;
        }

        try
        {
            return args[0] switch
            {
                "validate" => Validate(args[1..], output, error),
                _ => UsageError($"Unknown command '{args[0]}'", error),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"error: {ex.Message}");
            return Failed;
        }
    }

    private static int UsageError(string message, TextWriter error)
    {
        error.WriteLine(message);
        error.WriteLine(UsageText);
        return Usage;
    }

    /// <summary>Prints the findings of the folder and the closing line. Returns the report so another command can go on.</summary>
    internal static PluginFolderReport Check(string folder, TextWriter output)
    {
        var report = PluginFolderValidator.Validate(folder, PluginSdk.Version);
        foreach (var finding in report.Findings)
            output.WriteLine($"{(finding.Level == PluginFindingLevel.Error ? "error" : "warning")}: {finding.Message}");

        var errors = report.Findings.Count(f => f.Level == PluginFindingLevel.Error);
        output.WriteLine(errors == 0 ? $"OK: {report.Manifest!.Id} {report.Manifest.Version}" : $"FAILED: {errors} error(s)");
        return report;
    }

    private static int Validate(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length != 1) return UsageError("validate takes one folder", error);
        return Check(args[0], output).HasErrors ? Failed : Ok;
    }
}

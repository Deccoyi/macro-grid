using System.Text;
using System.Text.Json;
using MacroGrid.Core.Plugins;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.PluginTool;

public static partial class PluginToolApp
{
    private static int New(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParse(args, ["--name", "--out"], [], out var plain, out var options, out var problem) || plain.Count != 1)
            return UsageError(problem ?? "new takes one plugin id", error);

        var id = plain[0];
        if (!PluginManager.IsValidPluginId(id))
        {
            error.WriteLine($"error: '{id}' is not a usable plugin id: use up to 64 letters, digits, '.', '-' or '_', starting with a letter or digit");
            return Failed;
        }

        var name = options.TryGetValue("--name", out var n) ? n[0] : id;
        var parent = options.TryGetValue("--out", out var o) ? o[0] : Directory.GetCurrentDirectory();
        var dir = Path.Combine(parent, id);
        if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any())
        {
            error.WriteLine($"error: {dir} already exists and is not empty");
            return Failed;
        }

        Directory.CreateDirectory(dir);
        var utf8 = new UTF8Encoding(false);
        File.WriteAllText(Path.Combine(dir, "plugin.json"), ManifestTemplate(id, name), utf8);
        File.WriteAllText(Path.Combine(dir, "index.js"), ScriptTemplate.Replace("{id}", id), utf8);
        File.WriteAllText(Path.Combine(dir, "README.md"), ReadmeTemplate.Replace("{name}", name).Replace("{id}", id), utf8);

        output.WriteLine($"created {dir}");
        output.WriteLine($"next: macrogrid-plugin validate {dir}");
        output.WriteLine($"      macrogrid-plugin run {dir} --action {id}.bump");
        return Ok;
    }

    private static string ManifestTemplate(string id, string name) =>
        "{\n"
        + $"  \"id\": {JsonSerializer.Serialize(id)},\n"
        + $"  \"name\": {JsonSerializer.Serialize(name)},\n"
        + "  \"description\": \"A small plugin made with the plugin tool.\",\n"
        + "  \"version\": \"0.1.0\",\n"
        + $"  \"minMacroGrid\": \"{PluginSdk.Version}\",\n"
        + "  \"entry\": \"index.js\",\n"
        + "  \"kind\": \"js\",\n"
        + "  \"permissions\": [\"variables\", \"actions\"]\n"
        + "}\n";

    private const string ScriptTemplate = """
        // A small JavaScript plugin. It publishes a counter variable that any widget can show ("Count: {{id}.count}")
        // and adds one action that bumps it. Everything a plugin can do goes through the global `host` object.

        let count = 0;

        host.variables.describe([
          { name: '{id}.count', description: 'How many times the counter was bumped', example: '3', category: 'Plugin' },
        ]);
        host.variables.set('{id}.count', count);

        host.settings.page([
          { key: 'step', label: 'Step', kind: 'Number', default: 1, min: 1, max: 100 },
        ]);

        host.registerAction({
          type: '{id}.bump',
          name: 'Bump the counter',
          category: 'Plugin',
          description: 'Adds the configured step to the {id}.count variable.',
          icon: 'plus',
          fields: [
            { key: 'times', label: 'Times', kind: 'Number', default: 1, min: 1, max: 10 },
          ],
          run(context, settings) {
            const step = host.settings.get().step || 1;
            count += step * (settings.times || 1);
            host.variables.set('{id}.count', count);
          },
        });

        // Timers need no permission. The shortest allowed interval is 100 ms.
        host.every(5000, () => {
          host.status('counter', 'Count ' + count, 'Ok');
        });
        """;

    private const string ReadmeTemplate = """
        # {name}

        A Macro Grid plugin. Check it, try it and package it with the plugin tool:

        ```
        macrogrid-plugin validate .
        macrogrid-plugin run . --action {id}.bump
        macrogrid-plugin pack .
        ```

        The package can be installed from the Plugins window with Install from Folder.
        """;
}

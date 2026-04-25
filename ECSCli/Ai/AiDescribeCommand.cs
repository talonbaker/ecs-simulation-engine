using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using APIFramework.Core;

namespace ECSCli.Ai;

/// <summary>
/// <c>ai describe --out &lt;path&gt;</c>
///
/// Emits an engine fact sheet in Markdown: every component type, every system
/// registration (name + phase), and every <c>SimConfig</c> key with its type
/// and current value. This file is designed to be prompt-cached across all
/// Sonnet and Haiku calls (Pillar D.1).
///
/// EXIT CODES
/// ──────────
/// 0  success — file written at <c>--out</c> path.
/// 1  unexpected error.
/// </summary>
public static class AiDescribeCommand
{
    public static Command Build()
    {
        var outOpt = new Option<FileInfo>(
            name: "--out",
            description: "Path to write the Markdown fact sheet to.")
        { IsRequired = true };

        var cmd = new Command("describe",
            "Emit engine fact sheet: component types, system registrations, SimConfig keys.");
        cmd.AddOption(outOpt);

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var outFile = ctx.ParseResult.GetValueForOption(outOpt);
            try
            {
                Run(outFile!);
                ctx.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ai describe] ERROR: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });

        return cmd;
    }

    // ── Implementation ────────────────────────────────────────────────────────

    internal static void Run(FileInfo outFile)
    {
        // Boot with a single human to keep startup fast.
        var sim = new SimulationBootstrapper("SimConfig.json", humanCount: 1);

        var sb = new StringBuilder();

        AppendHeader(sb);
        AppendSystems(sb, sim);
        AppendComponents(sb);
        AppendSimConfig(sb, sim.Config, string.Empty);

        var dir = outFile.Directory;
        if (dir != null && !dir.Exists)
            dir.Create();

        File.WriteAllText(outFile.FullName, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"[ai describe] Written: {outFile.FullName}");
    }

    // ── Sections ─────────────────────────────────────────────────────────────

    private static void AppendHeader(StringBuilder sb)
    {
        sb.AppendLine("# ECS Simulation Engine — Fact Sheet");
        sb.AppendLine();
        sb.AppendLine($"**SimVersion:** {SimVersion.Full}");
        sb.AppendLine($"**Generated:** {DateTimeOffset.UtcNow:O}");
        sb.AppendLine($"**TelemetrySchema:** world-state.schema.json v0.1.0");
        sb.AppendLine();
    }

    private static void AppendSystems(StringBuilder sb, SimulationBootstrapper sim)
    {
        sb.AppendLine("## Registered Systems");
        sb.AppendLine();
        sb.AppendLine("| # | System | Phase | PhaseOrder |");
        sb.AppendLine("|:--|:-------|:------|:-----------|");

        var regs = sim.Engine.Registrations;
        for (int i = 0; i < regs.Count; i++)
        {
            var r = regs[i];
            sb.AppendLine(
                $"| {i + 1} | `{r.System.GetType().Name}` | `{r.Phase}` | {(int)r.Phase} |");
        }

        sb.AppendLine();
        sb.AppendLine($"**Total:** {regs.Count} systems");
        sb.AppendLine();
    }

    private static void AppendComponents(StringBuilder sb)
    {
        sb.AppendLine("## Component Types");
        sb.AppendLine();
        sb.AppendLine("All `struct` types from the `APIFramework.Components` namespace.");
        sb.AppendLine();
        sb.AppendLine("| Component | Fields |");
        sb.AppendLine("|:----------|:-------|");

        var assembly = typeof(APIFramework.Components.MetabolismComponent).Assembly;
        var components = assembly.GetTypes()
            .Where(t => t.IsValueType
                     && !t.IsEnum
                     && t.Namespace == "APIFramework.Components")
            .OrderBy(t => t.Name)
            .ToList();

        foreach (var type in components)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => $"`{f.Name}: {FormatTypeName(f.FieldType)}`")
                .ToList();
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .Select(p => $"`{p.Name}: {FormatTypeName(p.PropertyType)}`")
                .ToList();
            var all = fields.Concat(props).ToList();

            sb.AppendLine(
                $"| `{type.Name}` | " +
                $"{(all.Count == 0 ? "*(tag — no fields)*" : string.Join(", ", all))} |");
        }

        sb.AppendLine();
        sb.AppendLine($"**Total:** {components.Count} component types");
        sb.AppendLine();
    }

    private static void AppendSimConfig(StringBuilder sb, object config, string prefix)
    {
        if (prefix.Length == 0)
        {
            sb.AppendLine("## SimConfig Keys");
            sb.AppendLine();
            sb.AppendLine("| Key Path | Type | Current Value |");
            sb.AppendLine("|:---------|:-----|:--------------|");
        }

        foreach (var prop in config.GetType()
                                   .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                   .Where(p => p.CanRead))
        {
            var path = prefix.Length == 0 ? prop.Name : $"{prefix}.{prop.Name}";
            var val  = prop.GetValue(config);

            if (val == null) continue;

            if (prop.PropertyType.IsValueType || prop.PropertyType == typeof(string))
            {
                sb.AppendLine(
                    $"| `{path}` | `{FormatTypeName(prop.PropertyType)}` | `{val}` |");
            }
            else if (val is System.Collections.IDictionary dict)
            {
                // Dictionary config values — emit entries inline rather than recursing
                var entries = new System.Collections.Generic.List<string>();
                foreach (System.Collections.DictionaryEntry entry in dict)
                    entries.Add($"{entry.Key}={entry.Value}");
                sb.AppendLine(
                    $"| `{path}` | `Dictionary` | `{{{string.Join(", ", entries)}}}` |");
            }
            else if (!prop.PropertyType.IsArray && prop.PropertyType.IsClass)
            {
                // Recurse into nested config objects.
                AppendSimConfig(sb, val, path);
            }
        }

        if (prefix.Length == 0)
            sb.AppendLine();
    }

    private static string FormatTypeName(Type t)
    {
        if (t == typeof(float))  return "float";
        if (t == typeof(double)) return "double";
        if (t == typeof(int))    return "int";
        if (t == typeof(bool))   return "bool";
        if (t == typeof(string)) return "string";
        if (t == typeof(Guid))   return "Guid";
        return t.Name;
    }
}

using System.Text.Json;
using Warden.Contracts;

namespace Warden.Orchestrator.Reports;

/// <summary>
/// Renders an in-memory <see cref="Report"/> as indented JSON using the canonical
/// <see cref="JsonOptions.Pretty"/> settings so the file is both machine-parseable
/// and human-readable. Round-trips cleanly via <see cref="JsonOptions.Wire"/>.
/// </summary>
public static class JsonReportWriter
{
    public static string Render(Report report)
        => JsonSerializer.Serialize(report, JsonOptions.Pretty);
}

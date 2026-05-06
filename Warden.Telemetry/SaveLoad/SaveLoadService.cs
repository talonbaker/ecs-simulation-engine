using System.Text.Json;
using APIFramework.Config;
using APIFramework.Core;
using Warden.Contracts;
using Warden.Contracts.SchemaValidation;
using Warden.Contracts.Telemetry;

namespace Warden.Telemetry.SaveLoad;

/// <summary>
/// Entry points for serialising the running simulation to JSON and restoring it.
/// Save produces a <see cref="WorldStateDto"/> v0.5 payload (schema includes all persistent
/// component state). Load deserialises, migrates if needed, and boots a fresh
/// <see cref="SimulationBootstrapper"/> from the saved state.
/// </summary>
public static class SaveLoadService
{
    /// <summary>
    /// Serialises the full engine state to a compact JSON string.
    /// The returned string is a <see cref="WorldStateDto"/> v0.5 document that
    /// can be written to disk and later passed to <see cref="Load"/>.
    /// </summary>
    public static string Save(SimulationBootstrapper sim)
    {
        var dto = SaveProjector.Project(sim);
        return TelemetrySerializer.SerializeSnapshot(dto);
    }

    /// <summary>
    /// Deserialises <paramref name="json"/>, migrates the schema if needed, and
    /// returns a fully bootstrapped <see cref="SimulationBootstrapper"/> with all
    /// component state restored.
    /// </summary>
    /// <param name="json">JSON string produced by <see cref="Save"/>.</param>
    /// <param name="configProvider">Config source for system tuning values not persisted in the save.</param>
    /// <exception cref="SaveLoadException">
    /// Thrown when the JSON is malformed, the schema version is unsupported, or
    /// required save fields are missing.
    /// </exception>
    public static SimulationBootstrapper Load(string json, IConfigProvider configProvider)
    {
        WorldStateDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<WorldStateDto>(json, JsonOptions.Wire)
                  ?? throw new SaveLoadException("Deserialised null WorldStateDto — file may be empty.");
        }
        catch (JsonException ex)
        {
            throw new SaveLoadException("Save file is not valid JSON.", ex);
        }

        var target = SchemaVersions.WorldState;
        if (dto.SchemaVersion != target)
        {
            if (string.CompareOrdinal(dto.SchemaVersion, target) > 0)
                throw new SaveLoadException(
                    $"Save schema {dto.SchemaVersion} is newer than this engine ({target}). Upgrade the engine.");
            dto = SchemaVersionMigrator.Migrate(dto, target);
        }

        return SimulationBootstrapper.BootFromWorldStateDto(dto, configProvider);
    }
}

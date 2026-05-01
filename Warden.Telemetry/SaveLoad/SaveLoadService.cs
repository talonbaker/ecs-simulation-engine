using APIFramework.Config;
using APIFramework.Core;
using Warden.Contracts.Telemetry;

namespace Warden.Telemetry.SaveLoad;

/// <summary>
/// Entry points for save/load round-trips.
/// Save serializes all persistent state to JSON; Load deserializes and restores a
/// <see cref="SimulationBootstrapper"/> from that JSON.
/// </summary>
public static class SaveLoadService
{
    /// <summary>
    /// Serializes the current simulation state to a JSON save-game string.
    /// The string can be written to disk and later passed to <see cref="Load"/>.
    /// </summary>
    public static string Save(SimulationBootstrapper sim)
    {
        var dto = SaveProjector.Project(sim);
        return TelemetrySerializer.SerializeSnapshot(dto);
    }

    /// <summary>
    /// Deserializes a save-game JSON string and constructs a fully-restored
    /// <see cref="SimulationBootstrapper"/> ready for <c>Engine.Update()</c>.
    /// Migrates older schema versions automatically.
    /// </summary>
    /// <exception cref="SaveLoadException">
    /// Thrown when the JSON is malformed, the schema version is unrecognised,
    /// or the save file is newer than this runtime.
    /// </exception>
    public static SimulationBootstrapper Load(string json, IConfigProvider configProvider)
    {
        WorldStateDto dto;
        try
        {
            dto = TelemetrySerializer.DeserializeSnapshot(json)
                  ?? throw new SaveLoadException("Save JSON deserialised to null.");
        }
        catch (SaveLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SaveLoadException("Failed to deserialise save JSON.", ex);
        }

        dto = SchemaVersionMigrator.Migrate(dto);
        return SimulationBootstrapper.BootFromWorldStateDto(dto, configProvider);
    }
}
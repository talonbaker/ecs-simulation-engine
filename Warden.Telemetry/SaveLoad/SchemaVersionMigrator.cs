using Warden.Contracts.Telemetry;

namespace Warden.Telemetry.SaveLoad;

/// <summary>
/// Upgrades persisted <see cref="WorldStateDto"/> documents to the current schema version.
/// Only forward migrations are supported; loading a newer schema than the runtime understands
/// throws <see cref="SaveLoadException"/> (fail-closed).
/// </summary>
internal static class SchemaVersionMigrator
{
    internal static WorldStateDto Migrate(WorldStateDto dto)
    {
        return dto.SchemaVersion switch
        {
            "0.5.0" => dto,
            "0.4.0" => MigrateV04ToV05(dto),
            _ when IsNewer(dto.SchemaVersion) =>
                throw new SaveLoadException(
                    $"Save file uses schema v{dto.SchemaVersion} which is newer than this runtime (v0.5.0). " +
                    "Update the application to load this save."),
            _ => throw new SaveLoadException(
                    $"Unsupported schema version ''{dto.SchemaVersion}''. Cannot migrate.")
        };
    }

    private static WorldStateDto MigrateV04ToV05(WorldStateDto dto) =>
        dto with
        {
            SchemaVersion  = "0.5.0",
            SaveTick       = null,
            SaveTotalTime  = null,
            SaveTimeScale  = null,
            EntityIdCounter = null,
            NpcSaveStates  = null,
            TaskEntities   = null,
            StainEntities  = null,
            LockedDoors    = null
        };

    private static bool IsNewer(string version)
    {
        if (!Version.TryParse(version, out var v)) return false;
        return v > new Version(0, 5, 0);
    }
}
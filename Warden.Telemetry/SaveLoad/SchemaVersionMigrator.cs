using Warden.Contracts.Telemetry;

namespace Warden.Telemetry.SaveLoad;

/// <summary>
/// Migrates older-version <see cref="WorldStateDto"/> saves to the current engine schema.
/// Only supports v0.4 → v0.5. Higher-version saves fail-closed in <see cref="SaveLoadService"/>.
/// </summary>
public static class SchemaVersionMigrator
{
    /// <summary>
    /// Migrates <paramref name="dto"/> from its current schema version toward
    /// <paramref name="targetVersion"/>. Returns a new DTO at the target version.
    /// Throws <see cref="SaveLoadException"/> if the migration path is unsupported.
    /// </summary>
    public static WorldStateDto Migrate(WorldStateDto dto, string targetVersion)
    {
        var current = dto.SchemaVersion ?? "0.0.0";

        if (current == targetVersion)
            return dto;

        // v0.4 → v0.5: add default-filled save fields
        if (current == "0.4.0" && targetVersion == "0.5.0")
            return MigrateV04ToV05(dto);

        throw new SaveLoadException(
            $"No migration path from schema {current} to {targetVersion}.");
    }

    // v0.4 → v0.5: new save-specific fields default to null (no full state captured).
    // Loads from old saves restore only the telemetry subset (drives, physiology, positions).
    private static WorldStateDto MigrateV04ToV05(WorldStateDto dto) =>
        dto with
        {
            SchemaVersion    = "0.5.0",
            SaveTick         = null,
            SaveTotalTime    = null,
            SaveTimeScale    = null,
            EntityIdCounter  = null,
            NpcSaveStates    = null,
            TaskEntities     = null,
            StainEntities    = null,
            LockedDoors      = null,
        };
}

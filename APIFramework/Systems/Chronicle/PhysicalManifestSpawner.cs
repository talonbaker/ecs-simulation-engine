using System;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems.Chronicle;

/// <summary>
/// Creates Stain and BrokenItem entities as physical manifestations of chronicle events.
/// Called by <see cref="PersistenceThresholdDetector"/> when a candidate qualifies.
/// </summary>
public sealed class PhysicalManifestSpawner
{
    private readonly EntityManager   _em;
    private readonly SeededRandom    _rng;
    private readonly ChronicleConfig _config;

    public PhysicalManifestSpawner(EntityManager em, SeededRandom rng, ChronicleConfig config)
    {
        _em     = em;
        _rng    = rng;
        _config = config;
    }

    /// <summary>
    /// Spawns a <see cref="StainTag"/> entity near <paramref name="x"/>/<paramref name="z"/>.
    /// Returns the spawned entity's int ID.
    /// </summary>
    public int SpawnStain(float x, float z, string source, string chronicleEntryId, long tick)
    {
        int lo        = _config.StainMagnitudeRange.Length > 0 ? _config.StainMagnitudeRange[0] : 10;
        int hi        = _config.StainMagnitudeRange.Length > 1 ? _config.StainMagnitudeRange[1] : 80;
        int magnitude = _rng.NextInt(Math.Max(hi - lo, 1)) + lo;
        var entity    = EntityTemplates.Stain(_em, null, x, z, source, magnitude, chronicleEntryId, tick);
        return EntityIntId(entity);
    }

    /// <summary>
    /// Spawns a <see cref="BrokenItemTag"/> entity near <paramref name="x"/>/<paramref name="z"/>.
    /// Returns the spawned entity's int ID.
    /// </summary>
    public int SpawnBrokenItem(float x, float z, string originalKind, BreakageKind breakage, string chronicleEntryId, long tick)
    {
        var entity = EntityTemplates.BrokenItem(_em, originalKind, null, x, z, breakage, chronicleEntryId, tick);
        return EntityIntId(entity);
    }

    // ── ID helpers ─────────────────────────────────────────────────────────────

    public static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    public static string IntIdToGuidString(int id)
    {
        var bytes = new byte[16];
        bytes[0] = (byte)( id        & 0xFF);
        bytes[1] = (byte)((id >>  8) & 0xFF);
        bytes[2] = (byte)((id >> 16) & 0xFF);
        bytes[3] = (byte)((id >> 24) & 0xFF);
        return new Guid(bytes).ToString();
    }
}

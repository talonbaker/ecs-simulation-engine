using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using APIFramework.Components;
using APIFramework.Core;
using Warden.Contracts.Handshake;

namespace Warden.Telemetry;

/// <summary>
/// Applies a validated <see cref="AiCommandBatch"/> to a running
/// <see cref="SimulationBootstrapper"/> simulation.
///
/// SAFETY CONTRACT
/// ---------------
/// - The entire batch is validated BEFORE any mutation occurs.
///   If any command fails validation, the batch is rejected atomically:
///   <c>Applied == 0</c>, <c>Rejected == batch.Commands.Count</c>.
/// - Individual handlers take defensive copies of struct components so a
///   partially-applied handler cannot leave ECS index state inconsistent.
/// - <c>set-config-value</c> mutates <c>SimConfig</c> via direct property
///   assignment on the live config objects (same objects that systems reference),
///   matching the semantics of <see cref="SimulationBootstrapper.ApplyConfig"/>.
///
/// FORCE-DOMINANT LIMITATION
/// -------------------------
/// <c>force-dominant</c> writes urgency values to <c>DriveComponent</c> for the
/// current tick only. <c>BrainSystem</c> overwrites them on the next tick.
/// Persistent forced-drive support requires an engine-side override slot (future WP).
/// </summary>
public sealed class CommandDispatcher
{
    // -- Public API ------------------------------------------------------------

    /// <summary>
    /// Validates then applies <paramref name="batch"/> to <paramref name="sim"/>.
    /// Returns a <see cref="DispatchResult"/> describing what happened.
    /// Never throws — all errors are captured in the result.
    /// </summary>
    public DispatchResult Apply(SimulationBootstrapper sim, AiCommandBatch batch)
    {
        if (batch.Commands.Count == 0)
            return new DispatchResult(0, 0, Array.Empty<string>());

        // -- Phase 1 — validate everything before mutating anything ------------
        var errors = new List<string>();
        foreach (var cmd in batch.Commands)
            errors.AddRange(Validate(sim, cmd));

        if (errors.Count > 0)
            return new DispatchResult(0, batch.Commands.Count, errors);

        // -- Phase 2 — apply (every command passed validation) -----------------
        int applied = 0;
        foreach (var cmd in batch.Commands)
        {
            Dispatch(sim, cmd);
            applied++;
        }

        return new DispatchResult(applied, 0, Array.Empty<string>());
    }

    // -- Validation ------------------------------------------------------------

    private static IReadOnlyList<string> Validate(SimulationBootstrapper sim, AiCommand cmd)
    {
        return cmd switch
        {
            SpawnFoodCommand      c => ValidateSpawnFood(c),
            SpawnLiquidCommand    c => ValidateSpawnLiquid(c),
            RemoveEntityCommand   c => ValidateRemoveEntity(sim, c),
            SetPositionCommand    c => ValidateSetPosition(sim, c),
            ForceDominantCommand  c => ValidateForceDominant(sim, c),
            SetConfigValueCommand c => ValidateSetConfigValue(sim, c),
            _                       => new List<string> { $"Unknown command type: {cmd.GetType().Name}" },
        };
    }

    private static List<string> ValidateSpawnFood(SpawnFoodCommand c)
    {
        var errs = new List<string>();
        if (string.IsNullOrWhiteSpace(c.FoodType))
            errs.Add("spawn-food: FoodType must not be empty.");
        if (c.Count < 1)
            errs.Add($"spawn-food: Count must be >= 1 (got {c.Count}).");
        return errs;
    }

    private static List<string> ValidateSpawnLiquid(SpawnLiquidCommand c)
    {
        var errs = new List<string>();
        if (string.IsNullOrWhiteSpace(c.LiquidType))
            errs.Add("spawn-liquid: LiquidType must not be empty.");
        if (c.Count < 1)
            errs.Add($"spawn-liquid: Count must be >= 1 (got {c.Count}).");
        return errs;
    }

    private static List<string> ValidateRemoveEntity(SimulationBootstrapper sim, RemoveEntityCommand c)
    {
        var errs = new List<string>();
        if (!Guid.TryParse(c.EntityId, out var id))
        {
            errs.Add($"remove-entity: EntityId '{c.EntityId}' is not a valid GUID.");
            return errs;
        }
        if (FindEntity(sim.EntityManager, id) is null)
            errs.Add($"remove-entity: No entity with id '{c.EntityId}' found.");
        return errs;
    }

    private static List<string> ValidateSetPosition(SimulationBootstrapper sim, SetPositionCommand c)
    {
        var errs = new List<string>();
        if (!Guid.TryParse(c.EntityId, out var id))
        {
            errs.Add($"set-position: EntityId '{c.EntityId}' is not a valid GUID.");
            return errs;
        }
        var e = FindEntity(sim.EntityManager, id);
        if (e is null)
        {
            errs.Add($"set-position: No entity with id '{c.EntityId}' found.");
            return errs;
        }
        if (!e.Has<PositionComponent>())
            errs.Add($"set-position: Entity '{c.EntityId}' has no PositionComponent.");
        return errs;
    }

    private static readonly HashSet<string> ValidDominants =
        new(StringComparer.OrdinalIgnoreCase)
        { "None", "Eat", "Drink", "Sleep", "Defecate", "Pee" };

    private static List<string> ValidateForceDominant(SimulationBootstrapper sim, ForceDominantCommand c)
    {
        var errs = new List<string>();
        if (!Guid.TryParse(c.EntityId, out var id))
        {
            errs.Add($"force-dominant: EntityId '{c.EntityId}' is not a valid GUID.");
            return errs;
        }
        var e = FindEntity(sim.EntityManager, id);
        if (e is null)
        {
            errs.Add($"force-dominant: No entity with id '{c.EntityId}' found.");
            return errs;
        }
        // DriveComponent is written by BrainSystem on the first tick; a freshly-spawned
        // entity may not have it yet. We accept any living entity (MetabolismComponent)
        // and create the DriveComponent in the handler if it is absent.
        if (!ValidDominants.Contains(c.Dominant))
            errs.Add($"force-dominant: '{c.Dominant}' is not a valid drive " +
                     $"(expected one of: {string.Join(", ", ValidDominants)}).");
        if (c.DurationGameSeconds <= 0)
            errs.Add($"force-dominant: DurationGameSeconds must be > 0 (got {c.DurationGameSeconds}).");
        return errs;
    }

    private static List<string> ValidateSetConfigValue(SimulationBootstrapper sim, SetConfigValueCommand c)
    {
        var errs = new List<string>();
        if (string.IsNullOrWhiteSpace(c.Path))
        {
            errs.Add("set-config-value: Path must not be empty.");
            return errs;
        }

        if (!TryNavigateConfigPath(sim.Config, c.Path,
                out _, out var prop, out var navErr))
        {
            errs.Add($"set-config-value: {navErr}");
            return errs;
        }

        if (!prop.CanWrite)
        {
            errs.Add($"set-config-value: Property '{c.Path}' is read-only.");
            return errs;
        }

        if (!TryConvertJsonElement(c.Value, prop.PropertyType, out _, out var convErr))
            errs.Add($"set-config-value: Cannot convert value to {prop.PropertyType.Name}: {convErr}");

        return errs;
    }

    // -- Dispatch (runs only after full validation passes) ---------------------

    private static void Dispatch(SimulationBootstrapper sim, AiCommand cmd)
    {
        switch (cmd)
        {
            case SpawnFoodCommand      c: HandleSpawnFood(sim.EntityManager, c);      break;
            case SpawnLiquidCommand    c: HandleSpawnLiquid(sim.EntityManager, c);    break;
            case RemoveEntityCommand   c: HandleRemoveEntity(sim.EntityManager, c);   break;
            case SetPositionCommand    c: HandleSetPosition(sim.EntityManager, c);    break;
            case ForceDominantCommand  c: HandleForceDominant(sim.EntityManager, c);  break;
            case SetConfigValueCommand c: HandleSetConfigValue(sim, c);               break;
        }
    }

    // -- Handlers --------------------------------------------------------------

    private static void HandleSpawnFood(EntityManager em, SpawnFoodCommand cmd)
    {
        for (int i = 0; i < cmd.Count; i++)
        {
            var e = em.CreateEntity();
            e.Add(new BolusComponent
            {
                FoodType  = cmd.FoodType,
                Volume    = 50f,
                Toughness = 0.2f,
                Nutrients = new NutrientProfile
                {
                    // Sensible banana-equivalent defaults for any AI-spawned food.
                    Carbohydrates = 27f,
                    Proteins      = 1.3f,
                    Fats          = 0.4f,
                    Fiber         = 3.1f,
                    Water         = 89f,
                },
            });
            e.Add(new PositionComponent { X = cmd.X, Y = cmd.Y, Z = cmd.Z });
            e.Add(new RotComponent
            {
                AgeSeconds  = 0f,
                RotLevel    = 0f,
                RotStartAge = 86400f,   // 1 game-day freshness window
                RotRate     = 0.001f,
            });
        }
    }

    private static void HandleSpawnLiquid(EntityManager em, SpawnLiquidCommand cmd)
    {
        for (int i = 0; i < cmd.Count; i++)
        {
            var e = em.CreateEntity();
            e.Add(new LiquidComponent
            {
                LiquidType = cmd.LiquidType,
                VolumeMl   = 15f,
                Nutrients  = new NutrientProfile { Water = 15f },
            });
            e.Add(new PositionComponent { X = cmd.X, Y = cmd.Y, Z = cmd.Z });
        }
    }

    private static void HandleRemoveEntity(EntityManager em, RemoveEntityCommand cmd)
    {
        // Validation already confirmed the GUID parses and entity exists.
        var id     = Guid.Parse(cmd.EntityId);
        var entity = FindEntity(em, id)!;
        em.DestroyEntity(entity);
    }

    private static void HandleSetPosition(EntityManager em, SetPositionCommand cmd)
    {
        var id     = Guid.Parse(cmd.EntityId);
        var entity = FindEntity(em, id)!;

        // Defensive struct copy — read current component, overwrite X/Y/Z only.
        var pos = entity.Get<PositionComponent>();
        pos.X = cmd.X;
        pos.Y = cmd.Y;
        pos.Z = cmd.Z;
        entity.Add(pos);
    }

    private static void HandleForceDominant(EntityManager em, ForceDominantCommand cmd)
    {
        var id     = Guid.Parse(cmd.EntityId);
        var entity = FindEntity(em, id)!;

        // Defensive struct copy — zero all urgencies, then raise the target to 1.0.
        // Note: BrainSystem overwrites DriveComponent on the next tick (one-tick override).
        // If BrainSystem hasn't run yet the component won't exist; use a zeroed default.
        var drive = entity.Has<DriveComponent>() ? entity.Get<DriveComponent>() : default;
        drive.EatUrgency      = 0f;
        drive.DrinkUrgency    = 0f;
        drive.SleepUrgency    = 0f;
        drive.DefecateUrgency = 0f;
        drive.PeeUrgency      = 0f;

        switch (cmd.Dominant.ToUpperInvariant())
        {
            case "EAT":      drive.EatUrgency      = 1f; break;
            case "DRINK":    drive.DrinkUrgency     = 1f; break;
            case "SLEEP":    drive.SleepUrgency     = 1f; break;
            case "DEFECATE": drive.DefecateUrgency  = 1f; break;
            case "PEE":      drive.PeeUrgency       = 1f; break;
            // "None" → all remain 0.0 → Dominant computes to None. No-op.
        }

        entity.Add(drive);
    }

    private static void HandleSetConfigValue(SimulationBootstrapper sim, SetConfigValueCommand cmd)
    {
        // Validation already confirmed path is valid and value is convertible.
        TryNavigateConfigPath(sim.Config, cmd.Path, out var parent, out var prop, out _);
        TryConvertJsonElement(cmd.Value, prop.PropertyType, out var converted, out _);
        prop.SetValue(parent, converted);

        Console.WriteLine($"[Config] set-config-value '{cmd.Path}' applied.");
    }

    // -- Helpers ---------------------------------------------------------------

    private static Entity? FindEntity(EntityManager em, Guid id)
    {
        foreach (var e in em.GetAllEntities())
            if (e.Id == id) return e;
        return null;
    }

    /// <summary>
    /// Walks the dotted config path (e.g. "Systems.Brain.SleepMaxScore") from
    /// <paramref name="config"/>. Returns <c>true</c> when the path resolves to a
    /// writable property; populates <paramref name="parent"/> and
    /// <paramref name="prop"/> on success, <paramref name="error"/> on failure.
    /// </summary>
    private static bool TryNavigateConfigPath(
        object           config,
        string           path,
        out object       parent,
        out PropertyInfo prop,
        out string?      error)
    {
        parent = config;    // satisfy definite assignment; overwritten below
        prop   = null!;     // satisfy definite assignment; overwritten on success
        error  = null;

        var parts   = path.Split('.');
        object curr = config;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var seg = curr.GetType().GetProperty(
                parts[i], BindingFlags.Public | BindingFlags.Instance);

            if (seg is null)
            {
                error = $"Unknown config segment '{parts[i]}' in path '{path}'.";
                return false;
            }

            var next = seg.GetValue(curr);
            if (next is null)
            {
                error = $"Config segment '{parts[i]}' returned null in path '{path}'.";
                return false;
            }

            curr = next;
        }

        var leafName = parts[^1];
        var leafProp = curr.GetType().GetProperty(
            leafName, BindingFlags.Public | BindingFlags.Instance);

        if (leafProp is null)
        {
            error = $"Unknown config property '{leafName}' in path '{path}'.";
            return false;
        }

        parent = curr;
        prop   = leafProp;
        return true;
    }

    /// <summary>
    /// Converts a <see cref="JsonElement"/> to <paramref name="targetType"/>.
    /// Supports <c>float</c>, <c>double</c>, <c>int</c>, <c>bool</c>, and <c>string</c>.
    /// </summary>
    private static bool TryConvertJsonElement(
        JsonElement  element,
        Type         targetType,
        out object?  result,
        out string?  error)
    {
        result = null;
        error  = null;

        try
        {
            if (targetType == typeof(float))   { result = (float)element.GetDouble(); return true; }
            if (targetType == typeof(double))  { result = element.GetDouble();        return true; }
            if (targetType == typeof(int))     { result = element.GetInt32();         return true; }
            if (targetType == typeof(bool))    { result = element.GetBoolean();       return true; }
            if (targetType == typeof(string))  { result = element.GetString();        return true; }

            error = $"Unsupported target type '{targetType.Name}' for set-config-value.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

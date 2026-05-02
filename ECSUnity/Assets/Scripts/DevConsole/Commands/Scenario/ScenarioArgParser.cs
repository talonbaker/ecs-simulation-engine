#if WARDEN
using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;

/// <summary>
/// Lightweight argument-parsing utilities shared by all scenario sub-verbs.
/// Handles positional args, --flag presence, and --flag value extraction.
/// </summary>
public static class ScenarioArgParser
{
    /// <summary>Returns all positional args (those not starting with --).</summary>
    public static string[] GetPositional(string[] args)
    {
        var list = new List<string>();
        for (int i = 0; i < args.Length; i++)
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                list.Add(args[i]);
        return list.ToArray();
    }

    /// <summary>Returns true if the named flag (e.g. "--random") appears in args.</summary>
    public static bool HasFlag(string[] args, string flag)
    {
        foreach (var a in args)
            if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Returns the token immediately after <paramref name="flag"/>, or null if not found.
    /// </summary>
    public static string ParseFlagValue(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    /// <summary>
    /// Finds an entity by GUID or case-insensitive IdentityComponent.Name.
    /// Returns null if not found.
    /// </summary>
    public static Entity FindEntity(string idOrName, EngineHost host)
    {
        if (host?.Engine?.Entities == null) return null;

        if (Guid.TryParse(idOrName, out var guid))
            foreach (var e in host.Engine.Entities)
                if (e.Id == guid) return e;

        string lower = idOrName.ToLowerInvariant();
        foreach (var e in host.Engine.Entities)
        {
            if (e.Has<IdentityComponent>() &&
                string.Equals(e.Get<IdentityComponent>().Name, lower, StringComparison.OrdinalIgnoreCase))
                return e;
        }
        return null;
    }

    /// <summary>
    /// Returns the lower-32-bit integer id for use in IntendedActionComponent.TargetEntityId.
    /// Mirrors the same helper in RescueIntentSystem.
    /// </summary>
    public static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return BitConverter.ToInt32(b, 0);
    }
}
#endif

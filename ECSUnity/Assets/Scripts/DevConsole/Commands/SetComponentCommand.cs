#if WARDEN
// SetComponentCommand.cs
// Mutates a single field on any engine component via reflection.
//
// How it works:
//   1. Resolve the component type by short name in APIFramework.Components (via Assembly lookup).
//   2. Read the current component value from the entity using Entity.Get<T>() (invoked via
//      reflection since T is not known at compile-time).
//   3. Find the target field by name (public instance field).
//   4. Convert the string value to the field's declared type via Convert.ChangeType.
//   5. Box the struct, set the field, then write back via Entity.Add<T>() (replaces existing).
//
// Limitations:
//   - Only public instance fields are writable (not properties).
//   - Complex types (nested structs, arrays) cannot be set via a single string value.
//   - Enum fields ARE supported because Convert.ChangeType handles numeric -> enum.
//     To set an enum by name, use the numeric ordinal (e.g., State=2 for Deceased).
//
// Usage:
//   set-component <npcId|name> <ComponentType> <field>=<value>
//
// Examples:
//   set-component donna LifeStateComponent IncapacitatedTickBudget=300
//   set-component donna PositionComponent X=15.5
//   set-component donna LifeStateComponent State=2          (sets to Deceased)
//
// Return conventions:
//   Plain string on success.
//   "ERROR: ..."  on failure.

using System;
using System.Reflection;
using APIFramework.Components;

public sealed class SetComponentCommand : IDevConsoleCommand
{
    public string Name        => "set-component";
    public string Usage       => "set-component <npcId|name> <ComponentType> <field>=<value>";
    public string Description => "Mutate an engine component field via reflection.";
    public string[] Aliases   => System.Array.Empty<string>();

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length < 3)
            return "ERROR: Usage: " + Usage;

        if (ctx.Host?.Engine == null)
            return "ERROR: Engine not available.";

        var entity = FindEntity(args[0], ctx.Host);
        if (entity == null)
            return $"ERROR: Entity '{args[0]}' not found.";

        string componentName = args[1];
        string assignment    = args[2]; // expected form: "FieldName=value"

        // Split assignment at the first '=' only.
        int eq = assignment.IndexOf('=');
        if (eq <= 0)
            return $"ERROR: Assignment must be in form 'field=value', got '{assignment}'.";

        string fieldName = assignment.Substring(0, eq).Trim();
        string valueStr  = assignment.Substring(eq + 1).Trim();

        // Resolve the component type by short name.
        Type compType = ResolveComponentType(componentName);
        if (compType == null)
            return $"ERROR: Component type '{componentName}' not found in APIFramework.Components.";

        // Invoke Entity.Get<T>() via reflection to read the current component value.
        MethodInfo getMethod = typeof(APIFramework.Core.Entity)
            .GetMethod("Get", BindingFlags.Public | BindingFlags.Instance)
            ?.MakeGenericMethod(compType);

        if (getMethod == null)
            return "ERROR: Could not locate Entity.Get<T>() via reflection.";

        object component;
        try
        {
            component = getMethod.Invoke(entity, null);
        }
        catch (Exception ex)
        {
            return $"ERROR: entity.Get<{componentName}>() failed: " +
                   (ex.InnerException?.Message ?? ex.Message);
        }

        // Find the target field.
        FieldInfo field = compType.GetField(fieldName,
            BindingFlags.Public | BindingFlags.Instance);

        if (field == null)
            return $"ERROR: Field '{fieldName}' not found on {componentName}. " +
                   $"Only public instance fields are supported.";

        // Convert the string to the field's declared type.
        object converted;
        try
        {
            converted = Convert.ChangeType(
                valueStr, field.FieldType,
                System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return $"ERROR: Could not convert '{valueStr}' to {field.FieldType.Name}.";
        }

        // Structs are value types — must box, mutate the box, then write back.
        object boxed = component;
        field.SetValue(boxed, converted);

        // Invoke Entity.Add<T>(T) to replace the component.
        MethodInfo addMethod = typeof(APIFramework.Core.Entity)
            .GetMethod("Add", BindingFlags.Public | BindingFlags.Instance)
            ?.MakeGenericMethod(compType);

        if (addMethod == null)
            return "ERROR: Could not locate Entity.Add<T>() via reflection.";

        try
        {
            addMethod.Invoke(entity, new[] { boxed });
        }
        catch (Exception ex)
        {
            return $"ERROR: entity.Add<{componentName}>() failed: " +
                   (ex.InnerException?.Message ?? ex.Message);
        }

        string name = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : entity.Id.ToString();

        return $"Set {componentName}.{fieldName} = {valueStr} on '{name}'.";
    }

    // Look up a component type by short name. Checks APIFramework.Components namespace first,
    // then falls back to a fully-qualified name in case the caller passed the full path.
    private static Type ResolveComponentType(string shortName)
    {
        var asm = typeof(IdentityComponent).Assembly;
        return asm.GetType($"APIFramework.Components.{shortName}")
            ?? asm.GetType(shortName);
    }

    // Tries Guid first, then falls back to case-insensitive IdentityComponent.Name match.
    private static APIFramework.Core.Entity FindEntity(string idOrName, EngineHost host)
    {
        if (host?.Engine?.Entities == null) return null;

        if (System.Guid.TryParse(idOrName, out var guid))
        {
            foreach (var e in host.Engine.Entities)
                if (e.Id == guid) return e;
        }

        string lower = idOrName.ToLowerInvariant();
        foreach (var e in host.Engine.Entities)
        {
            if (e.Has<IdentityComponent>())
            {
                var id = e.Get<IdentityComponent>();
                if (id.Name?.ToLowerInvariant() == lower) return e;
            }
        }

        return null;
    }
}
#endif

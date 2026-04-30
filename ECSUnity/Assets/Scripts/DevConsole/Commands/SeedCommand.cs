#if WARDEN
// SeedCommand.cs
// Notes a desired RNG seed for the simulation. Full effect requires an engine restart.
//
// Why can't we apply the seed live?
//   The SimConfigAsset seed is consumed during engine initialisation (EntityManager.Init).
//   Random number generators seeded at that point are already constructed; re-seeding
//   them mid-session would change only future rolls, creating a partially-seeded run that
//   is impossible to reproduce. Reliable reproduction requires a clean restart with the
//   desired seed in the Inspector or launch args.
//
//   This command logs the intent so the developer can note it, then manually set the
//   SimConfigAsset.Seed field in the Unity Inspector before re-entering play mode.
//
// Future improvement:
//   If SimConfigAsset is accessible at runtime (e.g. via a static singleton or DI),
//   this command could write the seed there so it takes effect on the next play-mode
//   entry without requiring the developer to find the Inspector field.
//
// Usage:
//   seed <integer>
//
// Example:
//   seed 42
//   seed -1000000
//
// Return conventions:
//   "INFO: ..."  always — this command is advisory, not immediately effective.
//   "ERROR: ..."  if the argument is not a valid integer.

public sealed class SeedCommand : IDevConsoleCommand
{
    public string Name        => "seed";
    public string Usage       => "seed <integer>";
    public string Description => "Note the desired RNG seed (full effect requires engine restart).";
    public string[] Aliases   => System.Array.Empty<string>();

    public string Execute(string[] args, DevCommandContext ctx)
    {
        if (args.Length == 0)
            return "ERROR: Usage: " + Usage;

        if (!int.TryParse(args[0], out int seed))
            return $"ERROR: '{args[0]}' is not a valid 32-bit integer seed.";

        // Log to the Unity console so the developer can copy it into the Inspector.
        UnityEngine.Debug.Log($"[DevConsole] Seed requested: {seed}. " +
                              $"Set SimConfigAsset.Seed = {seed} in the Inspector and restart play mode.");

        return $"INFO: Seed {seed} noted. " +
               $"Set SimConfigAsset.Seed in the Inspector and re-enter play mode to apply.";
    }
}
#endif

# 04 — Build and Dev Setup

## Prerequisites

| Tool | Version | Notes |
|:-----|:--------|:------|
| .NET SDK | 8.0.x | Required. [Download](https://dotnet.microsoft.com/en-us/download/dotnet/8) |
| Git | any recent | For branch management |
| IDE | Rider 2024+ or Visual Studio 2022+ or VS Code with C# Dev Kit | All three work; Rider has the best ECS navigation |

Verify your .NET version:

```bash
dotnet --version
# Should output 8.0.xxx
```

---

## Repository Structure and Branches

The repository has two primary branches:

| Branch | Status | Contents |
|:-------|:-------|:---------|
| `master` | Stable Phase 1 | Physiology, mood, brain, cognition, social layer, dialog, AI CLI (WP-04) |
| `ecs-cleanup-post-wp-pass` | Phase 3 life-state work | LifeStateComponent/Enum, choking (WP-3.0.1), bereavement+corpse (WP-3.0.2), slip-and-fall (WP-3.0.3), fainting (WP-3.0.6) |

The `SimConfig.cs` file has visible merge conflict markers in the master branch — this is expected during active integration of WP-3.0.x work. The `<<<<<<< HEAD` / `=======` / `>>>>>>> 086c7c8` blocks in `SimConfig.cs` and `Tags.cs` indicate two parallel config expansions that have not yet been reconciled. On the `ecs-cleanup-post-wp-pass` branch these conflicts are resolved.

### Clone

```bash
git clone <repo-url>
cd _ecs-simulation-engine
```

### Switch to the life-state branch

```bash
git checkout ecs-cleanup-post-wp-pass
```

---

## Building

### Build all projects

From the repository root:

```bash
dotnet build
```

This builds `APIFramework`, `APIFramework.Tests`, `ECSCli`, and `ECSVisualizer` in one pass. Expect a clean build with zero errors on both branches.

### Build a specific project

```bash
dotnet build APIFramework/APIFramework.csproj
dotnet build ECSCli/ECSCli.csproj
dotnet build ECSVisualizer/ECSVisualizer.csproj
```

### Release build (for performance measurement or deployment)

```bash
dotnet build -c Release
```

Release mode disables debug assertions, enables inlining, and produces measurably faster tick rates. Use this when running balance sweeps or benchmarks with `--ticks`.

---

## Running Tests

### Run all tests

```bash
dotnet test
```

### Run all tests with verbose output

```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run tests by subsystem (filter by class name substring)

```bash
# All LifeState tests
dotnet test --filter LifeState

# All fainting tests (any class with Fainting in the name)
dotnet test --filter Fainting

# All choking tests
dotnet test --filter Choking

# All metabolism/biological tests
dotnet test --filter Metabolism

# All narrative tests
dotnet test --filter Narrative

# All social/cognition tests
dotnet test --filter ActionSelection

# All dialog tests
dotnet test --filter Dialog

# All integration tests
dotnet test --filter Integration
```

### Run a single acceptance test by AT number

```bash
# AT-01
dotnet test --filter "AT01"

# AT-16 (full fainting cycle)
dotnet test --filter "AT16"

# AT-09 (NPC becomes Incapacitated after drain)
dotnet test --filter "AT09"
```

### Run tests by test class name

```bash
dotnet test --filter "FullyQualifiedName~FaintingDetectionSystemTests"
dotnet test --filter "FullyQualifiedName~FaintingIntegrationTests"
dotnet test --filter "FullyQualifiedName~ChokingDetectionSystemTests"
```

### Run tests from a specific project

```bash
dotnet test APIFramework.Tests/APIFramework.Tests.csproj
```

### Gather test results as XML (for CI)

```bash
dotnet test --logger "trx;LogFileName=TestResults.trx"
# Results written to TestResults/TestResults.trx
```

### Gather test results as JSON

```bash
dotnet test --logger "json;LogFileName=test-results.json"
```

---

## Running ECSCli

### Basic run (forever, snapshots every 10 game-minutes)

```bash
dotnet run --project ECSCli
```

### Run for one game-day

```bash
dotnet run --project ECSCli -- --duration 86400
```

### Run for two game-days, snapshot every game-hour, quiet at end

```bash
dotnet run --project ECSCli -- --duration 172800 --snapshot 3600 --quiet
```

### Run a fixed number of ticks (benchmark mode)

```bash
dotnet run --project ECSCli -- --ticks 100000 --no-report
```

### Override TimeScale to run 10× faster than default

```bash
dotnet run --project ECSCli -- --timescale 1200 --duration 86400
```

Default TimeScale is loaded from `SimConfig.json` (typically 120). Passing `--timescale` overrides the config value for this run only.

### Using AI subcommands

```bash
# Generate a snapshot for AI analysis
dotnet run --project ECSCli -- ai snapshot --out snapshot.json --pretty

# Stream narrative events to stdout
dotnet run --project ECSCli -- ai narrative-stream --duration 3600

# Stream telemetry frames every 60 game-seconds
dotnet run --project ECSCli -- ai stream --out telemetry.jsonl --interval 60 --duration 3600

# Describe the engine (component types, systems, config keys)
dotnet run --project ECSCli -- ai describe --out engine-facts.md
```

See [06-cli-reference.md](06-cli-reference.md) for the full CLI reference.

---

## Running ECSVisualizer

```bash
dotnet run --project ECSVisualizer
```

The Avalonia GUI opens a dark-terminal window showing all entity cards with real-time physiology bars, mood panels, GI pipeline, and drive scores. The time-scale slider in the header controls `SimulationClock.TimeScale` live.

> **Note:** ECSVisualizer requires a desktop environment. It does not run headless.

---

## SimConfig.json Hot-Reload

`SimConfig.json` is located at the repository root. Any time you save this file while the CLI or Visualizer is running, the change is detected by `SimConfigWatcher` and applied on the next tick via `SimulationBootstrapper.ApplyConfig(SimConfig)`.

**What hot-reloads:**

- All system thresholds (hunger, thirst, tired, stressed, etc.)
- All drain and restore rates
- Brain drive score ceilings and mood multipliers
- Narrative event detection thresholds
- FaintingConfig (FearThreshold, FaintDurationTicks)
- ChokingConfig thresholds
- Social drive circadian amplitudes and phases
- Movement speed modifier parameters
- Dialog scoring parameters

**What does NOT hot-reload (requires sim restart):**

- Entity starting values (SatiationStart, EnergyStart, etc.) — only apply to newly spawned entities
- World structure (room layouts, spawn positions)
- The number of entities (humanCount is set at boot)

Hot-reload console output:

```
[Hot-reload] Change detected in SimConfig.json — applying next tick...
[Config] Reloaded — 2 value(s) changed:
         FaintingConfig.FearThreshold  85 → 70
         MoodSystemConfig.NegativeDecayRate  0.003 → 0.005
```

---

## Applying the WP-3.0.6 Fainting Patch

The fainting system was developed on the `ecs-cleanup-post-wp-pass` branch. If you are on `master` and want to apply the patch manually, follow these steps:

1. **Check out the target branch:**

```bash
git checkout ecs-cleanup-post-wp-pass
```

The fainting files are already present on this branch. No manual patch application is needed — the branch is the integrated state.

2. **Verify the fainting files are present:**

```bash
ls APIFramework/Systems/LifeState/Fainting*.cs
# Should list: FaintingDetectionSystem.cs, FaintingRecoverySystem.cs, FaintingCleanupSystem.cs

ls APIFramework/Components/FaintingComponent.cs
# Should exist

ls APIFramework.Tests/Systems/LifeState/Fainting*.cs
# Should list: FaintingDetectionSystemTests.cs, FaintingRecoverySystemTests.cs,
#              FaintingCleanupSystemTests.cs, FaintingIntegrationTests.cs
```

3. **Verify SimConfig.cs has FaintingConfig:**

```bash
grep -n "FaintingConfig" APIFramework/Config/SimConfig.cs
# Should find: public FaintingConfig Fainting { get; set; } = new();
# and the FaintingConfig class definition
```

4. **Run the fainting tests to verify:**

```bash
dotnet test --filter Fainting
# Expected: 19 tests, all passing (AT-01 through AT-19)
```

5. **Verify SimulationBootstrapper registers the fainting systems:**

```bash
grep -n "Fainting" APIFramework/Core/SimulationBootstrapper.cs
# Should find registrations for FaintingDetectionSystem, FaintingRecoverySystem, FaintingCleanupSystem
```

If you are applying from a `FromYouToMe/` work packet instead of a branch merge, the files to copy are:

```
docs/c2-infrastructure/work-packets/FromYouToMe/FaintingComponent.cs
    → APIFramework/Components/FaintingComponent.cs

docs/c2-infrastructure/work-packets/FromYouToMe/FaintingDetectionSystem.cs
    → APIFramework/Systems/LifeState/FaintingDetectionSystem.cs

docs/c2-infrastructure/work-packets/FromYouToMe/FaintingRecoverySystem.cs
    → APIFramework/Systems/LifeState/FaintingRecoverySystem.cs

docs/c2-infrastructure/work-packets/FromYouToMe/FaintingCleanupSystem.cs
    → APIFramework/Systems/LifeState/FaintingCleanupSystem.cs

docs/c2-infrastructure/work-packets/FromYouToMe/FaintingDetectionSystemTests.cs
    → APIFramework.Tests/Systems/LifeState/FaintingDetectionSystemTests.cs

docs/c2-infrastructure/work-packets/FromYouToMe/FaintingRecoverySystemTests.cs
    → APIFramework.Tests/Systems/LifeState/FaintingRecoverySystemTests.cs

docs/c2-infrastructure/work-packets/FromYouToMe/FaintingCleanupSystemTests.cs
    → APIFramework.Tests/Systems/LifeState/FaintingCleanupSystemTests.cs

docs/c2-infrastructure/work-packets/FromYouToMe/FaintingIntegrationTests.cs
    → APIFramework.Tests/Systems/LifeState/FaintingIntegrationTests.cs
```

Then add `FaintingConfig` to `SimConfig.cs` and register the three systems in `SimulationBootstrapper.RegisterSystems()` at the Cleanup phase before `LifeStateTransitions`.

---

## Common Build Errors and Fixes

### Error: `CS0246: The type or namespace name 'FaintingConfig' could not be found`

The `SimConfig.cs` file has merge conflict markers and the FaintingConfig class is only present in the `HEAD` side of the conflict. Resolve the conflict by accepting the `HEAD` version or by manually adding `FaintingConfig` from the work packet files.

### Error: `Ambiguous reference 'LifeStateConfig'`

`SimConfig.cs` defines `LifeStateConfig` twice — once in each side of the unresolved merge conflict. Resolve the merge conflict in `SimConfig.cs` by keeping one definition (the `ecs-cleanup-post-wp-pass` version is more complete).

### Error: `CS0104: 'IsChokingTag' is an ambiguous reference`

`Tags.cs` has two definitions of `IsChokingTag` — one in each conflict region. Resolve by keeping only one definition.

### Error: `Warden.*` namespaces not found

The `Warden.*` projects (Warden.Contracts, Warden.Telemetry, etc.) are additional projects in the solution for the AI agent interface. They must all be built as part of the solution. Run `dotnet build` from the root (not `dotnet build APIFramework`) to include all projects.

### Error: `Could not find 'SimConfig.json'`

The simulation looks for `SimConfig.json` by walking up to 6–8 directory levels from the current working directory. Either:
- Run from the repository root
- Or set the working directory to the repo root: `dotnet run --project ECSCli --working-dir /path/to/repo`

The simulation will fall back to compiled defaults if no file is found — the sim will run but you won't be able to tune values.

### Error: `Avalonia.AvaloniaLocator` not found

ECSVisualizer requires Avalonia. Run `dotnet restore` to fetch the Avalonia NuGet packages. If packages are still missing, check the NuGet source is available (`https://api.nuget.org/v3/index.json`).

### Build warning: `NutrientProfile` fields not serialized

This was a known issue pre-v0.7.2 where `System.Text.Json` silently ignored public fields on structs. Fixed in v0.7.2 by adding `IncludeFields = true` to `JsonSerializerOptions`. If you see zero values for nutrition content, confirm the fix is in place in `SimConfig.cs`'s `Load()` method.

---

## IDE Tips

**Rider / Visual Studio:**

- Open `ECSSimulation.sln` from the root directory to load all projects at once.
- The `APIFramework.Tests` project is an xUnit project — test discovery runs automatically.
- Use "Find Usages" on any tag struct (e.g., `IsFaintingTag`) to see every system that reads or writes it.

**VS Code:**

- Install the C# Dev Kit extension.
- Open the repo root as workspace.
- The `.csproj` files are discovered automatically.
- Run tests via the Testing sidebar.

---

*See also: [05-testing-guide.md](05-testing-guide.md) | [06-cli-reference.md](06-cli-reference.md)*

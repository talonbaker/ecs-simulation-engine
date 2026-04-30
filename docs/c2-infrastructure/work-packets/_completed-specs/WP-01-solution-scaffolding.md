# WP-01 — Solution Scaffolding

**Tier:** Sonnet
**Depends on:** none (this is the foundation)
**Timebox:** 45 minutes
**Budget:** $0.20

---

## Goal

Add the five new `Warden.*` projects to `ECSSimulation.sln`, configure their references per `03-naming-conventions.md`, and confirm the solution still builds green. This is the packet that makes every other packet possible. Do nothing else.

---

## Reference files (read these, do not read others)

- `docs/c2-infrastructure/00-SRD.md`
- `docs/c2-infrastructure/03-naming-conventions.md`
- `ECSSimulation.sln` (existing)
- `APIFramework/APIFramework.csproj` (for style reference — TargetFramework, Nullable, LangVersion)

## Non-goals (hard forbidden)

- Writing any code inside the new projects beyond a `Placeholder.cs` that proves compilation.
- Modifying `APIFramework/`, `APIFramework.Tests/`, `ECSCli/`, or `ECSVisualizer/` source files.
- Adding NuGet packages that are not enumerated below.
- Editing `SimConfig.json`.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Contracts/Warden.Contracts.csproj` | netstandard2.1 library; references `System.Text.Json` 8.0.x. |
| code | `Warden.Contracts/Placeholder.cs` | `namespace Warden.Contracts; internal static class Placeholder { }` |
| code | `Warden.Contracts.Tests/Warden.Contracts.Tests.csproj` | xunit test project; refs Warden.Contracts. |
| code | `Warden.Telemetry/Warden.Telemetry.csproj` | net8.0 library; refs APIFramework + Warden.Contracts. |
| code | `Warden.Telemetry/Placeholder.cs` | same pattern. |
| code | `Warden.Anthropic/Warden.Anthropic.csproj` | net8.0 library; refs Warden.Contracts; NuGet: `Microsoft.Extensions.Http` 8.0.x, `Polly` 8.4.x. |
| code | `Warden.Anthropic/Placeholder.cs` | same pattern. |
| code | `Warden.Orchestrator/Warden.Orchestrator.csproj` | net8.0 console app; refs Warden.Contracts + Warden.Anthropic. NuGet: `System.CommandLine` 2.0.0-beta4.x. |
| code | `Warden.Orchestrator/Program.cs` | `Console.WriteLine("Warden.Orchestrator v0.0.0 — scaffolded, not yet wired.");` and `return 0;` |
| code | `Warden.Orchestrator.Tests/Warden.Orchestrator.Tests.csproj` | xunit test project; refs Warden.Orchestrator. |
| config | `ECSSimulation.sln` (modified) | Five new project references added; build configs wired for Debug+Release AnyCPU. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-01.md` | Completion note (template in `prompts/sonnet-completion-note.md`). |

---

## Build settings that must be identical across new projects

```xml
<TargetFramework>net8.0</TargetFramework>          <!-- netstandard2.1 only for Warden.Contracts -->
<Nullable>enable</Nullable>
<LangVersion>latest</LangVersion>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<ImplicitUsings>enable</ImplicitUsings>
```

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `dotnet restore ECSSimulation.sln` succeeds with no warnings. | cli-exit-code |
| AT-02 | `dotnet build ECSSimulation.sln -c Debug` succeeds with zero warnings. | build |
| AT-03 | `dotnet build ECSSimulation.sln -c Release` succeeds with zero warnings. | build |
| AT-04 | `dotnet test ECSSimulation.sln` passes every existing test (no regression). | unit-test |
| AT-05 | `dotnet run --project Warden.Orchestrator` prints the scaffolded banner and returns 0. | cli-exit-code |
| AT-06 | `Warden.Orchestrator.csproj` does **not** reference `APIFramework` or `Warden.Telemetry` (enforced by string-grep in a new test `SolutionTopologyTests.Orchestrator_Has_No_Engine_Reference`). | unit-test |
| AT-07 | `ECSSimulation.sln` contains exactly 10 projects after this packet (4 existing + 6 new: Warden.Contracts, Warden.Contracts.Tests, Warden.Telemetry, Warden.Anthropic, Warden.Orchestrator, Warden.Orchestrator.Tests). Additional test projects (Warden.Telemetry.Tests, Warden.Anthropic.Tests, ECSCli.Tests) land in WP-03/05/04 respectively. | manual-review |

---

## Hand-off expectation

After this packet, every future WP can say "add a file to project `Warden.Foo`" without worrying that the project does not exist yet. That is the entire point. Do not expand scope.

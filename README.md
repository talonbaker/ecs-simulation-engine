# ECS Simulation Engine

A headless C# / .NET 8 entity-component-system simulation, growing into a 2.5D top-down office management sim — Sims-style social gameplay, Rimworld-style emergent management, with the lived-in charm of *The Office* and *Grossology*.

The simulation runs offline. An optional build-time AI orchestration layer (`Warden.*`) uses Anthropic's Claude API to generate and validate game content during development — never at runtime.

![demo](PlacePending/demo.gif)

---

## Status

**Phase 0 — Infrastructure.** Complete. The "factory" is built: a 13-project solution with telemetry, JSON schemas, prompt caching, message batching, cost ledger, fail-closed escalation, chain-of-thought persistence, and end-to-end orchestrator runs (mock and real-API both validated).

**Phase 1 — Content foundations.** In progress. Schema v0.2 (social pillar), narrative telemetry, world-bible-driven world bootstrap.

## Quickstart

```bash
dotnet restore ECSSimulation.sln
dotnet build ECSSimulation.sln -c Release
dotnet test ECSSimulation.sln
```

For full setup, the two operational workflows, and troubleshooting: see `docs/c2-infrastructure/RUNBOOK.md`.

## Architecture, in 60 seconds

Pure-data components → independent systems → emergent behavior. No scripted behavior; everything you see is a system reacting to another system's output. The same data shape (`WorldStateDto`) is used for telemetry, save/load, and AI-tier observation — one format, three uses.

The build-time AI layer (`Warden.*`) implements a 1-5-25 dispatch topology: one Opus "general" briefs five Sonnet "engineers" who in turn dispatch twenty-five Haiku "grunts" for parallel balance testing. All three tiers use strict JSON schemas. Fail-closed by default — a confused worker stops; it never recursively burns tokens.

## Documentation

- `docs/c2-infrastructure/RUNBOOK.md` — how to run things.
- `docs/c2-infrastructure/00-SRD.md` — master systems-requirement document.
- `docs/c2-infrastructure/SCHEMA-ROADMAP.md` — versioned schema evolution plan.
- `docs/PHASE-1-KICKOFF-BRIEF.md` — bootstrap context for picking up Phase 1.
- `docs/ECS-ARCHITECTURE-GUIDE.md` — engine internals.
- `docs/ENGINEERING-GUIDE.md` — code conventions and patterns.
- `docs/c2-content/` — game content bibles (world, cast, aesthetic).

## Stack

C# · .NET 8 · Avalonia UI · CommunityToolkit.MVVM · Polly · System.Text.Json · System.CommandLine

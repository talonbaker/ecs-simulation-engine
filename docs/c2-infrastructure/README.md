# C2 Infrastructure — Phase 0

**Command & Control layer for the 1-5-25 Claude Army running against the ECS Simulation Engine.**

This folder is the complete architectural spec for the infrastructure that has to exist **before any AI-driven simulation work can begin**. The game logic is not in scope here. The engine is already good. What we are building is the factory that lets tier-1 Claude (Opus) brief five Sonnet engineers, who in turn dispatch twenty-five Haiku balance-testers, all against a headless C# simulation and all while minimising API spend.

---

## How to read this folder

| File | Purpose | Read when |
|:---|:---|:---|
| `00-SRD.md` | The master Systems Requirement Document. Four pillars, topology, fail-closed policy. | Start here. |
| `01-architecture-diagram.md` | Visual of the 1-5-25 topology and all data flows. | After the SRD, to orient yourself before reading code. |
| `02-cost-model.md` | The ROI math. Caching discounts, batch discounts, per-tier budgets, burn-rate alarms. | When you need to justify an architectural choice in spend terms. |
| `03-naming-conventions.md` | Project names, namespace rules, file layout. | Before starting any Work Packet. |
| `work-packets/WP-01 … WP-12` | Twelve standalone briefs. Each is self-contained, with acceptance criteria, interfaces, and test requirements. Designed to be handed to a Sonnet agent verbatim. | One at a time, in dependency order. |
| `schemas/*.schema.json` | The strict JSON contracts between tiers. The Intelligence Handshake. | When you write or review any tier-crossing code. |
| `prompts/` | Templates for the bootstrap prompt each tier receives. Keeps context windows small and consistent. | When you dispatch a job to a Sonnet or Haiku agent. |

---

## Dependency order for the Work Packets

```
WP-01 Solution Scaffolding
   │
   ├──► WP-02 Warden.Contracts (DTOs & schemas)
   │        │
   │        ├──► WP-03 AI Telemetry (engine → JSON)
   │        │        │
   │        │        └──► WP-04 CLI AI verbs (inject/snapshot/stream/replay)
   │        │
   │        └──► WP-05 Anthropic client (Messages + Batches)
   │                 │
   │                 ├──► WP-06 Prompt Cache Manager
   │                 ├──► WP-07 Message Batch Scheduler
   │                 └──► WP-08 Cost Ledger
   │
   └──► WP-09 Orchestrator Core (Task.WhenAll + concurrency control)
             │
             ├──► WP-10 Chain-of-Thought Persistence
             ├──► WP-11 Fail-Closed Escalation Policy
             └──► WP-12 Report Aggregator (readable end-of-run reports)
```

Sonnet agents should take them in that order. Each packet names the packets it depends on, so a distracted agent cannot accidentally skip a foundation.

---

## The one-line philosophy

> **Build the factory. Don't write the game.**

Phase 0 ships zero lines of simulation-feature code. Every deliverable here is infrastructure that the eventual 1-5-25 workflow will ride on. When Phase 0 ends, the repository compiles, the orchestrator can run end-to-end with a mock Anthropic server, and a single command dispatches one Sonnet job and twenty-five batched Haiku jobs for a full cost-logged, chain-of-thought-tracked round trip. Only then does game logic work begin.

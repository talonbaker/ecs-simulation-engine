# ECS Simulation Engine — API Reference

Auto-generated from XML doc comments. See the [wiki](../wiki/) for conceptual documentation.

## Project layout

| Project | Role |
|---------|------|
| `APIFramework` | Core ECS library — entities, components, systems, simulation loop |
| `ECSCli` | Headless command-line driver and AI-orchestration verbs |
| `ECSVisualizer` | Avalonia/MVVM visual front-end |

## Entry points

- `APIFramework.Core.SimulationEngine` — owns the tick loop and system pipeline
- `APIFramework.Core.SimulationBootstrapper` — wires up the canonical system order
- `APIFramework.Config.SimConfig` — centralised tuning surface
- `APIFramework.Systems.Narrative.NarrativeEventBus` — pub/sub for narrative-relevant events

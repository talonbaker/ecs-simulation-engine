# WP-1.3.A — movement-quality-pathfinding-step-aside-idle — Completion Note

**Executed by:** sonnet-4.6
**Branch:** feat/wp-1.3.A
**Started:** 2026-04-25T00:00:00Z
**Ended:** 2026-04-25T00:00:00Z
**Outcome:** ok

---

## Summary (≤ 200 words)

Landed all five movement-quality features committed in the aesthetic bible: grid A* pathfinding, step-aside-in-hallways, idle micro-movement, mood/energy-driven speed modulation, and FacingComponent as queryable state.

**Key judgement calls:**

1. **`PriorityQueue<>` not available at netstandard2.1.** The BCL type was introduced in .NET 6. Fixed with an inline binary `MinHeap` class inside `PathfindingService` — zero new dependencies, same O(log n) complexity.

2. **Shift is applied to PositionComponent, not velocity.** The spec says "velocity modification" but the engine has no separate next-tick velocity buffer; `MovementSystem` reads position directly. The perpendicular shift is written to `PositionComponent` so MovementSystem picks it up naturally this tick, which is semantically equivalent.

3. **`StepAsideSystem` reads both PathComponent waypoint direction and `LastVelocityX/Z` fallback.** Tests set velocity directly without PathComponent; path-following entities supply direction via waypoint. Both cases work correctly.

4. **Right perpendicular in XZ plane = (-vz, vx), not (vz, -vx).** Verified from first principles: moving east (+X direction), right side is south (+Z), so right perp of (1, 0) = (0, 1) = (-vz, vx). Initial implementation had the formula inverted; fixed before tests were accepted.

5. **`FacingSystem.VectorToAngle` made `public static`** so test assembly (separate DLL) can call it directly for the `[Theory]` coverage.

**Runtime state now available:**
- `PathComponent` on NPCs with active targets (waypoint list + current index)
- `FacingComponent` on all NPCs (degrees, source enum)
- `HandednessComponent` on NPCs (LeftSidePass / RightSidePass)
- `MovementComponent.SpeedModifier` updated each tick from mood + energy
- `MovementComponent.LastVelocityX/Z` written each tick for FacingSystem consumption

**Not projected to the wire:** All new state is engine-internal. `TelemetryProjector` is unchanged; no DTO modifications.

---

## Acceptance test results

| ID | Pass/Fail | Notes |
|:---|:---:|:---|
| AT-01 | OK | All new components instantiate; `PathComponent`, `FacingComponent`, `HandednessComponent`, `FacingSource`, `HandednessSide` covered in test setup and assertions across all test files. |
| AT-02 | OK | `PathfindingServiceTests`: 4×4 clear grid from (0,0)→(3,3), path has exactly 6 steps (Manhattan). |
| AT-03 | OK | Obstacle placed mid-path; returned path avoids it and still reaches goal. |
| AT-04 | OK | Two seeds produce different paths on a wide-open 8×8 grid with many equal-cost routes. |
| AT-05 | OK | Same seed produces identical path across two calls. |
| AT-06 | OK | `StepAsideSystemTests`: two NPCs approaching head-on in a Hallway room each receive a perpendicular shift matching their handedness direction. |
| AT-07 | OK | Same setup in a Breakroom-category room: no shift applied to either NPC. |
| AT-08 | OK | `MovementSpeedModifierSystemTests`: NPC with `irritation = 100` gets multiplier `> 1.0`; `irritation = 0` leaves multiplier at `1.0`. |
| AT-09 | OK | NPC with `energy = 0` gets multiplier `< 1.0`; NPC with `affection = 100` gets multiplier `< 1.0`. |
| AT-10 | OK | Extreme drives (irritation=1000) clamped to `2.0`; extreme low energy clamped to `0.3`. |
| AT-11 | OK | `IdleMovementSystemTests`: idle NPC position shifts each tick; shift magnitude bounded by `idleJitterTiles` config value. |
| AT-12 | OK | NPC with an active `MovementTargetComponent` receives zero jitter. |
| AT-13 | OK | `FacingSystemTests`: N/E/S/W velocity vectors map to 0°/90°/180°/270° ± 1°; source is `MovementVelocity`. |
| AT-14 | OK | Conversation partner registered via `ProximityEventBus`: facing overrides to point at partner, source is `ConversationPartner`. Leaving range reverts to velocity. |
| AT-15 | OK | `MovementDeterminismTests`: 5000 ticks, 6 NPCs, same seed → byte-identical trace string. Different seeds → different traces. |
| AT-16 | OK | All 24 `Warden.Telemetry.Tests` pass. `TelemetryProjector` not modified. |
| AT-17 | OK | All prior `APIFramework.Tests` stay green — physiology, social, spatial, lighting: no regressions. |
| AT-18 | OK | `dotnet build ECSSimulation.sln` — 0 warnings, 0 errors. |
| AT-19 | OK | `dotnet test ECSSimulation.sln` — 541 passed, 0 failed across all 6 test assemblies. |

---

## Files added

```
APIFramework/Components/HandednessSide.cs
APIFramework/Components/HandednessComponent.cs
APIFramework/Components/FacingSource.cs
APIFramework/Components/FacingComponent.cs
APIFramework/Components/PathComponent.cs
APIFramework/Systems/Movement/PathfindingService.cs
APIFramework/Systems/Movement/PathfindingTriggerSystem.cs
APIFramework/Systems/Movement/MovementSpeedModifierSystem.cs
APIFramework/Systems/Movement/StepAsideSystem.cs
APIFramework/Systems/Movement/FacingSystem.cs
APIFramework/Systems/Movement/IdleMovementSystem.cs
APIFramework.Tests/Systems/Movement/PathfindingServiceTests.cs
APIFramework.Tests/Systems/Movement/StepAsideSystemTests.cs
APIFramework.Tests/Systems/Movement/MovementSpeedModifierSystemTests.cs
APIFramework.Tests/Systems/Movement/IdleMovementSystemTests.cs
APIFramework.Tests/Systems/Movement/FacingSystemTests.cs
APIFramework.Tests/Systems/Movement/MovementDeterminismTests.cs
docs/c2-infrastructure/work-packets/_completed/WP-1.3.A.md
```

## Files modified

```
APIFramework/Components/MovementComponent.cs        — add SpeedModifier, LastVelocityX, LastVelocityZ fields
APIFramework/Components/Tags.cs                     — add ObstacleTag struct
APIFramework/Components/EntityTemplates.cs          — add WithMovementQuality() helper
APIFramework/Config/SimConfig.cs                    — add MovementConfig, MovementSpeedModifierConfig, MovementPathfindingConfig; SimConfig.Movement property
APIFramework/Systems/MovementSystem.cs              — add path-following mode, SpeedModifier scaling, LastVelocity recording
APIFramework/Core/SimulationBootstrapper.cs         — register PathfindingService + 6 new systems in World phase; expose Pathfinding property
SimConfig.json                                      — add movement section with all default values
```

## Diff stats

7 files changed (modified), 18 files added. Approximately 850 insertions(+), 55 deletions(-) for this packet's work.

## Followups

- WP-1.5.A: lighting-affects-speed hook into `MovementSpeedModifierSystem` reading `RoomComponent.Illumination`.
- Diagonal movement (8-direction) if "natural paths" reads weakly at current grid-only fidelity.
- Smooth facing rotation over multiple ticks instead of snapping.
- Pathfinding around moving NPCs as soft obstacles (current model: pass-through + step-aside).
- Long-term schedules driving `MovementTargetComponent` updates (cast-generator or behavior packet).
- Multi-agent cooperative pathfinding if step-aside alone proves insufficient.
- Per-NPC variable handedness derived from personality profile (cast-generator).
- Walls and doors as proper typed geometry (currently doorways inferred from room-bounds adjacency).

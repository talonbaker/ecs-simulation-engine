# WP-3.2.2 — Rudimentary Physics (Mass / Breakable / Thrown Velocity)

> **DO NOT DISPATCH UNTIL WP-3.2.0 AND WP-3.2.1 ARE MERGED.**
> Save/load needs to handle the new physics components (3.2.0); breakage events emit sound triggers (3.2.1). The pickup-and-throw verb that consumes this packet ships in WP-3.1.D's followup; this packet ships the engine substrate.

**Tier:** Sonnet
**Depends on:** WP-3.2.0 (save/load), WP-3.2.1 (sound triggers), WP-3.0.4 (`IWorldMutationApi`), WP-3.0.3 (slip-and-fall — broken glass spawns fall-risk stain)
**Parallel-safe with:** WP-3.2.3 (chore rotation), WP-3.2.4 (rescue mechanic)
**Timebox:** 130 minutes
**Budget:** $0.55

---

## Goal

Per Talon's design philosophy memory: **rudimentary physics, not a simulator.** Drop, weight, gravity, breakage in some form — but no continuous-velocity solver, no collision-response tensor. Components and discrete events. Less depth than the digestion pipeline.

After this packet:

- A `MassComponent` records mass per entity. Default values per archetype + per-object-kind.
- A `BreakableComponent` records hit-energy threshold + post-break behavior.
- A `ThrownVelocityComponent` records one-shot velocity vector + decay rate.
- A `PhysicsTickSystem` runs at Cleanup phase, advancing positions of `ThrownVelocityComponent`-bearing entities. On hit-surface, computes hit energy = `0.5 * mass * velocityMagnitude^2`; if `> BreakableComponent.HitEnergyThreshold`, emits `Crash` or `Glass` sound trigger and applies breakage behavior.
- The pickup-and-throw verb (UX bible §2.5; build mode 3.1.D's followup) attaches `ThrownVelocityComponent` at release; engine handles the rest deterministically.
- Couples to slip-and-fall (3.0.3): breakage of glass / liquid item spawns a stain entity with `FallRiskComponent`.

The system is **deterministic, single-tick-resolution, no continuous physics.**

---

## Reference files

- **Talon's design philosophy memory** — "Rudimentary physics, not simulated. Cap physics scope at 'components and discrete events', not 'tick-driven solver'."
- `docs/c2-content/ux-ui-bible.md` §2.5 — pickup-and-throw verb.
- `docs/c2-infrastructure/work-packets/_completed/WP-3.0.3.md` — slip-and-fall + FallRiskComponent.
- `docs/c2-infrastructure/work-packets/_completed/WP-3.0.4.md` — `IWorldMutationApi`.
- `docs/c2-infrastructure/work-packets/WP-3.2.1-sound-trigger-bus.md` — Crash, Glass, Thud sound triggers.
- `APIFramework/Components/PositionComponent.cs` — modified by `ThrownVelocityComponent`'s update.
- `APIFramework/Components/Tags.cs` — `MutableTopologyTag`, plus new `BreakableTag`, `ThrownTag`.
- `APIFramework/Components/StainComponent.cs`, `FallRiskComponent.cs`.

---

## Non-goals

- Do **not** ship a continuous-velocity physics solver. Sub-tick interpolation forbidden.
- Do **not** ship collision response (bouncing, sliding, friction).
- Do **not** ship the player-facing pickup-and-throw UI verb. WP-3.1.D's followup.
- Do **not** ship NPC-thrown-object-from-anger. Future emergent.
- Do **not** ship gravity for non-thrown objects.
- Do **not** ship object-on-object stacking.
- Do **not** ship slip-trip-fall-from-thrown-object scenario directly. Substrate supports it; trigger is future.
- Do **not** retry, recurse, or self-heal.

---

## Design notes

### `MassComponent`

```csharp
public struct MassComponent
{
    public float MassKilograms;   // 0.01..200.0 typical; NPCs ~70, mug ~0.4, stapler ~1.2, chair ~10
}
```

### `BreakableComponent`

```csharp
public struct BreakableComponent
{
    public float HitEnergyThreshold;
    public BreakageBehavior OnBreak;
}

public enum BreakageBehavior
{
    Despawn = 0,
    SpawnLiquidStain = 1,
    SpawnGlassShards = 2,
    SpawnDebris = 3,
}
```

### `ThrownVelocityComponent`

```csharp
public struct ThrownVelocityComponent
{
    public float VelocityX, VelocityZ, VelocityY;
    public float DecayPerTick;
    public long  ThrownAtTick;
    public Guid  ThrownByEntityId;
}
```

### `PhysicsTickSystem`

Cleanup phase, after Movement / WorkloadSystem / MaskCrackSystem. Iterates entities with `ThrownVelocityComponent`:

```csharp
foreach (var entity in em.Query<ThrownVelocityComponent>().OrderBy(e => e.Id))
{
    var v = entity.Get<ThrownVelocityComponent>();
    var p = entity.Get<PositionComponent>();
    var m = entity.Get<MassComponent>();

    v.VelocityY -= cfg.GravityPerTick;
    var newPos = new PositionComponent {
        X = p.X + v.VelocityX * deltaTime,
        Z = p.Z + v.VelocityZ * deltaTime,
        Y = MathF.Max(0, p.Y + v.VelocityY * deltaTime)
    };

    var hit = collisionDetector.DetectHit(p, newPos, entity.Id);
    if (hit.Surface != HitSurface.None)
    {
        var velocityMag = MathF.Sqrt(v.VelocityX * v.VelocityX + v.VelocityZ * v.VelocityZ + v.VelocityY * v.VelocityY);
        var hitEnergy = 0.5f * m.MassKilograms * velocityMag * velocityMag;

        var soundKind = entity.Has<BreakableComponent>() && entity.Get<BreakableComponent>().OnBreak == BreakageBehavior.SpawnLiquidStain
            ? SoundTriggerKind.Glass : SoundTriggerKind.Crash;
        soundBus.Emit(soundKind, entity.Id, hit.X, hit.Z, MathF.Min(1.0f, hitEnergy / 100.0f), clock.CurrentTick);

        if (entity.Has<BreakableComponent>())
        {
            var breakable = entity.Get<BreakableComponent>();
            if (hitEnergy >= breakable.HitEnergyThreshold)
            {
                ApplyBreakage(entity, breakable, hit.X, hit.Z);
                continue;
            }
        }
        entity.Set(new PositionComponent { X = hit.X, Z = hit.Z, Y = hit.Y });
        entity.Remove<ThrownVelocityComponent>();
        entity.Remove<ThrownTag>();
        continue;
    }

    entity.Set(newPos);
    v.VelocityX *= (1.0f - v.DecayPerTick);
    v.VelocityZ *= (1.0f - v.DecayPerTick);

    if (MathF.Abs(v.VelocityX) < cfg.MinVelocity && MathF.Abs(v.VelocityZ) < cfg.MinVelocity)
    {
        entity.Remove<ThrownVelocityComponent>();
        entity.Remove<ThrownTag>();
        continue;
    }
    entity.Set(v);
}
```

Determinism: `OrderBy(e.Id)`, no RNG. Floating-point math deterministic given identical inputs.

### `ApplyBreakage`

```csharp
void ApplyBreakage(Entity entity, BreakableComponent breakable, float x, float z)
{
    switch (breakable.OnBreak)
    {
        case BreakageBehavior.Despawn:
            mutationApi.DespawnStructural(entity.Id); break;
        case BreakageBehavior.SpawnLiquidStain:
            mutationApi.DespawnStructural(entity.Id);
            mutationApi.SpawnStructural(StainTemplates.WaterPuddle, (int)x, (int)z); break;
        case BreakageBehavior.SpawnGlassShards:
            mutationApi.DespawnStructural(entity.Id);
            mutationApi.SpawnStructural(StainTemplates.BrokenGlass, (int)x, (int)z); break;
        case BreakageBehavior.SpawnDebris:
            entity.Add(new DebrisTag());
            entity.Remove<ThrownVelocityComponent>();
            entity.Remove<ThrownTag>(); break;
    }
}
```

### `IWorldMutationApi.ThrowEntity`

New method extending WP-3.0.4's API:

```csharp
void ThrowEntity(Guid entityId, float velocityX, float velocityZ, float velocityY, float decayPerTick);
```

Implementation attaches `ThrownVelocityComponent` and `ThrownTag` to the entity.

### SimConfig additions

```jsonc
{
  "physics": {
    "gravityPerTick":      1.5,
    "minVelocity":         0.05,
    "defaultDecayPerTick": 0.10,
    "wallHitClampMargin":  0.01
  }
}
```

### `object-mass-defaults.json`

```jsonc
{
  "schemaVersion": "0.1.0",
  "objectMass": [
    {"objectKind": "mug",         "massKg": 0.4,  "breakable": true,  "hitEnergyThreshold": 8.0,  "onBreak": "SpawnLiquidStain"},
    {"objectKind": "stapler",     "massKg": 1.2,  "breakable": false, "hitEnergyThreshold": 0,    "onBreak": "Despawn"},
    {"objectKind": "phone",       "massKg": 0.5,  "breakable": true,  "hitEnergyThreshold": 25.0, "onBreak": "Despawn"},
    {"objectKind": "chair",       "massKg": 10.0, "breakable": true,  "hitEnergyThreshold": 200.0,"onBreak": "SpawnDebris"},
    {"objectKind": "wine-bottle", "massKg": 1.4,  "breakable": true,  "hitEnergyThreshold": 6.0,  "onBreak": "SpawnLiquidStain"},
    {"objectKind": "window-pane", "massKg": 5.0,  "breakable": true,  "hitEnergyThreshold": 20.0, "onBreak": "SpawnGlassShards"},
    {"objectKind": "potted-plant","massKg": 3.0,  "breakable": true,  "hitEnergyThreshold": 30.0, "onBreak": "Despawn"}
  ]
}
```

### Tests

- `MassComponentTests.cs`, `BreakableComponentTests.cs`, `ThrownVelocityComponentTests.cs` — construction.
- `PhysicsTickSystemPositionAdvanceTests.cs`, `PhysicsTickSystemDecayTests.cs`, `PhysicsTickSystemFloorClampTests.cs`, `PhysicsTickSystemWallHitTests.cs`.
- `PhysicsTickBreakLiquidStainTests.cs`, `PhysicsTickBreakGlassShardsTests.cs`, `PhysicsTickDespawnNonBreakableTests.cs`.
- `PhysicsTickDeterminismTests.cs` — 5000 ticks deterministic.
- `IWorldMutationApiThrowEntityTests.cs`, `ObjectMassDefaultsJsonTests.cs`.
- `PhysicsSlipFallIntegrationTests.cs` — broken liquid stain → NPC walks → SlipAndFallSystem rolls.
- `PhysicsSoundEmissionTests.cs` — breakage emits Crash or Glass per kind.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/MassComponent.cs` | Mass. |
| code | `APIFramework/Components/BreakableComponent.cs` | Breakable. |
| code | `APIFramework/Components/ThrownVelocityComponent.cs` | Throw state. |
| code | `APIFramework/Components/Tags.cs` (modified) | Add `BreakableTag`, `ThrownTag`, `DebrisTag`. |
| code | `APIFramework/Systems/Physics/PhysicsTickSystem.cs` | Per-tick velocity + collision + breakage. |
| code | `APIFramework/Systems/Physics/CollisionDetector.cs` | Single-tick hit detection. |
| code | `APIFramework/Systems/Physics/StainTemplates.cs` | Template ids for breakage spawns. |
| code | `APIFramework/Mutation/IWorldMutationApi.cs` (modified) | Add `ThrowEntity`. |
| code | `APIFramework/Mutation/WorldMutationApi.cs` (modified) | Implementation. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `PhysicsTickSystem`. |
| code | `APIFramework/Config/SimConfig.cs` (modified) | `PhysicsConfig`. |
| config | `SimConfig.json` (modified) | `physics` section. |
| data | `docs/c2-content/objects/object-mass-defaults.json` | Per-kind defaults. |
| test | (~14 test files) | Comprehensive coverage. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.2.2.md` | Completion note. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | Components + tags compile and instantiate. | unit-test |
| AT-02 | Entity with `ThrownVelocityComponent { 5, 0, 0, 0.10 }` advances X by velocity each tick. | integration-test |
| AT-03 | Velocity decays each tick at `decayPerTick` rate. | unit-test |
| AT-04 | Falling-Y velocity → entity stops at floor (Y=0). | integration-test |
| AT-05 | Entity travels into wall → position clamps to wall edge; if breakable+threshold met → breakage. | integration-test |
| AT-06 | Mug thrown at high velocity into wall → emits Glass sound; despawns mug; spawns water-puddle stain with FallRisk. | integration-test |
| AT-07 | Stapler thrown at wall under-threshold → no break; clamps and stops. | integration-test |
| AT-08 | Broken liquid stain → NPC walks over → `SlipAndFallSystem` rolls correctly. | integration-test |
| AT-09 | `IWorldMutationApi.ThrowEntity` attaches `ThrownVelocityComponent` + `ThrownTag`. | integration-test |
| AT-10 | `object-mass-defaults.json` loads; all kinds present; values in range. | unit-test |
| AT-11 | Determinism: 5000 ticks deterministic throw events: byte-identical state across two seeds. | integration-test |
| AT-12 | Breakage emits correct sound trigger per kind. | integration-test |
| AT-13 | All Phase 0/1/2/3.0.x/3.1.x/3.2.0/3.2.1 tests stay green. | regression |
| AT-14 | `dotnet build` warning count = 0; `dotnet test` all green. | build + test |

---

## Followups (not in scope)

- Pickup-and-throw player UI verb. WP-3.1.D's followup.
- NPC-thrown-object-from-anger. Future emergent.
- Gravity for non-thrown objects.
- Object-on-object stacking.
- Collision response (bouncing). Out of scope.
- Friction on floor. Future polish.
- Weight-based slip risk. Future tuning.
- Per-archetype throw force.
- Throw arc visualization. UI-side polish.


---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: NOT NEEDED

This is a Track 1 (engine) packet. All verification is handled by the xUnit test suite. Once `dotnet test` returns green for `APIFramework.Tests` (and any other affected test project), the packet is ready to push and PR. **No Unity Editor steps required.**

The Sonnet executor's pipeline:

1. Implement the spec.
2. Add or update xUnit tests to cover all acceptance criteria.
3. Run `dotnet test` from the repo root. Must be green.
4. Run `dotnet build` to confirm no warnings introduced.
5. Stage all changes including the self-cleanup deletion (see below).
6. Commit on the worktree's feature branch.
7. Push the branch and open a PR against `staging`.
8. Stop. Do **not** merge. Talon merges after review.

If a test fails or compile fails, fix the underlying cause. Do **not** skip tests, do **not** mark expected-failures, do **not** push a red branch.

### Cost envelope (1-5-25 Claude army)

Target: **$0.50–$1.20** per packet wall-time on the orchestrator. Timebox is stated above in the packet header. If the executing Sonnet observes its own cost approaching the upper bound without nearing acceptance criteria, **escalate to Talon** by stopping work and committing a `WP-X-blocker.md` note to the worktree explaining what burned the budget. Do not silently exceed the envelope.

Cost-discipline rules of thumb:
- Read reference files at most once per session — cache content in working memory rather than re-reading.
- Run `dotnet test` against the focused subset (`--filter`) during iteration; full suite only at the end.
- If a refactor is pulling far more files than the spec named, stop and re-read the spec; the spec may be wrong about scope.

### Self-cleanup on merge

The active `docs/c2-infrastructure/work-packets/` directory should contain only **pending** packets. Shipped packets are deleted, not archived to `_completed-specs/` (Talon's convention from 2026-04-30 forward).

Before opening the PR, the executing Sonnet must:

1. **Check downstream dependents** with this command from the repo root:
   ```bash
   git grep -l "<THIS-PACKET-ID>" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```
   Replace `<THIS-PACKET-ID>` with this packet's identifier (e.g., `WP-3.0.4`).

2. **If the grep returns no results** (no other pending packet references this one): include `git rm docs/c2-infrastructure/work-packets/<this-packet-filename>.md` in the staging set. The deletion ships in the same commit as the implementation. Add the line `Self-cleanup: spec file deleted, no pending dependents.` to the commit message.

3. **If the grep returns one or more pending packets**: leave the spec file in place. Add a one-line status header to the top of this spec file (immediately under the H1):
   ```markdown
   > **STATUS:** SHIPPED to staging YYYY-MM-DD. Retained because pending packets depend on this spec: <list>.
   ```
   Add the line `Self-cleanup: spec retained, dependents: <list>.` to the commit message.

4. **Do not touch** files under `_completed/` or `_completed-specs/` — those are historical artifacts from earlier phases.

5. The git history (commit message + PR body) is the historical record. The spec file itself is ephemeral once shipped without dependents.

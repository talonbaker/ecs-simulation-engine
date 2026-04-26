# Smoke Mission: Cast Generator Validation

**Mission ID:** mission-cast-validate
**Created:** 2026-04-25

## Objective

Verify the v0.1 cast generator produces a population that reads as a believable office.

Inputs: The world-definition file at `docs/c2-content/world-definitions/office-starter.json`,
post-cast-generator boot. The spawned WorldStateDto is provided as input to the Sonnet worker.

## Tier-2 Sonnet brief

Read the spawned WorldStateDto (provided as input). For each NPC, verify:

- Drive values are within the archetype's chronically-elevated/depressed ranges.
- Personality dimensions are within archetype ranges.
- Inhibitions match the archetype's starter set.
- Relationships seeded match the cast bible's starting sketch counts
  (2 rivalries, 1 old flame, 1 mentor pair, 1 slept-with-their-spouse, 2 friend pairs,
  2 "the thing nobody talks about", plus affair/crush archetype-driven relationships).

Produce a SonnetResult with pass/fail assertions per category.

## Tier-3 Haiku batch (5 scenarios)

For each of 5 randomly sampled NPCs, dispatch a Haiku scenario:

- Score 0–100 on "internally consistent personality" (drives + personality + register align with archetype).
- Score 0–100 on "drive distribution feels right" (no NPC has all drives maxed; baselines vary).
- Score 0–100 on "this could be a real office worker" (sanity check on the whole package).
- Free-form note (≤ 280 chars) on what stood out.

## Acceptance criteria

1. `Warden.Orchestrator run --mission examples/smoke-mission-cast-validate.md --specs "examples/smoke-specs/cast-validate.json" --mock-anthropic` exits 0.
2. A `cost-ledger.jsonl` is produced under `./runs/<runId>/`.
3. No real API calls are made (observable by setting `ANTHROPIC_API_KEY` to an invalid value).

## Specs

One spec packet (`cast-validate`) is provided. It requests Sonnet validation of the spawned
cast and dispatches 5 Haiku per-NPC scoring scenarios.

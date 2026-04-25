# Smoke Mission

**Mission ID:** mission-smoke-01
**Created:** 2026-04-24

## Objective

Add an XML documentation comment to the `Initialize` method in `EntityManager.cs`.

This is the Phase 0 smoke test for the Warden Orchestrator. It validates the full
`run` pipeline end-to-end without spending real tokens: the `--mock-anthropic` flag
intercepts all Anthropic API calls and returns canned responses from `examples/mocks/`.

## Acceptance criteria

1. `Warden.Orchestrator run --mission examples/smoke-mission.md --specs "examples/smoke-specs/*.json" --mock-anthropic` exits 0.
2. A `cost-ledger.jsonl` is produced under `./runs/<runId>/`.
3. No real API calls are made (observable by setting `ANTHROPIC_API_KEY` to an invalid value).

## Specs

One spec packet (`spec-smoke-01`) is provided. It requests a trivial code change
and asks for 5 Haiku balance-validation scenarios.

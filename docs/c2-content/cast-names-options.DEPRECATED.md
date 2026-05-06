# DEPRECATED — cast-names-options.json

> **Superseded by** `docs/c2-content/cast/name-data.json` (WP-4.0.M, 2026-05-NN).

The original `cast-names-options.json` was an early-stage spec note for the cast naming system — a hybrid of JSON data fragments and prose describing the intended generation logic (vanilla / suffixed / hyphenated / double-suffixed-hyphen tiers).

That spec was implemented and extended in Talon's HTML/JS roster generator at
`~/talonbaker.github.io/name-face-gen/`, then ported into the engine as the
`APIFramework.Cast` library (WP-4.0.M). The mature catalog lives at
`docs/c2-content/cast/name-data.json` and powers `CastNameGenerator`.

The original file is preserved for audit-history (do not delete) but **no engine
code reads it**. The active sources are:

- `docs/c2-content/cast/name-data.json` — `CastNameGenerator` (probabilistic six-tier name + title generator).
- `docs/c2-content/cast/name-pool.json` — `NamePoolLoader` (legacy boot-time first-name pool used by `CastGenerator.SpawnAll`; will be retired in a future packet that swaps the boot path to consume `CastNameGenerator` directly).

See `docs/c2-infrastructure/MOD-API-CANDIDATES.md#MAC-017` for the modder-extension surface.

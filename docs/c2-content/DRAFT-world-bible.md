world-bible.md

Spatial scale. How many tiles or rooms? One floor or two? Roughly how many cubicles, conference rooms, bathrooms, hallways? This drives tile count, the v0.5 room-overlay schema sizing, and pathfinding budgets.
Population at a time. 8 NPCs? 20? 50? This is the single biggest performance lever. N NPCs means up to N² relationships (sparse in practice but capped), and telemetry token budgets scale linearly with N. I'd suggest committing to a target and a hard maximum — e.g., "12 typical, 25 hard cap." Every NPC beyond the cap costs measurable input tokens on every Haiku call once the v0.2 social schema lands.
Tone register and comedy contract. "Office sitcom" is a wide range. Office Space is dry-cynical. The Office is warmly absurd. Severance is melancholic-paranoid. Parks and Rec is sincere-optimistic. Pick a primary register and a secondary one. And tell me explicitly what this game's relationship is to crassness, profanity, sexual humor, and workplace conflict. Phase 4 dialogue templates can't be generated until those limits exist.
Era anchor. When is this set? What tech is around? CRTs or flat panels? Smartphones or flip phones? This decides what props exist, what catchphrases read as natural, what frustrations NPCs have (the printer, the VPN, the conference-room TV that nobody can connect to).
Named anchor locations, not categories. Not "breakroom" — the breakroom in this office. The microwave with the smell. The fridge with the passive-aggressive note. The window everyone gathers at. The supply closet that's actually a graveyard. Six to ten named locations beats fifty generic ones.
The persistence threshold. Per architectural axiom 8.4, some events stick. What's the bar for what counts? Does Frank's bad joke at lunch leave a mark, or only Frank stealing Donna's lunch? Set the threshold; the engine will respect it.

├── Company (one paragraph)
├── Building (spatial scale + named locations)
├── Population (target N, max N)
├── Tone (primary register + comedy contract + era)
├── Persistence threshold
└── Anything else load-bearing


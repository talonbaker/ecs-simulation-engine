# Content Layer (post-Phase-0)

This folder is the home of the *content* that Phase 1+ Sonnet packets will consume and produce. It is intentionally empty at the start of Phase 0 — the first deliverables here are written by Talon, by hand, before Phase 1 dispatches any packets.

The infrastructure (`../c2-infrastructure/`) is what *makes* content. This folder is what content *is*.

---

## Planned files

| File | Purpose | Owner |
|:---|:---|:---|
| `world-bible.md` | The office concept. Company, era, floor count, key locations, named tenants of the building, baseline mood. ~1 page. | Talon writes; Phase 1 Sonnets read. |
| `cast-bible.md` | 8–12 NPC archetypes — name, role, defining trait, signature behaviour, tic or catchphrase, starting relationship sketch. ~1 page. | Talon writes; Phase 1 Sonnets read. |
| `charm-catalog.md` | Structured taxonomy of every detail category the game ships (stain types, prop categories, ambient events, supply-cabinet artifacts, parking-lot signs). | Phase 3 Opus drafts; Sonnets implement. |
| `dialogue-templates/` | Template-driven NPC dialogue, organised by personality and situation. **No runtime LLM calls** — these are the templates the runtime plays back. | Phase 4 Sonnets author. |
| `event-recipes.md` | Authored persistent narrative events (the parking-lot fight, the broken vending machine) and how they manifest in world state. | Phase 4 Sonnets author. |

The first two files (`world-bible.md`, `cast-bible.md`) are the bottleneck for Phase 1 dispatch. They are the cached-prefix input for every Phase 1+ Sonnet call — once they exist, `WP-06`'s corpus manifest gets one entry per file and every downstream Sonnet pays cache-read prices on them.

---

## Suggested structure for `world-bible.md`

Optional — write it however reads best — but if a starting frame helps:

- **Company:** name, industry, public-or-private, age, current size.
- **Building:** era of construction, number of floors used, neighborhood vibe, parking situation.
- **Key locations:** breakroom, bathrooms, supply room, the IT closet, the cubicle of dead monitors, the parking lot, that one window everyone gathers at, etc.
- **Baseline state at game start:** what's already broken, what's already stained, what's already lost in the supply cabinet, what signs are already on the walls.
- **Tone target:** how dirty? how comedic? what shows or games does this most resemble?

One page is plenty. Density beats length.

---

## Suggested structure for `cast-bible.md`

For each archetype:

- **Name** — first name only is fine.
- **Role** — what they do at the company.
- **Defining trait** — the one thing about them that is *always* true.
- **Signature behaviour** — the one thing they do that other NPCs don't.
- **Tic or catchphrase** — a verbal or physical tell.
- **Starting relationships** — pairs, with a one-word sentiment (e.g., "Frank: rival", "Linda: crush").
- **Where they spend time** — preferred desks, break locations, lunch rituals.

8 archetypes is a workable minimum. 12 gives more emergent surprise. More than 15 dilutes recognisability — the player should be able to learn each NPC's "deal" within a session or two of play.

---

## What this folder is *not*

- Not a place for code. Code lives in the `Warden.*` projects.
- Not a place for schemas. Schemas live in `../c2-infrastructure/schemas/`.
- Not a place for runtime LLM prompts. The runtime makes no LLM calls (`../c2-infrastructure/00-SRD.md` §8.1).
- Not a place for game-design *philosophy* docs. Those live in `../ENGINEERING-GUIDE.md` and `../c2-infrastructure/00-SRD.md`. This folder is for *content artifacts the simulation reads or generates from*.

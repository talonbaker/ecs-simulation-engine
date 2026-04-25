aesthetic-bible.md

Pixel art, low-poly-with-texture-priority, dynamic lighting with falloff and shadows — that's important enough to deserve docs/c2-content/aesthetic-bible.md rather than buried in the world-bible. Two reasons:
First, lighting affects behavior, which makes it an engine concern not just a visual one. A flickering fluorescent makes an NPC anxious. A warm desk lamp soothes. A dark hallway raises caution. That means the engine needs queryable illumination state per tile or region, regardless of whether the visualizer renders it. This becomes a v0.6 schema bump (visualizer hints + lighting state) on the roadmap once you commit to it.
Second, the aesthetic constrains content density. Pixel art with low-poly bodies means players read silhouette and motion, not facial expressions. That shifts subtlety onto dialogue and persistent state. Knowing this shapes how the cast-bible's "visible silhouette" beat gets written.
Suggested contents:

* Visual style commitment in one paragraph.
* One or two reference touchstones — games or shows that share the look ("Stardew Valley meets early-Rare-3D," or whatever lands for you).
* Lighting model in plain English: do hallways dim at night? do offices have controllable desk lamps? can NPCs flip switches? are there windows with daylight cycles?
* Lighting → behavior mapping (the list above, populated with your specifics).
* Color palette intent — saturated, muted, era-specific?
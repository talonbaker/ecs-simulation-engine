/// <summary>
/// Vocabulary of chibi-emotion overlay icons that can appear above an NPC's head.
///
/// DESIGN (from UX-UI bible §3.8)
/// ────────────────────────────────
/// Emotion communicates through silhouette + chibi overlay, not through facial detail
/// (faces are not detailed enough per the aesthetic bible). The chibi overlay slot
/// (<see cref="ChibiEmotionSlot"/>) accepts an <see cref="IconKind"/> value and will
/// display the matching sprite when one is authored. At v0.1 the icons are stubs —
/// the slot exists and the API is stable, but no sprites are loaded yet.
///
/// ADDING NEW ICONS (future content packets)
/// ───────────────────────────────────────────
/// 1. Add the new value here.
/// 2. Author the sprite; add it to the chibi sprite atlas.
/// 3. Wire the new sprite into <see cref="ChibiEmotionSlot"/>'s atlas lookup.
/// 4. Optionally drive it from <see cref="NpcSilhouetteRenderer"/> based on a new
///    mood-threshold rule.
///
/// NON-GOALS
/// ──────────
/// This enum is the vocabulary only. No sprites are authored in this packet (WP-3.1.B).
/// Sprite content ships in WP-3.1.E (player UI) or dedicated art-pipeline packets.
/// </summary>
public enum IconKind
{
    /// <summary>No icon; slot is hidden.</summary>
    None,

    /// <summary>Anger lines — tight radiating marks around head. Driven by MoodComponent.Anger.</summary>
    Anger,

    /// <summary>Sweat drops — arcing away from forehead. Driven by MoodComponent.Fear or high urgency.</summary>
    Sweat,

    /// <summary>Sleep Z's — floating letter-Z bubbles. Driven by LifeState == Incapacitated or IsSleeping.</summary>
    SleepZ,

    /// <summary>Heart — floating pink heart. Driven by high DriveComponent.Attraction toward another NPC.</summary>
    Heart,

    /// <summary>Sparkle — four-pointed star burst. Driven by MoodComponent.Joy at ecstasy-tier.</summary>
    Sparkle,

    /// <summary>Question mark — quizzical overhead floating '?'. Driven by MoodComponent.Surprise at amazement-tier.</summary>
    QuestionMark,

    /// <summary>Exclamation mark — sudden '!' for shock or alarm. Driven by MoodComponent.Surprise spike.</summary>
    Exclamation,

    /// <summary>Stink lines — wavy lines. Driven by MoodComponent.Disgust at loathing-tier.</summary>
    Stink,

    // ── WP-4.0.E additions — face-state chibi overlays ───────────────────────

    /// <summary>Red face flush — cheek/face redness overlay. Driven by anger spike or embarrassment.</summary>
    RedFaceFlush,

    /// <summary>Green face nausea — green tinge overlay. Driven by Disgust threshold or sickness state.</summary>
    GreenFaceNausea,
}

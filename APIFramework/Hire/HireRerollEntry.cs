using APIFramework.Cast;

namespace APIFramework.Hire;

/// <summary>
/// One row of the per-session reroll history. Captures which candidate appeared
/// at which reroll index. Future hire-screen UIs can render the history as a
/// "candidates you considered" list; future chronicle systems can record
/// post-commit which candidates were rolled and which the player chose.
/// </summary>
public sealed record HireRerollEntry(
    int            Index,    // 0 = initial candidate; 1 = first reroll; etc.
    CastNameResult Result,
    long           Tick);    // tick the candidate was generated; 0 if no clock supplied

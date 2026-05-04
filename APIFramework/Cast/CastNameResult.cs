namespace APIFramework.Cast;

/// <summary>
/// The output of <see cref="CastNameGenerator.Generate(System.Random, System.Nullable{CastGender}, System.Nullable{CastNameTier})"/>.
/// <see cref="DisplayName"/> is the headline string consumers render; per-tier sub-fields are populated
/// based on which generation path the tier triggered (see <see cref="CastNameTier"/> documentation).
/// </summary>
public sealed record CastNameResult(
    string       DisplayName,
    CastNameTier Tier,
    CastGender   Gender,
    string       FirstName,
    string?      Surname,
    string?      Title,
    string?      LegendaryRoot,
    string?      LegendaryTitle,
    string?      CorporateTitle);

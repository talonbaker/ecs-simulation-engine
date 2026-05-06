using System;
using System.Collections.Generic;

namespace APIFramework.Cast;

/// <summary>
/// POCO mirror of <c>docs/c2-content/cast/name-data.json</c>. Loaded by
/// <see cref="CastNameDataLoader"/>; consumed by <see cref="CastNameGenerator"/>.
/// </summary>
public sealed class CastNameData
{
    public string                                 SchemaVersion    { get; set; } = "";
    public TierThresholdsDto                      TierThresholds   { get; set; } = new();
    public Dictionary<string, string[]>           FirstNames       { get; set; } = new();
    public string[]                               FusionPrefixes   { get; set; } = Array.Empty<string>();
    public string[]                               FusionSuffixes   { get; set; } = Array.Empty<string>();
    public string[]                               StaticLastNames  { get; set; } = Array.Empty<string>();
    public Dictionary<string, string[]>           LegendaryRoots   { get; set; } = new();
    public string[]                               LegendaryTitles  { get; set; } = Array.Empty<string>();
    public string[]                               CorporateTitles  { get; set; } = Array.Empty<string>();
    public TitleTiersDto                          TitleTiers       { get; set; } = new();
    public BadgeFlairDto?                         BadgeFlair       { get; set; }      // optional, unused by generator yet
    public DepartmentStampDto[]?                  DepartmentStamps { get; set; }      // optional, unused by generator yet
}

/// <summary>Cumulative drop-rate thresholds. Each is the upper bound (exclusive) for that tier on a [0,1) roll.</summary>
public sealed class TierThresholdsDto
{
    public double Common    { get; set; } = 0.55;
    public double Uncommon  { get; set; } = 0.82;
    public double Rare      { get; set; } = 0.94;
    public double Epic      { get; set; } = 0.98;
    public double Legendary { get; set; } = 0.995;
    public double Mythic    { get; set; } = 1.0;
}

public sealed class TitleTiersDto
{
    public TitleTierBucketDto Rank     { get; set; } = new();
    public TitleTierBucketDto Domain   { get; set; } = new();
    public TitleTierBucketDto Function { get; set; } = new();
}

public sealed class TitleTierBucketDto
{
    public string[] Mundane    { get; set; } = Array.Empty<string>();
    public string[] Silly      { get; set; } = Array.Empty<string>();
    public string[] HighStatus { get; set; } = Array.Empty<string>();
}

public sealed class BadgeFlairDto
{
    public BadgeMundaneDto?    Mundane    { get; set; }
    public BadgeHighStatusDto? HighStatus { get; set; }
}

public sealed class BadgeMundaneDto
{
    public string[] Conditions { get; set; } = Array.Empty<string>();
    public string[] OfficeSins { get; set; } = Array.Empty<string>();
    public string[] Access     { get; set; } = Array.Empty<string>();
    public string[] Stickers   { get; set; } = Array.Empty<string>();
}

public sealed class BadgeHighStatusDto
{
    public string[] Clearance { get; set; } = Array.Empty<string>();
    public string[] Legacy    { get; set; } = Array.Empty<string>();
    public string[] Signature { get; set; } = Array.Empty<string>();
}

public sealed class DepartmentStampDto
{
    public string Id    { get; set; } = "";
    public string Label { get; set; } = "";
    public string Emoji { get; set; } = "";
}

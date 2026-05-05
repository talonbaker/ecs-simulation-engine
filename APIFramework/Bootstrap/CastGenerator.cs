using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Bootstrap;

/// <summary>
/// Spawns fully-configured NPC entities from archetype data and seeds the
/// initial relationship matrix.  All sampling goes through <see cref="SeededRandom"/>
/// for replay-determinism.
/// </summary>
/// <remarks>
/// This is the second half of the data-driven boot pipeline. After
/// <see cref="WorldDefinitionLoader"/> reads <c>world.json</c> and produces
/// <see cref="NpcSlotTag"/> marker entities, <see cref="SpawnAll"/> walks those
/// markers, resolves each one against an <see cref="ArchetypeDto"/> from
/// <see cref="ArchetypeCatalog"/>, samples drives/personality/inhibitions/silhouette
/// from the configured ranges, and emits a fully-formed NPC entity.
/// <see cref="SeedRelationships"/> then layers archetype-driven and configured
/// relationship patterns on top, producing relationship entities tagged with
/// <c>RelationshipTag</c> and a <c>RelationshipComponent</c>.
/// </remarks>
/// <seealso cref="ArchetypeCatalog"/>
/// <seealso cref="WorldDefinitionLoader"/>
/// <seealso cref="CastGeneratorConfig"/>
public static class CastGenerator
{
    // -- Public entry points ---------------------------------------------------

    /// <summary>
    /// Spawns an NPC entity for every <see cref="NpcSlotTag"/> entity in
    /// <paramref name="em"/>.  Slot entities are destroyed after spawning.
    /// Returns the list of spawned NPC entities in iteration order.
    /// </summary>
    /// <param name="catalog">Archetype catalog used to resolve each slot's <c>ArchetypeHint</c>.</param>
    /// <param name="em">Entity manager containing the slot entities and receiving the new NPCs.</param>
    /// <param name="rng">Seeded RNG for deterministic sampling of all archetype ranges.</param>
    /// <param name="config">Cast-generator tuning (drive ranges, jitter, relationship counts).</param>
    /// <param name="namePool">
    /// Optional explicit name pool. When <c>null</c>, <see cref="NamePoolLoader.LoadDefault"/>
    /// is called. If no pool is available names are not assigned.
    /// </param>
    /// <returns>The newly spawned NPC entities in slot iteration order.</returns>
    public static IReadOnlyList<Entity> SpawnAll(
        ArchetypeCatalog    catalog,
        EntityManager       em,
        SeededRandom        rng,
        CastGeneratorConfig config,
        NamePoolDto?        namePool = null)
    {
        namePool ??= NamePoolLoader.LoadDefault();

        var slots     = em.Query<NpcSlotTag>().ToList();
        var result    = new List<Entity>(slots.Count);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var slot in slots)
        {
            var slotComp   = slot.Get<NpcSlotComponent>();
            var archetypeId = slotComp.ArchetypeHint ?? "";
            var archetype   = catalog.TryGet(archetypeId)
                              ?? catalog.AllArchetypes[rng.NextInt(catalog.AllArchetypes.Count)];

            var npc = SpawnNpc(archetype, slotComp, em, rng, config);

            if (namePool is not null)
            {
                var available = namePool.FirstNames
                    .Except(usedNames, StringComparer.Ordinal)
                    .ToList();
                if (available.Count == 0)
                    throw new InvalidOperationException(
                        "Name pool exhausted. Add more entries to name-pool.json or reduce cast size.");
                var idx  = rng.NextInt(available.Count);
                var name = available[idx];
                usedNames.Add(name);
                npc.Add(new IdentityComponent(name));
            }

            result.Add(npc);
            em.DestroyEntity(slot);
        }

        return result.AsReadOnly();
    }

    /// <summary>
    /// Spawns a single NPC from <paramref name="archetype"/> at the position given by
    /// <paramref name="slot"/>.  Does NOT destroy the slot entity — call <see cref="SpawnAll"/>
    /// for the full boot path.
    /// </summary>
    /// <param name="archetype">Archetype to sample drives/personality/inhibitions/silhouette from.</param>
    /// <param name="slot">Slot component supplying the spawn position (X, Y) in tile space.</param>
    /// <param name="em">Entity manager that receives the new NPC.</param>
    /// <param name="rng">Seeded RNG used for all sampling.</param>
    /// <param name="config">Cast-generator tuning (elevated/depressed/neutral drive ranges, jitter).</param>
    /// <returns>The newly created NPC entity, populated via <c>EntityTemplates.WithCastSpawn</c>.</returns>
    public static Entity SpawnNpc(
        ArchetypeDto        archetype,
        NpcSlotComponent    slot,
        EntityManager       em,
        SeededRandom        rng,
        CastGeneratorConfig config)
    {
        // -- 1. Drive baselines ------------------------------------------------
        var elevSet  = new HashSet<string>(archetype.ElevatedDrives,  StringComparer.OrdinalIgnoreCase);
        var depSet   = new HashSet<string>(archetype.DepressedDrives, StringComparer.OrdinalIgnoreCase);

        var drives = new SocialDrivesComponent
        {
            Belonging  = SampleDrive("belonging",  elevSet, depSet, rng, config),
            Status     = SampleDrive("status",     elevSet, depSet, rng, config),
            Affection  = SampleDrive("affection",  elevSet, depSet, rng, config),
            Irritation = SampleDrive("irritation", elevSet, depSet, rng, config),
            Attraction = SampleDrive("attraction", elevSet, depSet, rng, config),
            Trust      = SampleDrive("trust",      elevSet, depSet, rng, config),
            Suspicion  = SampleDrive("suspicion",  elevSet, depSet, rng, config),
            Loneliness = SampleDrive("loneliness", elevSet, depSet, rng, config),
        };

        // -- 2. Personality ----------------------------------------------------
        var pr          = archetype.PersonalityRanges;
        var personality = new PersonalityComponent(
            openness:          SampleBigFive(pr.Openness, rng),
            conscientiousness: SampleBigFive(pr.Conscientiousness, rng),
            extraversion:      SampleBigFive(pr.Extraversion, rng),
            agreeableness:     SampleBigFive(pr.Agreeableness, rng),
            neuroticism:       SampleBigFive(pr.Neuroticism, rng),
            register:          PersonalityComponent.ParseRegister(PickFrom(archetype.GetRegisters(), rng)));

        // -- 3. Willpower ------------------------------------------------------
        int wpBaseline = SampleIntRange(archetype.WillpowerBaselineRange, rng);
        var willpower  = new WillpowerComponent(wpBaseline, wpBaseline);

        // -- 4. Inhibitions ----------------------------------------------------
        var inhibList = new List<Inhibition>(archetype.StarterInhibitions.Length);
        foreach (var spec in archetype.StarterInhibitions)
        {
            var cls  = ArchetypeCatalog.ParseInhibitionClass(spec.Class);
            var str  = SampleIntRange(spec.StrengthRange, rng);
            var aware= ArchetypeCatalog.ParseAwareness(spec.Awareness);
            inhibList.Add(new Inhibition(cls, str, aware));
        }
        var inhibitions = new InhibitionsComponent(inhibList);

        // -- 5. Silhouette -----------------------------------------------------
        var sf = archetype.SilhouetteFamily;
        var silhouette = new SilhouetteComponent
        {
            Height          = sf.Heights.Length          > 0 ? PickFrom(sf.Heights,          rng) : "average",
            Build           = sf.Builds.Length           > 0 ? PickFrom(sf.Builds,           rng) : "average",
            Hair            = sf.Hair.Length             > 0 ? PickFrom(sf.Hair,             rng) : "short",
            Headwear        = sf.Headwear.Length         > 0 ? PickFrom(sf.Headwear,         rng) : "none",
            DominantColor   = sf.DominantColors.Length   > 0 ? PickFrom(sf.DominantColors,   rng) : "grey",
            DistinctiveItem = sf.DistinctiveItems.Length > 0 ? PickFrom(sf.DistinctiveItems, rng) : "lanyard",
        };

        // -- 6. Deal -----------------------------------------------------------
        var deal = new NpcDealComponent
        {
            Deal = archetype.DealOptions.Length > 0
                ? PickFrom(archetype.DealOptions, rng)
                : ""
        };

        // -- 7. Assemble entity ------------------------------------------------
        var entity = em.CreateEntity();
        entity.Add(new PositionComponent { X = slot.X, Y = 0f, Z = slot.Y });
        entity.Add(new MovementComponent { Speed = 0.04f, ArrivalDistance = 0.4f });

        EntityTemplates.WithCastSpawn(
            entity, drives, willpower, personality, inhibitions,
            silhouette, new NpcArchetypeComponent { ArchetypeId = archetype.Id }, deal);

        return entity;
    }

    /// <summary>
    /// Seeds the initial relationship matrix for <paramref name="npcs"/> using the
    /// archetype hints and the cast-bible starting sketch.
    /// Returns the relationship entities created.
    /// </summary>
    /// <param name="npcs">NPC entities (typically the result of <see cref="SpawnAll"/>) to wire relationships between.</param>
    /// <param name="catalog">Archetype catalog used to resolve each NPC's <c>RelationshipSpawnHints</c>.</param>
    /// <param name="em">Entity manager that receives the relationship entities.</param>
    /// <param name="rng">Seeded RNG for partner selection and intensity sampling.</param>
    /// <param name="config">Cast-generator tuning supplying per-pattern counts and intensity range.</param>
    /// <returns>All relationship entities created during this call, in creation order.</returns>
    public static IReadOnlyList<Entity> SeedRelationships(
        IReadOnlyList<Entity> npcs,
        ArchetypeCatalog      catalog,
        EntityManager         em,
        SeededRandom          rng,
        CastGeneratorConfig   config)
    {
        if (npcs.Count < 2)
            return Array.Empty<Entity>();

        // Assign sequential integers for RelationshipComponent canonical ordering.
        // RelationshipComponent uses int IDs (not Guid) for canonical pair tracking.
        var npcSeqId = new Dictionary<Guid, int>(npcs.Count);
        for (int i = 0; i < npcs.Count; i++)
            npcSeqId[npcs[i].Id] = i + 1;

        var created = new List<Entity>();

        // Track used pairs to avoid exact duplicates on same pattern.
        var usedPairs = new HashSet<(int, int)>();

        // -- 1. Archetype-driven relationships (The Affair, The Crush) ---------
        foreach (var npc in npcs)
        {
            if (!npc.Has<NpcArchetypeComponent>()) continue;
            var archetypeId = npc.Get<NpcArchetypeComponent>().ArchetypeId;
            var archetype   = catalog.TryGet(archetypeId);
            if (archetype?.RelationshipSpawnHints is null) continue;

            var hints  = archetype.RelationshipSpawnHints;
            var others = npcs.Where(e => e.Id != npc.Id).ToList();
            if (others.Count == 0) continue;

            // Prefer target archetypes if listed; fall back to any other NPC.
            var preferred = hints.TargetArchetypePreferences;
            Entity? target = null;
            if (preferred.Length > 0)
            {
                var candidates = others
                    .Where(e => e.Has<NpcArchetypeComponent>() &&
                                preferred.Contains(e.Get<NpcArchetypeComponent>().ArchetypeId,
                                                   StringComparer.OrdinalIgnoreCase))
                    .ToList();
                if (candidates.Count > 0)
                    target = candidates[rng.NextInt(candidates.Count)];
            }
            target ??= others[rng.NextInt(others.Count)];

            var pattern   = ParsePattern(hints.Pattern);
            var intensity = SampleIntRange(config.RelationshipIntensityRange, rng);

            // For The Crush (isTarget = true): another NPC has the crush toward this NPC.
            int seqA = hints.IsTarget ? npcSeqId[target.Id] : npcSeqId[npc.Id];
            int seqB = hints.IsTarget ? npcSeqId[npc.Id]    : npcSeqId[target.Id];

            var key = CanonicalPair(seqA, seqB);
            if (usedPairs.Contains(key)) continue;
            usedPairs.Add(key);

            var rel = CreateRelationship(em, seqA, seqB, new[] { pattern }, intensity);
            created.Add(rel);
        }

        // -- 2. Additional relationships from the starting sketch --------------
        SeedPattern(em, npcs, npcSeqId, rng, config, created, usedPairs,
            RelationshipPattern.Rival,                    config.RivalryCount);
        SeedPattern(em, npcs, npcSeqId, rng, config, created, usedPairs,
            RelationshipPattern.OldFlame,                 config.OldFlameCount);
        SeedPattern(em, npcs, npcSeqId, rng, config, created, usedPairs,
            RelationshipPattern.Mentor,                   config.MentorPairCount);
        SeedPattern(em, npcs, npcSeqId, rng, config, created, usedPairs,
            RelationshipPattern.SleptWithSpouse,          config.SleptWithSpouseCount);
        SeedPattern(em, npcs, npcSeqId, rng, config, created, usedPairs,
            RelationshipPattern.Friend,                   config.FriendPairCount);
        SeedPattern(em, npcs, npcSeqId, rng, config, created, usedPairs,
            RelationshipPattern.TheThingNobodyTalksAbout, config.ThingNobodyTalksAboutCount);

        return created.AsReadOnly();
    }

    // -- Helpers ----------------------------------------------------------------

    private static void SeedPattern(
        EntityManager             em,
        IReadOnlyList<Entity>     npcs,
        Dictionary<Guid, int>     npcSeqId,
        SeededRandom              rng,
        CastGeneratorConfig       config,
        List<Entity>              created,
        HashSet<(int, int)>       usedPairs,
        RelationshipPattern       pattern,
        int                       count)
    {
        int attempts = 0;
        int seeded   = 0;

        while (seeded < count && attempts < npcs.Count * npcs.Count)
        {
            attempts++;
            int ia = rng.NextInt(npcs.Count);
            int ib = rng.NextInt(npcs.Count);
            if (ia == ib) continue;

            var key = CanonicalPair(npcSeqId[npcs[ia].Id], npcSeqId[npcs[ib].Id]);
            if (usedPairs.Contains(key)) continue;

            usedPairs.Add(key);
            var intensity = SampleIntRange(config.RelationshipIntensityRange, rng);
            created.Add(CreateRelationship(
                em,
                npcSeqId[npcs[ia].Id],
                npcSeqId[npcs[ib].Id],
                new[] { pattern },
                intensity));
            seeded++;
        }
    }

    private static Entity CreateRelationship(
        EntityManager          em,
        int                    seqA,
        int                    seqB,
        RelationshipPattern[]  patterns,
        int                    intensity)
    {
        var entity = em.CreateEntity();
        entity.Add(new RelationshipTag());
        entity.Add(new RelationshipComponent(seqA, seqB, patterns, intensity));
        return entity;
    }

    private static DriveValue SampleDrive(
        string              driveName,
        HashSet<string>     elevSet,
        HashSet<string>     depSet,
        SeededRandom        rng,
        CastGeneratorConfig cfg)
    {
        int[] range = elevSet.Contains(driveName) ? cfg.ElevatedDriveRange
                    : depSet.Contains(driveName)  ? cfg.DepressedDriveRange
                    : cfg.NeutralDriveRange;

        int baseline = SampleIntRange(range, rng);
        int jitter   = SampleIntRange(cfg.CurrentJitterRange, rng);
        int current  = SocialDrivesComponent.Clamp0100(baseline + jitter);
        return new DriveValue { Baseline = baseline, Current = current };
    }

    private static int SampleBigFive(int[]? range, SeededRandom rng)
        => range is { Length: >= 2 }
            ? SampleIntRange(range, rng)
            : SampleIntRange(new[] { -1, 1 }, rng);

    private static int SampleIntRange(int[] range, SeededRandom rng)
    {
        if (range is not { Length: >= 2 }) return 0;
        int lo = range[0], hi = range[1];
        if (hi <= lo) return lo;
        return lo + rng.NextInt(hi - lo + 1);
    }

    private static T PickFrom<T>(IReadOnlyList<T> list, SeededRandom rng)
        => list[rng.NextInt(list.Count)];

    private static (int, int) CanonicalPair(int a, int b)
        => (Math.Min(a, b), Math.Max(a, b));

    private static RelationshipPattern ParsePattern(string s) => s switch
    {
        "rival"                    => RelationshipPattern.Rival,
        "oldFlame"                 => RelationshipPattern.OldFlame,
        "activeAffair"             => RelationshipPattern.ActiveAffair,
        "secretCrush"              => RelationshipPattern.SecretCrush,
        "mentor"                   => RelationshipPattern.Mentor,
        "mentee"                   => RelationshipPattern.Mentee,
        "bossOf"                   => RelationshipPattern.BossOf,
        "reportTo"                 => RelationshipPattern.ReportTo,
        "friend"                   => RelationshipPattern.Friend,
        "alliesOfConvenience"      => RelationshipPattern.AlliesOfConvenience,
        "sleptWithSpouse"          => RelationshipPattern.SleptWithSpouse,
        "confidant"                => RelationshipPattern.Confidant,
        "theThingNobodyTalksAbout" => RelationshipPattern.TheThingNobodyTalksAbout,
        _                          => RelationshipPattern.Friend
    };
}

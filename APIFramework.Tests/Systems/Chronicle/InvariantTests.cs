using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Chronicle;
using Xunit;

namespace APIFramework.Tests.Systems.Chronicle;

/// <summary>
/// Chronicle ↔ entity-tree integrity checks inside InvariantSystem.
///
/// AT-07 — BrokenItemTag entity with missing ChronicleEntryId → violation.
/// AT-08 — Chronicle entry with physicalManifestEntityId pointing to missing entity → violation.
/// Plus: StainTag with missing ChronicleEntryId → violation.
///       Well-wired stain + chronicle entry → no violation.
/// </summary>
public class InvariantTests
{
    private static SimulationClock MakeClock() => new();

    // ── AT-07a: StainTag entity with missing chronicle entry → violation ──

    [Fact]
    public void StainTag_MissingChronicleEntry_ProducesViolation()
    {
        var chronicle  = new ChronicleService();  // empty
        var em         = new EntityManager();
        var invariants = new InvariantSystem(MakeClock(), chronicle);

        var stain = EntityTemplates.Stain(em, null, 0f, 0f,
            source: "participant:1",
            magnitude: 30,
            chronicleEntryId: "does-not-exist",
            createdAtTick: 1L);

        invariants.Update(em, 1f);

        var violations = invariants.Violations;
        Assert.Contains(violations,
            v => v.Component == "StainComponent" && v.Property == "ChronicleEntryId");
    }

    // ── AT-07b: BrokenItemTag entity with missing chronicle entry → violation

    [Fact]
    public void BrokenItemTag_MissingChronicleEntry_ProducesViolation()
    {
        var chronicle  = new ChronicleService();  // empty
        var em         = new EntityManager();
        var invariants = new InvariantSystem(MakeClock(), chronicle);

        EntityTemplates.BrokenItem(em, "coffee-mug", null, 0f, 0f,
            BreakageKind.Dropped,
            chronicleEntryId: "ghost-entry",
            createdAtTick: 5L);

        invariants.Update(em, 1f);

        var violations = invariants.Violations;
        Assert.Contains(violations,
            v => v.Component == "BrokenItemComponent" && v.Property == "ChronicleEntryId");
    }

    // ── AT-08: Chronicle entry with missing physicalManifestEntityId → violation

    [Fact]
    public void ChronicleEntry_MissingPhysicalManifestEntity_ProducesViolation()
    {
        var chronicle  = new ChronicleService();
        var em         = new EntityManager();
        var invariants = new InvariantSystem(MakeClock(), chronicle);

        // Append an entry that references an entity that doesn't exist
        string fakeEntityGuid = PhysicalManifestSpawner.IntIdToGuidString(99999);
        chronicle.Append(new ChronicleEntry(
            Id:                       "aaaa0001-0000-0000-0000-000000000000",
            Kind:                     ChronicleEventKind.SpilledSomething,
            Tick:                     1L,
            ParticipantIds:           new List<int>(),
            Location:                 string.Empty,
            Description:              "A spill",
            Persistent:               true,
            PhysicalManifestEntityId: fakeEntityGuid));

        invariants.Update(em, 1f);

        var violations = invariants.Violations;
        Assert.Contains(violations,
            v => v.Component == "ChronicleService" && v.Property == "PhysicalManifestEntityId");
    }

    // ── Well-wired: valid chronicle + stain → no violation ───────────────

    [Fact]
    public void WellWired_StainAndChronicleEntry_NoViolation()
    {
        var chronicle  = new ChronicleService();
        var em         = new EntityManager();
        var invariants = new InvariantSystem(MakeClock(), chronicle);

        const string entryId = "aaaa0002-0000-0000-0000-000000000000";

        var stainEntity = EntityTemplates.Stain(em, null, 3f, 5f,
            source: "participant:1",
            magnitude: 40,
            chronicleEntryId: entryId,
            createdAtTick: 10L);

        string manifestGuid = PhysicalManifestSpawner.IntIdToGuidString(
            PhysicalManifestSpawner.EntityIntId(stainEntity));

        chronicle.Append(new ChronicleEntry(
            Id:                       entryId,
            Kind:                     ChronicleEventKind.SpilledSomething,
            Tick:                     10L,
            ParticipantIds:           new List<int>(),
            Location:                 string.Empty,
            Description:              "Coffee everywhere",
            Persistent:               true,
            PhysicalManifestEntityId: manifestGuid));

        invariants.Update(em, 1f);

        var chronicleViolations = invariants.Violations
            .Where(v => v.Component is "StainComponent"
                                    or "BrokenItemComponent"
                                    or "ChronicleService")
            .ToList();

        Assert.Empty(chronicleViolations);
    }

    // ── No chronicle service → chronicle checks skipped (no crash) ────────

    [Fact]
    public void NoChronicleService_ChronicleChecksSkipped()
    {
        var em         = new EntityManager();
        var invariants = new InvariantSystem(MakeClock());  // chronicle = null

        EntityTemplates.Stain(em, null, 0f, 0f,
            "x", 30, "missing-entry", 1L);

        // Should not throw; no chronicle integrity check runs
        invariants.Update(em, 1f);

        // No StainComponent violation expected (chronicle check not active)
        Assert.DoesNotContain(invariants.Violations,
            v => v.Component == "StainComponent" && v.Property == "ChronicleEntryId");
    }
}

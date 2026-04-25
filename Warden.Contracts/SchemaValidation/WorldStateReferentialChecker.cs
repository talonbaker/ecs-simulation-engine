using System;
using System.Collections.Generic;
using Warden.Contracts.Telemetry;

namespace Warden.Contracts.SchemaValidation;

/// <summary>
/// Validates referential integrity within a <see cref="WorldStateDto"/> that
/// JSON-schema validation alone cannot enforce (cross-array id resolution,
/// canonical pair ordering, duplicate-pair detection).
///
/// Returns a <see cref="ValidationResult"/> with the same shape as
/// <see cref="SchemaValidator.Validate"/> so callers handle one error type.
/// </summary>
public static class WorldStateReferentialChecker
{
    public static ValidationResult Check(WorldStateDto dto)
    {
        var errors = new List<string>();

        // Build entity-id set; flag duplicate entity ids.
        var entityIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entity in dto.Entities)
        {
            if (!entityIds.Add(entity.Id))
                errors.Add($"duplicate entity id '{entity.Id}'");
        }

        var relationshipIds = new HashSet<string>(StringComparer.Ordinal);

        if (dto.Relationships is { } rels)
        {
            // Normalised (lo|hi) pair keys for unordered duplicate detection.
            var seenPairs = new HashSet<string>(StringComparer.Ordinal);

            foreach (var rel in rels)
            {
                if (rel.ParticipantA == rel.ParticipantB)
                    errors.Add($"relationship '{rel.Id}': participantA and participantB must differ.");

                if (!entityIds.Contains(rel.ParticipantA))
                    errors.Add($"relationship '{rel.Id}': participantA '{rel.ParticipantA}' not found in entities.");

                if (!entityIds.Contains(rel.ParticipantB))
                    errors.Add($"relationship '{rel.Id}': participantB '{rel.ParticipantB}' not found in entities.");

                // Canonical normalisation: smaller id always comes first.
                var (lo, hi) = string.Compare(rel.ParticipantA, rel.ParticipantB, StringComparison.Ordinal) <= 0
                    ? (rel.ParticipantA, rel.ParticipantB)
                    : (rel.ParticipantB, rel.ParticipantA);

                if (!seenPairs.Add($"{lo}|{hi}"))
                    errors.Add("duplicate-pair");

                relationshipIds.Add(rel.Id);
            }
        }

        if (dto.MemoryEvents is { } events)
        {
            foreach (var ev in events)
            {
                // v0.2 rule: global scope is reserved for v0.3.
                if (ev.Scope == MemoryScope.Global)
                    errors.Add("global-scope-reserved-for-v0.3");

                foreach (var p in ev.Participants)
                    if (!entityIds.Contains(p))
                        errors.Add($"memoryEvent '{ev.Id}': participant '{p}' not found in entities.");

                if (ev.RelationshipId is not null && !relationshipIds.Contains(ev.RelationshipId))
                    errors.Add($"memoryEvent '{ev.Id}': relationshipId '{ev.RelationshipId}' not found in relationships.");
            }
        }

        return errors.Count == 0
            ? ValidationResult.Ok
            : new ValidationResult(false, errors.AsReadOnly());
    }
}

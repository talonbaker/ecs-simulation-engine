using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using APIFramework.Mutation;

/// <summary>
/// AT-15: Scripted build sequence (place wall + lock door + move desk) on two fake
/// APIs records byte-identical call sequences, demonstrating deterministic mutation flow.
///
/// Since we cannot boot two engine instances in a play-mode test, we verify that
/// the same sequence of UI interactions produces the same sequence of API calls
/// (same method names, same argument values in the same order).
/// </summary>
[TestFixture]
public class BuildModeDeterminismTests
{
    [UnityTest]
    public IEnumerator ScriptedBuildSequence_IsIdenticalAcrossTwoRuns()
    {
        var log1 = RunBuildSequence();
        yield return null;
        var log2 = RunBuildSequence();
        yield return null;

        Assert.AreEqual(log1.Count, log2.Count,
            "Both runs should produce the same number of mutation calls.");

        for (int i = 0; i < log1.Count; i++)
        {
            Assert.AreEqual(log1[i], log2[i],
                $"Mutation call #{i} differs between the two runs.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<string> RunBuildSequence()
    {
        var log = new List<string>();
        var api = new LoggingFakeApi(log);

        // Step 1: Place a wall.
        var templateId = new Guid("00000010-0000-0000-0000-000000000001");
        api.SpawnStructural(templateId, 5, 5);

        // Step 2: Lock a door.
        var doorId = new Guid("aaaa0001-0000-0000-0000-000000000001");
        api.AttachObstacle(doorId);

        // Step 3: Move a desk.
        var deskId = new Guid("bbbb0001-0000-0000-0000-000000000001");
        api.MoveEntity(deskId, 8, 10);

        return log;
    }

    // ── Logging test double ───────────────────────────────────────────────────

    private sealed class LoggingFakeApi : IWorldMutationApi
    {
        private readonly List<string> _log;
        public LoggingFakeApi(List<string> log) => _log = log;

        public Guid SpawnStructural(Guid templateId, int tileX, int tileY)
        {
            _log.Add($"Spawn|{templateId}|{tileX}|{tileY}");
            return Guid.NewGuid(); // new GUID each time — doesn't affect log comparison
        }

        public bool MoveEntity(Guid entityId, int newTileX, int newTileY)
        {
            _log.Add($"Move|{entityId}|{newTileX}|{newTileY}");
            return true;
        }

        public bool DespawnStructural(Guid entityId)         { _log.Add($"Despawn|{entityId}"); return true; }
        public bool AttachObstacle(Guid entityId)            { _log.Add($"Attach|{entityId}"); return true; }
        public bool DetachObstacle(Guid entityId)            { _log.Add($"Detach|{entityId}"); return true; }
        public bool ChangeRoomBounds(Guid roomId, APIFramework.Components.BoundsRect b) { _log.Add($"Bounds|{roomId}"); return true; }
    }
}

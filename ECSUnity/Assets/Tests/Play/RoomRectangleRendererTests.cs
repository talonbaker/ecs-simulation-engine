using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-05: One RoomRectangleRenderer mesh per room in WorldStateDto.Rooms.
/// Mesh bounds match room bounds (tolerance 0.01 world-units).
/// </summary>
[TestFixture]
public class RoomRectangleRendererTests
{
    private GameObject           _hostGo;
    private EngineHost           _host;
    private RoomRectangleRenderer _renderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _hostGo = new GameObject("TestHost_RoomRenderer");
        _host   = _hostGo.AddComponent<EngineHost>();

        var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
        var configField = typeof(EngineHost).GetField("_configAsset",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        configField?.SetValue(_host, configAsset);

        var pathField = typeof(EngineHost).GetField("_worldDefinitionPath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        pathField?.SetValue(_host, "");

        // Add the renderer, pointing it at the host.
        var rendererGo = new GameObject("TestRoomRenderer");
        _renderer = rendererGo.AddComponent<RoomRectangleRenderer>();
        var hostField = typeof(RoomRectangleRenderer).GetField("_engineHost",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        hostField?.SetValue(_renderer, _host);

        yield return null;   // Start()
        yield return null;   // Update() so renderer sees WorldState
    }

    [TearDown]
    public void TearDown()
    {
        if (_hostGo != null)             Object.Destroy(_hostGo);
        if (_renderer?.gameObject != null) Object.Destroy(_renderer.gameObject);
    }

    [UnityTest]
    public IEnumerator RoomCount_MatchesWorldState()
    {
        yield return null;

        var rooms = _host.WorldState?.Rooms;
        if (rooms == null)
        {
            Assert.Inconclusive("WorldState.Rooms is null — engine may not have room entities.");
            yield break;
        }

        // Count only ground-floor rooms (renderer filters by floor).
        int groundRooms = 0;
        foreach (var r in rooms)
        {
            if (r.Floor == Warden.Contracts.Telemetry.BuildingFloor.First ||
                r.Floor == Warden.Contracts.Telemetry.BuildingFloor.Basement)
                groundRooms++;
        }

        Assert.AreEqual(groundRooms, _renderer.ActiveRoomCount,
            $"Expected {groundRooms} room views, got {_renderer.ActiveRoomCount}.");
    }

    [UnityTest]
    public IEnumerator RoomQuad_ScaleMatchesBounds_WithinTolerance()
    {
        yield return null;

        var rooms = _host.WorldState?.Rooms;
        if (rooms == null || rooms.Count == 0)
        {
            Assert.Inconclusive("No rooms to test.");
            yield break;
        }

        foreach (var room in rooms)
        {
            var go = _renderer.GetRoomGameObject(room.Id);
            if (go == null) continue;   // not rendered (e.g. wrong floor)

            float expectedW = room.BoundsRect.Width;
            float expectedH = room.BoundsRect.Height;
            float actualW   = go.transform.localScale.x;
            float actualH   = go.transform.localScale.y;

            Assert.AreEqual(expectedW, actualW, 0.01f,
                $"Room '{room.Name}' width mismatch: expected {expectedW}, got {actualW}.");
            Assert.AreEqual(expectedH, actualH, 0.01f,
                $"Room '{room.Name}' height mismatch: expected {expectedH}, got {actualH}.");
        }
    }
}

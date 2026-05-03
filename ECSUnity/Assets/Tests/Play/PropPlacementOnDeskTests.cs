using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using APIFramework.Build;
using APIFramework.Components;

/// <summary>
/// AT-02  Monitor on desk: valid stack; monitor lands at desk's TopHeight.
/// AT-04  Printer on desk: valid stack; printer lands at desk's TopHeight.
/// AT-03  Desk on chair: rejected.
/// AT-05  Two-tile desk half on small platform, half on empty floor: rejected.
/// AT-06  Two-tile desk on two adjacent empty tiles: valid.
///
/// These tests exercise the pure <see cref="FootprintGeometry"/> logic used by
/// <see cref="DragHandler"/> — they verify the expected Y heights and validity
/// results using the canonical tile-based CanPlaceAt helper, which is what
/// the engine-side logic uses before translating to Unity world-space.
/// </summary>
[TestFixture]
public class PropPlacementOnDeskTests
{
    // ── Fixtures from catalog v0.1.0 ──────────────────────────────────────────

    private static BuildFootprintComponent DeskFp     => new() { WidthTiles = 2, DepthTiles = 1, BottomHeight = 0f, TopHeight = 0.75f, CanStackOnTop = true  };
    private static BuildFootprintComponent ChairFp    => new() { WidthTiles = 1, DepthTiles = 1, BottomHeight = 0f, TopHeight = 0.45f, CanStackOnTop = false };
    private static BuildFootprintComponent MonitorFp  => new() { WidthTiles = 1, DepthTiles = 1, BottomHeight = 0f, TopHeight = 0.40f, CanStackOnTop = false };
    private static BuildFootprintComponent PrinterFp  => new() { WidthTiles = 1, DepthTiles = 1, BottomHeight = 0f, TopHeight = 0.35f, CanStackOnTop = true  };
    private static BuildFootprintComponent PlatformFp => new() { WidthTiles = 1, DepthTiles = 1, BottomHeight = 0f, TopHeight = 0.30f, CanStackOnTop = true  };

    // AT-02: Monitor on desk lands at desk's TopHeight.
    [UnityTest]
    public IEnumerator MonitorOnDesk_ValidPlacement_AtDeskTopHeight()
    {
        yield return null;
        var desk = DeskFp;
        System.Func<(int, int), (string, BuildFootprintComponent)?> occ =
            t => (t == (5, 5) || t == (6, 5)) ? ("desk-1", desk) : ((string, BuildFootprintComponent)?)null;

        bool valid = FootprintGeometry.CanPlaceAt((5, 5), MonitorFp, occ, out float surfaceY);

        Assert.IsTrue(valid, "Monitor on desk must be a valid placement.");
        Assert.AreEqual(desk.BottomHeight + desk.TopHeight, surfaceY, 0.001f,
            "Monitor must land at desk.BottomHeight + desk.TopHeight.");
    }

    // AT-04: Printer on desk lands at desk's TopHeight.
    [UnityTest]
    public IEnumerator PrinterOnDesk_ValidPlacement_AtDeskTopHeight()
    {
        yield return null;
        var desk = DeskFp;
        System.Func<(int, int), (string, BuildFootprintComponent)?> occ =
            t => (t == (5, 5) || t == (6, 5)) ? ("desk-1", desk) : ((string, BuildFootprintComponent)?)null;

        bool valid = FootprintGeometry.CanPlaceAt((5, 5), PrinterFp, occ, out float surfaceY);

        Assert.IsTrue(valid, "Printer on desk must be a valid placement.");
        Assert.AreEqual(desk.BottomHeight + desk.TopHeight, surfaceY, 0.001f,
            "Printer must land at desk.BottomHeight + desk.TopHeight.");
    }

    // AT-03: Desk on chair is rejected.
    [UnityTest]
    public IEnumerator DeskOnChair_Rejected()
    {
        yield return null;
        var chair = ChairFp;
        System.Func<(int, int), (string, BuildFootprintComponent)?> occ =
            t => t == (5, 5) ? ("chair-1", chair) : ((string, BuildFootprintComponent)?)null;

        bool valid = FootprintGeometry.CanPlaceAt((5, 5), DeskFp, occ, out _);
        Assert.IsFalse(valid, "Desk on chair must be rejected (chair.CanStackOnTop = false).");
    }

    // AT-05: Two-tile desk half on small platform, half on empty floor → rejected.
    [UnityTest]
    public IEnumerator Desk_HalfOnPlatform_HalfOnFloor_Rejected()
    {
        yield return null;
        var platform = PlatformFp;
        System.Func<(int, int), (string, BuildFootprintComponent)?> occ =
            t => t == (5, 5) ? ("platform-1", platform) : ((string, BuildFootprintComponent)?)null;

        bool valid = FootprintGeometry.CanPlaceAt((5, 5), DeskFp, occ, out _);
        Assert.IsFalse(valid, "Desk half on platform, half on empty floor must be rejected.");
    }

    // AT-06: Two-tile desk on two adjacent empty floor tiles → valid.
    [UnityTest]
    public IEnumerator Desk_OnTwoAdjacentEmptyTiles_Valid()
    {
        yield return null;
        bool valid = FootprintGeometry.CanPlaceAt((5, 5), DeskFp, _ => null, out float y);
        Assert.IsTrue(valid, "Desk on two adjacent empty tiles must be valid.");
        Assert.AreEqual(0f, y, 0.001f, "Floor placement surfaceY must be 0.");
    }

    // PropFootprintBridge.ToComponent() round-trips to correct BuildFootprintComponent.
    [UnityTest]
    public IEnumerator PropFootprintBridge_ToComponent_RoundTrips()
    {
        var go     = new GameObject("PropPlacement_Bridge");
        var bridge = go.AddComponent<PropFootprintBridge>();
        bridge.Configure(2, 1, 0f, 0.75f, canStackOnTop: true);
        yield return null;

        var fp = bridge.ToComponent();
        Assert.AreEqual(2,    fp.WidthTiles);
        Assert.AreEqual(1,    fp.DepthTiles);
        Assert.AreEqual(0f,   fp.BottomHeight, 0.001f);
        Assert.AreEqual(0.75f,fp.TopHeight,    0.001f);
        Assert.IsTrue(fp.CanStackOnTop);

        Object.Destroy(go);
    }
}

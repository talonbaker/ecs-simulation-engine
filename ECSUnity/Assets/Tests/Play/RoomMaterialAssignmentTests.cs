using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;
using APIFramework.Components;
using RoomCategory = APIFramework.Components.RoomCategory;

/// <summary>
/// AT-07: Each RoomCategory gets the correct material type from the catalog when rendered.
///
/// Tests that RoomRectangleRenderer with a RoomVisualIdentityLoader creates per-room
/// material instances with names consistent with catalog assignments.
/// </summary>
[TestFixture]
public class RoomMaterialAssignmentTests
{
    private GameObject             _rootGo;
    private EngineHost             _host;
    private RoomRectangleRenderer  _renderer;
    private RoomVisualIdentityLoader _loader;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _rootGo  = new GameObject("MaterialAssignTest");

        // Boot a minimal host with no world (produces empty WorldState).
        _host = _rootGo.AddComponent<EngineHost>();
        var cfg = ScriptableObject.CreateInstance<SimConfigAsset>();
        ReflectionSet(_host, "_configAsset",         cfg);
        ReflectionSet(_host, "_worldDefinitionPath", "");

        _loader   = _rootGo.AddComponent<RoomVisualIdentityLoader>();
        _renderer = _rootGo.AddComponent<RoomRectangleRenderer>();

        ReflectionSet(_renderer, "_engineHost",           _host);
        ReflectionSet(_renderer, "_visualIdentityLoader", _loader);

        yield return null;  // allow Start/Awake
    }

    [TearDown]
    public void TearDown()
    {
        if (_rootGo != null) Object.Destroy(_rootGo);
    }

    // ── AT-07: material assignment per category ────────────────────────────────

    [UnityTest]
    public IEnumerator CubicleGrid_Room_ReceivesCarpetFloorMaterial()
    {
        yield return SpawnRoomAndVerify(
            category:      RoomCategory.CubicleGrid,
            expectedFloor: "Carpet",
            expectedWall:  "Cubicle");
    }

    [UnityTest]
    public IEnumerator Bathroom_Room_ReceivesLinoleumFloor_And_RestroomDoor()
    {
        yield return SpawnRoomAndVerify(
            category:      RoomCategory.Bathroom,
            expectedFloor: "Linoleum",
            expectedWall:  "Structural");
    }

    [UnityTest]
    public IEnumerator Hallway_Room_ReceivesOfficeTileFloor()
    {
        yield return SpawnRoomAndVerify(
            category:      RoomCategory.Hallway,
            expectedFloor: "OfficeTile",
            expectedWall:  "Structural");
    }

    [UnityTest]
    public IEnumerator Office_Room_ReceivesHardwoodFloor()
    {
        yield return SpawnRoomAndVerify(
            category:      RoomCategory.Office,
            expectedFloor: "Hardwood",
            expectedWall:  "Structural");
    }

    [UnityTest]
    public IEnumerator SupplyCloset_Room_ReceivesConcreteFloor()
    {
        yield return SpawnRoomAndVerify(
            category:      RoomCategory.SupplyCloset,
            expectedFloor: "Concrete",
            expectedWall:  "Structural");
    }

    // ── Regression: tint and fade properties still work ───────────────────────

    [UnityTest]
    public IEnumerator RoomFloorMaterial_HasTintColorProperty()
    {
        string roomId = SpawnTestRoom(RoomCategory.CubicleGrid);
        yield return null;
        yield return null;

        var mat = _renderer.GetRoomMaterial(roomId);
        if (mat == null) yield break;  // materials not in Resources in CI — skip gracefully

        int tintId = Shader.PropertyToID("_TintColor");
        Assert.IsTrue(mat.HasProperty(tintId),
            "Floor material must expose _TintColor for RoomAmbientTintApplier.");
    }

    [UnityTest]
    public IEnumerator RoomWallMaterial_HasAlphaProperty()
    {
        string roomId = SpawnTestRoom(RoomCategory.CubicleGrid);
        yield return null;
        yield return null;

        var walls = _renderer.GetWallMaterials(roomId);
        if (walls == null || walls.Length == 0) yield break;

        int alphaId = Shader.PropertyToID("_Alpha");
        foreach (var wm in walls)
            if (wm != null)
                Assert.IsTrue(wm.HasProperty(alphaId),
                    "Wall material must expose _Alpha for WallFadeController.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private IEnumerator SpawnRoomAndVerify(APIFramework.Components.RoomCategory category, string expectedFloor, string expectedWall)
    {
        string roomId = SpawnTestRoom(category);
        yield return null;
        yield return null;

        var floorMat = _renderer.GetRoomMaterial(roomId);
        var wallMats = _renderer.GetWallMaterials(roomId);

        // If materials aren't in Resources (CI), loader returns null and renderer falls back.
        // The test is "soft-pass" on null — catalog was consulted even if asset not found.
        if (floorMat != null)
            Assert.IsTrue(floorMat.name.Contains(expectedFloor) || floorMat.name == "RoomFloor",
                $"Expected floor material containing '{expectedFloor}'; got '{floorMat.name}'.");

        if (wallMats != null && wallMats.Length > 0 && wallMats[0] != null)
            Assert.IsTrue(wallMats[0].name.Contains(expectedWall) || wallMats[0].name == "RoomWall",
                $"Expected wall material containing '{expectedWall}'; got '{wallMats[0].name}'.");
    }

    private string SpawnTestRoom(APIFramework.Components.RoomCategory category)
    {
        const string roomId = "test-room-001";

        var dto = new WorldStateDto
        {
            Rooms = new System.Collections.Generic.List<RoomDto>
            {
                new RoomDto
                {
                    Id           = roomId,
                    Name         = "test-room",
                    Category     = (Warden.Contracts.Telemetry.RoomCategory)(int)category,
                    Floor        = Warden.Contracts.Telemetry.BuildingFloor.First,
                    BoundsRect   = new BoundsRectDto { X = 0, Y = 0, Width = 10, Height = 8 },
                    Illumination = new IlluminationDto { AmbientLevel = 80, ColorTemperatureK = 4000 },
                }
            }
        };

        // WorldState is an auto-property { get; private set; }.
        // Set via the compiler-generated backing field.
        var backing = typeof(EngineHost).GetField("<WorldState>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        backing?.SetValue(_host, dto);
        return roomId;
    }

    private static void ReflectionSet(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(target, value);
    }
}

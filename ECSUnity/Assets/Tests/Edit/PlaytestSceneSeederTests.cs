using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit-mode tests for PlaytestSceneSeeder defaults and the playtest-office world definition.
/// </summary>
public class PlaytestSceneSeederTests
{
    private const string WorldDefinitionPath =
        "ECSUnity/Assets/StreamingAssets/playtest-office.json";

    // ── World-definition JSON tests ───────────────────────────────────────────

    [Test]
    public void WorldDefinition_ParsesWithoutError()
    {
        string path = FindWorldDefinitionPath();
        Assert.IsNotNull(path, $"playtest-office.json not found (searched from {Application.dataPath})");

        string json = File.ReadAllText(path);
        Assert.DoesNotThrow(() => JObject.Parse(json), "playtest-office.json must parse as valid JSON");
    }

    [Test]
    public void WorldDefinition_HasExpectedNpcCount()
    {
        var root = LoadWorldJson();
        var npcSlots = root["npcSlots"] as JArray;
        Assert.IsNotNull(npcSlots, "npcSlots array must exist");
        Assert.AreEqual(15, npcSlots.Count, "World definition must define exactly 15 NPC slots");
    }

    [Test]
    public void WorldDefinition_CoversAllTenArchetypes()
    {
        var root = LoadWorldJson();
        var npcSlots = root["npcSlots"] as JArray;
        Assert.IsNotNull(npcSlots);

        var archetypes = new System.Collections.Generic.HashSet<string>();
        foreach (var slot in npcSlots)
            if (slot["archetypeHint"] != null)
                archetypes.Add(slot["archetypeHint"].ToString());

        string[] required =
        {
            "the-vent", "the-hermit", "the-climber", "the-cynic", "the-newbie",
            "the-old-hand", "the-affair", "the-recovering", "the-founders-nephew", "the-crush"
        };

        foreach (var archetype in required)
            Assert.IsTrue(archetypes.Contains(archetype),
                $"World definition must include at least one NPC with archetypeHint '{archetype}'");
    }

    [Test]
    public void WorldDefinition_HasExpectedRoomCount()
    {
        var root = LoadWorldJson();
        var rooms = root["rooms"] as JArray;
        Assert.IsNotNull(rooms, "rooms array must exist");
        Assert.GreaterOrEqual(rooms.Count, 5, "Must have at least 5 rooms (cubicle area, kitchen, bathrooms, supply closet)");
    }

    [Test]
    public void WorldDefinition_HasAtLeastThreeStainHazards()
    {
        var root = LoadWorldJson();
        var objects = root["objectsAtAnchors"] as JArray;
        Assert.IsNotNull(objects);

        int stainCount = 0;
        foreach (var obj in objects)
            if (obj["physicalType"]?.ToString() == "stain") stainCount++;

        Assert.GreaterOrEqual(stainCount, 3, "Must pre-seed at least 3 stain hazards");
    }

    [Test]
    public void WorldDefinition_HasAtLeastSixBreakableProps()
    {
        var root = LoadWorldJson();
        var objects = root["objectsAtAnchors"] as JArray;
        Assert.IsNotNull(objects);

        int breakableCount = 0;
        foreach (var obj in objects)
            if (obj["hasBreakableComponent"] != null && obj["hasBreakableComponent"].Value<bool>())
                breakableCount++;

        Assert.GreaterOrEqual(breakableCount, 6, "Must pre-seed at least 6 breakable props");
    }

    [Test]
    public void WorldDefinition_HasLockableBathroomDoors()
    {
        var root = LoadWorldJson();
        var objects = root["objectsAtAnchors"] as JArray;
        Assert.IsNotNull(objects);

        int lockableDoorCount = 0;
        foreach (var obj in objects)
            if (obj["physicalType"]?.ToString() == "door" &&
                obj["lockComponent"]?.ToString() == "LockedInComponent")
                lockableDoorCount++;

        Assert.GreaterOrEqual(lockableDoorCount, 1, "Must have at least one lockable door (bathroom)");
    }

    [Test]
    public void WorldDefinition_HasKitchenFoodItems()
    {
        var root = LoadWorldJson();
        var objects = root["objectsAtAnchors"] as JArray;
        Assert.IsNotNull(objects);

        bool hasMicrowave = false;
        foreach (var obj in objects)
        {
            if (obj["physicalType"]?.ToString() == "microwave")
            {
                hasMicrowave = true;
                var foodItems = obj["foodItems"] as JArray;
                Assert.IsNotNull(foodItems, "Microwave must have foodItems (choking-on-food substrate)");
                Assert.Greater(foodItems.Count, 0, "Microwave must have at least one food item");
                break;
            }
        }

        Assert.IsTrue(hasMicrowave, "Kitchen must contain a microwave object");
    }

    // ── PlaytestSceneSeeder default field tests ───────────────────────────────

    [Test]
    public void SeederDefaults_NpcCountIsPositive()
    {
        var go = new GameObject("TestSeeder");
        go.AddComponent<EngineHost>();
        var seeder = go.AddComponent<PlaytestSceneSeeder>();

        Assert.Greater(seeder.NpcCount, 0, "_npcCount default must be > 0");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void SeederDefaults_AllArchetypesRepresented()
    {
        // With EvenAcrossAll and 15 NPCs across 10 archetypes, NpcCount >= archetype count.
        var go = new GameObject("TestSeeder");
        go.AddComponent<EngineHost>();
        var seeder = go.AddComponent<PlaytestSceneSeeder>();

        Assert.GreaterOrEqual(seeder.NpcCount, 10,
            "Default NPC count must be >= 10 to cover all archetypes with EvenAcrossAll mode");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void SeederDefaults_WorldDefinitionPathIsPlaytestOffice()
    {
        var go = new GameObject("TestSeeder");
        go.AddComponent<EngineHost>();
        var seeder = go.AddComponent<PlaytestSceneSeeder>();

        Assert.AreEqual("playtest-office.json", seeder.WorldDefinitionPath,
            "Default world path must be 'playtest-office.json'");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void SeederDefaults_SeedStainCountIsPositive()
    {
        var go = new GameObject("TestSeeder");
        go.AddComponent<EngineHost>();
        var seeder = go.AddComponent<PlaytestSceneSeeder>();

        Assert.Greater(seeder.SeedStainsAtBoot, 0, "_seedStainsAtBoot default must be > 0");

        Object.DestroyImmediate(go);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static JObject LoadWorldJson()
    {
        string path = FindWorldDefinitionPath();
        Assert.IsNotNull(path, "playtest-office.json must exist");
        return JObject.Parse(File.ReadAllText(path));
    }

    private static string FindWorldDefinitionPath()
    {
        // Try StreamingAssets-relative from Application.dataPath
        var candidates = new[]
        {
            Path.Combine(Application.streamingAssetsPath, "playtest-office.json"),
            Path.Combine(Application.dataPath, "..", WorldDefinitionPath),
            Path.Combine(Application.dataPath, "..", "..", "..", WorldDefinitionPath)
        };

        foreach (var c in candidates)
            if (File.Exists(c)) return Path.GetFullPath(c);

        return null;
    }
}

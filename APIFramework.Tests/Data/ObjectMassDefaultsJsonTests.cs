using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace APIFramework.Tests.Data;

/// <summary>AT-10: object-mass-defaults.json loads; all required kinds present; values in range.</summary>
public class ObjectMassDefaultsJsonTests
{
    private static readonly string[] RequiredKinds =
        { "mug", "stapler", "phone", "chair", "wine-bottle", "window-pane", "potted-plant" };

    private static JArray LoadJson()
    {
        // Walk up from cwd to find the file (mirrors SimConfig discovery pattern)
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "docs", "c2-content", "objects", "object-mass-defaults.json");
            if (File.Exists(candidate))
            {
                var root = JObject.Parse(File.ReadAllText(candidate));
                return (JArray)root["objectMass"]!;
            }
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        throw new FileNotFoundException("object-mass-defaults.json not found from CWD upward.");
    }

    [Fact]
    public void AT10_JsonFile_Exists_AndParseable()
    {
        var arr = LoadJson();
        Assert.NotNull(arr);
        Assert.NotEmpty(arr);
    }

    [Fact]
    public void AT10_AllRequiredKinds_Present()
    {
        var arr   = LoadJson();
        var kinds = arr.Select(obj => obj["objectKind"]!.Value<string>()).ToHashSet();
        foreach (var required in RequiredKinds)
            Assert.Contains(required, kinds);
    }

    [Fact]
    public void AT10_MassKg_InRange_ForAllEntries()
    {
        var arr = LoadJson();
        foreach (var obj in arr)
        {
            var massKg = obj["massKg"]!.Value<float>();
            Assert.True(massKg >= 0.01f && massKg <= 200f,
                $"massKg={massKg} out of range for {obj["objectKind"]}");
        }
    }

    [Fact]
    public void AT10_HitEnergyThreshold_NonNegative()
    {
        var arr = LoadJson();
        foreach (var obj in arr)
        {
            var threshold = obj["hitEnergyThreshold"]!.Value<float>();
            Assert.True(threshold >= 0f,
                $"hitEnergyThreshold={threshold} is negative for {obj["objectKind"]}");
        }
    }

    [Fact]
    public void AT10_SchemaVersion_IsPresent()
    {
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "docs", "c2-content", "objects", "object-mass-defaults.json");
            if (File.Exists(candidate))
            {
                var root = JObject.Parse(File.ReadAllText(candidate));
                Assert.NotNull(root["schemaVersion"]);
                return;
            }
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        throw new FileNotFoundException("object-mass-defaults.json not found.");
    }
}

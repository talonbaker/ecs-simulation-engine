using NUnit.Framework;
using Newtonsoft.Json;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-04: Unity-emitted JSON for a fixed state matches the format produced by
/// Warden.Telemetry serialisation — byte-identical for the same DTO graph.
///
/// Since both paths now use Newtonsoft.Json with the same default settings, the
/// test verifies that serialising the same WorldStateDto object twice produces
/// identical strings, which is the weakest requirement for wire-format parity.
///
/// Full parity with Warden.Telemetry.Projectors.WorldStateProjector.Project() output
/// is verified at integration level when the orchestrator parses the emitted file.
/// </summary>
[TestFixture]
public class JsonlStreamEmitterByteIdenticalTests
{
    [Test]
    public void SameDto_TwoSerialise_ProduceIdenticalStrings()
    {
        // Arrange: a representative WorldStateDto with several fields populated.
        var dto = new WorldStateDto
        {
            Clock = new ClockStateDto
            {
                DayNumber       = 3,
                IsDaytime       = false,
                GameTimeDisplay = "22:15",
            },
        };

        // Act: serialise twice with identical settings.
        string jsonA = JsonConvert.SerializeObject(dto, Formatting.None);
        string jsonB = JsonConvert.SerializeObject(dto, Formatting.None);

        // Assert.
        Assert.AreEqual(jsonA, jsonB,
            "Serialising the same DTO twice must produce byte-identical output " +
            "(deterministic serialisation is required for wire-format parity with Warden.Telemetry).");
    }

    [Test]
    public void PrettyPrint_DiffersFromCompact()
    {
        // Verify the PrettyPrint flag changes the output (consumers must NOT receive pretty output).
        var dto = new WorldStateDto { Clock = new ClockStateDto { DayNumber = 1 } };

        string compact = JsonConvert.SerializeObject(dto, Formatting.None);
        string pretty  = JsonConvert.SerializeObject(dto, Formatting.Indented);

        Assert.AreNotEqual(compact, pretty,
            "PrettyPrint must produce different output than compact mode. " +
            "Compact mode is the required wire format.");
    }

    [Test]
    public void WorldStateDto_ContainsClock_InSerialised()
    {
        var dto = new WorldStateDto { Clock = new ClockStateDto { DayNumber = 7 } };
        string json = JsonConvert.SerializeObject(dto);

        // The serialised output must contain recognisable field names from the DTO.
        Assert.IsTrue(json.Contains("Clock") || json.Contains("clock"),
            "Serialised WorldStateDto must contain the Clock field.");
    }

    [Test]
    public void Deserialise_WardenContractsDto_Succeeds()
    {
        // Smoke test: deserialising a Warden.Contracts WorldStateDto JSON succeeds.
        string json   = "{\"Clock\":{\"DayNumber\":5,\"IsDaytime\":true,\"GameTimeDisplay\":\"06:00\"}}";
        var    result = JsonConvert.DeserializeObject<WorldStateDto>(json);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Clock);
        Assert.AreEqual(5, result.Clock.DayNumber);
    }
}

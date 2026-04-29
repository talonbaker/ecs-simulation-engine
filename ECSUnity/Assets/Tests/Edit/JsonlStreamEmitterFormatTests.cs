using System.IO;
using NUnit.Framework;
using Newtonsoft.Json;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-03: Each JSONL line parses as a valid WorldStateDto; round-trip through
/// JsonSerializer.Deserialize produces an equivalent DTO.
/// </summary>
[TestFixture]
public class JsonlStreamEmitterFormatTests
{
    [Test]
    public void WorldStateDto_SerialiseDeserialise_RoundTrip()
    {
        // Arrange: build a minimal WorldStateDto.
        var dto = new WorldStateDto
        {
            Clock = new ClockStateDto
            {
                DayNumber        = 1,
                IsDaytime        = true,
                GameTimeDisplay  = "08:00",
            },
        };

        // Act: serialise then deserialise.
        string json       = JsonConvert.SerializeObject(dto, Formatting.None);
        var    roundTrip  = JsonConvert.DeserializeObject<WorldStateDto>(json);

        // Assert.
        Assert.IsNotNull(roundTrip, "Deserialised DTO must not be null.");
        Assert.IsNotNull(roundTrip.Clock, "Clock must survive round-trip.");
        Assert.AreEqual(dto.Clock.DayNumber, roundTrip.Clock.DayNumber,
            "DayNumber must survive round-trip.");
        Assert.AreEqual(dto.Clock.GameTimeDisplay, roundTrip.Clock.GameTimeDisplay,
            "GameTimeDisplay must survive round-trip.");
    }

    [Test]
    public void WorldStateDto_SerialiseIsValidJson()
    {
        var dto  = new WorldStateDto { Clock = new ClockStateDto { DayNumber = 2 } };
        string json = JsonConvert.SerializeObject(dto);

        // Must not contain newlines — JSONL requires one JSON object per line.
        Assert.IsFalse(json.Contains('\n'),
            "Serialised DTO must not contain newlines (JSONL format requires one object per line).");
        Assert.IsFalse(json.Contains('\r'),
            "Serialised DTO must not contain carriage returns.");
    }

    [Test]
    public void NullDto_SerialisesToNullString_NotThrow()
    {
        // Serialising a null DTO should produce "null" and not throw.
        bool threw = false;
        string json = null;
        try { json = JsonConvert.SerializeObject((WorldStateDto)null); }
        catch { threw = true; }

        Assert.IsFalse(threw, "Serialising null DTO must not throw.");
        Assert.AreEqual("null", json, "Serialising null must produce the string 'null'.");
    }

    [Test]
    public void EmptyDto_SerialisesAndDeserialises()
    {
        var dto      = new WorldStateDto();
        string json  = JsonConvert.SerializeObject(dto);
        var result   = JsonConvert.DeserializeObject<WorldStateDto>(json);

        Assert.IsNotNull(result, "Empty DTO must deserialise without error.");
    }
}

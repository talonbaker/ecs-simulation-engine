using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Audio;
using APIFramework.Systems.Dialog;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Audio;

/// <summary>
/// Tests that DialogFragmentRetrievalSystem emits SpeechFragment with register-scaled intensity.
/// </summary>
public class DialogSpeechFragmentEmitTests
{
    private const string CasualCorpus = """
        {
          "schemaVersion": "0.1.0",
          "fragments": [
            {
              "id": "casual-greeting-A",
              "text": "Hey there.",
              "register": "casual",
              "context": "greeting",
              "valenceProfile": {},
              "noteworthiness": 10
            }
          ]
        }
        """;

    private const string CrassCorpus = """
        {
          "schemaVersion": "0.1.0",
          "fragments": [
            {
              "id": "crass-greeting-A",
              "text": "Oi!",
              "register": "crass",
              "context": "greeting",
              "valenceProfile": {},
              "noteworthiness": 10
            }
          ]
        }
        """;

    private const string ClippedCorpus = """
        {
          "schemaVersion": "0.1.0",
          "fragments": [
            {
              "id": "clipped-greeting-A",
              "text": "Yes.",
              "register": "clipped",
              "context": "greeting",
              "valenceProfile": {},
              "noteworthiness": 10
            }
          ]
        }
        """;

    private static DialogConfig DefaultCfg() => new()
    {
        ValenceMatchScore        = 5,
        RecencyPenalty           = -10,
        CalcifyBiasScore         = 3,
        ValenceLowMaxValue       = 33,
        ValenceMidMaxValue       = 66,
        RecencyWindowSeconds     = 300,
        TicRecognitionThreshold  = 5,
        DriveContextThreshold    = 60,
        DialogAttemptProbability = 1.0,
    };

    private static (EntityManager em, Entity speaker, Entity listener, SoundTriggerBus bus,
                    DialogFragmentRetrievalSystem sys)
        BuildSetup(string corpusJson, VocabularyRegister register = VocabularyRegister.Casual,
                   string context = "greeting")
    {
        var em      = new EntityManager();
        var bus     = new SoundTriggerBus();
        var proxBus = new ProximityEventBus();
        var corpus  = new DialogCorpusService(corpusJson);
        var cfg     = DefaultCfg();

        var speaker = em.CreateEntity();
        speaker.Add(new NpcTag());
        speaker.Add(new PositionComponent { X = 2f, Y = 0f, Z = 3f });
        speaker.Add(new PersonalityComponent(0, 0, 0, 0, 0, register));
        speaker.Add(new SocialDrivesComponent());
        speaker.Add(new DialogHistoryComponent());
        speaker.Add(new RecognizedTicComponent());

        var listener = em.CreateEntity();
        listener.Add(new NpcTag());
        listener.Add(new PositionComponent { X = 3f, Y = 0f, Z = 3f });
        listener.Add(new RecognizedTicComponent());

        var queue = new PendingDialogQueue();
        queue.Enqueue(speaker, listener, context);

        var sys = new DialogFragmentRetrievalSystem(queue, corpus, proxBus, cfg, bus);

        return (em, speaker, listener, bus, sys);
    }

    [Fact]
    public void SpeechFragment_Emitted_WhenFragmentSelected()
    {
        var (em, speaker, listener, bus, sys) = BuildSetup(CasualCorpus, VocabularyRegister.Casual);

        var events = new List<SoundTriggerEvent>();
        bus.Subscribe(e => events.Add(e));

        sys.Update(em, 1f);

        Assert.Contains(events, e => e.Kind == SoundTriggerKind.SpeechFragment);
    }

    [Fact]
    public void SpeechFragment_NormalRegister_HasNormalIntensity()
    {
        var (em, speaker, listener, bus, sys) = BuildSetup(CasualCorpus, VocabularyRegister.Casual);

        SoundTriggerEvent? evt = null;
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.SpeechFragment) evt = e; });

        sys.Update(em, 1f);

        Assert.NotNull(evt);
        Assert.Equal(0.6f, evt!.Value.Intensity, 4);
    }

    [Fact]
    public void SpeechFragment_CrassRegister_HasLoudIntensity()
    {
        var (em, speaker, listener, bus, sys) = BuildSetup(CrassCorpus, VocabularyRegister.Crass);

        SoundTriggerEvent? evt = null;
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.SpeechFragment) evt = e; });

        sys.Update(em, 1f);

        Assert.NotNull(evt);
        Assert.Equal(1.0f, evt!.Value.Intensity, 4);
    }

    [Fact]
    public void SpeechFragment_ClippedRegister_HasQuietIntensity()
    {
        var (em, speaker, listener, bus, sys) = BuildSetup(ClippedCorpus, VocabularyRegister.Clipped);

        SoundTriggerEvent? evt = null;
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.SpeechFragment) evt = e; });

        sys.Update(em, 1f);

        Assert.NotNull(evt);
        Assert.Equal(0.3f, evt!.Value.Intensity, 4);
    }

    [Fact]
    public void SpeechFragment_HasSpeakerEntityId()
    {
        var (em, speaker, listener, bus, sys) = BuildSetup(CasualCorpus, VocabularyRegister.Casual);

        SoundTriggerEvent? evt = null;
        bus.Subscribe(e => { if (e.Kind == SoundTriggerKind.SpeechFragment) evt = e; });

        sys.Update(em, 1f);

        Assert.NotNull(evt);
        Assert.Equal(speaker.Id, evt!.Value.SourceEntityId);
    }
}

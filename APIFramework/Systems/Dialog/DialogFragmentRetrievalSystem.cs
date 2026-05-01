using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Audio;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems.Dialog;

/// <summary>
/// Phase 75 (after DialogContextDecisionSystem) — reads PendingDialogQueue,
/// scores corpus fragments for each pending (Speaker, Listener, Context) tuple,
/// selects the best fragment, emits SpokenFragmentEvent on ProximityEventBus,
/// and updates the speaker's DialogHistoryComponent.
///
/// Scoring (per fragment):
///   +ValenceMatchScore  per drive key whose ordinal matches the speaker's live drive level
///   +CalcifyBiasScore   if the fragment is calcified for this speaker
///   -RecencyPenalty     if the fragment was used within RecencyWindowSeconds
/// Tie-break: fragment id ascending (deterministic).
/// </summary>
/// <remarks>
/// Phase: Dialog (75), registered between <see cref="DialogContextDecisionSystem"/> and
/// <see cref="DialogCalcifySystem"/>. Reads <see cref="PendingDialogQueue"/>,
/// <c>PersonalityComponent</c>, <c>SocialDrivesComponent</c>; reads and writes
/// <c>DialogHistoryComponent</c> and <c>RecognizedTicComponent</c>; publishes
/// <see cref="SpokenFragmentEvent"/> on the proximity bus.
/// Skips non-Alive speakers.
/// </remarks>
public sealed class DialogFragmentRetrievalSystem : ISystem
{
    private readonly PendingDialogQueue  _queue;
    private readonly DialogCorpusService _corpus;
    private readonly ProximityEventBus   _bus;
    private readonly DialogConfig        _cfg;
    private readonly SoundTriggerBus?    _soundBus;

    private long   _tick;
    private double _gameTimeSec;

    /// <summary>
    /// Stores queue, corpus, bus, and tuning references used per tick.
    /// </summary>
    /// <param name="queue">Queue from which pending pairs are drained each tick.</param>
    /// <param name="corpus">Phrase corpus consulted for matching fragments.</param>
    /// <param name="bus">Bus on which <see cref="SpokenFragmentEvent"/> is published.</param>
    /// <param name="cfg">Dialog config — supplies scoring weights and recency window.</param>
    public DialogFragmentRetrievalSystem(
        PendingDialogQueue  queue,
        DialogCorpusService corpus,
        ProximityEventBus   bus,
        DialogConfig        cfg,
        SoundTriggerBus?    soundBus = null)
    {
        _queue    = queue;
        _corpus   = corpus;
        _bus      = bus;
        _cfg      = cfg;
        _soundBus = soundBus;
    }

    /// <summary>
    /// Per-tick entry point. For every pending pair, scores corpus fragments, picks the
    /// best one, updates dialog history, and emits <see cref="SpokenFragmentEvent"/>.
    /// </summary>
    /// <param name="em">Entity manager (unused — pairs come from the queue).</param>
    /// <param name="deltaTime">Tick delta in seconds; accumulated into the system's clock.</param>
    public void Update(EntityManager em, float deltaTime)
    {
        _tick++;
        _gameTimeSec += deltaTime;

        foreach (var pending in _queue.Items)
        {
            var speaker  = pending.Speaker;
            var listener = pending.Listener;
            var context  = pending.Context;

            if (!LifeStateGuard.IsAlive(speaker)) continue;  // WP-3.0.0: skip non-Alive NPCs
            if (!speaker.Has<PersonalityComponent>()) continue;

            var personality = speaker.Get<PersonalityComponent>();
            var register    = RegisterString(personality.VocabularyRegister);

            var candidates = _corpus.QueryByRegisterAndContext(register, context);
            if (candidates.Count == 0) continue;

            SocialDrivesComponent drives = speaker.Has<SocialDrivesComponent>()
                ? speaker.Get<SocialDrivesComponent>()
                : default;

            DialogHistoryComponent hist = speaker.Has<DialogHistoryComponent>()
                ? speaker.Get<DialogHistoryComponent>()
                : new DialogHistoryComponent();

            int listenerIntId = EntityIntId(listener);
            var best = SelectFragment(candidates, drives, hist, listenerIntId);
            if (best is null) continue;

            // Update global history
            if (!hist.UsesByFragmentId.TryGetValue(best.Id, out var rec))
            {
                rec = new FragmentUseRecord
                {
                    FirstUseTick      = _tick,
                    DominantContext   = context,
                    LastUseGameTimeSec = _gameTimeSec,
                };
                hist.UsesByFragmentId[best.Id] = rec;
            }

            rec.UseCount++;
            rec.LastUseTick       = _tick;
            rec.LastUseGameTimeSec = _gameTimeSec;

            if (!rec.ContextCounts.TryGetValue(context, out int prev))
                prev = 0;
            rec.ContextCounts[context] = prev + 1;

            // Recompute dominant context
            rec.DominantContext = rec.ContextCounts
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .First().Key;

            // Update per-listener history
            if (!hist.UsesByListenerAndFragmentId.TryGetValue(listenerIntId, out var perFragment))
            {
                perFragment = new Dictionary<string, int>();
                hist.UsesByListenerAndFragmentId[listenerIntId] = perFragment;
            }
            perFragment[best.Id] = perFragment.GetValueOrDefault(best.Id, 0) + 1;

            // Persist updated struct back (dictionary reference is shared, but
            // adding a new key to UsesByFragmentId mutated the shared dict — the
            // struct re-Add is only needed for the first insertion path).
            if (!speaker.Has<DialogHistoryComponent>())
                speaker.Add(hist);

            int speakerIntId = EntityIntId(speaker);

            _bus.RaiseSpokenFragment(new SpokenFragmentEvent(speaker, listener, best.Id, _tick));

            // Emit SpeechFragment sound — intensity based on vocabulary register
            if (_soundBus != null)
            {
                float speechIntensity = personality.VocabularyRegister switch
                {
                    VocabularyRegister.Crass  => 1.0f, // Loud
                    VocabularyRegister.Folksy => 0.3f, // Quiet
                    VocabularyRegister.Clipped => 0.3f, // Quiet
                    _                         => 0.6f, // Normal
                };
                var speakerPos = speaker.Has<PositionComponent>() ? speaker.Get<PositionComponent>() : default;
                _soundBus.Emit(SoundTriggerKind.SpeechFragment, speaker.Id, speakerPos.X, speakerPos.Z, speechIntensity, _tick);
            }

            // Notify listener's RecognizedTicComponent
            if (listener.Has<RecognizedTicComponent>())
            {
                var tic = listener.Get<RecognizedTicComponent>();

                var key = (speakerIntId, best.Id);
                tic.HearingCounts.TryGetValue(key, out int count);
                tic.HearingCounts[key] = count + 1;

                if (count + 1 >= _cfg.TicRecognitionThreshold)
                {
                    if (!tic.RecognizedTicsBySpeakerId.TryGetValue(speakerIntId, out var ticSet))
                    {
                        ticSet = new HashSet<string>();
                        tic.RecognizedTicsBySpeakerId[speakerIntId] = ticSet;
                    }
                    ticSet.Add(best.Id);
                }
            }
        }
    }

    private PhraseFragment? SelectFragment(
        IReadOnlyList<PhraseFragment> candidates,
        SocialDrivesComponent         drives,
        DialogHistoryComponent        hist,
        int?                          listenerIntId = null)
    {
        PhraseFragment? best      = null;
        int             bestScore = int.MinValue;

        foreach (var frag in candidates)
        {
            int score = 0;

            // Valence match
            foreach (var kv in frag.ValenceProfile)
            {
                int driveValue = DriveValue(drives, kv.Key);
                string ordinal = DriveOrdinal(driveValue, _cfg.ValenceLowMaxValue, _cfg.ValenceMidMaxValue);
                if (ordinal == kv.Value)
                    score += _cfg.ValenceMatchScore;
            }

            // Calcify bias + recency penalty
            if (hist.UsesByFragmentId.TryGetValue(frag.Id, out var rec))
            {
                if (rec.Calcified)
                    score += _cfg.CalcifyBiasScore;

                if (_gameTimeSec - rec.LastUseGameTimeSec < _cfg.RecencyWindowSeconds)
                    score += _cfg.RecencyPenalty;
            }

            // Per-listener bias: this speaker has used this fragment with this listener before
            if (listenerIntId.HasValue
                && hist.UsesByListenerAndFragmentId.TryGetValue(listenerIntId.Value, out var perFrag)
                && perFrag.TryGetValue(frag.Id, out var listenerUseCount)
                && listenerUseCount > 0)
            {
                score += _cfg.PerListenerBiasScore;
            }

            if (score > bestScore ||
                (score == bestScore && (best is null ||
                    string.Compare(frag.Id, best.Id, StringComparison.Ordinal) < 0)))
            {
                best      = frag;
                bestScore = score;
            }
        }

        return best;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static string RegisterString(VocabularyRegister r) => r switch
    {
        VocabularyRegister.Formal   => "formal",
        VocabularyRegister.Casual   => "casual",
        VocabularyRegister.Crass    => "crass",
        VocabularyRegister.Clipped  => "clipped",
        VocabularyRegister.Academic => "academic",
        VocabularyRegister.Folksy   => "folksy",
        _                           => "casual",
    };

    private static int DriveValue(SocialDrivesComponent d, string name) => name switch
    {
        "belonging"  => d.Belonging.Current,
        "status"     => d.Status.Current,
        "affection"  => d.Affection.Current,
        "irritation" => d.Irritation.Current,
        "attraction" => d.Attraction.Current,
        "trust"      => d.Trust.Current,
        "suspicion"  => d.Suspicion.Current,
        "loneliness" => d.Loneliness.Current,
        _            => 0,
    };

    private static string DriveOrdinal(int value, int lowMax, int midMax)
    {
        if (value <= lowMax) return "low";
        if (value <= midMax) return "mid";
        return "high";
    }

    /// <summary>Extracts the lower 32 bits of the entity's deterministic counter-Guid.</summary>
    private static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }
}

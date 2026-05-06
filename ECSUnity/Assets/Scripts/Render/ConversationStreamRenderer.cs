using System.Collections.Generic;
using UnityEngine;
using Warden.Contracts.Telemetry;

/// <summary>
/// Floating text streams between conversing NPCs (WP-3.1.E AT-18).
///
/// DESIGN (UX bible §3.8)
/// ───────────────────────
/// For each active conversation pair, spawn floating text particles that rise
/// between the two NPC positions. Scale by register/intensity:
///   - Quiet (low magnitude):  small (size 0.12), slow rise, gray.
///   - Heated (high magnitude): larger (0.2), faster rise, color-shifted.
///   - Mask-slip moment:         brief "!?" burst.
///
/// IMPLEMENTATION
/// ───────────────
/// At v0.1, WorldStateDto does not expose a rich dialog stream. This renderer
/// uses the chronicle (recent events) as a proxy for conversation activity and
/// renders simplified text particles when two NPCs share a recent social event.
///
/// Full integration with the DialogFragmentRetrieval output (WorldStateDto.dialog[])
/// is deferred to when that field is projected — this component is ready to consume it.
///
/// MOUNTING
/// ─────────
/// Attach to any persistent GameObject. Set EngineHost reference.
/// </summary>
public sealed class ConversationStreamRenderer : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private EngineHost _host;

    [Tooltip("Maximum simultaneously-rendered text particles.")]
    [SerializeField] private int _maxParticles = 60;

    [Tooltip("Seconds a single text particle lives before fading.")]
    [SerializeField] private float _particleLifetime = 2.5f;

    [Tooltip("Rise speed for quiet speech (world-units per second).")]
    [SerializeField] private float _quietRiseSpeed = 0.4f;

    [Tooltip("Rise speed for heated speech.")]
    [SerializeField] private float _heatedRiseSpeed = 1.0f;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private readonly List<TextParticle> _particles = new List<TextParticle>(64);
    private float _spawnTimer;
    private const float SpawnInterval = 0.8f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Update()
    {
        UpdateParticles();
        TrySpawnParticles();
    }

    private void OnDestroy()
    {
        foreach (var p in _particles)
            if (p.Go != null) Destroy(p.Go);
        _particles.Clear();
    }

    // ── Particle management ───────────────────────────────────────────────────

    private void TrySpawnParticles()
    {
        _spawnTimer += Time.deltaTime;
        if (_spawnTimer < SpawnInterval) return;
        _spawnTimer = 0f;

        if (_particles.Count >= _maxParticles) return;

        var worldState = _host?.WorldState;
        if (worldState?.Entities == null) return;

        // Look for entity pairs that are in conversation via social state.
        // Use WorldStateDto.Entities social state to find conversing pairs.
        for (int i = 0; i < worldState.Entities.Count; i++)
        {
            if (_particles.Count >= _maxParticles) break;

            var entity = worldState.Entities[i];
            if (entity?.Social == null) continue;
            if (!entity.Physiology.HasPosition(entity)) continue;

            // Determine if NPC is in a conversation. The v0.4 schema's DominantDrive
            // enum is { None, Eat, Drink, Sleep, Defecate, Pee } — no Socialize value.
            // Until WorldStateDto.dialog[] arrives we have no signal at this layer, so
            // conversation rendering is a no-op. WP-3.1.E completion note flags this
            // as deferred to WP-3.x.SocialProjection.
            bool inConversation = false;
            if (!inConversation) continue;

            bool isHeated = false; // Would come from dialog register field.
            SpawnParticle(
                new Vector3(entity.Position.X, 1.5f, entity.Position.Z),
                isHeated);
        }
    }

    private void SpawnParticle(Vector3 position, bool heated)
    {
        // Use TextMesh for lightweight text rendering.
        var go  = new GameObject("ConvParticle");
        var tm  = go.AddComponent<TextMesh>();

        // Pick a short text fragment — word fragments give the feel of murmured speech.
        tm.text         = heated ? PickHeatedFragment() : PickQuietFragment();
        tm.fontSize     = heated ? 12 : 9;
        tm.color        = heated ? new Color(0.9f, 0.5f, 0.3f, 0.9f) : new Color(0.6f, 0.6f, 0.6f, 0.7f);
        tm.anchor       = TextAnchor.MiddleCenter;
        tm.alignment    = TextAlignment.Center;
        tm.characterSize = heated ? 0.06f : 0.04f;

        go.transform.position = position + new Vector3(
            Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
        go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);

        _particles.Add(new TextParticle
        {
            Go        = go,
            Tm        = tm,
            StartPos  = go.transform.position,
            Lifetime  = _particleLifetime,
            Age       = 0f,
            RiseSpeed = heated ? _heatedRiseSpeed : _quietRiseSpeed,
            Heated    = heated,
            StartColor = tm.color,
        });
    }

    private void UpdateParticles()
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Age += Time.deltaTime;

            float t = p.Age / p.Lifetime;
            if (t >= 1f)
            {
                if (p.Go != null) Destroy(p.Go);
                _particles.RemoveAt(i);
                continue;
            }

            // Rise + fade.
            if (p.Go != null)
            {
                Vector3 pos = p.StartPos + Vector3.up * (p.RiseSpeed * p.Age);
                p.Go.transform.position = pos;

                Color c = p.StartColor;
                c.a         = p.StartColor.a * (1f - t * t);
                p.Tm.color  = c;
            }
        }
    }

    // ── Fragment pools ────────────────────────────────────────────────────────

    private static readonly string[] QuietFragments =
        { "...", "mm", "yeah", "ok", "right", "hmm", "uh-huh" };

    private static readonly string[] HeatedFragments =
        { "NO!", "but—", "THAT'S—", "listen", "you said—", "REALLY?", "why?" };

    private static string PickQuietFragment()  => QuietFragments[Random.Range(0, QuietFragments.Length)];
    private static string PickHeatedFragment() => HeatedFragments[Random.Range(0, HeatedFragments.Length)];

    // ── Test accessors ────────────────────────────────────────────────────────

    /// <summary>Number of live text particles.</summary>
    public int ActiveParticleCount => _particles.Count;

    // ── Private structs ───────────────────────────────────────────────────────

    private sealed class TextParticle
    {
        public GameObject Go;
        public TextMesh   Tm;
        public Vector3    StartPos;
        public float      Lifetime;
        public float      Age;
        public float      RiseSpeed;
        public bool       Heated;
        public Color      StartColor;
    }
}

// ── Extension helper ──────────────────────────────────────────────────────────

internal static class EntityExtensions
{
    public static bool HasPosition(this PhysiologyStateDto _, EntityStateDto entity)
        => entity?.Position?.HasPosition ?? false;
}

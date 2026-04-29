using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Warden.Contracts.Telemetry;

/// <summary>
/// Manager-office notification overlay — v0.1 placeholder (WP-3.1.E AT-11).
///
/// DESIGN (UX bible §3.2 / §3.4)
/// ────────────────────────────────
/// Three diegetic indicators: Phone, Fax Tray, Email Indicator.
/// Volume is sparse: at most 1–2 player-direct orders per game-day.
/// This v0.1 implementation is a placeholder — final shape lands in v0.2 bible.
///
/// EVENTS
/// ───────
/// Subscribes to WorldStateDto.Chronicle for order-type narrative events.
/// New order event → Phone visual rings; Fax tray fills; Email blinks.
///
/// MOUNTING
/// ─────────
/// Attach to a persistent UI canvas. Wire EngineHost reference.
/// </summary>
public sealed class NotificationPanel : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private EngineHost _host;
    [SerializeField] private UIDocument _document;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private VisualElement _root;
    private VisualElement _phoneIndicator;
    private VisualElement _faxIndicator;
    private VisualElement _emailIndicator;

    private int   _faxCount;
    private bool  _emailPending;
    private bool  _phonePending;
    private float _phoneRingTimer;
    private float _emailBlinkTimer;

    // Chronicle tick we last consumed — avoids re-processing old entries.
    private long _lastConsumedTick = -1;

    // Event queue (order details).
    private readonly Queue<string> _orderQueue = new Queue<string>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (_document != null)
        {
            _root             = _document.rootVisualElement?.Q("notification-root");
            _phoneIndicator   = _root?.Q("notif-phone");
            _faxIndicator     = _root?.Q("notif-fax");
            _emailIndicator   = _root?.Q("notif-email");
        }
    }

    private void Update()
    {
        PollNewOrders();
        AnimateIndicators();
    }

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>Inject a notification directly (used in tests).</summary>
    public void InjectOrderNotification(string description)
    {
        _orderQueue.Enqueue(description);
        _phonePending   = true;
        _phoneRingTimer = 2f;
        _faxCount++;
        _emailPending   = true;
        _emailBlinkTimer = 5f;

        Debug.Log($"[NotificationPanel] Order received: {description}");
        RefreshIndicators();
    }

    /// <summary>True when the phone indicator is currently ringing.</summary>
    public bool IsPhoneRinging => _phonePending;

    /// <summary>Number of pending orders in the fax tray.</summary>
    public int FaxCount => _faxCount;

    /// <summary>True when the email indicator is blinking.</summary>
    public bool IsEmailBlinking => _emailPending;

    // ── Internal ──────────────────────────────────────────────────────────────

    private void PollNewOrders()
    {
        var chronicle = _host?.WorldState?.Chronicle;
        if (chronicle == null) return;

        foreach (var entry in chronicle)
        {
            if (entry.Tick <= _lastConsumedTick) continue;
            if (!IsOrderKind(entry.Kind)) continue;

            _lastConsumedTick = entry.Tick;
            InjectOrderNotification(entry.Description);
        }
    }

    private static bool IsOrderKind(ChronicleEventKind kind)
    {
        // Order kinds: TaskCompleted, OverdueTask, ScheduleMissed — player-direct orders.
        return kind == ChronicleEventKind.TaskCompleted
            || kind == ChronicleEventKind.OverdueTask;
    }

    private void AnimateIndicators()
    {
        if (_phoneRingTimer > 0f)
        {
            _phoneRingTimer -= Time.unscaledDeltaTime;
            if (_phoneRingTimer <= 0f) _phonePending = false;
        }

        if (_emailBlinkTimer > 0f)
        {
            _emailBlinkTimer -= Time.unscaledDeltaTime;
            if (_emailBlinkTimer <= 0f) _emailPending = false;
        }

        RefreshIndicators();
    }

    private void RefreshIndicators()
    {
        // UI Toolkit indicators.
        if (_phoneIndicator != null)
        {
            _phoneIndicator.EnableInClassList("ringing", _phonePending);
        }
        if (_faxIndicator != null)
        {
            _faxIndicator.EnableInClassList("fax-full", _faxCount > 0);
        }
        if (_emailIndicator != null)
        {
            _emailIndicator.EnableInClassList("blinking", _emailPending);
        }
    }

    // ── IMGUI fallback ────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (_root != null) return;

        float x = 8f, y = 8f, w = 130f, h = 64f;
        GUI.Box(new Rect(x, y, w, h), "Office Desk");

        GUI.Label(new Rect(x + 4f, y + 18f, 120f, 18f),
            _phonePending ? "Phone: RINGING" : "Phone: quiet");
        GUI.Label(new Rect(x + 4f, y + 36f, 120f, 18f),
            $"Fax: {_faxCount} pending");
        GUI.Label(new Rect(x + 4f, y + 54f, 120f, 18f),
            _emailPending ? "Email: NEW" : "Email: none");
    }
}

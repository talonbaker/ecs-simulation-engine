using UnityEngine;
using UnityEngine.UIElements;
using Warden.Contracts.Telemetry;

/// <summary>
/// Represents a single event-log entry row in the Event Log panel — WP-3.1.G.
///
/// Each row displays:
///   - Game-day badge ("Day 14, 2:47 PM").
///   - Event kind icon (from EventKindIconCatalog).
///   - One-line description.
///   - Invisible click-zone that fires OnRowClicked.
///
/// This is a plain C# class (not MonoBehaviour) that wraps a VisualElement for
/// use inside a UI Toolkit ListView.
/// </summary>
public sealed class EventLogRow
{
    // ── Backing data ──────────────────────────────────────────────────────────

    /// <summary>The chronicle entry this row represents.</summary>
    public ChronicleEntryDto Entry { get; private set; }

    // ── Visual element ────────────────────────────────────────────────────────

    /// <summary>Root visual element to add to the list.</summary>
    public VisualElement Root { get; }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the row is clicked. Carries the chronicle entry.</summary>
    public System.Action<ChronicleEntryDto> OnRowClicked;

    // ── Child elements ────────────────────────────────────────────────────────

    private readonly Label _dayBadge;
    private readonly Label _descriptionLabel;
    private readonly VisualElement _iconElement;

    // ── Constructor ────────────────────────────────────────────────────────────

    public EventLogRow()
    {
        Root = new VisualElement();
        Root.AddToClassList("event-log-row");

        _iconElement = new VisualElement();
        _iconElement.AddToClassList("event-log-icon");

        _dayBadge = new Label("Day --");
        _dayBadge.AddToClassList("event-log-day-badge");

        _descriptionLabel = new Label("...");
        _descriptionLabel.AddToClassList("event-log-description");

        Root.Add(_iconElement);
        Root.Add(_dayBadge);
        Root.Add(_descriptionLabel);

        // Register click callback.
        Root.RegisterCallback<ClickEvent>(_ => OnRowClicked?.Invoke(Entry));
    }

    // ── Bind ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Binds this row to a chronicle entry.
    /// Call when populating (or recycling) a list row.
    /// </summary>
    /// <param name="entry">The entry to display.</param>
    /// <param name="ticksPerDay">Used to convert Tick to game-day + time-of-day.</param>
    public void Bind(ChronicleEntryDto entry, long ticksPerDay = 1200)
    {
        Entry = entry;

        if (entry == null)
        {
            _dayBadge.text         = "Day --";
            _descriptionLabel.text = string.Empty;
            return;
        }

        // Compute game-day and approximate time-of-day from tick.
        long day         = ticksPerDay > 0 ? (entry.Tick / ticksPerDay) + 1 : 1;
        long tickInDay   = ticksPerDay > 0 ? entry.Tick % ticksPerDay : 0;
        float fracDay    = ticksPerDay > 0 ? (float)tickInDay / ticksPerDay : 0f;
        int   hour       = Mathf.FloorToInt(fracDay * 24f);
        int   minute     = Mathf.FloorToInt((fracDay * 24f - hour) * 60f);
        string amPm      = hour >= 12 ? "PM" : "AM";
        int    hour12    = hour == 0 ? 12 : (hour > 12 ? hour - 12 : hour);

        _dayBadge.text = $"Day {day}, {hour12}:{minute:D2} {amPm}";

        // Description: use the DTO description if available; otherwise synthesise from kind.
        _descriptionLabel.text = !string.IsNullOrWhiteSpace(entry.Description)
            ? entry.Description
            : $"[{entry.Kind}] — {(entry.Participants?.Count > 0 ? string.Join(", ", entry.Participants) : "Unknown")}";
    }
}

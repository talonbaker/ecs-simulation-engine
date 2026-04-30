using System;

namespace ECSVisualizer.ViewModels;

/// <summary>
/// One time-series track for the live chart dashboard.
///
/// Internally a circular ring buffer — oldest sample is evicted first when full.
/// Push() is called by ChartViewModel on each game-time sample.
/// The Refreshed event fires after every push; ScrollingChart subscribes to it
/// and calls InvalidateVisual() to redraw.
///
/// Thread-safety: call only from the UI thread (DispatcherTimer thread).
/// </summary>
public sealed class ChartSeriesViewModel
{
    // ── Ring buffer ───────────────────────────────────────────────────────────
    private readonly double[] _ring;
    private int _writePos; // index of NEXT write position (oldest after wrap)
    private int _count;    // number of valid samples (0 → Capacity)

    // ── Public metadata ───────────────────────────────────────────────────────
    /// <summary>Short display label shown at the top-left of the chart (e.g. "SATIATION").</summary>
    public string Label    { get; }
    /// <summary>Hex color string (e.g. "#30D158") used to draw the line.</summary>
    public string ColorHex { get; }
    /// <summary>Lower bound of the Y-axis scale.</summary>
    public double Min      { get; }
    /// <summary>Upper bound of the Y-axis scale.</summary>
    public double Max      { get; }
    /// <summary>Maximum number of samples retained in the ring buffer.</summary>
    public int    Capacity => _ring.Length;
    /// <summary>Number of samples currently stored (grows up to <see cref="Capacity"/>).</summary>
    public int    Count    => _count;

    /// <summary>Most recent value pushed (displayed in top-right of chart).</summary>
    public double Current  { get; private set; }

    /// <summary>Fired after every Push(); ScrollingChart subscribes to this.</summary>
    public event Action? Refreshed;

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new chart series with a fixed-size ring buffer, display
    /// label, line color, and Y-axis range.
    /// </summary>
    /// <param name="label">   Short display label ("SATIATION", "FPS", …).</param>
    /// <param name="colorHex">Hex color for the chart line, e.g. "#30D158".</param>
    /// <param name="min">     Y-axis minimum (typically 0).</param>
    /// <param name="max">     Y-axis maximum (typically 100).</param>
    /// <param name="capacity">How many samples to keep (= visible history width).</param>
    public ChartSeriesViewModel(string label, string colorHex,
                                double min, double max, int capacity = 480)
    {
        Label    = label;
        ColorHex = colorHex;
        Min      = min;
        Max      = max;
        _ring    = new double[capacity];
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Add a new sample. If the buffer is full the oldest sample is silently evicted.
    /// Fires Refreshed after writing.
    /// </summary>
    /// <param name="value">The new sample to append.</param>
    public void Push(double value)
    {
        _ring[_writePos] = value;
        _writePos = (_writePos + 1) % _ring.Length;
        if (_count < _ring.Length) _count++;
        Current = value;
        Refreshed?.Invoke();
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Copies the last <paramref name="n"/> samples (oldest first) into
    /// <paramref name="dest"/>.  <paramref name="n"/> must be ≤ Count.
    /// </summary>
    /// <param name="dest">Destination buffer; must be at least <paramref name="n"/> elements long.</param>
    /// <param name="n">Number of samples to copy in chronological order.</param>
    public void CopyOrderedTo(double[] dest, int n)
    {
        if (n <= 0) return;
        // When buffer hasn't wrapped: oldest sample is at index 0
        // When buffer has wrapped:   oldest sample is at _writePos
        int start = _count < _ring.Length ? 0 : _writePos;
        for (int i = 0; i < n; i++)
            dest[i] = _ring[(start + i) % _ring.Length];
    }
}

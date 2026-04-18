using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ECSVisualizer.ViewModels;
using System;
using System.Globalization;

namespace ECSVisualizer.Controls;

/// <summary>
/// Seismograph-style scrolling line chart.
///
/// Bind <see cref="Series"/> to a <see cref="ChartSeriesViewModel"/>.
/// The control subscribes to the VM's Refreshed event and calls
/// InvalidateVisual() so Avalonia's Skia renderer redraws the frame.
///
/// No extra NuGet packages — all rendering via Avalonia's DrawingContext.
///
/// LAYOUT HINT: give this control a fixed Height (e.g. 140) in AXAML.
/// </summary>
public sealed class ScrollingChart : Control
{
    // ── Series StyledProperty (binds from AXAML with compiled bindings) ───────

    public static readonly StyledProperty<ChartSeriesViewModel?> SeriesProperty =
        AvaloniaProperty.Register<ScrollingChart, ChartSeriesViewModel?>(nameof(Series));

    public ChartSeriesViewModel? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    // ── Static brushes / pens shared across all instances ────────────────────

    private static readonly IBrush BgBrush    = new SolidColorBrush(Color.Parse("#080808"));
    private static readonly Pen    GridPen    = new(new SolidColorBrush(Color.Parse("#1A1A1A")), 0.5);
    private static readonly IBrush LabelColor = new SolidColorBrush(Color.Parse("#606060"));
    private static readonly IBrush ValueColor = new SolidColorBrush(Color.Parse("#909090"));
    private static readonly Typeface MonoFace = new("Consolas");

    private const double LabelFontSize = 13;
    private const double ValueFontSize = 11;

    // ── Per-instance caches ───────────────────────────────────────────────────

    private Pen?   _linePen;
    private string _linePenColorHex = "";

    // Double scratch buffer reused each frame — avoids per-frame allocation
    private double[] _vals = Array.Empty<double>();
    // Point scratch buffer for the StreamGeometry
    private Point[]  _pts  = Array.Empty<Point>();

    // ── React to Series changes ───────────────────────────────────────────────

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != SeriesProperty) return;

        if (change.OldValue is ChartSeriesViewModel old)
            old.Refreshed -= OnRefresh;

        if (change.NewValue is ChartSeriesViewModel next)
        {
            next.Refreshed += OnRefresh;
            // Reset line pen cache so new color is picked up
            _linePen = null;
        }

        InvalidateVisual();
    }

    private void OnRefresh() => InvalidateVisual();

    // ── Rendering ─────────────────────────────────────────────────────────────

    public override void Render(DrawingContext ctx)
    {
        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w < 2 || h < 2) return;

        var bgRect = new Rect(0, 0, w, h);

        // ── Background ────────────────────────────────────────────────────────
        ctx.FillRectangle(BgBrush, bgRect);

        // ── Grid lines at 25 / 50 / 75 % ─────────────────────────────────────
        double q = h / 4.0;
        ctx.DrawLine(GridPen, new Point(0, q),     new Point(w, q));
        ctx.DrawLine(GridPen, new Point(0, q * 2), new Point(w, q * 2));
        ctx.DrawLine(GridPen, new Point(0, q * 3), new Point(w, q * 3));

        var series = Series;

        // ── No data ───────────────────────────────────────────────────────────
        if (series == null)
        {
            DrawText(ctx, "NO DATA", LabelColor, new Point(5, 4), LabelFontSize);
            return;
        }

        DrawText(ctx, series.Label, LabelColor, new Point(5, 4), LabelFontSize);
        DrawText(ctx, $"{series.Current:F1}", ValueColor, new Point(w - 44, 4), ValueFontSize);

        int n = series.Count;
        if (n < 2) return;

        // ── Ensure scratch buffers are large enough ───────────────────────────
        if (_vals.Length < n) _vals = new double[n];
        if (_pts.Length  < n) _pts  = new Point[n];

        // ── Copy ordered data into double[] ───────────────────────────────────
        series.CopyOrderedTo(_vals, n);

        // ── Map values → pixel coordinates ───────────────────────────────────
        double range = series.Max - series.Min;
        if (range < 0.001) range = 1;

        for (int i = 0; i < n; i++)
        {
            double x    = w * i / (n - 1);
            double norm = (_vals[i] - series.Min) / range;   // 0..1
            double y    = h - Math.Clamp(norm, 0, 1) * h;   // flip Y
            _pts[i]     = new Point(x, y);
        }

        // ── Draw StreamGeometry line ──────────────────────────────────────────
        var geo = new StreamGeometry();
        using (var sgc = geo.Open())
        {
            sgc.BeginFigure(_pts[0], false);
            for (int i = 1; i < n; i++)
                sgc.LineTo(_pts[i]);
            sgc.EndFigure(false);
        }
        ctx.DrawGeometry(null, GetLinePen(series.ColorHex), geo);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void DrawText(DrawingContext ctx, string text, IBrush brush, Point origin, double fontSize)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            MonoFace,
            fontSize,
            brush);
        ctx.DrawText(ft, origin);
    }

    private Pen GetLinePen(string colorHex)
    {
        if (_linePen == null || _linePenColorHex != colorHex)
        {
            _linePen = new Pen(
                new SolidColorBrush(Color.Parse(colorHex)),
                thickness: 1.5,
                lineCap: PenLineCap.Round,
                lineJoin: PenLineJoin.Round);
            _linePenColorHex = colorHex;
        }
        return _linePen;
    }
}

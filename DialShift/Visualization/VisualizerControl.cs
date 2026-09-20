using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DialShift.Visualization;

public enum VisualizerStyle { Bars, Scope, Off }

// A small Winamp-style visualizer: a handful of render modes driven by DialShift's
// own decoded audio (see SystemAudioTap). Click the strip (or the style button next
// to it) to cycle modes, same gesture as switching Winamp's visuals.
public sealed class VisualizerControl : FrameworkElement
{
    private const int Bars = 28;
    private readonly AudioSpectrum spectrum;
    private readonly DispatcherTimer timer;
    private readonly double[] samples = new double[AudioSpectrum.FftSize];
    private readonly double[] re = new double[AudioSpectrum.FftSize];
    private readonly double[] im = new double[AudioSpectrum.FftSize];
    private readonly float[] latest = new float[AudioSpectrum.FftSize];
    private readonly double[] barLevels = new double[Bars];
    private readonly Pen scopePen = new(new SolidColorBrush(Color.FromRgb(0xC2, 0xF2, 0x78)), 1.6);
    private readonly Brush barBrush = new LinearGradientBrush(
        Color.FromRgb(0x8B, 0xC9, 0x4E), Color.FromRgb(0xC2, 0xF2, 0x78), 90);
    private double scopeGainPeak = 0.05;

    public event Action<VisualizerStyle>? StyleChanged;
    public VisualizerStyle VisualStyle { get; private set; } = VisualizerStyle.Bars;

    public VisualizerControl(AudioSpectrum spectrum, VisualizerStyle initial)
    {
        this.spectrum = spectrum;
        VisualStyle = initial;
        Height = 108;
        Cursor = Cursors.Hand;
        ToolTip = "Click to change the visualization";
        scopePen.Freeze();
        MouseLeftButtonUp += (_, _) => CycleStyle();
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(1000.0 / 30), DispatcherPriority.Render, (_, _) => Tick(), Dispatcher);
        timer.Start();
        Unloaded += (_, _) => timer.Stop();
    }

    public void CycleStyle()
    {
        VisualStyle = VisualStyle switch
        {
            VisualizerStyle.Bars => VisualizerStyle.Scope,
            VisualizerStyle.Scope => VisualizerStyle.Off,
            _ => VisualizerStyle.Bars
        };
        Array.Clear(barLevels);
        StyleChanged?.Invoke(VisualStyle);
        InvalidateVisual();
    }

    public void SetStyle(VisualizerStyle style)
    {
        VisualStyle = style;
        Array.Clear(barLevels);
        InvalidateVisual();
    }

    private void Tick()
    {
        if (VisualStyle != VisualizerStyle.Off) InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth; var height = ActualHeight;
        if (width <= 0 || height <= 0) return;
        var background = new Rect(0, 0, width, height);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0x14, 0x20, 0x1D)), null, background, 8, 8);
        if (VisualStyle == VisualizerStyle.Off || !spectrum.HasSignal)
        {
            var label = VisualStyle == VisualizerStyle.Off ? "VISUALIZER OFF · CLICK TO ENABLE" : "WAITING FOR AUDIO…";
            var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, new SolidColorBrush(Color.FromRgb(0x6B, 0x82, 0x84)), VisualTreeHelper.GetDpi(this).PixelsPerDip)
            { TextAlignment = TextAlignment.Center };
            dc.DrawText(text, new Point(width / 2, height / 2 - text.Height / 2));
            return;
        }

        spectrum.CopyLatest(latest);
        for (var i = 0; i < latest.Length; i++) samples[i] = latest[i];

        switch (VisualStyle)
        {
            case VisualizerStyle.Bars: RenderBars(dc, width, height); break;
            case VisualizerStyle.Scope: RenderScope(dc, width, height); break;
        }
    }

    private void RenderBars(DrawingContext dc, double width, double height)
    {
        var magnitudes = Magnitudes();
        var slot = width / Bars;
        for (var i = 0; i < Bars; i++)
        {
            var target = Math.Clamp(magnitudes[i], 0, 1);
            barLevels[i] = target > barLevels[i] ? target : barLevels[i] * 0.82 + target * 0.18;
            var barHeight = Math.Max(2, barLevels[i] * (height - 6));
            var x = i * slot + slot * 0.15;
            var barWidth = slot * 0.7;
            dc.DrawRoundedRectangle(barBrush, null, new Rect(x, height - barHeight, barWidth, barHeight), 2, 2);
        }
    }

    private void RenderScope(DrawingContext dc, double width, double height)
    {
        // Real-world playback volume is often a small fraction of full scale, which made
        // the raw waveform nearly flat. Auto-gain to the recent peak (fast attack, slow
        // release) so the trace stays lively regardless of the station's loudness.
        var peak = 0.0;
        for (var i = 0; i < samples.Length; i++) { var a = Math.Abs(samples[i]); if (a > peak) peak = a; }
        scopeGainPeak = peak > scopeGainPeak ? peak : scopeGainPeak * 0.95 + peak * 0.05;
        var gain = Math.Min(18.0, 1.0 / Math.Max(scopeGainPeak, 0.02));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var step = width / (samples.Length - 1);
            ctx.BeginFigure(new Point(0, height / 2 + ClampUnit(samples[0] * gain) * height * 0.47), false, false);
            for (var i = 1; i < samples.Length; i++)
                ctx.LineTo(new Point(i * step, height / 2 + ClampUnit(samples[i] * gain) * height * 0.47), true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, scopePen, geometry);
    }

    private static double ClampUnit(double value) => Math.Max(-1, Math.Min(1, value));

    private double[] Magnitudes()
    {
        for (var i = 0; i < samples.Length; i++)
        {
            var hann = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (samples.Length - 1));
            re[i] = samples[i] * hann;
            im[i] = 0;
        }
        Fft.Transform(re, im);

        var usableBins = re.Length / 2;
        var result = new double[Bars];
        var minBin = 1;
        for (var bar = 0; bar < Bars; bar++)
        {
            var fromFraction = Math.Pow((double)bar / Bars, 2.2);
            var toFraction = Math.Pow((double)(bar + 1) / Bars, 2.2);
            var from = Math.Max(minBin, (int)(fromFraction * usableBins));
            var to = Math.Max(from + 1, (int)(toFraction * usableBins));
            double sum = 0; var count = 0;
            for (var bin = from; bin < to && bin < usableBins; bin++)
            {
                sum += Math.Sqrt(re[bin] * re[bin] + im[bin] * im[bin]);
                count++;
            }
            var average = count > 0 ? sum / count : 0;
            result[bar] = Math.Min(1.0, average * 0.18 * Math.Log2(bar + 4));
        }
        return result;
    }
}

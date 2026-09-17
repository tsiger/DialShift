using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DialShift.Visualization;

// Feeds the visualizer with DialShift's own decoded audio, tapped via Windows'
// per-process WASAPI loopback (Windows 10 2004+). This reads what this process
// renders without touching LibVLC's audio pipeline in any way, so it cannot affect
// what the user hears; it is also immune to per-app output-device redirection
// (Settings > Sound > App volume and device preferences), which a plain "default
// device" loopback capture would miss entirely.
public sealed class SystemAudioTap : IDisposable
{
    private readonly AudioSpectrum spectrum;
    private WasapiRecorder? recorder;
    private float[] scratch = new float[8192];

    public SystemAudioTap(AudioSpectrum spectrum) => this.spectrum = spectrum;

    public async void Start()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)) return;
        try
        {
            recorder = await BuildRecorder();
            recorder.DataAvailable += OnDataAvailable;
            recorder.StartRecording();
        }
        catch (Exception ex)
        {
            // No audio session available yet, or the platform doesn't support process
            // loopback. The visualizer simply stays idle; playback itself is unaffected.
            App.Log(ex);
            recorder = null;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041")]
    private static System.Threading.Tasks.Task<WasapiRecorder> BuildRecorder() => new WasapiRecorderBuilder()
        .WithProcessLoopback((uint)Environment.ProcessId, ProcessLoopbackMode.IncludeTargetProcessTree)
        .BuildAsync();

    private void OnDataAvailable(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
    {
        var format = recorder?.WaveFormat;
        if (format == null || format.Channels == 0) return;
        var channels = format.Channels;
        if (format.Encoding != WaveFormatEncoding.IeeeFloat || format.BitsPerSample != 32) return;
        var frames = buffer.Length / 4 / channels;
        if (scratch.Length < frames) scratch = new float[frames];
        for (var i = 0; i < frames; i++)
        {
            float sum = 0;
            for (var c = 0; c < channels; c++) sum += BitConverter.ToSingle(buffer.Slice((i * channels + c) * 4, 4));
            scratch[i] = sum / channels;
        }
        spectrum.PushMono(new ReadOnlySpan<float>(scratch, 0, frames));
    }

    public void Dispose()
    {
        if (recorder == null) return;
        try { recorder.StopRecording(); } catch (Exception ex) { App.Log(ex); }
        recorder.DataAvailable -= OnDataAvailable;
        recorder.Dispose();
        recorder = null;
    }
}

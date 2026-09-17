using System;

namespace DialShift.Visualization;

// A small ring buffer of the most recently heard audio, in mono float samples.
// Decoupled from any particular capture source: whoever is listening to the
// system's audio (see SystemAudioTap) just pushes samples in here.
public sealed class AudioSpectrum
{
    public const int FftSize = 1024;
    private const int RingCapacity = FftSize * 4;

    private readonly object gate = new();
    private readonly float[] ring = new float[RingCapacity];
    private int writeIndex;
    private long totalWritten;

    public void PushMono(ReadOnlySpan<float> monoSamples)
    {
        if (monoSamples.Length == 0) return;
        lock (gate)
        {
            foreach (var sample in monoSamples)
            {
                ring[writeIndex] = sample;
                writeIndex = (writeIndex + 1) % RingCapacity;
            }
            totalWritten += monoSamples.Length;
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            Array.Clear(ring);
            totalWritten = 0;
        }
    }

    // True once enough audio has flowed through to fill a full analysis window.
    public bool HasSignal
    {
        get { lock (gate) return totalWritten >= FftSize; }
    }

    public void CopyLatest(float[] destination)
    {
        if (destination.Length != FftSize) throw new ArgumentException($"Destination must be {FftSize} samples.");
        lock (gate)
        {
            var start = (writeIndex - FftSize + RingCapacity) % RingCapacity;
            for (var i = 0; i < FftSize; i++) destination[i] = ring[(start + i) % RingCapacity];
        }
    }
}

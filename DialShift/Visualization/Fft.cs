using System;

namespace DialShift.Visualization;

// Minimal iterative radix-2 Cooley-Tukey FFT. Good enough for visual bars; not a DSP library.
internal static class Fft
{
    public static void Transform(double[] re, double[] im)
    {
        var n = re.Length;
        if (n != im.Length || (n & (n - 1)) != 0) throw new ArgumentException("Length must be a power of two.");

        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
        }

        for (var length = 2; length <= n; length <<= 1)
        {
            var angle = -2 * Math.PI / length;
            var wReal = Math.Cos(angle);
            var wImag = Math.Sin(angle);
            for (var start = 0; start < n; start += length)
            {
                double curReal = 1, curImag = 0;
                for (var k = 0; k < length / 2; k++)
                {
                    var evenIndex = start + k;
                    var oddIndex = start + k + length / 2;
                    var oddReal = re[oddIndex] * curReal - im[oddIndex] * curImag;
                    var oddImag = re[oddIndex] * curImag + im[oddIndex] * curReal;
                    re[oddIndex] = re[evenIndex] - oddReal;
                    im[oddIndex] = im[evenIndex] - oddImag;
                    re[evenIndex] += oddReal;
                    im[evenIndex] += oddImag;
                    var nextReal = curReal * wReal - curImag * wImag;
                    curImag = curReal * wImag + curImag * wReal;
                    curReal = nextReal;
                }
            }
        }
    }
}

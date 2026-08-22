// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;

namespace WolfCurses.Apps.MediaPlayer
{
    /// <summary>
    ///     A block of sound turned into the heights of a set of bars.
    ///     <para>
    ///         <b>This is signal processing and it stays in the application on purpose.</b> Every other reusable
    ///         piece of this screen went down into WolfCurses, but a Fourier transform is not about terminals: the
    ///         library draws the bars (<see cref="WolfCurses.Window.Control.ColumnChart" />) and knows nothing about where
    ///         numbers come from, and giving it an audio namespace holding one function would be a story it does
    ///         not have. Anybody wanting this can copy eighty lines out of an example, which is what an example is
    ///         for.
    ///     </para>
    ///     <para>
    ///         Three things here are the difference between a spectrum and a picture of noise, and all three are
    ///         the sort of thing that looks fine until you compare it with a real one:
    ///     </para>
    ///     <para>
    ///         <b>The window.</b> A transform assumes the block repeats forever, so the jump from its last sample
    ///         back to its first is a click - and a click is every frequency at once, which smears energy across
    ///         the whole display. Tapering the block to nothing at both ends removes the join.
    ///     </para>
    ///     <para>
    ///         <b>The bands are spaced logarithmically.</b> Hearing is: the octave from 100 to 200 hertz sounds like
    ///         the same distance as the one from 1000 to 2000. Space the bands evenly instead and everything anybody
    ///         listens to lands in the first two bars while eighteen more show the hiss above five kilohertz.
    ///     </para>
    ///     <para>
    ///         <b>The heights are decibels.</b> Loudness is a ratio, not an amount, and on a linear scale
    ///         everything except the very loudest moment is indistinguishable from silence.
    ///     </para>
    /// </summary>
    internal static class Spectrum
    {
        /// <summary>The quietest level drawn, in decibels below the loudest a sample can be.</summary>
        private const double FloorDecibels = -55d;

        /// <summary>The window, worked out once for the block size the pipe hands out.</summary>
        private static readonly double[] _window = Hann(AudioPipe.BlockSamples);

        /// <summary>The real part of the transform, kept between calls so nothing is allocated per block.</summary>
        private static readonly double[] _real = new double[AudioPipe.BlockSamples];

        /// <summary>The imaginary part.</summary>
        private static readonly double[] _imaginary = new double[AudioPipe.BlockSamples];

        /// <summary>
        ///     Turns a block of samples into band heights from zero to one.
        /// </summary>
        /// <param name="samples">The block. Anything other than the pipe's own block size is ignored.</param>
        /// <param name="bands">Where to put the heights; its length is how many bars there are.</param>
        public static void Compute(short[] samples, double[] bands)
        {
            if (bands == null || bands.Length == 0)
                return;

            if (samples == null || samples.Length != AudioPipe.BlockSamples)
            {
                Array.Clear(bands);
                return;
            }

            var n = AudioPipe.BlockSamples;

            for (var i = 0; i < n; i++)
            {
                _real[i] = samples[i] / 32768d * _window[i];
                _imaginary[i] = 0d;
            }

            Transform(_real, _imaginary);

            // Only the first half means anything: the second is its mirror image, since the samples are real.
            var bins = n / 2;

            for (var band = 0; band < bands.Length; band++)
            {
                var from = BinAt(band, bands.Length, bins);
                var to = Math.Max(from + 1, BinAt(band + 1, bands.Length, bins));

                var loudest = 0d;

                for (var bin = from; bin < to && bin < bins; bin++)
                {
                    var magnitude = Math.Sqrt(_real[bin] * _real[bin] + _imaginary[bin] * _imaginary[bin]);

                    if (magnitude > loudest)
                        loudest = magnitude;
                }

                bands[band] = Decibels(loudest * 2d / n);
            }
        }

        /// <summary>
        ///     Which bin a band starts at, spaced so each band covers the same musical distance as the last.
        ///     <para>
        ///         Bin zero is left out on purpose: it is the average level of the block rather than a frequency,
        ///         and on anything with a hum in it, it is permanently the tallest bar on the screen.
        ///     </para>
        /// </summary>
        /// <param name="band">Which band.</param>
        /// <param name="bands">How many bands there are.</param>
        /// <param name="bins">How many bins the transform produced.</param>
        /// <returns>The first bin of that band.</returns>
        private static int BinAt(int band, int bands, int bins)
        {
            var fraction = (double) band / bands;
            var top = Math.Max(2, bins - 1);

            return Math.Clamp((int) Math.Round(Math.Pow(top, fraction)), 1, bins - 1);
        }

        /// <summary>Turns a magnitude into a height from zero to one on a decibel scale.</summary>
        /// <param name="magnitude">The magnitude, where one is as loud as a sample can be.</param>
        /// <returns>The height.</returns>
        private static double Decibels(double magnitude)
        {
            if (magnitude <= 0d)
                return 0d;

            var db = 20d * Math.Log10(magnitude);

            return Math.Clamp((db - FloorDecibels) / -FloorDecibels, 0d, 1d);
        }

        /// <summary>
        ///     A raised cosine window, which tapers a block to nothing at both ends so its two ends meet without a
        ///     step in between.
        /// </summary>
        /// <param name="length">How many samples.</param>
        /// <returns>The window.</returns>
        private static double[] Hann(int length)
        {
            var window = new double[length];

            for (var i = 0; i < length; i++)
                window[i] = 0.5d - 0.5d * Math.Cos(2d * Math.PI * i / (length - 1));

            return window;
        }

        /// <summary>
        ///     An in-place radix-two fast Fourier transform. The length must be a power of two, which
        ///     <see cref="AudioPipe.BlockSamples" /> is chosen to be.
        /// </summary>
        /// <param name="real">The real parts, replaced by the result.</param>
        /// <param name="imaginary">The imaginary parts, replaced by the result.</param>
        private static void Transform(double[] real, double[] imaginary)
        {
            var n = real.Length;

            // Reorders the samples so the butterflies below read them in the right order. Each index swaps with the
            // one that is its own bits reversed, and doing it in place means only doing each pair once.
            for (int i = 1, j = 0; i < n; i++)
            {
                var bit = n >> 1;

                for (; (j & bit) != 0; bit >>= 1)
                    j ^= bit;

                j ^= bit;

                if (i >= j)
                    continue;

                (real[i], real[j]) = (real[j], real[i]);
                (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
            }

            for (var length = 2; length <= n; length <<= 1)
            {
                var angle = -2d * Math.PI / length;
                var stepReal = Math.Cos(angle);
                var stepImaginary = Math.Sin(angle);

                for (var start = 0; start < n; start += length)
                {
                    var spinReal = 1d;
                    var spinImaginary = 0d;

                    for (var k = 0; k < length / 2; k++)
                    {
                        var evenReal = real[start + k];
                        var evenImaginary = imaginary[start + k];

                        var oddReal = real[start + k + length / 2] * spinReal -
                                      imaginary[start + k + length / 2] * spinImaginary;

                        var oddImaginary = real[start + k + length / 2] * spinImaginary +
                                           imaginary[start + k + length / 2] * spinReal;

                        real[start + k] = evenReal + oddReal;
                        imaginary[start + k] = evenImaginary + oddImaginary;
                        real[start + k + length / 2] = evenReal - oddReal;
                        imaginary[start + k + length / 2] = evenImaginary - oddImaginary;

                        var nextReal = spinReal * stepReal - spinImaginary * stepImaginary;
                        spinImaginary = spinReal * stepImaginary + spinImaginary * stepReal;
                        spinReal = nextReal;
                    }
                }
            }
        }
    }
}

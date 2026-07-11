// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     Maps 24-bit RGB colors onto the 256-color xterm palette. The palette is the 16 legacy system colors, a
    ///     6x6x6 color cube (indices 16-231) and a 24-step grayscale ramp (indices 232-255). For any given color we
    ///     consider both the nearest cube entry and the nearest gray entry and keep whichever is a closer match, which
    ///     avoids the muddy look you get from forcing near-gray colors through the coarse cube.
    /// </summary>
    internal static class Ansi256
    {
        /// <summary>
        ///     The six intensity steps used by each axis of the color cube. Note the large gap between 0 and 95: the
        ///     cube is deliberately biased toward brighter values.
        /// </summary>
        private static readonly int[] CubeLevels = {0, 95, 135, 175, 215, 255};

        /// <summary>
        ///     Returns the palette index (16-255) that most closely matches the given color.
        /// </summary>
        public static int FromRgb(byte r, byte g, byte b)
        {
            // Best match inside the 6x6x6 color cube.
            var ri = NearestCubeIndex(r);
            var gi = NearestCubeIndex(g);
            var bi = NearestCubeIndex(b);
            var cubeIndex = 16 + 36 * ri + 6 * gi + bi;
            var cubeDistance = Distance(r, g, b, CubeLevels[ri], CubeLevels[gi], CubeLevels[bi]);

            // Best match on the dedicated grayscale ramp (values 8, 18, ... 238).
            var gray = (r * 299 + g * 587 + b * 114) / 1000; // Rec. 601 luma
            var grayStep = (gray - 8 + 5) / 10; // round to nearest ramp step
            if (grayStep < 0) grayStep = 0;
            if (grayStep > 23) grayStep = 23;
            var grayValue = 8 + 10 * grayStep;
            var grayIndex = 232 + grayStep;
            var grayDistance = Distance(r, g, b, grayValue, grayValue, grayValue);

            return grayDistance < cubeDistance ? grayIndex : cubeIndex;
        }

        /// <summary>
        ///     Returns the grayscale palette index (232-255, plus the cube's pure black/white endpoints) whose shade is
        ///     nearest the given color's luminance. Used by <see cref="AnsiColorMode.Grayscale" />.
        /// </summary>
        public static int GrayFromRgb(byte r, byte g, byte b)
        {
            var gray = (r * 299 + g * 587 + b * 114) / 1000; // Rec. 601 luma

            // Pure black and pure white live in the color cube, not the ramp, so include them as candidates to avoid a
            // washed-out darkest/lightest shade.
            if (gray < 4) return 16; // cube black
            if (gray > 246) return 231; // cube white

            var grayStep = (gray - 8 + 5) / 10;
            if (grayStep < 0) grayStep = 0;
            if (grayStep > 23) grayStep = 23;
            return 232 + grayStep;
        }

        /// <summary>Finds the index into <see cref="CubeLevels" /> whose value is closest to the channel.</summary>
        private static int NearestCubeIndex(int value)
        {
            var best = 0;
            var bestDelta = int.MaxValue;
            for (var i = 0; i < CubeLevels.Length; i++)
            {
                var delta = value - CubeLevels[i];
                if (delta < 0) delta = -delta;
                if (delta >= bestDelta) continue;
                bestDelta = delta;
                best = i;
            }

            return best;
        }

        /// <summary>Squared Euclidean distance between two colors; only used for comparison so the square root is skipped.</summary>
        private static int Distance(int r1, int g1, int b1, int r2, int g2, int b2)
        {
            var dr = r1 - r2;
            var dg = g1 - g2;
            var db = b1 - b2;
            return dr * dr + dg * dg + db * db;
        }
    }
}

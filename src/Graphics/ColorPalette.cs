// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     A small set of colors chosen to represent a specific image, plus the mapping from that image's colors onto
    ///     it. True-pixel protocols like sixel are indexed: the picture declares a palette up front and then refers to
    ///     its entries, so an image with more colors than the palette holds has to be reduced to fit.
    ///     <para>
    ///         The reduction is median cut. All the image's distinct colors start in one box; the box with the widest
    ///         spread in any one channel is repeatedly split at its weighted median along that channel, until there are
    ///         as many boxes as the palette has entries. Each box then contributes one color — the average of the colors
    ///         it holds, weighted by how often each occurs — so the palette spends its entries where the image actually
    ///         has detail, rather than on a fixed grid of colors most images barely touch.
    ///     </para>
    ///     <para>
    ///         Because the palette is built from the very pixels that will be encoded, every color the encoder asks for
    ///         is guaranteed to be in the map: <see cref="IndexOf" /> is an exact dictionary lookup and no nearest-color
    ///         search is ever needed. Build it from the <em>final, resized</em> image — resampling invents colors that
    ///         were not in the original.
    ///     </para>
    /// </summary>
    internal sealed class ColorPalette
    {
        /// <summary>Maps a packed 0xRRGGBB color onto its palette entry.</summary>
        private readonly Dictionary<int, byte> _map;

        private ColorPalette(Rgb24[] colors, Dictionary<int, byte> map)
        {
            Colors = colors;
            _map = map;
        }

        /// <summary>The chosen colors, in palette-entry order. Never more than the requested maximum.</summary>
        public Rgb24[] Colors { get; }

        /// <summary>
        ///     The palette entry representing the given packed 0xRRGGBB color. Only valid for colors that were present
        ///     (and opaque enough) in the image the palette was built from.
        /// </summary>
        public byte IndexOf(int packedRgb)
        {
            return _map[packedRgb];
        }

        /// <summary>Packs a color into the 0xRRGGBB key form used throughout this class.</summary>
        public static int Pack(byte r, byte g, byte b)
        {
            return (r << 16) | (g << 8) | b;
        }

        /// <summary>
        ///     Chooses a palette of at most <paramref name="maxColors" /> entries representing the opaque pixels of
        ///     <paramref name="image" />.
        /// </summary>
        /// <param name="image">The final, already-resized image whose pixels will be encoded.</param>
        /// <param name="alphaThreshold">
        ///     Alpha below which a pixel counts as invisible and takes no part in the palette — it will not be drawn, so
        ///     spending a palette entry on its color would waste one.
        /// </param>
        /// <param name="maxColors">Palette size limit, clamped to 1-256 (the range a sixel color register can hold).</param>
        public static ColorPalette Build(PixelBuffer image, byte alphaThreshold, int maxColors)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            maxColors = Math.Max(1, Math.Min(256, maxColors));

            var histogram = BuildHistogram(image, alphaThreshold);
            if (histogram.Count == 0)
                return new ColorPalette(Array.Empty<Rgb24>(), new Dictionary<int, byte>());

            var colors = new int[histogram.Count];
            var counts = new int[histogram.Count];
            var next = 0;
            foreach (var entry in histogram)
            {
                colors[next] = entry.Key;
                counts[next] = entry.Value;
                next++;
            }

            // Few enough colors to keep every one of them: no reduction, and the result is lossless.
            if (colors.Length <= maxColors)
                return Exact(colors);

            var boxes = MedianCut(colors, counts, maxColors);
            return FromBoxes(boxes, colors, counts);
        }

        /// <summary>Counts how often each opaque color occurs in the image.</summary>
        private static Dictionary<int, int> BuildHistogram(PixelBuffer image, byte alphaThreshold)
        {
            var histogram = new Dictionary<int, int>();
            var data = image.Data;

            for (var offset = 0; offset < data.Length; offset += 4)
            {
                if (data[offset + 3] < alphaThreshold)
                    continue;

                var packed = Pack(data[offset], data[offset + 1], data[offset + 2]);
                histogram.TryGetValue(packed, out var count);
                histogram[packed] = count + 1;
            }

            return histogram;
        }

        /// <summary>A palette that keeps every color exactly, for images that already fit.</summary>
        private static ColorPalette Exact(int[] colors)
        {
            var entries = new Rgb24[colors.Length];
            var map = new Dictionary<int, byte>(colors.Length);

            for (var i = 0; i < colors.Length; i++)
            {
                entries[i] = Unpack(colors[i]);
                map[colors[i]] = (byte) i;
            }

            return new ColorPalette(entries, map);
        }

        /// <summary>
        ///     Splits the colors into at most <paramref name="maxColors" /> boxes, each a contiguous range of the
        ///     (repeatedly re-sorted) <paramref name="colors" /> array.
        /// </summary>
        private static List<(int Start, int Length, int Range)> MedianCut(int[] colors, int[] counts, int maxColors)
        {
            var boxes = new List<(int Start, int Length, int Range)>
            {
                (0, colors.Length, WidestChannelRange(colors, 0, colors.Length))
            };

            while (boxes.Count < maxColors)
            {
                var target = WidestBox(boxes);
                if (target < 0)
                    break; // Every box holds a single color: splitting further is impossible.

                var (start, length, _) = boxes[target];
                var shift = WidestChannelShift(colors, start, length);

                // Sorting the range by the widest channel makes "split at the median" a matter of picking an index,
                // and keeps every box a contiguous range so no per-box color lists are needed.
                Array.Sort(colors, counts, start, length,
                    Comparer<int>.Create((a, b) => ((a >> shift) & 0xFF).CompareTo((b >> shift) & 0xFF)));

                // Each box caches its own spread, recomputed only for the two boxes a split actually changes. Scanning
                // every color of every box on every split instead would cost O(colors x palette size), seconds of work
                // on a photograph, to re-learn ranges that did not move.
                var split = WeightedMedian(counts, start, length);
                var leftLength = split - start;
                var rightLength = start + length - split;

                boxes[target] = (start, leftLength, WidestChannelRange(colors, start, leftLength));
                boxes.Insert(target + 1, (split, rightLength, WidestChannelRange(colors, split, rightLength)));
            }

            return boxes;
        }

        /// <summary>
        ///     Index of the box with the widest spread in any single channel — the one whose colors differ most, and so
        ///     the one that gains most from being split — or -1 when no box can be split further.
        /// </summary>
        private static int WidestBox(List<(int Start, int Length, int Range)> boxes)
        {
            var best = -1;
            var bestRange = -1;

            for (var i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i];
                if (box.Length < 2 || box.Range <= bestRange)
                    continue;

                best = i;
                bestRange = box.Range;
            }

            return best;
        }

        /// <summary>The bit shift (16 red, 8 green, 0 blue) of the channel that varies most across a box.</summary>
        private static int WidestChannelShift(int[] colors, int start, int length)
        {
            var shift = 0;
            var widest = -1;

            for (var channel = 0; channel < 3; channel++)
            {
                var candidate = channel * 8;
                var range = ChannelRange(colors, start, length, candidate);
                if (range <= widest)
                    continue;

                widest = range;
                shift = candidate;
            }

            return shift;
        }

        /// <summary>The largest of the box's three per-channel ranges.</summary>
        private static int WidestChannelRange(int[] colors, int start, int length)
        {
            var widest = 0;
            for (var channel = 0; channel < 3; channel++)
            {
                var range = ChannelRange(colors, start, length, channel * 8);
                if (range > widest)
                    widest = range;
            }

            return widest;
        }

        /// <summary>Difference between the highest and lowest value of one channel across a box.</summary>
        private static int ChannelRange(int[] colors, int start, int length, int shift)
        {
            var min = 255;
            var max = 0;

            for (var i = start; i < start + length; i++)
            {
                var value = (colors[i] >> shift) & 0xFF;
                if (value < min)
                    min = value;
                if (value > max)
                    max = value;
            }

            return max - min;
        }

        /// <summary>
        ///     The index to split a sorted box at, chosen so that roughly as many <em>pixels</em> (not colors) fall on
        ///     each side. Weighting by pixel count matters: a thousand pixels of one blue and one stray red should not
        ///     hand half the box to the red. The result always leaves both sides non-empty.
        /// </summary>
        private static int WeightedMedian(int[] counts, int start, int length)
        {
            long total = 0;
            for (var i = start; i < start + length; i++)
                total += counts[i];

            long running = 0;
            for (var i = start; i < start + length - 1; i++)
            {
                running += counts[i];
                if (running * 2 >= total)
                    return i + 1;
            }

            return start + length - 1;
        }

        /// <summary>Turns finished boxes into palette entries and the color-to-entry map.</summary>
        private static ColorPalette FromBoxes(List<(int Start, int Length, int Range)> boxes, int[] colors, int[] counts)
        {
            var entries = new Rgb24[boxes.Count];
            var map = new Dictionary<int, byte>(colors.Length);

            for (var i = 0; i < boxes.Count; i++)
            {
                var (start, length, _) = boxes[i];
                long red = 0, green = 0, blue = 0, weight = 0;

                for (var c = start; c < start + length; c++)
                {
                    var count = counts[c];
                    red += ((colors[c] >> 16) & 0xFF) * (long) count;
                    green += ((colors[c] >> 8) & 0xFF) * (long) count;
                    blue += (colors[c] & 0xFF) * (long) count;
                    weight += count;
                    map[colors[c]] = (byte) i;
                }

                entries[i] = weight == 0
                    ? new Rgb24(0, 0, 0)
                    : new Rgb24((byte) (red / weight), (byte) (green / weight), (byte) (blue / weight));
            }

            return new ColorPalette(entries, map);
        }

        /// <summary>Unpacks a 0xRRGGBB key back into a color.</summary>
        private static Rgb24 Unpack(int packed)
        {
            return new Rgb24((byte) ((packed >> 16) & 0xFF), (byte) ((packed >> 8) & 0xFF), (byte) (packed & 0xFF));
        }
    }
}

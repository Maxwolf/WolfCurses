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
        private readonly PackedColorMap _map;

        private ColorPalette(Rgb24[] colors, PackedColorMap map)
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
            return (byte) _map.Get(packedRgb);
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
                return new ColorPalette(Array.Empty<Rgb24>(), new PackedColorMap(0));

            var colors = new int[histogram.Count];
            var counts = new int[histogram.Count];
            histogram.ExportTo(colors, counts);

            // Few enough colors to keep every one of them: no reduction, and the result is lossless.
            if (colors.Length <= maxColors)
                return Exact(colors);

            var boxes = MedianCut(colors, counts, maxColors);
            return FromBoxes(boxes, colors, counts);
        }

        /// <summary>Counts how often each opaque color occurs in the image.</summary>
        private static PackedColorMap BuildHistogram(PixelBuffer image, byte alphaThreshold)
        {
            // The hint is only a starting size; the map grows with the distinct colors actually found, so memory
            // follows the image's color variety rather than its pixel count.
            var histogram = new PackedColorMap(image.Width * image.Height);
            var data = image.Data;

            for (var offset = 0; offset < data.Length; offset += 4)
            {
                if (data[offset + 3] < alphaThreshold)
                    continue;

                histogram.Increment(Pack(data[offset], data[offset + 1], data[offset + 2]));
            }

            return histogram;
        }

        /// <summary>A palette that keeps every color exactly, for images that already fit.</summary>
        private static ColorPalette Exact(int[] colors)
        {
            var entries = new Rgb24[colors.Length];
            var map = new PackedColorMap(colors.Length);

            for (var i = 0; i < colors.Length; i++)
            {
                entries[i] = Unpack(colors[i]);
                map.Set(colors[i], i);
            }

            return new ColorPalette(entries, map);
        }

        /// <summary>
        ///     Splits the colors into at most <paramref name="maxColors" /> boxes, each a contiguous range of the
        ///     (repeatedly re-sorted) <paramref name="colors" /> array.
        /// </summary>
        private static List<(int Start, int Length, int Range, int Shift)> MedianCut(int[] colors, int[] counts,
            int maxColors)
        {
            var boxes = new List<(int Start, int Length, int Range, int Shift)>
            {
                MakeBox(colors, 0, colors.Length)
            };

            // Scratch space for the counting sort, sized once for the whole run rather than per split.
            var scratchColors = new int[colors.Length];
            var scratchCounts = new int[colors.Length];
            var buckets = new int[257];

            while (boxes.Count < maxColors)
            {
                var target = WidestBox(boxes);
                if (target < 0)
                    break; // Every box holds a single color: splitting further is impossible.

                var (start, length, _, shift) = boxes[target];

                // Sorting the range by the widest channel makes "split at the median" a matter of picking an index,
                // and keeps every box a contiguous range so no per-box color lists are needed. The key is a single
                // byte, so a counting sort does it in one linear pass — a comparison sort through a comparer
                // delegate here was the single most expensive part of building a palette from a photograph.
                CountingSortByChannel(colors, counts, start, length, shift, scratchColors, scratchCounts, buckets);

                // Each box caches its own spread and widest channel, computed in one fused pass when the box is made
                // and never rescanned. Scanning every color of every box on every split instead would cost
                // O(colors x palette size), seconds of work on a photograph, to re-learn ranges that did not move.
                var split = WeightedMedian(counts, start, length);
                var leftLength = split - start;
                var rightLength = start + length - split;

                boxes[target] = MakeBox(colors, start, leftLength);
                boxes.Insert(target + 1, MakeBox(colors, split, rightLength));
            }

            return boxes;
        }

        /// <summary>
        ///     Builds a box record over a range: one pass finds the minimum and maximum of all three channels at once,
        ///     giving both the widest spread (what decides which box splits next) and the channel it lies in (what the
        ///     split sorts by). One fused scan instead of the six a split used to spend re-measuring its boxes.
        /// </summary>
        private static (int Start, int Length, int Range, int Shift) MakeBox(int[] colors, int start, int length)
        {
            int minR = 255, maxR = 0, minG = 255, maxG = 0, minB = 255, maxB = 0;

            for (var i = start; i < start + length; i++)
            {
                var color = colors[i];
                var r = (color >> 16) & 0xFF;
                var g = (color >> 8) & 0xFF;
                var b = color & 0xFF;
                if (r < minR) minR = r;
                if (r > maxR) maxR = r;
                if (g < minG) minG = g;
                if (g > maxG) maxG = g;
                if (b < minB) minB = b;
                if (b > maxB) maxB = b;
            }

            // Ties keep the order WidestChannelShift used to produce (blue wins an equal spread, then green), so the
            // palette comes out the same as before this was fused into one pass.
            var rangeR = maxR - minR;
            var rangeG = maxG - minG;
            var rangeB = maxB - minB;

            var range = rangeB;
            var shift = 0;
            if (rangeG > range)
            {
                range = rangeG;
                shift = 8;
            }

            if (rangeR > range)
            {
                range = rangeR;
                shift = 16;
            }

            return (start, length, range, shift);
        }

        /// <summary>
        ///     Sorts <paramref name="colors" /> (and <paramref name="counts" /> alongside) over
        ///     [<paramref name="start" />, <paramref name="start" /> + <paramref name="length" />) by the 8-bit channel
        ///     at <paramref name="shift" />, using a counting sort: one pass to bucket, one to place. Equal keys keep
        ///     their relative order, which is as valid as any — the comparison sort this replaces was unstable, and
        ///     median cut only needs the range ordered by the channel value.
        /// </summary>
        private static void CountingSortByChannel(int[] colors, int[] counts, int start, int length, int shift,
            int[] scratchColors, int[] scratchCounts, int[] buckets)
        {
            Array.Clear(buckets, 0, buckets.Length);

            var end = start + length;
            for (var i = start; i < end; i++)
                buckets[((colors[i] >> shift) & 0xFF) + 1]++;

            for (var b = 1; b < buckets.Length; b++)
                buckets[b] += buckets[b - 1];

            for (var i = start; i < end; i++)
            {
                var slot = buckets[(colors[i] >> shift) & 0xFF]++;
                scratchColors[slot] = colors[i];
                scratchCounts[slot] = counts[i];
            }

            Array.Copy(scratchColors, 0, colors, start, length);
            Array.Copy(scratchCounts, 0, counts, start, length);
        }

        /// <summary>
        ///     Index of the box with the widest spread in any single channel — the one whose colors differ most, and so
        ///     the one that gains most from being split — or -1 when no box can be split further.
        /// </summary>
        private static int WidestBox(List<(int Start, int Length, int Range, int Shift)> boxes)
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
        private static ColorPalette FromBoxes(List<(int Start, int Length, int Range, int Shift)> boxes, int[] colors,
            int[] counts)
        {
            var entries = new Rgb24[boxes.Count];
            var map = new PackedColorMap(colors.Length);

            for (var i = 0; i < boxes.Count; i++)
            {
                var (start, length, _, _) = boxes[i];
                long red = 0, green = 0, blue = 0, weight = 0;

                for (var c = start; c < start + length; c++)
                {
                    var count = counts[c];
                    red += ((colors[c] >> 16) & 0xFF) * (long) count;
                    green += ((colors[c] >> 8) & 0xFF) * (long) count;
                    blue += (colors[c] & 0xFF) * (long) count;
                    weight += count;
                    map.Set(colors[c], i);
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

        /// <summary>
        ///     An open-addressing hash map from packed 0xRRGGBB keys to int values, replacing
        ///     <see cref="Dictionary{TKey,TValue}" /> on this class's hottest paths (one histogram update per pixel
        ///     when building, one lookup per pixel when encoding).
        ///     <para>
        ///         Keys and values live in dense arrays <b>in insertion order</b>, with the hash table holding only
        ///         indices into them — and the ordering is load-bearing, not tidiness. <see cref="ExportTo" /> feeds
        ///         the array median cut works on, palette entry numbers are positional, and the counting sort is
        ///         stable, so the export order decides both which color becomes register 0 and how sort ties resolve.
        ///         First-seen order is what the <see cref="Dictionary{TKey,TValue}" /> this replaced produced, and an
        ///         earlier draft that exported in hash-slot order silently reordered every multi-color palette.
        ///     </para>
        /// </summary>
        private sealed class PackedColorMap
        {
            /// <summary>Hash table: 0 means empty, otherwise 1 + the entry's index in the dense arrays.</summary>
            private int[] _slots;

            private int[] _keys;
            private int[] _values;
            private int _mask;
            private int _shift;

            public PackedColorMap(int capacityHint)
            {
                // Start modest and double as needed rather than presizing for the hint's worst case: the hint is a
                // pixel count, and "every pixel a distinct color" would charge ~16 bytes per pixel up front — tens of
                // megabytes per palette build on a full-screen downscale — to dodge a few amortized linear rehashes.
                // Growth is bounded by the distinct colors actually seen, never by the pixel count.
                var capacity = 16;
                var target = Math.Min(Math.Max(capacityHint, 1), 4096);
                while (capacity < target * 2)
                    capacity <<= 1;

                _slots = new int[capacity];
                _keys = new int[capacity / 2];
                _values = new int[capacity / 2];
                _mask = capacity - 1;
                _shift = 32 - System.Numerics.BitOperations.TrailingZeroCount(capacity);
            }

            /// <summary>How many distinct keys are stored.</summary>
            public int Count { get; private set; }

            /// <summary>
            ///     Fibonacci hashing: the top bits of the golden-ratio product spread the sequential-ish packed colors
            ///     evenly, and taking exactly as many top bits as the table needs is what makes it work.
            /// </summary>
            private int Hash(int key)
            {
                unchecked
                {
                    return (int) ((uint) key * 2654435769u >> _shift);
                }
            }

            /// <summary>Adds one to the count stored under <paramref name="key" />, inserting it at zero first.</summary>
            public void Increment(int key)
            {
                var slot = FindSlot(key);
                var stored = _slots[slot];
                if (stored != 0)
                {
                    _values[stored - 1]++;
                    return;
                }

                Add(slot, key, 1);
            }

            /// <summary>Stores <paramref name="value" /> under <paramref name="key" />, replacing any previous value.</summary>
            public void Set(int key, int value)
            {
                var slot = FindSlot(key);
                var stored = _slots[slot];
                if (stored != 0)
                {
                    _values[stored - 1] = value;
                    return;
                }

                Add(slot, key, value);
            }

            /// <summary>The value stored under <paramref name="key" />; throws like a dictionary when it is absent.</summary>
            public int Get(int key)
            {
                var stored = _slots[FindSlot(key)];
                if (stored == 0)
                    throw new KeyNotFoundException($"Color {key:X6} was never added to the palette map.");

                return _values[stored - 1];
            }

            /// <summary>Copies every stored key and value into the given arrays, in first-seen order.</summary>
            public void ExportTo(int[] keys, int[] values)
            {
                Array.Copy(_keys, keys, Count);
                Array.Copy(_values, values, Count);
            }

            /// <summary>
            ///     The slot where <paramref name="key" /> lives, or the empty slot where it belongs. Always terminates:
            ///     the dense arrays fill before the table passes half full, and filling them forces a grow.
            /// </summary>
            private int FindSlot(int key)
            {
                var slot = Hash(key) & _mask;
                while (true)
                {
                    var stored = _slots[slot];
                    if (stored == 0 || _keys[stored - 1] == key)
                        return slot;

                    slot = (slot + 1) & _mask;
                }
            }

            /// <summary>Appends a new entry to the dense arrays and points the hash slot at it.</summary>
            private void Add(int slot, int key, int value)
            {
                if (Count == _keys.Length)
                {
                    Grow();
                    slot = FindSlot(key); // The table was rebuilt, so the empty slot moved.
                }

                _keys[Count] = key;
                _values[Count] = value;
                Count++;
                _slots[slot] = Count;
            }

            /// <summary>
            ///     Doubles the table, re-pointing every slot at the dense entries — which never move, so insertion
            ///     order survives every growth by construction.
            /// </summary>
            private void Grow()
            {
                var capacity = _slots.Length << 1;
                Array.Resize(ref _keys, capacity / 2);
                Array.Resize(ref _values, capacity / 2);

                _slots = new int[capacity];
                _mask = capacity - 1;
                _shift = 32 - System.Numerics.BitOperations.TrailingZeroCount(capacity);

                for (var dense = 0; dense < Count; dense++)
                {
                    var slot = Hash(_keys[dense]) & _mask;
                    while (_slots[slot] != 0)
                        slot = (slot + 1) & _mask;

                    _slots[slot] = dense + 1;
                }
            }
        }
    }
}

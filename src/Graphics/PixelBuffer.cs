// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     A decoder-agnostic, in-memory raster image: a flat row-major array of 32-bit RGBA pixels. This is the common
    ///     exchange type between an <see cref="IImageDecoder" /> (which turns a file or stream into pixels) and the
    ///     <see cref="AnsiImageRenderer" /> (which turns pixels into an ANSI string). It intentionally has no external
    ///     dependencies so the rendering half of the feature can be exercised with hand-built synthetic images in tests.
    ///     <para>
    ///         <b><see cref="Fill(Rgba32)" /> paints; every <c>Draw</c> composites.</b> That split is not an accident of
    ///         naming and there are no exceptions to it. <see cref="Fill(Rgba32)" /> has to <i>set</i> pixels, because
    ///         clearing a canvas is exactly what a compositing operation provably cannot do — source-over with a
    ///         transparent colour leaves the destination untouched, so a compositing "clear to transparent" is a no-op.
    ///         Everything else composites, because a caller who wants replacement passes an opaque colour and gets it
    ///         for free, while a caller who wants a translucent fireball over a missile trail has no way to fake a blend
    ///         out of a paint. This is also why there is no <c>FillCircle</c>: a compositing method wearing the
    ///         <c>Fill</c> prefix would be an exception you could only learn from documentation, and — worse — one whose
    ///         wrongness is invisible over a black canvas, which is the only canvas anybody would test it on.
    ///     </para>
    /// </summary>
    public sealed class PixelBuffer
    {
        /// <summary>
        ///     Number of bytes that make up a single pixel: red, green, blue, alpha.
        /// </summary>
        internal const int BytesPerPixel = 4;

        /// <summary>
        ///     Pixels of work (the larger of source and destination) below which <see cref="Resize" /> stays on one
        ///     thread. Fanning a resize out across cores pays for itself on the buffers the true-pixel renderers
        ///     chew through — a photograph, a sixel canvas — and costs more than it saves on a thumbnail, so small
        ///     jobs keep the simple path. 100K pixels is roughly where the crossover sits.
        /// </summary>
        private const int ParallelResizeThresholdPixels = 100_000;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PixelBuffer" /> class wrapping an existing RGBA byte array
        ///     without copying it. The array must contain exactly <paramref name="width" /> * <paramref name="height" />
        ///     * 4 bytes laid out row by row, top to bottom, each pixel as red, green, blue, alpha.
        /// </summary>
        /// <param name="width">Image width in pixels; must be greater than zero.</param>
        /// <param name="height">Image height in pixels; must be greater than zero.</param>
        /// <param name="data">Row-major RGBA pixel bytes.</param>
        public PixelBuffer(int width, int height, byte[] data)
        {
            var expected = ValidatedByteCount(width, height);
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.Length != expected)
                throw new ArgumentException(
                    $"Pixel data length {data.Length} does not match {width}x{height} RGBA ({expected} bytes expected).",
                    nameof(data));

            Width = width;
            Height = height;
            Data = data;
        }

        /// <summary>
        ///     Initializes a new, fully transparent black <see cref="PixelBuffer" /> of the given size.
        /// </summary>
        /// <param name="width">Image width in pixels; must be greater than zero.</param>
        /// <param name="height">Image height in pixels; must be greater than zero.</param>
        public PixelBuffer(int width, int height)
        {
            var byteCount = ValidatedByteCount(width, height);
            Width = width;
            Height = height;
            Data = new byte[byteCount];
        }

        /// <summary>Image width in pixels.</summary>
        public int Width { get; }

        /// <summary>Image height in pixels.</summary>
        public int Height { get; }

        /// <summary>
        ///     Row-major RGBA pixel bytes, length equals <see cref="Width" /> * <see cref="Height" /> * 4.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>Reads the pixel at the given coordinate.</summary>
        /// <param name="x">Column, 0 to <see cref="Width" /> - 1.</param>
        /// <param name="y">Row, 0 to <see cref="Height" /> - 1.</param>
        /// <returns>The <see cref="Rgba32" /> color at that coordinate.</returns>
        public Rgba32 GetPixel(int x, int y)
        {
            if ((uint) x >= (uint) Width)
                throw new ArgumentOutOfRangeException(nameof(x), x, "Column is outside the image bounds.");
            if ((uint) y >= (uint) Height)
                throw new ArgumentOutOfRangeException(nameof(y), y, "Row is outside the image bounds.");

            var i = (y * Width + x) * BytesPerPixel;
            return new Rgba32(Data[i], Data[i + 1], Data[i + 2], Data[i + 3]);
        }

        /// <summary>Writes the pixel at the given coordinate.</summary>
        /// <param name="x">Column, 0 to <see cref="Width" /> - 1.</param>
        /// <param name="y">Row, 0 to <see cref="Height" /> - 1.</param>
        /// <param name="color">Color to store.</param>
        public void SetPixel(int x, int y, Rgba32 color)
        {
            if ((uint) x >= (uint) Width)
                throw new ArgumentOutOfRangeException(nameof(x), x, "Column is outside the image bounds.");
            if ((uint) y >= (uint) Height)
                throw new ArgumentOutOfRangeException(nameof(y), y, "Row is outside the image bounds.");

            var i = (y * Width + x) * BytesPerPixel;
            Data[i] = color.R;
            Data[i + 1] = color.G;
            Data[i + 2] = color.B;
            Data[i + 3] = color.A;
        }

        /// <summary>Paints every pixel of the image one colour.</summary>
        /// <param name="color">The colour to paint.</param>
        public void Fill(Rgba32 color)
        {
            Fill(0, 0, Width, Height, color);
        }

        /// <summary>
        ///     Paints a rectangle one colour, <b>clipped</b> to the image rather than throwing — a rectangle that
        ///     hangs off the edge paints the part that lands, and one entirely outside paints nothing.
        ///     <para>
        ///         Clipping rather than validating is deliberate, and matches <see cref="DrawImage" />: the callers
        ///         that want a rectangle are compositing, where "draw this tile at that offset" routinely runs off
        ///         the edge and having to bounds-check every call before making it is how the arithmetic ends up
        ///         duplicated at every call site. Replaces the nested <see cref="SetPixel" /> loop that
        ///         <see cref="Decoding.GifDecoder" /> and any compositing caller would otherwise write, and writes
        ///         the row bytes directly rather than going through the per-pixel bounds test.
        ///     </para>
        ///     <para>
        ///         Because it clips, <c>Fill(x, y, 1, 1, colour)</c> is also the bounds-safe single pixel:
        ///         <see cref="SetPixel" /> throws, and a caller plotting a computed shape almost always wants the clip
        ///         rather than the exception. For shapes with more than one pixel in them see <see cref="DrawLine(int, int, int, int, Rgba32)" />
        ///         and <see cref="DrawDisc" />, which composite rather than paint — see the remarks on this class.
        ///     </para>
        /// </summary>
        /// <param name="x">Left edge, which may be negative.</param>
        /// <param name="y">Top edge, which may be negative.</param>
        /// <param name="width">Width in pixels; zero or less paints nothing.</param>
        /// <param name="height">Height in pixels; zero or less paints nothing.</param>
        /// <param name="color">The colour to paint.</param>
        public void Fill(int x, int y, int width, int height, Rgba32 color)
        {
            var left = Math.Max(0, x);
            var top = Math.Max(0, y);
            var right = Math.Min(Width, x + width);
            var bottom = Math.Min(Height, y + height);

            if (right <= left || bottom <= top)
                return;

            for (var row = top; row < bottom; row++)
            {
                var i = (row * Width + left) * BytesPerPixel;
                for (var column = left; column < right; column++)
                {
                    Data[i] = color.R;
                    Data[i + 1] = color.G;
                    Data[i + 2] = color.B;
                    Data[i + 3] = color.A;
                    i += BytesPerPixel;
                }
            }
        }

        /// <summary>
        ///     Produces a resized copy of this image using area-averaging (box) resampling. Every destination pixel is the
        ///     coverage-weighted average of the source pixels it overlaps, which gives smooth down-scaling of photographs
        ///     without the sparkle of nearest-neighbour. Averaging is done in premultiplied-alpha space so the color of a
        ///     transparent pixel never bleeds into its opaque neighbours (this is what keeps a dark halo from forming
        ///     around the edges of a transparent PNG such as a logo on a soft edge).
        /// </summary>
        /// <param name="newWidth">Target width in pixels; must be greater than zero.</param>
        /// <param name="newHeight">Target height in pixels; must be greater than zero.</param>
        /// <returns>A new <see cref="PixelBuffer" /> of the requested size.</returns>
        public PixelBuffer Resize(int newWidth, int newHeight)
        {
            var dstByteCount = ValidatedByteCount(newWidth, newHeight, nameof(newWidth), nameof(newHeight));

            // Nothing to do when the dimensions already match; hand back a defensive copy so callers can freely mutate.
            if (newWidth == Width && newHeight == Height)
                return new PixelBuffer(Width, Height, (byte[]) Data.Clone());

            var dst = new byte[dstByteCount];

            // Scale factors expressed as source-pixels per destination-pixel. These are doubles so the fractional
            // overlap along the edges of each destination cell is accounted for exactly.
            var scaleX = (double) Width / newWidth;
            var scaleY = (double) Height / newHeight;

            // Which source columns each destination column draws from, worked out once. It is the same on every row, so
            // computing it inside the loop meant a million-odd repetitions of the same two thousand answers — and each
            // one costs a multiply, a floor and a ceiling, which is most of what a cheap pixel costs. Hoisting it is
            // worth more than the short cut below on its own.
            var columnLeft = new double[newWidth];
            var columnRight = new double[newWidth];
            var columnStart = new int[newWidth];
            var columnEnd = new int[newWidth];
            for (var dx = 0; dx < newWidth; dx++)
            {
                var left = dx * scaleX;
                var right = (dx + 1) * scaleX;
                var end = (int) Math.Ceiling(right);

                columnLeft[dx] = left;
                columnRight[dx] = right;
                columnStart[dx] = (int) Math.Floor(left);
                columnEnd[dx] = end > Width ? Width : end;
            }

            // Every destination row writes a disjoint slice of dst and reads only the immutable source, so rows can
            // be computed on any thread in any order and the bytes come out identical to the sequential loop —
            // scheduling cannot change a single-writer answer. Work is estimated as the larger of source and
            // destination pixel counts, since upscales are dominated by destination pixels and downscales by source
            // reads. Measured on the 78x16 compositing downscale of a 1280-wide photograph: ~34 ms to ~6 ms.
            var workPixels = Math.Max((long) Width * Height, (long) newWidth * newHeight);
            if (workPixels >= ParallelResizeThresholdPixels && newHeight > 1)
            {
                Parallel.For(0, newHeight, ResizeRow);
            }
            else
            {
                for (var dy = 0; dy < newHeight; dy++)
                    ResizeRow(dy);
            }

            void ResizeRow(int dy)
            {
                // Local copies of everything the closure captures: the row body is a hot loop, and reading these
                // through the compiler-generated display class on every iteration measurably slows it down compared
                // to the plain loop this used to be, where they were true locals.
                var src = Data;
                var srcWidth = Width;
                var srcHeight = Height;
                var dstWidth = newWidth;
                var dstBytes = dst;
                var colLeft = columnLeft;
                var colRight = columnRight;
                var colStart = columnStart;
                var colEnd = columnEnd;

                var srcTop = dy * scaleY;
                var srcBottom = (dy + 1) * scaleY;
                var y0 = (int) Math.Floor(srcTop);
                var y1 = (int) Math.Ceiling(srcBottom);
                if (y1 > srcHeight) y1 = srcHeight;

                var singleRow = y1 - y0 == 1;
                var rowBase = dy * dstWidth * BytesPerPixel;
                var sourceRowBase = y0 * srcWidth * BytesPerPixel;

                for (var dx = 0; dx < dstWidth; dx++)
                {
                    var srcLeft = colLeft[dx];
                    var srcRight = colRight[dx];
                    var x0 = colStart[dx];
                    var x1 = colEnd[dx];

                    var di = rowBase + dx * BytesPerPixel;

                    // The destination cell lies wholly inside one source pixel, so the average of what it covers is
                    // that pixel and nothing else. Worth saying out loud because the arithmetic below would arrive at
                    // exactly the same byte after a dozen floating-point operations, a premultiply and two divides:
                    // with one pixel in the sum, sumR/sumColorWeight is its red however the weights are chosen, and
                    // outA is its alpha. This is a short cut, not an approximation.
                    //
                    // It exists because enlarging is the common case and the expensive one. Scaling a 360-wide canvas
                    // up to the 1980-pixel grid a sixel terminal wants makes each destination cell about a fifth of a
                    // source pixel across, so it lands inside one roughly nineteen times in twenty — and there are 1.6
                    // million of them, every frame. Measured on that exact upscale: 44.7ms before, and it was 40% of
                    // the frame.
                    if (singleRow && x1 - x0 == 1)
                    {
                        var single = sourceRowBase + x0 * BytesPerPixel;

                        // A transparent source pixel leaves transparent black rather than its own hue, which is what
                        // the long way round does too: it weights color by alpha, so a transparent pixel contributes
                        // no color to recover, and the destination keeps the zeros it was born with.
                        if (src[single + 3] != 0)
                        {
                            dstBytes[di] = src[single];
                            dstBytes[di + 1] = src[single + 1];
                            dstBytes[di + 2] = src[single + 2];
                            dstBytes[di + 3] = src[single + 3];
                        }

                        continue;
                    }

                    // Accumulators. Color is summed premultiplied by (coverage * alpha) so a fully transparent source
                    // pixel contributes nothing to the resulting hue; alpha is summed by coverage alone.
                    double sumR = 0, sumG = 0, sumB = 0;
                    double sumAlphaWeighted = 0; // sum of (coverage)               -> weights the alpha average
                    double sumColorWeight = 0;   // sum of (coverage * alpha/255)   -> weights the color average
                    double sumCoverage = 0;

                    for (var sy = y0; sy < y1; sy++)
                    {
                        var yOverlap = Math.Min(srcBottom, sy + 1) - Math.Max(srcTop, sy);
                        if (yOverlap <= 0) continue;

                        var rowOffset = sy * srcWidth * BytesPerPixel;
                        for (var sx = x0; sx < x1; sx++)
                        {
                            var xOverlap = Math.Min(srcRight, sx + 1) - Math.Max(srcLeft, sx);
                            if (xOverlap <= 0) continue;

                            var coverage = xOverlap * yOverlap;
                            var i = rowOffset + sx * BytesPerPixel;
                            double a = src[i + 3];
                            var colorWeight = coverage * (a / 255.0);

                            sumR += src[i] * colorWeight;
                            sumG += src[i + 1] * colorWeight;
                            sumB += src[i + 2] * colorWeight;
                            sumAlphaWeighted += coverage * a;
                            sumColorWeight += colorWeight;
                            sumCoverage += coverage;
                        }
                    }

                    if (sumCoverage <= 0)
                        continue; // leave transparent black (already zeroed)

                    var outA = sumAlphaWeighted / sumCoverage;

                    // Un-premultiply to recover a straight-alpha color. When every overlapping pixel was transparent
                    // there is no hue to recover, so the destination stays transparent black.
                    if (sumColorWeight > 0)
                    {
                        dstBytes[di] = ClampToByte(sumR / sumColorWeight);
                        dstBytes[di + 1] = ClampToByte(sumG / sumColorWeight);
                        dstBytes[di + 2] = ClampToByte(sumB / sumColorWeight);
                    }

                    dstBytes[di + 3] = ClampToByte(outA);
                }
            }

            return new PixelBuffer(newWidth, newHeight, dst);
        }

        /// <summary>
        ///     Returns a new image containing the given rectangular region of this one (a straight copy, no scaling).
        /// </summary>
        /// <param name="x">Left edge of the region, 0 to <see cref="Width" /> - 1.</param>
        /// <param name="y">Top edge of the region, 0 to <see cref="Height" /> - 1.</param>
        /// <param name="width">Region width in pixels; must be greater than zero and fit within the image.</param>
        /// <param name="height">Region height in pixels; must be greater than zero and fit within the image.</param>
        public PixelBuffer Crop(int x, int y, int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Crop width must be greater than zero.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Crop height must be greater than zero.");
            if (x < 0 || y < 0 || x + width > Width || y + height > Height)
                throw new ArgumentOutOfRangeException(nameof(x),
                    $"Crop rectangle ({x},{y},{width}x{height}) lies outside the {Width}x{Height} image.");

            var byteCount = ValidatedByteCount(width, height);
            var dst = new byte[byteCount];
            var rowBytes = width * BytesPerPixel;
            for (var row = 0; row < height; row++)
            {
                var srcOffset = ((y + row) * Width + x) * BytesPerPixel;
                Array.Copy(Data, srcOffset, dst, row * rowBytes, rowBytes);
            }

            return new PixelBuffer(width, height, dst);
        }

        /// <summary>
        ///     Alpha-composites <paramref name="overlay" /> on top of this image at pixel offset
        ///     (<paramref name="x" />, <paramref name="y" />), mutating this image in place. This is the standard
        ///     "source over" (Porter-Duff) blend done in straight alpha, so a semi-transparent overlay lets the image
        ///     underneath show through, a fully transparent overlay pixel changes nothing, and the result keeps a
        ///     correct alpha channel (a spot where both images are transparent stays transparent). Any part of the
        ///     overlay that falls outside this image is clipped.
        /// </summary>
        /// <param name="overlay">The image to draw on top.</param>
        /// <param name="x">Horizontal offset of the overlay's left edge within this image (may be negative).</param>
        /// <param name="y">Vertical offset of the overlay's top edge within this image (may be negative).</param>
        public void DrawImage(PixelBuffer overlay, int x, int y)
        {
            if (overlay == null)
                throw new ArgumentNullException(nameof(overlay));

            for (var oy = 0; oy < overlay.Height; oy++)
            {
                var dy = y + oy;
                if (dy < 0 || dy >= Height) continue;

                for (var ox = 0; ox < overlay.Width; ox++)
                {
                    var dx = x + ox;
                    if (dx < 0 || dx >= Width) continue;

                    var si = (oy * overlay.Width + ox) * BytesPerPixel;
                    int sa = overlay.Data[si + 3];
                    if (sa == 0)
                        continue; // fully transparent overlay pixel: leave the destination untouched

                    var di = (dy * Width + dx) * BytesPerPixel;
                    BlendInto(di, overlay.Data[si], overlay.Data[si + 1], overlay.Data[si + 2], sa);
                }
            }
        }

        /// <summary>
        ///     Draws a one-pixel-wide straight line between two points, compositing it over what is already there.
        ///     Both endpoints are included, and the line is clipped to the image rather than refused by it.
        /// </summary>
        /// <param name="x0">Start column, which may lie outside the image.</param>
        /// <param name="y0">Start row, which may lie outside the image.</param>
        /// <param name="x1">End column, which may lie outside the image.</param>
        /// <param name="y1">End row, which may lie outside the image.</param>
        /// <param name="color">The colour to draw; a translucent one blends, an opaque one replaces.</param>
        public void DrawLine(int x0, int y0, int x1, int y1, Rgba32 color)
        {
            DrawLine(x0, y0, x1, y1, color, 1);
        }

        /// <summary>
        ///     Draws a straight line of the given thickness between two points, compositing it over what is already
        ///     there. Both endpoints are included, and the line is clipped to the image rather than refused by it.
        ///     <para>
        ///         <b>Every pixel of the line is blended exactly once.</b> The line is drawn as one perpendicular span
        ///         per integer step of its major axis — distinct steps are distinct major coordinates, so the spans are
        ///         provably disjoint. That is the whole reason this method is worth having rather than being left to
        ///         the caller: the obvious way to give a line thickness is to stamp a square at each step, and
        ///         consecutive stamps overlap by all but one row, so a <i>translucent</i> stamped line blends most of
        ///         itself twice and comes out blotchy and darker than asked for. An opaque line hides the bug
        ///         completely, which is how it survives being eyeballed.
        ///     </para>
        ///     <para>
        ///         Thickness grows perpendicular to the major axis and the ends are square-cut rather than extended,
        ///         so a thick line covers exactly the span the thin one did, widened. An even thickness cannot be
        ///         centred on a pixel, so it grows one further down (or right) than up (or left).
        ///     </para>
        /// </summary>
        /// <param name="x0">Start column, which may lie outside the image.</param>
        /// <param name="y0">Start row, which may lie outside the image.</param>
        /// <param name="x1">End column, which may lie outside the image.</param>
        /// <param name="y1">End row, which may lie outside the image.</param>
        /// <param name="color">The colour to draw; a translucent one blends, an opaque one replaces.</param>
        /// <param name="thickness">How many pixels wide the line is; zero or less draws nothing.</param>
        public void DrawLine(int x0, int y0, int x1, int y1, Rgba32 color, int thickness)
        {
            if (thickness <= 0 || color.A == 0)
                return;

            // How far the span reaches back from the centre line. Integer division is what makes an even thickness
            // lean one way, which is documented above rather than corrected.
            var half = (thickness - 1) / 2;

            // Reject in constant time when nothing can land. Expanding both axes by the half-width is deliberately
            // conservative - the span only widens across one of them - since a reject that fires slightly less often
            // costs nothing and getting the axis wrong would clip a line that should have been drawn.
            if (Math.Max(x0, x1) + (long) half < 0 || Math.Min(x0, x1) - (long) half >= Width)
                return;
            if (Math.Max(y0, y1) + (long) half < 0 || Math.Min(y0, y1) - (long) half >= Height)
                return;

            var dx = (long) x1 - x0;
            var dy = (long) y1 - y0;
            var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));

            if (steps == 0)
            {
                // A zero-length line is still a mark rather than nothing, which is what a caller drawing a trail from
                // a missile's origin to a missile that has not moved yet is relying on.
                BlendSpanVertical(x0, y0 - (long) half, y0 - (long) half + thickness - 1, color);
                return;
            }

            // A horizontal line writes one pixel per column the long way round: correct, but it walks down a
            // row-major array a whole stride at a time for every one of them. Drawn as rows instead it is the same
            // set of pixels, still each visited once, at a fraction of the cache cost - and this is the common case,
            // being every rule, axis and ground line anybody draws.
            if (dy == 0)
            {
                var top = y0 - (long) half;
                for (var row = top; row < top + thickness; row++)
                {
                    if (row >= 0 && row < Height)
                        BlendSpanHorizontal((int) row, Math.Min(x0, x1), Math.Max(x0, x1), color);
                }

                return;
            }

            var xMajor = Math.Abs(dx) >= Math.Abs(dy);

            // Which values of the step counter put the major axis inside the image. Clipping the loop rather than the
            // pixels is what keeps a line drawn between coordinates millions of pixels apart from costing millions of
            // iterations - and it is only safe because the position below is a pure function of `step`, computed from
            // the original endpoints every time. An incremental error accumulator would give different pixels
            // depending on where the loop was entered, which is the bug this shape exists to not have.
            var majorStart = xMajor ? x0 : y0;
            var majorLimit = xMajor ? Width : Height;
            var forward = xMajor ? dx > 0 : dy > 0;

            var first = forward ? -(long) majorStart : majorStart - (majorLimit - 1L);
            var last = forward ? majorLimit - 1L - majorStart : majorStart;
            if (first < 0) first = 0;
            if (last > steps) last = steps;

            for (var step = first; step <= last; step++)
            {
                // The major axis advances exactly one pixel per step, so only the minor one is interpolated - and
                // rounding it away from zero keeps the line symmetric when it is drawn in either direction.
                var minor = (int) Math.Round((double) (xMajor ? dy : dx) * step / steps, MidpointRounding.AwayFromZero);

                if (xMajor)
                {
                    var x = x0 + (forward ? step : -step);
                    var top = y0 + minor - (long) half;
                    BlendSpanVertical((int) x, top, top + thickness - 1, color);
                }
                else
                {
                    var y = y0 + (forward ? step : -step);
                    var left = x0 + minor - (long) half;
                    BlendSpanHorizontal((int) y, left, left + thickness - 1, color);
                }
            }
        }

        /// <summary>
        ///     Draws a filled circle centred on a point, compositing it over what is already there and clipping it to
        ///     the image rather than refusing it.
        ///     <para>
        ///         <b>Every pixel of the disc is blended exactly once</b>, because it is drawn as one horizontal span
        ///         per row and rows cannot overlap. That is the point of the method. The textbook alternative — a
        ///         midpoint circle mirrored into eight octants — plots the octant seams and the four axis extremes
        ///         twice, which is invisible for an opaque colour and paints a dark X straight through a
        ///         <i>translucent</i> one. Anything drawing a fading blast, a glow or a soft highlight hits that
        ///         immediately, and it is the kind of bug that reads as "my alpha maths is wrong".
        ///     </para>
        ///     <para>
        ///         It is called <c>DrawDisc</c> and not <c>FillCircle</c> because it composites: see the remarks on
        ///         this class for why nothing named <c>Fill</c> is allowed to. There is deliberately no outline circle
        ///         and no outline rectangle — an outline is a different algorithm with its own double-plotting seams,
        ///         and neither has a caller.
        ///     </para>
        /// </summary>
        /// <param name="centerX">Column of the centre, which may lie outside the image.</param>
        /// <param name="centerY">Row of the centre, which may lie outside the image.</param>
        /// <param name="radius">Radius in pixels; zero or less draws nothing.</param>
        /// <param name="color">The colour to draw; a translucent one blends, an opaque one replaces.</param>
        public void DrawDisc(int centerX, int centerY, int radius, Rgba32 color)
        {
            if (radius <= 0 || color.A == 0)
                return;

            if ((long) centerX + radius < 0 || (long) centerX - radius >= Width ||
                (long) centerY + radius < 0 || (long) centerY - radius >= Height)
                return;

            // Clipped before the loop runs rather than inside it, so a radius far larger than the image costs one
            // span per row of the image and not one per row of the circle.
            var top = (int) Math.Max(0L, (long) centerY - radius);
            var bottom = (int) Math.Min(Height - 1L, (long) centerY + radius);

            // Promoted to double BEFORE the multiply. In int arithmetic this overflows for any radius above 46,340
            // and the radicand comes out negative, so every row's half-width is the square root of a negative number
            // cast to zero - a disc that silently draws nothing at exactly the sizes somebody passed by mistake.
            var radiusSquared = (double) radius * radius;

            for (var y = top; y <= bottom; y++)
            {
                // Also a double: the clipped row can be an arbitrary distance from a centre that lies far off the
                // image, and the difference of two ints at those extremes overflows.
                var distance = (double) y - centerY;
                var halfWidth = (long) Math.Sqrt(radiusSquared - distance*distance);
                BlendSpanHorizontal(y, centerX - halfWidth, centerX + halfWidth, color);
            }
        }

        /// <summary>
        ///     Composites a run of one colour along a row, clipped to the image. The alpha test is hoisted out of the
        ///     loop because it is constant for the whole span, which is what makes an opaque draw write the same bytes
        ///     by the same shape of code as the equivalent <see cref="Fill(int, int, int, int, Rgba32)" />.
        /// </summary>
        /// <param name="y">The row to write.</param>
        /// <param name="left">Leftmost column, which may be negative.</param>
        /// <param name="right">Rightmost column inclusive, which may be past the right edge.</param>
        /// <param name="color">The colour to composite.</param>
        private void BlendSpanHorizontal(int y, long left, long right, Rgba32 color)
        {
            if ((uint) y >= (uint) Height)
                return;

            var from = (int) Math.Max(0L, left);
            var to = (int) Math.Min(Width - 1L, right);
            if (to < from)
                return;

            var index = (y * Width + from) * BytesPerPixel;

            if (color.A == 255)
            {
                for (var x = from; x <= to; x++)
                {
                    Data[index] = color.R;
                    Data[index + 1] = color.G;
                    Data[index + 2] = color.B;
                    Data[index + 3] = 255;
                    index += BytesPerPixel;
                }

                return;
            }

            for (var x = from; x <= to; x++)
            {
                BlendInto(index, color.R, color.G, color.B, color.A);
                index += BytesPerPixel;
            }
        }

        /// <summary>Composites a run of one colour down a column, clipped to the image.</summary>
        /// <param name="x">The column to write.</param>
        /// <param name="top">Topmost row, which may be negative.</param>
        /// <param name="bottom">Bottommost row inclusive, which may be past the bottom edge.</param>
        /// <param name="color">The colour to composite.</param>
        private void BlendSpanVertical(int x, long top, long bottom, Rgba32 color)
        {
            if ((uint) x >= (uint) Width)
                return;

            var from = (int) Math.Max(0L, top);
            var to = (int) Math.Min(Height - 1L, bottom);
            if (to < from)
                return;

            var index = (from * Width + x) * BytesPerPixel;
            var stride = Width * BytesPerPixel;

            if (color.A == 255)
            {
                for (var y = from; y <= to; y++)
                {
                    Data[index] = color.R;
                    Data[index + 1] = color.G;
                    Data[index + 2] = color.B;
                    Data[index + 3] = 255;
                    index += stride;
                }

                return;
            }

            for (var y = from; y <= to; y++)
            {
                BlendInto(index, color.R, color.G, color.B, color.A);
                index += stride;
            }
        }

        /// <summary>
        ///     Composites one straight-alpha "source over" pixel into the buffer at an already-validated byte offset:
        ///     each channel becomes the alpha-weighted mix of source and destination, divided back out by the result
        ///     alpha, which is provably within 0-255 so no clamping is needed.
        ///     <para>
        ///         <b>The one and only source-over blend in this class, and every drawing method routes through it.</b>
        ///         A second copy of this arithmetic written for the shape primitives would disagree with
        ///         <see cref="DrawImage" /> by a rounding step and nothing would ever catch it — which is the same trap
        ///         that had three separate escape-sequence parsers in this repository before
        ///         <see cref="AnsiText" /> was published, arriving this time inside a single file.
        ///     </para>
        ///     <para>
        ///         Does no bounds checking whatsoever: <paramref name="destIndex" /> is a byte offset the caller has
        ///         already clipped, which is what lets the span writers hoist the clip out of their inner loops.
        ///     </para>
        /// </summary>
        /// <param name="destIndex">Byte offset of the destination pixel, known to be in range.</param>
        /// <param name="r">Source red channel.</param>
        /// <param name="g">Source green channel.</param>
        /// <param name="b">Source blue channel.</param>
        /// <param name="sourceAlpha">Source alpha, 0-255.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BlendInto(int destIndex, byte r, byte g, byte b, int sourceAlpha)
        {
            if (sourceAlpha == 0)
                return; // fully transparent source: leave the destination untouched

            if (sourceAlpha == 255)
            {
                // Opaque source fully replaces the destination.
                Data[destIndex] = r;
                Data[destIndex + 1] = g;
                Data[destIndex + 2] = b;
                Data[destIndex + 3] = 255;
                return;
            }

            int da = Data[destIndex + 3];
            var dstContribution = da * (255 - sourceAlpha) / 255; // destination weight after the source covers it
            var outA = sourceAlpha + dstContribution;
            if (outA <= 0)
                return; // both transparent -> nothing to write (destination already transparent)

            Data[destIndex] = (byte) ((r * sourceAlpha + Data[destIndex] * dstContribution + outA / 2) / outA);
            Data[destIndex + 1] = (byte) ((g * sourceAlpha + Data[destIndex + 1] * dstContribution + outA / 2) / outA);
            Data[destIndex + 2] = (byte) ((b * sourceAlpha + Data[destIndex + 2] * dstContribution + outA / 2) / outA);
            Data[destIndex + 3] = (byte) outA;
        }

        /// <summary>
        ///     Validates that the dimensions are positive and that the total RGBA byte count fits in a 32-bit array
        ///     length, then returns that count. Doing the multiply in 64-bit math prevents a very large width/height —
        ///     which can arrive from a crafted image header via a decoder — from silently overflowing to a small,
        ///     plausible-looking length that would later crash or read out of bounds.
        /// </summary>
        private static int ValidatedByteCount(int width, int height, string widthName = "width", string heightName = "height")
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(widthName, width, "Image width must be greater than zero.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(heightName, height, "Image height must be greater than zero.");

            var total = (long) width * height * BytesPerPixel;
            if (total > int.MaxValue)
                throw new ArgumentOutOfRangeException(widthName,
                    $"Image {width}x{height} is too large; its RGBA byte count ({total}) exceeds the maximum array size.");

            return (int) total;
        }

        /// <summary>Rounds and clamps a floating point channel value into the 0-255 byte range.</summary>
        private static byte ClampToByte(double value)
        {
            var rounded = (int) Math.Round(value, MidpointRounding.AwayFromZero);
            if (rounded < 0) return 0;
            if (rounded > 255) return 255;
            return (byte) rounded;
        }
    }
}

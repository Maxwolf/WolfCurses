// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     A straight-alpha (non-premultiplied) 32-bit color value in red, green, blue, alpha channel order. Alpha of
    ///     zero is fully transparent and 255 is fully opaque.
    /// </summary>
    public readonly struct Rgba32
    {
        /// <summary>Red channel, 0-255.</summary>
        public readonly byte R;

        /// <summary>Green channel, 0-255.</summary>
        public readonly byte G;

        /// <summary>Blue channel, 0-255.</summary>
        public readonly byte B;

        /// <summary>Alpha channel, 0 (transparent) to 255 (opaque).</summary>
        public readonly byte A;

        /// <summary>Initializes a new instance of the <see cref="Rgba32" /> struct.</summary>
        public Rgba32(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }
    }

    /// <summary>
    ///     An opaque 24-bit color value in red, green, blue channel order. Used for the colors emitted into the ANSI
    ///     escape sequences where there is no alpha channel to speak of.
    /// </summary>
    public readonly struct Rgb24
    {
        /// <summary>Red channel, 0-255.</summary>
        public readonly byte R;

        /// <summary>Green channel, 0-255.</summary>
        public readonly byte G;

        /// <summary>Blue channel, 0-255.</summary>
        public readonly byte B;

        /// <summary>Initializes a new instance of the <see cref="Rgb24" /> struct.</summary>
        public Rgb24(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }
    }

    /// <summary>
    ///     A decoder-agnostic, in-memory raster image: a flat row-major array of 32-bit RGBA pixels. This is the common
    ///     exchange type between an <see cref="IImageDecoder" /> (which turns a file or stream into pixels) and the
    ///     <see cref="AnsiImageRenderer" /> (which turns pixels into an ANSI string). It intentionally has no external
    ///     dependencies so the rendering half of the feature can be exercised with hand-built synthetic images in tests.
    /// </summary>
    public sealed class PixelBuffer
    {
        /// <summary>
        ///     Number of bytes that make up a single pixel: red, green, blue, alpha.
        /// </summary>
        internal const int BytesPerPixel = 4;

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

            for (var dy = 0; dy < newHeight; dy++)
            {
                var srcTop = dy * scaleY;
                var srcBottom = (dy + 1) * scaleY;
                var y0 = (int) Math.Floor(srcTop);
                var y1 = (int) Math.Ceiling(srcBottom);
                if (y1 > Height) y1 = Height;

                for (var dx = 0; dx < newWidth; dx++)
                {
                    var srcLeft = dx * scaleX;
                    var srcRight = (dx + 1) * scaleX;
                    var x0 = (int) Math.Floor(srcLeft);
                    var x1 = (int) Math.Ceiling(srcRight);
                    if (x1 > Width) x1 = Width;

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

                        var rowOffset = sy * Width * BytesPerPixel;
                        for (var sx = x0; sx < x1; sx++)
                        {
                            var xOverlap = Math.Min(srcRight, sx + 1) - Math.Max(srcLeft, sx);
                            if (xOverlap <= 0) continue;

                            var coverage = xOverlap * yOverlap;
                            var i = rowOffset + sx * BytesPerPixel;
                            double a = Data[i + 3];
                            var colorWeight = coverage * (a / 255.0);

                            sumR += Data[i] * colorWeight;
                            sumG += Data[i + 1] * colorWeight;
                            sumB += Data[i + 2] * colorWeight;
                            sumAlphaWeighted += coverage * a;
                            sumColorWeight += colorWeight;
                            sumCoverage += coverage;
                        }
                    }

                    var di = (dy * newWidth + dx) * BytesPerPixel;
                    if (sumCoverage <= 0)
                        continue; // leave transparent black (already zeroed)

                    var outA = sumAlphaWeighted / sumCoverage;

                    // Un-premultiply to recover a straight-alpha color. When every overlapping pixel was transparent
                    // there is no hue to recover, so the destination stays transparent black.
                    if (sumColorWeight > 0)
                    {
                        dst[di] = ClampToByte(sumR / sumColorWeight);
                        dst[di + 1] = ClampToByte(sumG / sumColorWeight);
                        dst[di + 2] = ClampToByte(sumB / sumColorWeight);
                    }

                    dst[di + 3] = ClampToByte(outA);
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
                    if (sa == 255)
                    {
                        // Opaque overlay pixel fully replaces the destination.
                        Data[di] = overlay.Data[si];
                        Data[di + 1] = overlay.Data[si + 1];
                        Data[di + 2] = overlay.Data[si + 2];
                        Data[di + 3] = 255;
                        continue;
                    }

                    int da = Data[di + 3];
                    var dstContribution = da * (255 - sa) / 255; // destination weight after the overlay covers it
                    var outA = sa + dstContribution;
                    if (outA <= 0)
                        continue; // both transparent -> nothing to write (destination already transparent)

                    // Straight-alpha "over": each channel is the alpha-weighted mix, divided back out by the result
                    // alpha. Values are provably within 0-255, so no clamping is needed.
                    Data[di] = (byte) ((overlay.Data[si] * sa + Data[di] * dstContribution + outA / 2) / outA);
                    Data[di + 1] = (byte) ((overlay.Data[si + 1] * sa + Data[di + 1] * dstContribution + outA / 2) / outA);
                    Data[di + 2] = (byte) ((overlay.Data[si + 2] * sa + Data[di + 2] * dstContribution + outA / 2) / outA);
                    Data[di + 3] = (byte) outA;
                }
            }
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

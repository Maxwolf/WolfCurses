// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.IO;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     The friendly entry point for the ANSI graphics feature. It ties an <see cref="IImageDecoder" /> to the
    ///     <see cref="AnsiImageRenderer" />: load a picture from a file, stream, byte array or existing
    ///     <see cref="PixelBuffer" />, then turn it into a string of escape sequences you can drop straight into a
    ///     WolfCurses window's rendered text. Because the whole library is built around composing a screen as a string,
    ///     an image is "just more string" — assign the result into your window/form's text and the scene graph will
    ///     draw it like anything else.
    /// </summary>
    /// <remarks>
    ///     Rendering is not free: each <see cref="ToAnsi(AnsiImageOptions)" /> / <see cref="RenderFile" /> call decodes
    ///     (when loading from a file or stream) and area-average resamples the image. A window's <c>OnRenderWindow</c>
    ///     is invoked on <em>every</em> tick so the scene graph can diff the screen text, so do not decode or render
    ///     there. The escape string for a given image at a given target size never changes — build it once (in the
    ///     window's constructor, in <c>OnFirstTick</c>, or a cached field) and return the cached string. Re-render only
    ///     when you deliberately want a different size.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     // Once, at start-up, so Windows interprets the escapes and shows the block glyphs:
    ///     AnsiConsole.Enable();
    ///
    ///     // Decode + render ONCE and cache the resulting string (the escapes never change):
    ///     private readonly string _logo = AnsiImage.RenderFile("media/logo.jpg");
    ///
    ///     // ...then in your window/form's OnRenderWindow just return the cached text every tick:
    ///     public override string OnRenderWindow() => _logo;
    ///     </code>
    /// </example>
    public sealed class AnsiImage
    {
        private AnsiImage(PixelBuffer pixels)
        {
            Pixels = pixels;
        }

        /// <summary>The decoded pixels backing this image.</summary>
        public PixelBuffer Pixels { get; }

        /// <summary>Image width in pixels.</summary>
        public int Width => Pixels.Width;

        /// <summary>Image height in pixels.</summary>
        public int Height => Pixels.Height;

        /// <summary>Wraps an already-decoded <see cref="PixelBuffer" /> (no decoding performed).</summary>
        public static AnsiImage FromPixels(PixelBuffer pixels)
        {
            if (pixels == null)
                throw new ArgumentNullException(nameof(pixels));
            return new AnsiImage(pixels);
        }

        /// <summary>Decodes an image from a stream using the given decoder, or <see cref="ImageDecoders.Default" />.</summary>
        public static AnsiImage FromStream(Stream source, IImageDecoder decoder = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            return new AnsiImage((decoder ?? ImageDecoders.Default).Decode(source));
        }

        /// <summary>Decodes an image from an in-memory byte array using the given decoder, or the default.</summary>
        public static AnsiImage FromBytes(byte[] data, IImageDecoder decoder = null)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            using var stream = new MemoryStream(data, false);
            return FromStream(stream, decoder);
        }

        /// <summary>Opens and decodes an image file using the given decoder, or the default.</summary>
        public static AnsiImage FromFile(string path, IImageDecoder decoder = null)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            using var stream = File.OpenRead(path);
            return FromStream(stream, decoder);
        }

        /// <summary>
        ///     Renders this image to a string using the supplied options (or the defaults), drawn by whichever
        ///     <see cref="IImageRenderer" /> is installed as <see cref="ImageRenderers.Default" /> — half-block text
        ///     unless the application changed it at start-up.
        /// </summary>
        public string ToAnsi(AnsiImageOptions options = null)
        {
            return ImageRenderers.Default.Render(Pixels, options);
        }

        /// <summary>
        ///     Renders this image with an explicitly chosen <see cref="IImageRenderer" />, ignoring
        ///     <see cref="ImageRenderers.Default" />. Use this to draw one image differently from the rest — a sixel
        ///     photograph on a screen whose other images are half-block, say — without disturbing the global default.
        /// </summary>
        /// <param name="options">Rendering options, or null to use the defaults.</param>
        /// <param name="renderer">The renderer to draw with.</param>
        public string ToAnsi(AnsiImageOptions options, IImageRenderer renderer)
        {
            if (renderer == null)
                throw new ArgumentNullException(nameof(renderer));
            return renderer.Render(Pixels, options);
        }

        /// <summary>Renders this image bounded to the given column and row budget, using the default renderer.</summary>
        public string ToAnsi(int maxColumns, int maxRows)
        {
            return ToAnsi(new AnsiImageOptions
            {
                MaxColumns = maxColumns,
                MaxRows = maxRows
            });
        }

        /// <summary>
        ///     Returns a new image with <paramref name="foreground" /> alpha-composited on top of this one, centered.
        ///     Use this to place a transparent image — a logo, badge, character sprite — over a background image and
        ///     see both together. This image is the background; the result keeps this image's size, and the overlay's
        ///     transparency is honored, so it can itself sit over yet another image.
        /// </summary>
        public AnsiImage Overlay(AnsiImage foreground)
        {
            if (foreground == null)
                throw new ArgumentNullException(nameof(foreground));
            return Overlay(foreground, (Width - foreground.Width) / 2, (Height - foreground.Height) / 2);
        }

        /// <summary>
        ///     Returns a new image with <paramref name="foreground" /> alpha-composited on top of this one at the given
        ///     pixel offset from this image's top-left (either may be negative). Anything past the edges is clipped.
        /// </summary>
        public AnsiImage Overlay(AnsiImage foreground, int x, int y)
        {
            if (foreground == null)
                throw new ArgumentNullException(nameof(foreground));

            var combined = new PixelBuffer(Width, Height, (byte[]) Pixels.Data.Clone());
            combined.DrawImage(foreground.Pixels, x, y);
            return new AnsiImage(combined);
        }

        /// <summary>
        ///     Returns a new image resampled to the given pixel size. Aspect ratio is not preserved; use this to scale
        ///     an overlay to a background before <see cref="Overlay(AnsiImage)" />, or to pre-size an image.
        /// </summary>
        public AnsiImage Resize(int width, int height)
        {
            return new AnsiImage(Pixels.Resize(width, height));
        }

        /// <summary>Composites <paramref name="foreground" /> centered on top of <paramref name="background" />.</summary>
        public static AnsiImage Composite(AnsiImage background, AnsiImage foreground)
        {
            if (background == null)
                throw new ArgumentNullException(nameof(background));
            return background.Overlay(foreground);
        }

        /// <summary>Convenience: decode a file and render it in one call.</summary>
        public static string RenderFile(string path, AnsiImageOptions options = null)
        {
            return FromFile(path).ToAnsi(options);
        }

        /// <summary>Convenience: decode a stream and render it in one call.</summary>
        public static string RenderStream(Stream source, AnsiImageOptions options = null)
        {
            return FromStream(source).ToAnsi(options);
        }

        /// <summary>Convenience: decode a byte array and render it in one call.</summary>
        public static string RenderBytes(byte[] data, AnsiImageOptions options = null)
        {
            return FromBytes(data).ToAnsi(options);
        }
    }
}

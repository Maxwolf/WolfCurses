// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/16/2026

using System;
using System.Text;

namespace WolfCurses.Graphics
{
    /// <summary>
    ///     An <see cref="IImageRenderer" /> that draws with the kitty graphics protocol: like
    ///     <see cref="SixelImageRenderer" /> it paints real pixels rather than colored character cells, but it hands the
    ///     terminal the pixels as they are — full 24-bit color with a real alpha channel, no palette and no color
    ///     reduction — so it is the better choice wherever it is available.
    ///     <para>
    ///         Support is narrower than sixel's: kitty itself, WezTerm, Ghostty, and Konsole implement it; most others
    ///         do not. Nothing here detects that — a terminal without it prints the escape sequence as garbage — so an
    ///         application should install this deliberately:
    ///         <code>
    ///             ImageRenderers.Default = new KittyImageRenderer();
    ///         </code>
    ///     </para>
    ///     <para>
    ///         Output follows <see cref="AnsiGraphics" />' contract for multi-row pictures, so it drops into a window's
    ///         rendered text like any other string. Render once and cache the string: the payload is the whole image
    ///         base64-encoded, which is large and not worth rebuilding every tick.
    ///     </para>
    /// </summary>
    public sealed class KittyImageRenderer : IImageRenderer
    {
        /// <summary>The ASCII escape control character (0x1B) that begins every ANSI control sequence.</summary>
        private const char Escape = (char) 27;

        /// <summary>
        ///     Payload characters per command. The protocol requires the base64 data be split across several commands
        ///     rather than sent as one enormous one; 4096 is the size its documentation specifies.
        /// </summary>
        private const int MaxPayloadChunk = 4096;

        /// <summary>
        ///     Initializes a new instance of the <see cref="KittyImageRenderer" /> class.
        /// </summary>
        /// <param name="cellPixelWidth">
        ///     How many pixels wide one terminal character cell is, used to work out how many rows the picture covers so
        ///     the surrounding text lands in the right place. The protocol can be told to fit a picture into a given
        ///     number of cells, so this only needs to be approximately right — unlike with sixel, a wrong value does not
        ///     change the picture's size, only the library's idea of how much room it takes.
        /// </param>
        /// <param name="cellPixelHeight">How many pixels tall one terminal character cell is; see above.</param>
        public KittyImageRenderer(int cellPixelWidth = 10, int cellPixelHeight = 20)
        {
            if (cellPixelWidth < 1)
                throw new ArgumentOutOfRangeException(nameof(cellPixelWidth), cellPixelWidth,
                    "A character cell must be at least one pixel wide.");
            if (cellPixelHeight < 1)
                throw new ArgumentOutOfRangeException(nameof(cellPixelHeight), cellPixelHeight,
                    "A character cell must be at least one pixel tall.");

            CellPixelWidth = cellPixelWidth;
            CellPixelHeight = cellPixelHeight;
        }

        /// <summary>Pixels across one terminal character cell.</summary>
        public int CellPixelWidth { get; }

        /// <summary>Pixels down one terminal character cell.</summary>
        public int CellPixelHeight { get; }

        /// <inheritdoc />
        public string Render(PixelBuffer image, AnsiImageOptions options = null)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            options ??= new AnsiImageOptions();

            var pixels = ImageFit.FitToPixels(image, options, CellPixelWidth, CellPixelHeight);
            if (options.BackgroundColor.HasValue)
                pixels = ImageFit.Flatten(pixels, options.BackgroundColor.Value);

            var rows = Math.Max(1, (pixels.Height + CellPixelHeight - 1) / CellPixelHeight);
            var payload = Encode(pixels);
            return AnsiGraphics.PayloadBlock(payload, rows);
        }

        /// <summary>
        ///     Encodes the image as kitty graphics commands: the raw RGBA bytes, base64'd and split into chunks.
        ///     <para>
        ///         The first command carries the picture's format and dimensions and asks for it to be shown; the rest
        ///         carry only continuation flags. Every command but the last sets <c>m=1</c> meaning "more follows", and
        ///         the terminal only draws once it sees <c>m=0</c>.
        ///     </para>
        /// </summary>
        private static string Encode(PixelBuffer image)
        {
            var base64 = Convert.ToBase64String(image.Data);
            var builder = new StringBuilder(base64.Length + 256);

            for (var offset = 0; offset < base64.Length; offset += MaxPayloadChunk)
            {
                var length = Math.Min(MaxPayloadChunk, base64.Length - offset);
                var last = offset + length >= base64.Length;

                builder.Append(Escape).Append("_G");
                if (offset == 0)
                {
                    // f=32 says the payload is 32-bit RGBA, which is exactly PixelBuffer's own layout; a=T means
                    // transmit this data and display it immediately.
                    builder.Append("a=T,f=32,s=").Append(image.Width).Append(",v=").Append(image.Height)
                        .Append(",m=").Append(last ? '0' : '1');
                }
                else
                {
                    builder.Append("m=").Append(last ? '0' : '1');
                }

                builder.Append(';').Append(base64, offset, length);
                builder.Append(Escape).Append('\\');
            }

            return builder.ToString();
        }
    }
}

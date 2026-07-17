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
        ///     Removes whatever picture is already placed where this one is about to go, and frees it.
        ///     <para>
        ///         Unlike sixel, a kitty picture is not painted into the character cells — it is an object the terminal
        ///         keeps in a layer of its own, which survives anything done to the text underneath it. So a picture is
        ///         not replaced by drawing another over it; both are simply there, the newer one merely on top, and a
        ///         slideshow would stack every slide it ever showed (and leak each one's pixels, since nothing frees
        ///         them). Erasing rows cannot help — that is text, and text is not what is on screen.
        ///     </para>
        ///     <para>
        ///         <c>a=d</c> deletes; <c>d=C</c> means "every placement overlapping the cursor", which is precisely the
        ///         picture this one is about to replace and nothing else — an application drawing several pictures in
        ///         one frame puts them at different cursor positions, so they do not delete each other. The capital
        ///         letter is what also frees the pixel data once no placement of it remains.
        ///     </para>
        /// </summary>
        private static readonly string _replacePictureAtCursor = Escape + "_Ga=d,d=C" + Escape + "\\";

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

            // Flattening happens before placement, not after, so the transparent aspect padding a placement may add
            // stays transparent: flattening afterwards would paint the padding as an opaque background-colored bar
            // filling out the rounded-up cell rectangle. Same order as the sixel enlargement path, and a pointwise
            // operation, so it composes the same picture either way.
            if (options.BackgroundColor.HasValue)
                image = ImageFit.Flatten(image, options.BackgroundColor.Value);

            var (pixels, columns, rows) = ResolvePlacement(image, options);
            var payload = _replacePictureAtCursor + Encode(pixels, columns, rows);
            return AnsiGraphics.PayloadBlock(payload, rows);
        }

        /// <summary>
        ///     Decides what pixels to transmit and the cell rectangle (<c>c=</c>/<c>r=</c>) the terminal should scale
        ///     them into.
        ///     <para>
        ///         The kitty protocol scales a transmitted image into a requested columns-by-rows rectangle itself, so
        ///         unlike sixel there is no need to resample the source up to terminal pixels on the CPU — a 360-wide
        ///         sprite canvas can be sent as its own 360-wide pixels instead of the 1980-wide upscale the old path
        ///         built and base64'd every frame. The library only does the parts the terminal cannot: choosing the
        ///         rectangle, Cover's crop, and padding the source to the rectangle's aspect ratio (with both
        ///         <c>c</c> and <c>r</c> given the terminal stretches to fill, so aspect is preserved here, cheaply,
        ///         at source resolution).
        ///     </para>
        ///     <para>
        ///         Transmitting source pixels is only a win while the source is <em>smaller</em> than the fitted pixel
        ///         area; a large photograph shown small would be megabytes of payload for a few cells. So each path
        ///         transmits whichever is fewer pixels: the source as-is, or the source resized down to the fitted
        ///         area — exactly the old behavior, now with the rectangle stated explicitly so the claimed row count
        ///         is honored by the terminal rather than inferred from an assumed cell size.
        ///     </para>
        /// </summary>
        private (PixelBuffer Pixels, int Columns, int Rows) ResolvePlacement(PixelBuffer image, AnsiImageOptions options)
        {
            var (maxColumns, maxRows) = AnsiImageRenderer.ResolveBounds(options);
            var areaWidth = Math.Max(1, maxColumns * CellPixelWidth);
            var areaHeight = Math.Max(1, maxRows * CellPixelHeight);

            switch (options.Fit)
            {
                case AnsiImageFitEnum.Stretch:
                    // Stretch means distortion is asked for: the terminal stretching whatever arrives into the full
                    // rectangle is the correct picture, so the source goes as-is (downsized first only when it holds
                    // more pixels than the rectangle, where transmitting it raw would cost more than the resize).
                    return (SmallerOf(image, areaWidth, areaHeight), maxColumns, maxRows);

                case AnsiImageFitEnum.Cover:
                {
                    // Cover's crop must stay on the CPU (it chooses which pixels survive) but its scale-to-fill need
                    // not: the cropped rectangle already has the area's proportions, so the terminal's stretch into
                    // the full rectangle reproduces it without distortion.
                    var cropped = ImageFit.CoverCrop(image, areaWidth, areaHeight, options);
                    return (SmallerOf(cropped, areaWidth, areaHeight), maxColumns, maxRows);
                }

                default:
                {
                    // Contain / ScaleDown: the same scale arithmetic as ImageFit.FitToPixels, but the fitted size is
                    // only used to pick the cell rectangle; the terminal does the actual scaling.
                    var scale = Math.Min(areaWidth / (double) image.Width, areaHeight / (double) image.Height);
                    if (options.Fit == AnsiImageFitEnum.ScaleDown)
                        scale = Math.Min(scale, 1.0);
                    if (!double.IsFinite(scale) || scale <= 0)
                        scale = 1.0;

                    var width = Math.Max(1, (int) Math.Round(image.Width * scale, MidpointRounding.AwayFromZero));
                    var height = Math.Max(1, (int) Math.Round(image.Height * scale, MidpointRounding.AwayFromZero));

                    var columns = Math.Max(1, (width + CellPixelWidth - 1) / CellPixelWidth);
                    var rows = Math.Max(1, (height + CellPixelHeight - 1) / CellPixelHeight);
                    var rectWidth = columns * CellPixelWidth;
                    var rectHeight = rows * CellPixelHeight;

                    // The rectangle is a whole number of cells, so it is up to one cell bigger than the fitted size in
                    // each direction; the terminal stretches into all of it. Padding the transmitted pixels (bottom and
                    // right, transparent) to the rectangle's aspect keeps the picture undistorted and top-left aligned,
                    // exactly where the old pixel-exact buffer put it.
                    var padWidth = Math.Max(image.Width,
                        (int) Math.Round(image.Width * (rectWidth / (double) width), MidpointRounding.AwayFromZero));
                    var padHeight = Math.Max(image.Height,
                        (int) Math.Round(image.Height * (rectHeight / (double) height), MidpointRounding.AwayFromZero));

                    if ((long) padWidth * padHeight <= (long) rectWidth * rectHeight)
                        return (Pad(image, padWidth, padHeight), columns, rows);

                    // Source is larger than the fitted area: resize down first (fewer pixels to transmit), then pad
                    // the fitted buffer out to the exact rectangle.
                    return (Pad(image.Resize(width, height), rectWidth, rectHeight), columns, rows);
                }
            }
        }

        /// <summary>
        ///     The image itself when it already holds no more pixels than the target area, otherwise the image resized
        ///     to that area — whichever is fewer pixels to transmit.
        /// </summary>
        private static PixelBuffer SmallerOf(PixelBuffer image, int areaWidth, int areaHeight)
        {
            return (long) image.Width * image.Height <= (long) areaWidth * areaHeight
                ? image
                : image.Resize(areaWidth, areaHeight);
        }

        /// <summary>
        ///     Extends the image with transparent pixels on the right and bottom to the given size (a straight row
        ///     copy — no resampling), or returns it unchanged when it already fills the size.
        /// </summary>
        private static PixelBuffer Pad(PixelBuffer image, int width, int height)
        {
            if (width <= image.Width && height <= image.Height)
                return image;

            width = Math.Max(width, image.Width);
            height = Math.Max(height, image.Height);

            var padded = new PixelBuffer(width, height);
            var sourceRowBytes = image.Width * PixelBuffer.BytesPerPixel;
            var paddedRowBytes = width * PixelBuffer.BytesPerPixel;
            for (var y = 0; y < image.Height; y++)
                Array.Copy(image.Data, y * sourceRowBytes, padded.Data, y * paddedRowBytes, sourceRowBytes);

            return padded;
        }

        /// <summary>
        ///     Encodes the image as kitty graphics commands: the raw RGBA bytes, base64'd and split into chunks.
        ///     <para>
        ///         The first command carries the picture's format and dimensions, the cell rectangle to scale it into
        ///         (<c>c=</c> columns, <c>r=</c> rows — which also makes the placement's on-screen size exact on any
        ///         terminal, whatever its real cell size), and asks for it to be shown; the rest carry only
        ///         continuation flags. Every command but the last sets <c>m=1</c> meaning "more follows", and the
        ///         terminal only draws once it sees <c>m=0</c>.
        ///     </para>
        /// </summary>
        private static string Encode(PixelBuffer image, int columns, int rows)
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
                    // transmit this data and display it immediately; c/r ask the terminal to scale it into that many
                    // cells, which is what lets the data be source-resolution instead of a CPU-side upscale.
                    builder.Append("a=T,f=32,s=").Append(image.Width).Append(",v=").Append(image.Height)
                        .Append(",c=").Append(columns).Append(",r=").Append(rows)
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

using System;
using System.Linq;
using System.Text.RegularExpressions;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Pins the kitty graphics protocol output of <see cref="KittyImageRenderer" />: the APC envelope, the control
    ///     keys that describe the picture, the 4096-character payload chunking with its continuation flags, and that the
    ///     transmitted bytes really are the image's own pixels.
    /// </summary>
    public class KittyImageRendererTests
    {
        private const char ESC = (char) 27;

        private static AnsiImageOptions Opts(int cols, int rows) => new()
        {
            MaxColumns = cols,
            MaxRows = rows,
            Fit = AnsiImageFitEnum.Stretch
        };

        private static PixelBuffer Solid(int width, int height, Rgba32 color)
        {
            var buffer = new PixelBuffer(width, height);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                buffer.SetPixel(x, y, color);
            return buffer;
        }

        /// <summary>The payload row of a rendered picture, with its graphics marker stripped.</summary>
        private static string Payload(string rendered)
        {
            return rendered.Split('\n')[0].TrimEnd('\r').Substring(1);
        }

        /// <summary>
        ///     Every APC command in a payload, as (parameters, base64 data) pairs. The <c>;payload</c> half is optional
        ///     in the protocol — a delete command carries no data — so a command without one reports empty data rather
        ///     than swallowing the next command's parameters looking for a semicolon.
        /// </summary>
        private static (string Parameters, string Data)[] Commands(string payload)
        {
            return Regex.Matches(payload, $"{ESC}_G([^;{ESC}]*)(?:;([^{ESC}]*))?{ESC}\\\\")
                .Select(m => (m.Groups[1].Value, m.Groups[2].Value))
                .ToArray();
        }

        /// <summary>
        ///     The commands that transmit the picture, i.e. everything after the leading "delete whatever is already
        ///     here" command that every render begins with.
        /// </summary>
        private static (string Parameters, string Data)[] ImageCommands(string payload)
        {
            return Commands(payload)
                .Where(c => !c.Parameters.StartsWith("a=d", StringComparison.Ordinal))
                .ToArray();
        }

        [Fact]
        public void Render_SmallImage_IsOneSelfContainedCommand()
        {
            var image = Solid(2, 2, new Rgba32(255, 0, 0, 255));
            var commands = ImageCommands(Payload(new KittyImageRenderer(1, 1).Render(image, Opts(2, 2))));

            var single = Assert.Single(commands);

            // a=T transmit-and-display, f=32 for 32-bit RGBA (PixelBuffer's own layout), s/v the pixel dimensions,
            // c/r the cell rectangle the terminal scales the picture into (what lets the data be source resolution),
            // and m=0 meaning nothing more follows so the terminal can draw immediately.
            Assert.Equal("a=T,f=32,s=2,v=2,c=2,r=2,m=0", single.Parameters);
        }

        [Fact]
        public void Render_BeginsByDeletingWhateverPictureIsAlreadyAtTheCursor()
        {
            // A kitty picture is an object in a layer of its own, not paint in the character cells, so drawing a second
            // one at the same place does not replace the first — both are there, and a slideshow stacks every slide it
            // has shown while leaking each one's pixels. Nothing else can clean this up: erasing rows only clears text,
            // which is not what the terminal is showing. So each render disowns its predecessor first.
            var image = Solid(2, 2, new Rgba32(255, 0, 0, 255));
            var payload = Payload(new KittyImageRenderer(1, 1).Render(image, Opts(2, 2)));

            // d=C: every placement overlapping the cursor — which is this picture's own spot and nothing else, so
            // sibling pictures elsewhere in the frame survive. Capital C also frees the pixel data.
            Assert.StartsWith($"{ESC}_Ga=d,d=C{ESC}\\", payload, StringComparison.Ordinal);

            // ...and it must come before the transmission, or it would delete the picture just drawn.
            var deleteAt = payload.IndexOf("a=d", StringComparison.Ordinal);
            var transmitAt = payload.IndexOf("a=T", StringComparison.Ordinal);
            Assert.True(deleteAt < transmitAt, "The delete has to precede the transmission.");
        }

        [Fact]
        public void Render_TransmittedPayload_IsTheImagesOwnRgbaBytes()
        {
            var image = Solid(2, 2, new Rgba32(10, 20, 30, 255));
            var commands = ImageCommands(Payload(new KittyImageRenderer(1, 1).Render(image, Opts(2, 2))));

            var decoded = Convert.FromBase64String(commands.Single().Data);
            Assert.Equal(image.Data, decoded);
        }

        [Fact]
        public void Render_PayloadOverChunkLimit_IsSplitWithContinuationFlags()
        {
            // 32x32 RGBA is 4096 bytes, which base64s to 5464 characters: more than one 4096-character chunk.
            var image = Solid(32, 32, new Rgba32(1, 2, 3, 255));
            var commands = ImageCommands(Payload(new KittyImageRenderer(1, 1).Render(image, Opts(32, 32))));

            Assert.Equal(2, commands.Length);

            // Only the first command describes the picture; the rest carry nothing but the continuation flag.
            Assert.Equal("a=T,f=32,s=32,v=32,c=32,r=32,m=1", commands[0].Parameters);
            Assert.Equal("m=0", commands[1].Parameters);

            Assert.Equal(4096, commands[0].Data.Length);
            Assert.True(commands[1].Data.Length <= 4096);
        }

        [Fact]
        public void Render_ChunkedPayload_ReassemblesToTheOriginalPixels()
        {
            var image = Solid(32, 32, new Rgba32(9, 8, 7, 255));
            var commands = ImageCommands(Payload(new KittyImageRenderer(1, 1).Render(image, Opts(32, 32))));

            var reassembled = Convert.FromBase64String(string.Concat(commands.Select(c => c.Data)));
            Assert.Equal(image.Data, reassembled);
        }

        [Fact]
        public void Render_KeepsAlphaRatherThanFlatteningIt()
        {
            // Unlike sixel, kitty carries a real alpha channel, so a transparent pixel stays transparent in the
            // transmitted bytes instead of being dropped or composited.
            var image = new PixelBuffer(1, 1);
            image.SetPixel(0, 0, new Rgba32(255, 0, 0, 0));
            var commands = ImageCommands(Payload(new KittyImageRenderer(1, 1).Render(image, Opts(1, 1))));

            var decoded = Convert.FromBase64String(commands.Single().Data);
            Assert.Equal(0, decoded[3]);
        }

        [Fact]
        public void Render_BackgroundColor_CompositesAwayTransparency()
        {
            var image = new PixelBuffer(1, 1);
            image.SetPixel(0, 0, new Rgba32(255, 0, 0, 0));
            var rendered = new KittyImageRenderer(1, 1).Render(image, new AnsiImageOptions
            {
                MaxColumns = 1,
                MaxRows = 1,
                Fit = AnsiImageFitEnum.Stretch,
                BackgroundColor = new Rgb24(0, 0, 255)
            });

            var decoded = Convert.FromBase64String(ImageCommands(Payload(rendered)).Single().Data);
            Assert.Equal(new byte[] {0, 0, 255, 255}, decoded);
        }

        [Fact]
        public void Render_PictureTallerThanOneRow_AccountsForCoveredRowsWithPlaceholders()
        {
            var image = Solid(10, 30, new Rgba32(255, 0, 0, 255));
            var rendered = new KittyImageRenderer(10, 10).Render(image, new AnsiImageOptions
            {
                MaxColumns = 1,
                MaxRows = 3,
                Fit = AnsiImageFitEnum.Stretch
            });

            var lines = rendered.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            Assert.Equal(3, lines.Length);
            Assert.True(lines[0].Length > 1, "The first line must carry the payload.");
            Assert.Equal(AnsiGraphics.RowPlaceholder, lines[1]);
            Assert.Equal(AnsiGraphics.RowPlaceholder, lines[2]);
        }

        [Fact]
        public void Render_SmallSource_TransmitsSourcePixelsAndLetsTheTerminalEnlarge()
        {
            // A 12x12 picture fitting (Contain) into a 24x24-pixel area: the old renderer would upscale to 24x24 on
            // the CPU and transmit four times the data; now the source's own bytes go out untouched and c/r ask the
            // terminal to do the enlarging. This is the whole kitty speedup, so the transmitted data being *exactly*
            // the source buffer is the thing to pin.
            var image = new PixelBuffer(12, 12);
            for (var y = 0; y < 12; y++)
            for (var x = 0; x < 12; x++)
                image.SetPixel(x, y, new Rgba32((byte) (x * 20), (byte) (y * 20), 200, 255));

            var rendered = new KittyImageRenderer(4, 12).Render(image, new AnsiImageOptions
            {
                MaxColumns = 6,
                MaxRows = 2
            });

            var single = Assert.Single(ImageCommands(Payload(rendered)));
            Assert.Equal("a=T,f=32,s=12,v=12,c=6,r=2,m=0", single.Parameters);
            Assert.Equal(image.Data, Convert.FromBase64String(single.Data));
        }

        [Fact]
        public void Render_CellRectangleWiderThanTheFit_PadsTheSourceInsteadOfDistorting()
        {
            // A 10x10 picture fits (Contain) at 30x30 pixels, but 30 pixels is one and a half 20-pixel rows, so the
            // claimed rectangle rounds up to 3x2 cells = 30x40 pixels — and with both c and r given the terminal
            // stretches to fill the whole rectangle. Aspect survives because the source is padded (transparent, bottom
            // edge) to the rectangle's own proportions first: 10x13 transmitted, of which the last three rows are
            // nothing. The picture lands top-left in its rectangle, exactly where the old pixel-exact buffer put it.
            var image = Solid(10, 10, new Rgba32(0, 255, 0, 255));
            var rendered = new KittyImageRenderer(10, 20).Render(image, new AnsiImageOptions
            {
                MaxColumns = 3,
                MaxRows = 3
            });

            var single = Assert.Single(ImageCommands(Payload(rendered)));
            Assert.StartsWith("a=T,f=32,s=10,v=13,c=3,r=2,", single.Parameters, StringComparison.Ordinal);

            // The claimed row count and the r= key must agree — that is the marker contract holding by construction.
            Assert.Equal(2, rendered.Split(Environment.NewLine).Length);

            var data = Convert.FromBase64String(single.Data);
            Assert.Equal(10 * 13 * 4, data.Length);

            // Ten rows of the picture, then three rows of transparent padding: every padding byte is zero.
            for (var i = 0; i < 10 * 10 * 4; i++)
                Assert.Equal(image.Data[i], data[i]);
            for (var i = 10 * 10 * 4; i < data.Length; i++)
                Assert.Equal(0, data[i]);
        }

        [Fact]
        public void Render_BackgroundColor_DoesNotPaintTheAspectPadding()
        {
            // Flattening runs before placement so the picture's own transparency composites onto the background, but
            // the aspect padding added afterwards stays transparent — flattening in the other order would fill the
            // rounded-up cell rectangle with an opaque background-colored bar the old renderer never drew.
            var image = Solid(10, 10, new Rgba32(0, 0, 255, 0)); // fully transparent picture
            var rendered = new KittyImageRenderer(10, 20).Render(image, new AnsiImageOptions
            {
                MaxColumns = 3,
                MaxRows = 3,
                BackgroundColor = new Rgb24(255, 0, 0)
            });

            var single = Assert.Single(ImageCommands(Payload(rendered)));
            var data = Convert.FromBase64String(single.Data);
            Assert.Equal(10 * 13 * 4, data.Length);

            // The picture's rows flattened to opaque background; the three padding rows stayed transparent.
            for (var i = 0; i < 10 * 10 * 4; i += 4)
                Assert.Equal(new byte[] {255, 0, 0, 255}, data[i..(i + 4)]);
            for (var i = 10 * 10 * 4; i < data.Length; i++)
                Assert.Equal(0, data[i]);
        }

        [Fact]
        public void Render_LargeSourceShownSmall_IsResizedDownBeforeTransmission()
        {
            // The opposite gate: a 200x200 photograph into a 20x20-pixel area would be 160 KB of payload sent raw for
            // a couple of cells. Transmitting whichever is fewer pixels means this path still resizes on the CPU
            // first, exactly as before.
            var image = Solid(200, 200, new Rgba32(255, 0, 0, 255));
            var rendered = new KittyImageRenderer(10, 20).Render(image, new AnsiImageOptions
            {
                MaxColumns = 2,
                MaxRows = 1
            });

            var commands = ImageCommands(Payload(rendered));
            Assert.StartsWith("a=T,f=32,s=20,v=20,c=2,r=1,", commands[0].Parameters, StringComparison.Ordinal);

            var data = Convert.FromBase64String(string.Concat(commands.Select(c => c.Data)));
            Assert.Equal(20 * 20 * 4, data.Length);
            Assert.Equal(new byte[] {255, 0, 0, 255}, data.Take(4).ToArray());
        }

        [Fact]
        public void Render_NullImage_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new KittyImageRenderer().Render(null));
        }

        [Theory]
        [InlineData(0, 20)]
        [InlineData(10, 0)]
        public void Constructor_RejectsUnusableCellSize(int cellWidth, int cellHeight)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new KittyImageRenderer(cellWidth, cellHeight));
        }
    }
}

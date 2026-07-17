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

        /// <summary>Every APC command in a payload, as (parameters, base64 data) pairs.</summary>
        private static (string Parameters, string Data)[] Commands(string payload)
        {
            return Regex.Matches(payload, $"{ESC}_G([^;]*);([^{ESC}]*){ESC}\\\\")
                .Select(m => (m.Groups[1].Value, m.Groups[2].Value))
                .ToArray();
        }

        [Fact]
        public void Render_SmallImage_IsOneSelfContainedCommand()
        {
            var image = Solid(2, 2, new Rgba32(255, 0, 0, 255));
            var commands = Commands(Payload(new KittyImageRenderer(1, 1).Render(image, Opts(2, 2))));

            var single = Assert.Single(commands);

            // a=T transmit-and-display, f=32 for 32-bit RGBA (PixelBuffer's own layout), s/v the pixel dimensions,
            // and m=0 meaning nothing more follows so the terminal can draw immediately.
            Assert.Equal("a=T,f=32,s=2,v=2,m=0", single.Parameters);
        }

        [Fact]
        public void Render_TransmittedPayload_IsTheImagesOwnRgbaBytes()
        {
            var image = Solid(2, 2, new Rgba32(10, 20, 30, 255));
            var commands = Commands(Payload(new KittyImageRenderer(1, 1).Render(image, Opts(2, 2))));

            var decoded = Convert.FromBase64String(commands.Single().Data);
            Assert.Equal(image.Data, decoded);
        }

        [Fact]
        public void Render_PayloadOverChunkLimit_IsSplitWithContinuationFlags()
        {
            // 32x32 RGBA is 4096 bytes, which base64s to 5464 characters: more than one 4096-character chunk.
            var image = Solid(32, 32, new Rgba32(1, 2, 3, 255));
            var commands = Commands(Payload(new KittyImageRenderer(1, 1).Render(image, Opts(32, 32))));

            Assert.Equal(2, commands.Length);

            // Only the first command describes the picture; the rest carry nothing but the continuation flag.
            Assert.Equal("a=T,f=32,s=32,v=32,m=1", commands[0].Parameters);
            Assert.Equal("m=0", commands[1].Parameters);

            Assert.Equal(4096, commands[0].Data.Length);
            Assert.True(commands[1].Data.Length <= 4096);
        }

        [Fact]
        public void Render_ChunkedPayload_ReassemblesToTheOriginalPixels()
        {
            var image = Solid(32, 32, new Rgba32(9, 8, 7, 255));
            var commands = Commands(Payload(new KittyImageRenderer(1, 1).Render(image, Opts(32, 32))));

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
            var commands = Commands(Payload(new KittyImageRenderer(1, 1).Render(image, Opts(1, 1))));

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

            var decoded = Convert.FromBase64String(Commands(Payload(rendered)).Single().Data);
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

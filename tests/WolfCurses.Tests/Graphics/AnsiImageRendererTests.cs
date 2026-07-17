using System;
using System.Linq;
using WolfCurses.Graphics;
using WolfCurses.Tests.Support;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Pins the exact escape-sequence output of the half-block renderer against hand-built synthetic images: color
    ///     encoding, the four transparency arrangements of the two stacked pixels, background compositing, the color
    ///     downgrades, run-length suppression of repeated colors, and the aspect-ratio fit math. No real image files
    ///     are needed here, which keeps these fast and deterministic.
    /// </summary>
    public class AnsiImageRendererTests
    {
        private const char ESC = (char) 27;
        private const char Upper = '▀';
        private const char Lower = '▄';

        /// <summary>Mirror of the renderer's own brightness ramp, ordered darkest to lightest.</summary>
        private const string AsciiRamp = " .:-=+*#%@";

        private static string Fg(int r, int g, int b) => $"{ESC}[38;2;{r};{g};{b}m";
        private static string Bg(int r, int g, int b) => $"{ESC}[48;2;{r};{g};{b}m";
        private static string Reset => $"{ESC}[0m";

        /// <summary>Builds a 1-column, 2-row image (one character cell) from a top and bottom pixel.</summary>
        private static PixelBuffer Cell(Rgba32 top, Rgba32 bottom)
        {
            var buffer = new PixelBuffer(1, 2);
            buffer.SetPixel(0, 0, top);
            buffer.SetPixel(0, 1, bottom);
            return buffer;
        }

        private static AnsiImageOptions Opts(int cols, int rows, AnsiColorModeEnum mode = AnsiColorModeEnum.TrueColor,
            Rgb24? background = null) => new()
        {
            MaxColumns = cols,
            MaxRows = rows,
            ColorMode = mode,
            CellAspectRatio = 2.0,
            BackgroundColor = background
        };

        [Fact]
        public void Render_BothPixelsOpaque_UpperBlockWithFgAndBg()
        {
            var image = Cell(new Rgba32(255, 0, 0, 255), new Rgba32(0, 0, 255, 255));
            var result = AnsiImageRenderer.Render(image, Opts(1, 1));
            Assert.Equal(Fg(255, 0, 0) + Bg(0, 0, 255) + Upper + Reset, result);
        }

        [Fact]
        public void Render_TransparentBottom_UpperBlockOverDefaultBackground()
        {
            // Top opaque, bottom transparent: upper half block, foreground only, background left at terminal default
            // (which is already in effect at line start, so no background escape is emitted at all).
            var image = Cell(new Rgba32(255, 0, 0, 255), new Rgba32(0, 0, 0, 0));
            var result = AnsiImageRenderer.Render(image, Opts(1, 1));
            Assert.Equal(Fg(255, 0, 0) + Upper + Reset, result);
        }

        [Fact]
        public void Render_TransparentTop_LowerBlockOverDefaultBackground()
        {
            var image = Cell(new Rgba32(0, 0, 0, 0), new Rgba32(0, 255, 0, 255));
            var result = AnsiImageRenderer.Render(image, Opts(1, 1));
            Assert.Equal(Fg(0, 255, 0) + Lower + Reset, result);
        }

        [Fact]
        public void Render_BothTransparent_SpaceOverDefaultBackground()
        {
            var image = Cell(new Rgba32(0, 0, 0, 0), new Rgba32(0, 0, 0, 0));
            var result = AnsiImageRenderer.Render(image, Opts(1, 1));
            Assert.Equal(" " + Reset, result);
        }

        [Fact]
        public void Render_TransparentWithBackgroundColor_CompositesToBackground()
        {
            // With a background configured, a fully transparent pixel resolves to the background color and is drawn.
            var image = Cell(new Rgba32(0, 0, 0, 0), new Rgba32(0, 0, 0, 0));
            var result = AnsiImageRenderer.Render(image, Opts(1, 1, background: new Rgb24(10, 20, 30)));
            Assert.Equal(Fg(10, 20, 30) + Bg(10, 20, 30) + Upper + Reset, result);
        }

        [Fact]
        public void Render_SemiTransparentOverBackground_AlphaComposites()
        {
            // 50% opaque (128) orange over black composites to a darker orange via source-over.
            var image = Cell(new Rgba32(200, 100, 50, 128), new Rgba32(200, 100, 50, 128));
            var result = AnsiImageRenderer.Render(image, Opts(1, 1, background: new Rgb24(0, 0, 0)));
            Assert.Equal(Fg(100, 50, 25) + Bg(100, 50, 25) + Upper + Reset, result);
        }

        [Fact]
        public void Render_Palette256Mode_UsesIndexedEscapes()
        {
            var image = Cell(new Rgba32(255, 0, 0, 255), new Rgba32(0, 0, 0, 255));
            var result = AnsiImageRenderer.Render(image, Opts(1, 1, AnsiColorModeEnum.Palette256));
            Assert.Equal($"{ESC}[38;5;196m{ESC}[48;5;16m{Upper}{Reset}", result);
        }

        [Fact]
        public void Render_NoneMode_ShadesWithAsciiRamp()
        {
            // White over black averages to mid brightness (127) -> ramp index 4 -> '='. No color escapes at all.
            var image = Cell(new Rgba32(255, 255, 255, 255), new Rgba32(0, 0, 0, 255));
            var result = AnsiImageRenderer.Render(image, Opts(1, 1, AnsiColorModeEnum.None));
            Assert.Equal("=", result);
        }

        [Fact]
        public void Render_NoneMode_TransparentCellIsSpace()
        {
            var image = Cell(new Rgba32(0, 0, 0, 0), new Rgba32(0, 0, 0, 0));
            var result = AnsiImageRenderer.Render(image, Opts(1, 1, AnsiColorModeEnum.None));
            Assert.Equal(" ", result);
        }

        [Fact]
        public void Render_GrayscaleMode_UsesGrayRampEscapes()
        {
            // Deliberately the same red-over-black cell as the Palette256 test above, so the two downgrades can be
            // compared directly: red's luma (76) lands on the 24-step gray ramp at 239, where the color cube kept it
            // red at 196. Black resolves to 16 in both, coming from the cube rather than the ramp's washed-out
            // darkest step.
            var image = Cell(new Rgba32(255, 0, 0, 255), new Rgba32(0, 0, 0, 255));
            var result = AnsiImageRenderer.Render(image, Opts(1, 1, AnsiColorModeEnum.Grayscale));
            Assert.Equal($"{ESC}[38;5;239m{ESC}[48;5;16m{Upper}{Reset}", result);
        }

        [Fact]
        public void Render_NoneMode_MultiRowImage_IsPlainTextWithNoEscapes()
        {
            // The bottom rung of the fallback ladder promises "regular console text": a colorless terminal gets shaded
            // characters and nothing it cannot print. Worth pinning on a multi-row image because the single-cell cases
            // above cannot see line structure, and a stray escape or trailing reset only shows up once there is more
            // than one cell to separate.
            var image = new PixelBuffer(3, 6);
            for (var y = 0; y < 6; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    var level = (byte) (y * 51);
                    image.SetPixel(x, y, new Rgba32(level, level, level, 255));
                }
            }

            var result = AnsiImageRenderer.Render(image, Opts(3, 3, AnsiColorModeEnum.None));
            var lines = result.Split(Environment.NewLine);

            // Ordinal on purpose: the default culture-sensitive search treats ESC as a zero-weight character and
            // "finds" it at position 0 of any string at all, so this assertion would pass no matter what.
            Assert.DoesNotContain(ESC.ToString(), result, StringComparison.Ordinal);
            Assert.Equal(3, lines.Length);
            Assert.All(lines, line => Assert.Equal(3, line.Length));

            // Each row averages its two stacked pixels, so the ramp climbs as the gradient does.
            Assert.All(lines, line => Assert.Single(line.Distinct()));
            Assert.True(AsciiRamp.IndexOf(lines[0][0]) < AsciiRamp.IndexOf(lines[2][0]),
                "a brightening gradient should climb the ramp from dark to light");
        }

        [Fact]
        public void Render_RepeatedColorsAcrossRow_EmittedOnceThenReused()
        {
            // A 2x2 cell block of a single solid color: the second column must not re-emit the color escapes, and each
            // of the two rows is independent and ends with a reset.
            var image = new PixelBuffer(2, 4);
            for (var y = 0; y < 4; y++)
            for (var x = 0; x < 2; x++)
                image.SetPixel(x, y, new Rgba32(255, 0, 0, 255));

            var result = AnsiImageRenderer.Render(image, Opts(2, 2));

            var line = Fg(255, 0, 0) + Bg(255, 0, 0) + Upper + Upper + Reset;
            Assert.Equal(line + Text.NL + line, result);
        }

        [Theory]
        [InlineData(100, 100, 80, 80, 80, 40)] // square -> full width, half the rows (2 px per row)
        [InlineData(200, 100, 80, 80, 80, 20)] // wide -> width-bound
        [InlineData(100, 200, 80, 80, 80, 80)] // tall but fits -> full height
        [InlineData(100, 400, 80, 80, 40, 80)] // very tall -> height-bound, narrower
        public void ComputeTargetCells_PreservesAspectWithinBounds(
            int imgW, int imgH, int maxCols, int maxRows, int expectedCols, int expectedRows)
        {
            var options = new AnsiImageOptions
            {
                MaxColumns = maxCols,
                MaxRows = maxRows,
                CellAspectRatio = 2.0
            };

            var (cols, rows) = AnsiImageRenderer.ComputeTargetCells(imgW, imgH, options);

            Assert.Equal(expectedCols, cols);
            Assert.Equal(expectedRows, rows);
        }

        [Fact]
        public void ComputeTargetCells_AutoBounds_NeverDegenerate()
        {
            // Null max bounds fall back to console (or its 80x24 stand-in) and must still yield a usable size.
            var (cols, rows) = AnsiImageRenderer.ComputeTargetCells(640, 480, new AnsiImageOptions());
            Assert.True(cols >= 1);
            Assert.True(rows >= 1);
        }

        [Fact]
        public void ComputeTargetCells_NonFiniteCellAspect_FallsBackWithoutCollapsing()
        {
            // A non-finite CellAspectRatio must fall back to the default aspect, not collapse the picture to one row.
            var options = new AnsiImageOptions
            {
                MaxColumns = 80,
                MaxRows = 40,
                CellAspectRatio = double.PositiveInfinity
            };

            var (cols, rows) = AnsiImageRenderer.ComputeTargetCells(100, 400, options);

            Assert.InRange(cols, 1, 80);
            Assert.InRange(rows, 1, 40);
            Assert.True(rows > 1, "Non-finite cell aspect should fall back to a sane aspect, not one row.");
        }

        [Theory]
        [InlineData(1e-9)] // extremely tall effective aspect -> height-bound, columns clamp to 1
        [InlineData(1e9)] // extremely wide effective aspect -> width-bound, rows clamp to 1
        public void ComputeTargetCells_ExtremeFiniteCellAspect_StaysInBoundsWithoutOverflow(double cellAspect)
        {
            var options = new AnsiImageOptions
            {
                MaxColumns = 80,
                MaxRows = 40,
                CellAspectRatio = cellAspect
            };

            var (cols, rows) = AnsiImageRenderer.ComputeTargetCells(100, 400, options);

            Assert.InRange(cols, 1, 80);
            Assert.InRange(rows, 1, 40);
        }

        private static PixelBuffer Solid(int width, int height, Rgba32 color)
        {
            var buffer = new PixelBuffer(width, height);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                buffer.SetPixel(x, y, color);
            return buffer;
        }

        private static int Count(string text, char c)
        {
            var n = 0;
            foreach (var ch in text)
                if (ch == c)
                    n++;
            return n;
        }

        [Fact]
        public void Render_Stretch_FillsAreaExactlyIgnoringAspect()
        {
            // An 8x8 square stretched into a 3-column, 2-row area fills it exactly (grid 3x4), distorting freely.
            var image = Solid(8, 8, new Rgba32(255, 0, 0, 255));
            var options = new AnsiImageOptions
            {
                MaxColumns = 3,
                MaxRows = 2,
                ColorMode = AnsiColorModeEnum.TrueColor,
                CellAspectRatio = 2.0,
                Fit = AnsiImageFitEnum.Stretch
            };

            var result = AnsiImageRenderer.Render(image, options);

            var line = Fg(255, 0, 0) + Bg(255, 0, 0) + new string(Upper, 3) + Reset;
            Assert.Equal(line + Text.NL + line, result);
        }

        [Fact]
        public void Render_Cover_FillsWholeAreaWithNoLetterbox()
        {
            // A wide 2x1 source under Cover completely fills a 4-col, 3-row area: 3 rows of 4 opaque cells, no spaces.
            var image = Solid(2, 1, new Rgba32(0, 128, 0, 255));
            var options = new AnsiImageOptions
            {
                MaxColumns = 4,
                MaxRows = 3,
                ColorMode = AnsiColorModeEnum.TrueColor,
                CellAspectRatio = 2.0,
                Fit = AnsiImageFitEnum.Cover
            };

            var result = AnsiImageRenderer.Render(image, options);

            var lines = result.Split('\n');
            Assert.Equal(3, lines.Length);
            Assert.DoesNotContain(' ', result);
            foreach (var line in lines)
                Assert.Equal(4, Count(line, Upper));
        }

        [Fact]
        public void Render_Cover_AlignmentSelectsCropRegion()
        {
            // Source: left half red, right half blue. Cover into a tall 2-col area crops the width to one column;
            // Left keeps the red side, Right keeps the blue side.
            var image = new PixelBuffer(4, 4);
            for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                image.SetPixel(x, y, x < 2 ? new Rgba32(255, 0, 0, 255) : new Rgba32(0, 0, 255, 255));

            AnsiImageOptions Opt(AnsiHorizontalAlignmentEnum align) => new()
            {
                MaxColumns = 2,
                MaxRows = 4,
                ColorMode = AnsiColorModeEnum.TrueColor,
                CellAspectRatio = 2.0,
                Fit = AnsiImageFitEnum.Cover,
                HorizontalAlignment = align
            };

            var left = AnsiImageRenderer.Render(image, Opt(AnsiHorizontalAlignmentEnum.Left));
            var right = AnsiImageRenderer.Render(image, Opt(AnsiHorizontalAlignmentEnum.Right));

            Assert.Contains(Fg(255, 0, 0), left);
            Assert.DoesNotContain(Fg(0, 0, 255), left);
            Assert.Contains(Fg(0, 0, 255), right);
            Assert.DoesNotContain(Fg(255, 0, 0), right);
        }

        [Fact]
        public void ComputeTargetCells_ScaleDown_DoesNotEnlargeBeyondNative()
        {
            // A 10x10 image in an 80x40 area: ScaleDown caps at native size (10 cols, 5 rows = 10 px tall)...
            var scaleDown = new AnsiImageOptions
            {
                MaxColumns = 80,
                MaxRows = 40,
                CellAspectRatio = 2.0,
                Fit = AnsiImageFitEnum.ScaleDown
            };
            var (cols, rows) = AnsiImageRenderer.ComputeTargetCells(10, 10, scaleDown);
            Assert.Equal(10, cols);
            Assert.Equal(5, rows);

            // ...whereas Contain (the default) would enlarge the same tiny image to fill the area.
            var contain = new AnsiImageOptions
            {
                MaxColumns = 80,
                MaxRows = 40,
                CellAspectRatio = 2.0,
                Fit = AnsiImageFitEnum.Contain
            };
            var (containCols, _) = AnsiImageRenderer.ComputeTargetCells(10, 10, contain);
            Assert.True(containCols > 10);
        }
    }
}

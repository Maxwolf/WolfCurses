using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Tests.Graphics
{
    /// <summary>
    ///     Exercises how <see cref="ConsolePresenter" /> honors the <see cref="AnsiGraphics" /> contract for true-pixel
    ///     pictures — the rules that keep its row-by-row diffing from erasing straight through a sixel or kitty image it
    ///     has just drawn.
    /// </summary>
    public class ConsolePresenterGraphicsTests
    {
        private const char Esc = (char) 27;

        private static readonly string _eraseToLineEnd = Esc + "[K";

        /// <summary>Stands in for a real sixel payload: an escape sequence of zero visible width.</summary>
        private const string FakePayload = "P0;1;0q#0;2;100;0;0#0~\\";

        private static string PayloadRow => AnsiGraphics.RowPlaceholder + FakePayload;

        [Fact]
        public void BuildAnsiUpdate_PayloadRow_IsWrittenWithoutItsMarker()
        {
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {PayloadRow, "text"}, null, 80, 25);

            Assert.Contains($"{Esc}[1;1H{FakePayload}", update);
            Assert.DoesNotContain(AnsiGraphics.RowPlaceholder, update);
        }

        [Fact]
        public void BuildAnsiUpdate_PayloadRow_IsNotErasedAfter()
        {
            // The payload has zero visible width, so the ordinary "shorter than the console, erase the tail" rule would
            // fire. It must not: a terminal leaves the cursor below a sixel it has drawn, so the erase would blank a
            // row of the picture instead of a leftover tail.
            var update = ConsolePresenter.BuildAnsiUpdate(new[] {PayloadRow}, null, 80, 25);

            var afterPayload = update.Substring(update.IndexOf(FakePayload, System.StringComparison.Ordinal) +
                                                FakePayload.Length);
            Assert.DoesNotContain(_eraseToLineEnd, afterPayload);
        }

        [Fact]
        public void BuildAnsiUpdate_PlaceholderRows_AreNeitherWrittenNorErased()
        {
            // Rows 2 and 3 are covered by the picture whose payload sits on row 1. The terminal is showing image
            // there; touching them at all would punch a hole in it.
            var update = ConsolePresenter.BuildAnsiUpdate(
                new[] {PayloadRow, AnsiGraphics.RowPlaceholder, AnsiGraphics.RowPlaceholder, "prompt"},
                null, 80, 25);

            Assert.DoesNotContain($"{Esc}[2;1H", update);
            Assert.DoesNotContain($"{Esc}[3;1H", update);
            Assert.Contains($"{Esc}[4;1Hprompt", update);
        }

        [Fact]
        public void BuildAnsiUpdate_TextRowBecomingPlaceholder_IsStillNotErased()
        {
            // Even when the previous frame had text on a row that is now covered by a picture, the presenter must not
            // erase it: the picture drawn on the payload row above has already painted over it. The payload row
            // changing is what repaints the area.
            var update = ConsolePresenter.BuildAnsiUpdate(
                new[] {PayloadRow, AnsiGraphics.RowPlaceholder},
                new[] {"old text", "more old text"}, 80, 25);

            Assert.DoesNotContain($"{Esc}[2;1H", update);
        }

        [Fact]
        public void BuildAnsiUpdate_UnchangedPictureRows_ProduceNoOutput()
        {
            var frame = new[] {PayloadRow, AnsiGraphics.RowPlaceholder, "prompt"};
            var update = ConsolePresenter.BuildAnsiUpdate(frame, frame, 80, 25);

            Assert.Equal(string.Empty, update);
        }

        [Fact]
        public void BuildAnsiUpdate_FullRedraw_RepaintsThePictureSoItSelfHeals()
        {
            // The periodic full redraw is what recovers a picture the terminal lost (a resize, another process
            // writing). The payload must be re-emitted rather than skipped as unchanged.
            var frame = new[] {PayloadRow, AnsiGraphics.RowPlaceholder};
            var update = ConsolePresenter.BuildAnsiUpdate(frame, null, 80, 25);

            Assert.Contains(FakePayload, update);
        }

        [Fact]
        public void ParkPosition_SkipsPictureRows_AndLandsAfterTheLastRealText()
        {
            var (row, column) = ConsolePresenter.ParkPosition(new[]
            {
                "prompt >",
                PayloadRow,
                AnsiGraphics.RowPlaceholder
            });

            // The cursor belongs where typing appears, which is never on the picture.
            Assert.Equal(1, row);
            Assert.Equal(9, column);
        }

        [Fact]
        public void ParkPosition_PictureOnlyFrame_FallsBackToHome()
        {
            var (row, column) = ConsolePresenter.ParkPosition(new[] {PayloadRow, AnsiGraphics.RowPlaceholder});

            Assert.Equal(1, row);
            Assert.Equal(1, column);
        }

        [Fact]
        public void StripMarkers_TurnsAFrameIntoSomethingSafeToPrintDirectly()
        {
            var frame = PayloadRow + "\n" + AnsiGraphics.RowPlaceholder + "\n" + "prompt";

            Assert.Equal(FakePayload + "\n" + "\n" + "prompt", AnsiGraphics.StripMarkers(frame));
        }

        [Fact]
        public void StripMarkers_FrameWithoutPictures_IsReturnedUnchanged()
        {
            const string frame = "just\ntext";

            Assert.Same(frame, AnsiGraphics.StripMarkers(frame));
        }

        [Fact]
        public void StripMarkers_Null_IsReturnedUnchanged()
        {
            Assert.Null(AnsiGraphics.StripMarkers(null));
        }

        [Fact]
        public void Present_RealSixelPictureInAFrame_ReachesTheTerminalIntactAndWithoutMarkers()
        {
            // End to end through the public API, the way an application actually uses this: a real picture rendered to
            // sixel, embedded in a window's text between a header and a prompt, pushed through the presenter, and
            // captured as the bytes a terminal would receive. The parts are covered separately elsewhere; this is the
            // seam between them — and the one place a stray marker character would escape to the screen.
            var image = new PixelBuffer(4, 40);
            for (var y = 0; y < 40; y++)
            for (var x = 0; x < 4; x++)
                image.SetPixel(x, y, new Rgba32(255, 0, 0, 255));

            var picture = new SixelImageRenderer(2, 20).Render(image, new AnsiImageOptions
            {
                MaxColumns = 2,
                MaxRows = 2,
                Fit = AnsiImageFitEnum.Stretch
            });

            var frame = "header" + System.Environment.NewLine + picture + System.Environment.NewLine + "prompt >";

            var captured = new System.IO.StringWriter();
            var previous = System.Console.Out;
            try
            {
                System.Console.SetOut(captured);
                new ConsolePresenter(useAnsi: true).Present(frame);
            }
            finally
            {
                System.Console.SetOut(previous);
            }

            var written = captured.ToString();

            Assert.DoesNotContain(AnsiGraphics.RowPlaceholder, written);
            Assert.Contains($"{Esc}P0;1;0q", written, System.StringComparison.Ordinal);
            Assert.Contains($"{Esc}[1;1Hheader", written, System.StringComparison.Ordinal);

            // The picture starts on row 2 and covers row 3, so the prompt belongs on row 4 — the placeholder row
            // keeping the count honest is what puts it there.
            Assert.Contains($"{Esc}[4;1Hprompt >", written, System.StringComparison.Ordinal);
            Assert.DoesNotContain($"{Esc}[3;1H", written, System.StringComparison.Ordinal);
        }
    }
}

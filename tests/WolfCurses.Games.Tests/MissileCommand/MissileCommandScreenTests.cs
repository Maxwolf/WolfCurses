using System;
using System.Threading;
using WolfCurses.Core;
using WolfCurses.Games.MissileCommand;
using WolfCurses.Games.Tests.Support;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Games.Tests.MissileCommand
{
    /// <summary>
    ///     Missile Command as a player meets it, driven headlessly through the published driver surface.
    ///     <para>
    ///         <b>Every "did the screen change" assertion goes through <see cref="Body" />.</b> The scene graph puts
    ///         an activity spinner on the very first line and it advances on every single tick, so the whole frame is
    ///         different every time it is read and a naive comparison passes no matter what the game did — or, worse,
    ///         a "nothing changed" assertion fails no matter what.
    ///     </para>
    ///     <para>
    ///         A headless host cannot enable virtual-terminal processing, so this screen always selects its
    ///         character board here. That is not a gap being papered over: it is the branch a real console with a
    ///         legacy host takes too, and it is the one the picture path can never be tested on. The picture is
    ///         covered separately by driving the painter directly, which needs no terminal at all.
    ///     </para>
    /// </summary>
    [Collection("GamesApp")]
    public class MissileCommandScreenTests
    {
        /// <summary>The frame with the spinner line dropped, so a comparison is about the game and not the clock.</summary>
        private static string Body(string screen)
        {
            var lines = screen.Split('\n');
            return lines.Length <= 2 ? string.Empty : string.Join('\n', lines[2..]);
        }

        /// <summary>Lets real time pass while ticking, since everything on this screen is paced by elapsed time.</summary>
        private static void Play(DrivenGamesApp game, int frames, int milliseconds = 40)
        {
            for (var i = 0; i < frames; i++)
            {
                Thread.Sleep(milliseconds);
                game.Tick();
            }
        }

        [Fact]
        public void TheArcadeMenuOffersMissileCommand()
        {
            using var game = new DrivenGamesApp();

            Assert.Contains("Missile Command", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void ItOpensOnWaveOneWithEveryCityStanding()
        {
            using var game = new DrivenGamesApp();

            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);

            Assert.Contains("Wave 1", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Score 0", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Cities ######", game.Screen, StringComparison.Ordinal);
            Assert.Contains("Ammo 10/10/10", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void EscapeBacksOutToTheMenu()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);
            Assert.Contains("Wave 1", game.Screen, StringComparison.Ordinal);

            game.Escape();

            Assert.Contains("Which game?", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void FiringDoesNotLeaveASpaceInTheEchoedPrompt()
        {
            // InputFillsBuffer => false. It matters more here than anywhere except Tetris, because SPACE is the fire
            // button and a printable character, so every shot would widen the prompt.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);

            game.PressChar(' ', ConsoleKey.Spacebar);
            game.PressChar('w', ConsoleKey.W);

            Assert.Equal(string.Empty, game.App.InputManager.InputBuffer);
        }

        [Fact]
        public void TheFieldMovesOnItsOwnClockWithNobodyTouchingIt()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);

            // Long enough for the wave to actually start sending something: the first warhead is a second out,
            // so a shorter wait would be asserting that an empty sky looks like an empty sky.
            var opening = Body(game.Screen);
            Play(game, 45);

            Assert.NotEqual(opening, Body(game.Screen));
        }

        [Fact]
        public void FiringSpendsAShellAndPutsItOnTheStatusLine()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);
            Assert.Contains("Ammo 10/10/10", game.Screen, StringComparison.Ordinal);

            game.PressChar(' ', ConsoleKey.Spacebar);
            Play(game, 3);

            Assert.DoesNotContain("Ammo 10/10/10", game.Screen, StringComparison.Ordinal);
        }

        [Fact]
        public void AHeldArrowMovesTheCrosshairByElapsedTimeAndNotByHowManyKeysArrived()
        {
            // THE test for this game's one genuinely new problem. InputManager spends every buffered key inside a
            // single tick, so a burst of eight presses with no time between them is exactly what a held arrow looks
            // like when the loop is behind. A crosshair moved one step per press flies eight times as far for that
            // burst as it does for one press held over the same real time; a crosshair moved by elapsed time barely
            // moves for the burst and sweeps for the hold.
            //
            // Deliberately compared as "far less than", not "equal to something": the exact distance depends on the
            // acceleration ramp, and pinning it would make this a test about the ramp instead of about the rule.
            // Two runs, one after the other rather than side by side: GamesSimulationApp is a singleton that refuses
            // to be built twice, so the first has to be disposed before the second exists.
            int burstMoved;
            using (var burst = new DrivenGamesApp())
            {
                burst.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);
                var start = CrosshairColumn(burst);

                for (var i = 0; i < 8; i++)
                {
                    burst.App.InputManager.SendConsoleKey(
                        new ConsoleKeyInfo((char) 0, ConsoleKey.RightArrow, false, false, false));
                }

                // The sleep is on THIS side of the tick and it is not padding. Frames are paced, so a tick taken
                // with no elapsed time composes nothing and the crosshair is read back at its old position - which
                // makes the burst look motionless for the wrong reason and lets the very mutation this test exists
                // to catch walk straight past it. Both arms must get a rendered frame; only one gets time to move.
                Thread.Sleep(45);
                burst.Tick();
                burstMoved = Math.Abs(CrosshairColumn(burst) - start);
            }

            int heldMoved;
            using (var held = new DrivenGamesApp())
            {
                held.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);
                var start = CrosshairColumn(held);

                // One press per frame, which is what a held key really looks like when the loop is keeping up.
                for (var i = 0; i < 8; i++)
                {
                    held.Press(ConsoleKey.RightArrow);
                    Thread.Sleep(45);
                    held.Tick();
                }

                heldMoved = Math.Abs(CrosshairColumn(held) - start);
            }

            // Comfortably more, not merely more. Both arms get exactly the same eight key presses and only one gets
            // any time, so under the rule this test defends the difference is large; a crosshair driven by key
            // arrivals moves the SAME distance in both arms, and a margin of one column would let noise decide.
            Assert.True(heldMoved > burstMoved*3,
                $"a burst of eight keys with no time between them moved the crosshair {burstMoved} columns, and the " +
                $"same eight keys held over a third of a second moved it {heldMoved} - so aiming is being driven by " +
                "how many keys arrived rather than by how long they were held");
        }

        /// <summary>Where the crosshair is on screen, read off the frame rather than out of the form.</summary>
        private static int CrosshairColumn(DrivenGamesApp game)
        {
            var rows = game.Screen.Split('\n');
            for (var row = 0; row < rows.Length; row++)
            {
                var column = rows[row].IndexOf('+', StringComparison.Ordinal);
                if (column >= 0)
                    return column;
            }

            return -1;
        }

        [Fact]
        public void ItDrawsTheCharacterBoardWhenTheHostCannotShowAPicture()
        {
            // A test host has no console to enable virtual-terminal processing on, so the game must take its text
            // path. Asserted on glyphs rather than on the absence of escapes, because the character board is itself
            // coloured - it is a fallback for missing GRAPHICS, not for missing colour.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.MissileCommand);

            var screen = game.Screen;

            Assert.Contains('▲', screen); // a battery
            Assert.Contains('█', screen); // a city
            Assert.Contains('+', screen); // the crosshair
            Assert.DoesNotContain('', screen); // and no true-pixel payload marker anywhere
        }

        [Fact]
        public void TheCharacterBoardIsPlayableWithNoColourAtAll()
        {
            // Each kind of thing is told apart by its glyph as well as by its colour, so stripping every escape
            // leaves a board somebody could still play. This is the ChessTextBoard lesson restated.
            var field = new MissileField(new Randomizer(17));
            field.Spawn(MissileKindEnum.Icbm, 0.40, 0.80, 0.40, MissileField.GroundY, 0.05);
            field.Spawn(MissileKindEnum.SmartBomb, 1.10, 0.70, 1.10, MissileField.GroundY, 0.05);
            field.Detonate(0.80, 0.60, true);
            field.Advance(TimeSpan.FromMilliseconds(90));

            var drawn = WolfCurses.Graphics.AnsiText.StripEscapes(
                MissileFieldText.Render(field, 0.80, 0.50, 74, 18));

            Assert.Contains('*', drawn); // warhead
            Assert.Contains('%', drawn); // smart bomb
            Assert.Contains('▲', drawn); // battery
            Assert.Contains('█', drawn); // city
        }

        [Fact]
        public void EveryRowOfTheCharacterBoardIsTheSameVisibleWidth()
        {
            // Measured with AnsiText rather than with Length, because a coloured row is several hundred bytes wide
            // and twenty-odd columns wide, and only one of those numbers is the one that matters.
            var field = new MissileField(new Randomizer(23));
            field.Advance(TimeSpan.FromMilliseconds(90));

            var drawn = MissileFieldText.Render(field, 0.80, 0.50, 74, 18);
            var rows = drawn.Replace("\r\n", "\n").Split('\n');

            Assert.Equal(18, rows.Length);
            foreach (var row in rows)
                Assert.Equal(74, WolfCurses.Graphics.AnsiText.VisibleLength(row));
        }

        [Fact]
        public void ThePainterDrawsAFieldWithNoConsoleAnywhereNearIt()
        {
            // The only coverage the picture path can have, and it needs no terminal: a PixelBuffer is correct or not
            // on its own terms. What this catches is the whole class of "the canvas is 320x200 but the world is 1.6
            // wide" mistakes, which would otherwise reach a real terminal untouched.
            var field = new MissileField(new Randomizer(29));
            field.Spawn(MissileKindEnum.Icbm, 0.40, 0.80, 0.40, MissileField.GroundY, 0.05);
            field.Detonate(0.80, 0.60, true);
            field.Advance(TimeSpan.FromMilliseconds(90));

            var art = new MissileFieldArt(320, 200);
            var painted = art.Paint(field, 0.80, 0.50);

            Assert.Equal(320, painted.Width);
            Assert.Equal(200, painted.Height);

            // The ground really is along the bottom, and the sky really is not.
            Assert.True(RowHasColourOtherThanSky(painted, 198), "nothing was drawn on the ground row");
            Assert.False(RowHasColourOtherThanSky(painted, 2), "the very top of the sky should be empty");

            // And something is in the air - a trail, a fireball or the crosshair.
            var painting = false;
            for (var y = 40; y < 150 && !painting; y++)
                painting = RowHasColourOtherThanSky(painted, y);

            Assert.True(painting, "the sky was empty despite a warhead, a fireball and a crosshair being in it");
        }

        [Fact]
        public void TheCanvasKeepsTheFieldsProportions()
        {
            // A canvas that did not would draw circles as ovals, and the player would learn to aim at something
            // other than what the rules are testing.
            IImageRenderer[] renderers = {new HalfBlockImageRenderer(), new SixelImageRenderer()};

            foreach (var renderer in renderers)
            foreach (var rows in new[] {10, 24, 50, 80})
            {
                var (width, height) = MissileFieldArt.SizeFor(rows, renderer);

                var ratio = (double) width/height;
                Assert.True(Math.Abs(ratio - MissileField.Aspect) < 0.02,
                    $"{rows} rows on {renderer.Name} gave a {width}x{height} canvas, ratio {ratio:F3}");
            }
        }

        [Fact]
        public void ARendererWithRealPixelsGetsACanvasNearItsOwnGridRatherThanOneToMagnify()
        {
            // The bug this replaced was silent and affected every stroke on the screen. A fit defaults to
            // Contain and ImageFit ENLARGES, so a canvas sized for half blocks was blown up on its way to a
            // sixel terminal - and the strokes are chosen in OUTPUT pixels, so the deliberate two-pixel trail
            // arrived several pixels thick. Asking the renderer how big its cell is removes the guess.
            IImageRenderer half = new HalfBlockImageRenderer();
            IImageRenderer sixel = new SixelImageRenderer();

            var (_, halfHeight) = MissileFieldArt.SizeFor(18, half);
            var (_, sixelHeight) = MissileFieldArt.SizeFor(18, sixel);

            Assert.True(sixelHeight > halfHeight*1.5,
                $"real pixels got {sixelHeight} rows of canvas against half blocks' {halfHeight}");

            // And bounded, because every one of those pixels is base64 on its way out thirty times a second
            // and every Fill and DrawLine is paid per pixel per frame.
            Assert.InRange(sixelHeight, 1, 420);
        }

        [Fact]
        public void TheHalfBlockCanvasIsStillExactlyTwiceWhatWillBeDrawn()
        {
            // The absolute half of the pair above. Half blocks draw two pixels a row and the canvas is a 2x
            // supersample of that, which is what makes a one-cell-pixel trail survive being averaged down.
            // Thirty rows, chosen because 30 * 2 * 2 is 120: inside the 100..200 clamp, so this measures
            // the arithmetic and not the bound. At twenty-four it would be clamped up to 100 and the
            // test would be about the floor instead, which is how the first version of it failed.
            IImageRenderer renderer = new HalfBlockImageRenderer();
            var (_, height) = MissileFieldArt.SizeFor(30, renderer);

            Assert.Equal(30*renderer.CellPixelHeight*2, height);
        }

        /// <summary>Whether any pixel on a row is something other than the night sky the canvas is cleared to.</summary>
        private static bool RowHasColourOtherThanSky(WolfCurses.Graphics.PixelBuffer buffer, int row)
        {
            var sky = buffer.GetPixel(0, 0);
            for (var x = 0; x < buffer.Width; x++)
            {
                if (!buffer.GetPixel(x, row).Equals(sky))
                    return true;
            }

            return false;
        }
    }
}

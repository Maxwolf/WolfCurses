using System;
using System.Collections.Generic;
using WolfCurses.Games.Tests.Support;
using Xunit;

namespace WolfCurses.Games.Tests
{
    /// <summary>
    ///     The Tetris right-hand column, and the thing that used to move about in it.
    ///     <para>
    ///         <b>The Next panel used to change height with the piece it was previewing.</b> A tetromino's box is
    ///         two, three or four cells square and the preview drew each piece's own box, so the Next frame came out
    ///         four, five or six rows tall. A shuffled seven-bag deals every piece every seven pieces, so the Stats
    ///         panel below it shifted up and down several times a minute for the whole game, while the file's own
    ///         comment claimed the panel stayed a fixed height.
    ///     </para>
    ///     <para>
    ///         <b>Nothing caught it, and the reason is worth more than the bug.</b> The layout test beside this one
    ///         is about WIDTH, and the frame's own height is set by the well - eighteen rows against the right-hand
    ///         column's twelve - so a Next box growing or shrinking by two rows never changed the frame at all. A
    ///         test measuring the frame passes against the broken version, which is exactly what the first draft of
    ///         this one did. The assertion has to be about the row the Stats panel starts on.
    ///     </para>
    /// </summary>
    [Collection("GamesApp")]
    public class TetrisPanelTests
    {
        [Fact]
        public void TheStatsPanelStaysPutWhicheverPieceIsComingNext()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Tetris);

            var shapes = new HashSet<string>(StringComparer.Ordinal);

            // A shuffled seven-bag deals every one of the seven within seven pieces, so this sees the O in its
            // two-by-two box and the I in its four-by-four at least once - the two that used to move the panel
            // furthest. Pieces are pushed alternately to the two walls so the well cannot top out inside the run,
            // which would end the game and change the screen for a reason that has nothing to do with the preview.
            for (var piece = 0; piece < 7; piece++)
            {
                shapes.Add(Shape(game));

                for (var nudge = 0; nudge < 5; nudge++)
                    game.Press(piece % 2 == 0 ? ConsoleKey.LeftArrow : ConsoleKey.RightArrow);

                game.PressChar(' ', ConsoleKey.Spacebar);
                game.Tick();
            }

            shapes.Add(Shape(game));

            Assert.True(shapes.Count == 1,
                "the panel moved as pieces came and went: " + string.Join(" / ", shapes) + Environment.NewLine +
                game.Describe());
        }

        /// <summary>
        ///     Which row the Stats box's title sits on, and the frame's own size beside it. Both are read off the
        ///     rendered screen rather than from any constant in the game, so the test cannot agree with the code by
        ///     restating it.
        /// </summary>
        /// <param name="game">The running arcade.</param>
        /// <returns>A description of where things are, for comparing frame to frame.</returns>
        private static string Shape(DrivenGamesApp game)
        {
            var rows = game.Screen.Split('\n');
            var widest = 0;
            var stats = -1;

            for (var row = 0; row < rows.Length; row++)
            {
                var line = rows[row].TrimEnd('\r').TrimEnd();
                widest = Math.Max(widest, line.Length);

                if (stats < 0 && line.Contains("Stats", StringComparison.Ordinal))
                    stats = row;
            }

            Assert.True(stats >= 0, "the Stats panel is not on screen:" + Environment.NewLine + game.Describe());

            return "stats@" + stats + " frame=" + rows.Length + "x" + widest;
        }
    }
}

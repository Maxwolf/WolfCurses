using System;
using WolfCurses.Games.Tests.Support;
using Xunit;

namespace WolfCurses.Games.Tests.Chess
{
    /// <summary>
    ///     Where the chess board sits in the frame, and the fact that it does not move.
    ///     <para>
    ///         <b>The whole board used to shift down exactly one row the moment White played.</b> The move list was
    ///         a conditional second line of the status <i>above</i> the board, so the frame was one row taller from
    ///         move one onward. A player sees that as a jolt; on a sixel or kitty terminal it is worse, because the
    ///         payload row has moved and the presenter must erase the picture's area and repaint the entire payload
    ///         rather than leave an unchanged picture alone. The moves now have a row of their own <i>under</i> the
    ///         board, always present and empty before the first move.
    ///     </para>
    ///     <para>
    ///         Driven through the letter board, which is the one of the two views a test can locate a row of. The
    ///         picture board is a payload row of escapes with no visible content to search for, and which of the two
    ///         a host opens on depends on its console size.
    ///     </para>
    /// </summary>
    [Collection("GamesApp")]
    public class ChessLayoutTests
    {
        [Fact]
        public void TheBoardDoesNotMoveWhenTheFirstMoveIsPlayed()
        {
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Chess);

            if (!IsLetterBoard(game.Screen))
                game.Type("text");

            Assert.True(IsLetterBoard(game.Screen),
                "could not get to the letter board:" + Environment.NewLine + game.Describe());

            var before = FileLabelRow(game);
            var heightBefore = RowCount(game);

            game.Type("e4");

            // Read straight after the player's move rather than after the reply, because the row that moved was
            // added by the player's own move appearing in the list - waiting for the bot would test the same thing
            // one ply later and take a second doing it.
            var after = FileLabelRow(game);

            Assert.True(before == after,
                $"the board was on row {before} and moved to row {after} when White played");
            Assert.Equal(heightBefore, RowCount(game));
        }

        [Fact]
        public void TheMoveListIsUnderTheBoardRatherThanAboveIt()
        {
            // What the class documentation and the app notes both already claimed while it was false. It matters
            // beyond tidiness: a picture board is one payload row that paints across many, so nothing may sit
            // beside it and anything added above it moves it.
            using var game = new DrivenGamesApp();
            game.ChooseMenuItem((int) GamesCommandsEnum.Chess);

            if (!IsLetterBoard(game.Screen))
                game.Type("text");

            game.Type("e4");

            var rows = game.Screen.Split('\n');
            var board = FileLabelRow(game);
            var moves = -1;

            for (var row = 0; row < rows.Length; row++)
            {
                if (rows[row].Contains("1.e4", StringComparison.Ordinal))
                {
                    moves = row;
                    break;
                }
            }

            Assert.True(moves >= 0, "the move list is not on screen:" + Environment.NewLine + game.Describe());
            Assert.True(moves > board,
                $"the move list is on row {moves}, above the board on row {board}");
        }

        /// <summary>Which row carries the file letters, which is the one row of the letter board that is findable.</summary>
        private static int FileLabelRow(DrivenGamesApp game)
        {
            var rows = game.Screen.Split('\n');

            for (var row = 0; row < rows.Length; row++)
            {
                if (rows[row].Contains("a  b  c  d  e  f  g  h", StringComparison.Ordinal))
                    return row;
            }

            return -1;
        }

        private static int RowCount(DrivenGamesApp game)
        {
            return game.Screen.Split('\n').Length;
        }

        private static bool IsLetterBoard(string screen)
        {
            return screen.Contains("a  b  c  d  e  f  g  h", StringComparison.Ordinal);
        }
    }
}

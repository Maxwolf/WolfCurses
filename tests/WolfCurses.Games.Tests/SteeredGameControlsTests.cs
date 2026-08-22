using System;
using System.Collections.Generic;
using WolfCurses.Games;
using WolfCurses.Games.Tests.Support;
using Xunit;

namespace WolfCurses.Games.Tests
{
    /// <summary>
    ///     Every game that is steered rather than typed at has to say on screen how it is played, because the row it
    ///     would naturally say it on does not exist.
    ///     <para>
    ///         <b>This is a regression test for eight screens that were all wrong at once.</b> A steered form
    ///         overrides <c>InputFillsBuffer</c> to false so that W, A, S, D and SPACE do not pile up in the echoed
    ///         prompt. <c>WindowManager.AcceptingInput</c> answers false for exactly that shape, and
    ///         <c>SceneGraph</c> only appends the prompt row when it is accepting input, so the prompt is never
    ///         drawn at all. All eight screens set <c>ParentWindow.PromptText</c> anyway. Labyrinth kept its entire
    ///         control list there and Battlezone had trimmed its own to fit eighty columns of a row nobody was ever
    ///         going to see.
    ///     </para>
    ///     <para>
    ///         <b>It asserts the last drawn row, not merely that the words are somewhere.</b> Putting the legend
    ///         back on <c>PromptText</c> would leave the keys named in the source and absent from the frame, which
    ///         is the exact regression, and a test looking anywhere in the screen would still pass for a screen that
    ///         happened to mention ESC in a status line.
    ///     </para>
    /// </summary>
    [Collection("GamesApp")]
    public class SteeredGameControlsTests
    {
        /// <summary>
        ///     The games whose forms turn the input buffer off. Minesweeper and chess are deliberately absent: both
        ///     are typed at, both leave <c>InputFillsBuffer</c> at its default, and both really do get a prompt row.
        /// </summary>
        public static TheoryData<GamesCommandsEnum> SteeredGames =>
        [
            GamesCommandsEnum.Snake,
            GamesCommandsEnum.Tetris,
            GamesCommandsEnum.MissileCommand,
            GamesCommandsEnum.Labyrinth,
            GamesCommandsEnum.PacMan,
            GamesCommandsEnum.Blackjack,
            GamesCommandsEnum.Poker,
            GamesCommandsEnum.Battlezone
        ];

        [Theory]
        [MemberData(nameof(SteeredGames))]
        public void ASteeredGameNamesTheWayOutOnItsOwnLastRow(GamesCommandsEnum game)
        {
            using var arcade = new DrivenGamesApp();
            arcade.ChooseMenuItem((int) game);

            var last = LastDrawnRow(arcade);

            // ESC leaves every game here and ENTER leaves most of them, so between them they are the way out
            // whatever the screen decided to advertise. Named rather than matched loosely, because "the bottom row
            // has something on it" would pass for a status line and prove nothing about the controls.
            Assert.True(
                last.Contains("ESC", StringComparison.Ordinal) || last.Contains("ENTER", StringComparison.Ordinal),
                $"{game} draws no control legend on its last row, so nothing on screen says how to play or leave. " +
                $"The row read: \"{last}\". A legend set on ParentWindow.PromptText is never drawn for a form whose " +
                "InputFillsBuffer is false - see GameControls.");
        }

        [Theory]
        [MemberData(nameof(SteeredGames))]
        public void TheLegendFitsTheRowItIsDrawnOn(GamesCommandsEnum game)
        {
            // The half that only started mattering once these were drawn. Auto-wrap is off for the frame, so an
            // over-wide row is truncated rather than wrapped and the end of a legend is where ESC usually is. Two
            // of these were past a hundred characters while they were prompt text, which cost nothing because none
            // of it reached the screen.
            using var arcade = new DrivenGamesApp();
            arcade.ChooseMenuItem((int) game);

            Assert.InRange(LastDrawnRow(arcade).Length, 1, 80);
        }

        /// <summary>The last row of the frame that has anything on it, read off the screen rather than counted to.</summary>
        /// <param name="arcade">The running arcade.</param>
        /// <returns>The row.</returns>
        private static string LastDrawnRow(DrivenGamesApp arcade)
        {
            var rows = new List<string>(arcade.Screen.Replace("\r\n", "\n").Split('\n'));

            for (var i = rows.Count - 1; i >= 0; i--)
            {
                var row = rows[i].TrimEnd('\r').TrimEnd();
                if (row.Length > 0)
                    return row;
            }

            return string.Empty;
        }
    }
}

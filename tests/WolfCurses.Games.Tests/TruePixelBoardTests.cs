using System;
using WolfCurses.Games.Tests.Support;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Games.Tests
{
    /// <summary>
    ///     A game board drawn with a terminal's real pixels.
    ///     <para>
    ///         <b>Which arm of this runs depends on the host, and that is stated rather than skipped around.</b>
    ///         Every picture in the arcade is gated on <see cref="AnsiConsole.SupportsPictures" />, which answers
    ///         false wherever virtual-terminal processing could not be enabled - every Windows test host, whose
    ///         output is a pipe - and true on a Linux or macOS one, where nothing needs enabling. So the letters
    ///         are what this checks on a Windows machine and a real sixel payload is what it checks on a CI runner,
    ///         and both are worth checking: they are the two branches of the same screen, and until the build
    ///         matrix covered more than one operating system, exactly one of them had ever run.
    ///     </para>
    ///     <para>
    ///         The renderer is installed by assignment rather than through a menu, since the arcade has no
    ///         equivalent of the demo's <b>Force render type</b> screen. Assigning before the application is created
    ///         is the documented way to overrule the start-up probe, and it has to be put back by hand: it is
    ///         process-wide and outlives the game that drew with it.
    ///     </para>
    /// </summary>
    [Collection("GamesApp")]
    public class TruePixelBoardTests
    {
        [Fact]
        public void TheChessBoardIsEitherOneWellFormedPictureOrTheLetters()
        {
            var restore = ImageRenderers.Default;

            try
            {
                ImageRenderers.Default = new SixelImageRenderer();

                using var game = new DrivenGamesApp();
                game.ChooseMenuItem((int) GamesCommandsEnum.Chess);

                var rows = Rows(game.RawScreen);
                var payloads = 0;
                var placeholders = 0;

                foreach (var row in rows)
                {
                    if (AnsiGraphics.IsPayloadRow(row))
                        payloads++;
                    else if (AnsiGraphics.IsRowPlaceholder(row))
                        placeholders++;

                    // Whichever arm is running: the marker means "this row is a picture and cannot be sliced", and
                    // the presenter only recognises it at index zero. A board with its rank numbers drawn beside
                    // the payload would reach the terminal as escape garbage instead of as a board.
                    Assert.False(row.IndexOf(AnsiGraphics.Marker) > 0,
                        "something was drawn beside the board:" + game.Describe());
                }

                if (payloads == 0)
                {
                    // No picture reaches this console, so the game owes the player the letters rather than an empty
                    // rectangle, and it must not have left a stray placeholder row behind either.
                    Assert.Equal(0, placeholders);
                    Assert.Contains("a  b  c  d  e  f  g  h", game.Screen, StringComparison.Ordinal);
                    return;
                }

                Assert.True(payloads == 1, "the board frame had " + payloads + " payload rows");
                Assert.True(placeholders > 0, "the picture claimed no rows at all:" + game.Describe());

                // The board is drawn as pixels, so the letters are not: one board, not two overlaid.
                Assert.DoesNotContain("a  b  c  d  e  f  g  h", game.Screen, StringComparison.Ordinal);

                // And the chrome around it survived, which is what a picture that had swallowed its own rows would
                // have taken with it.
                Assert.Contains("WolfChess 5000", game.Screen, StringComparison.Ordinal);
            }
            finally
            {
                ImageRenderers.Default = restore;
            }
        }

        /// <summary>The frame's rows, with the carriage returns taken off.</summary>
        /// <param name="raw">The frame exactly as the terminal would receive it.</param>
        /// <returns>The rows.</returns>
        private static string[] Rows(string raw)
        {
            var rows = raw.Split('\n');

            for (var i = 0; i < rows.Length; i++)
                rows[i] = rows[i].TrimEnd('\r');

            return rows;
        }
    }
}

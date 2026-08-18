using System.Collections.Generic;
using WolfCurses.Games.Minesweeper;

namespace WolfCurses.Games.Tests.Support
{
    /// <summary>
    ///     Reads a minesweeper board off the screen without being told anything about it.
    ///     <para>
    ///         <b>Nothing here may assume a board size.</b> The screen picks the largest of the boards that fits the
    ///         terminal, so a test host with a real console attached gets a different one from a host without — and
    ///         a helper carrying a hardcoded nine passes on a build machine and fails on somebody's desk. That rule
    ///         has been broken twice and caught both times by a full run rather than a filtered one, so everything
    ///         here is found from the drawing: the field's top-left corner locates it, and its own lattice pitch
    ///         does the rest.
    ///     </para>
    /// </summary>
    public static class MinesweeperScreen
    {
        /// <summary>
        ///     The highlight hairline inside a closed square, which nothing else on the panel draws. An opened
        ///     square has nothing there, so counting these counts the squares still to be opened.
        /// </summary>
        public const char Raised = '▏';

        /// <summary>The flag.</summary>
        public const char Flag = '¶';

        /// <summary>The top-left corner of the field, which is what locates it.</summary>
        private const char FieldCorner = '┌';

        /// <summary>Every row of the frame.</summary>
        /// <param name="screen">The frame.</param>
        /// <returns>The rows.</returns>
        public static string[] Lines(string screen)
        {
            return (screen ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        }

        /// <summary>Which screen row the field's own top line is drawn on, or -1 when there is no field.</summary>
        /// <param name="screen">The frame.</param>
        /// <returns>The row index.</returns>
        public static int OriginRow(string screen)
        {
            var lines = Lines(screen);

            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf(FieldCorner) >= 0)
                    return i;
            }

            return -1;
        }

        /// <summary>Which screen column the field's own left line is drawn on, or -1.</summary>
        /// <param name="screen">The frame.</param>
        /// <returns>The column index.</returns>
        public static int OriginColumn(string screen)
        {
            var row = OriginRow(screen);
            return row < 0 ? -1 : Lines(screen)[row].IndexOf(FieldCorner);
        }

        /// <summary>Every row of the frame that has any part of the field on it.</summary>
        /// <param name="screen">The frame.</param>
        /// <returns>The rows, in order.</returns>
        public static IEnumerable<string> BoardRows(string screen)
        {
            var originRow = OriginRow(screen);
            if (originRow < 0)
                yield break;

            var lines = Lines(screen);
            var last = originRow;

            for (var i = originRow; i < lines.Length; i++)
            {
                if (lines[i].IndexOf('│') >= 0 || lines[i].IndexOf('─') >= 0)
                    last = i;
            }

            for (var i = originRow; i <= last; i++)
                yield return lines[i];
        }

        /// <summary>How many squares are still closed, counted off the highlight each one carries.</summary>
        /// <param name="screen">The frame.</param>
        /// <returns>The count.</returns>
        public static int Hidden(string screen)
        {
            var hidden = 0;

            foreach (var character in screen ?? string.Empty)
            {
                if (character == Raised)
                    hidden++;
            }

            return hidden;
        }

        /// <summary>The name of a square that is still closed, such as "C4", or null when none is.</summary>
        /// <param name="screen">The frame.</param>
        /// <returns>The square's name.</returns>
        public static string FirstHiddenName(string screen)
        {
            var originRow = OriginRow(screen);
            var originColumn = OriginColumn(screen);
            if (originRow < 0)
                return null;

            var lines = Lines(screen);

            for (var row = originRow; row < lines.Length; row++)
            {
                var at = lines[row].IndexOf(Raised);
                if (at < 0)
                    continue;

                var x = (at - originColumn - 1)/MinesweeperFace.TileWidth;
                var y = (row - originRow - 1)/MinesweeperFace.TileHeight;
                return $"{(char) ('A' + x)}{y + 1}";
            }

            return null;
        }

        /// <summary>What the left-hand counter says: the first three-digit run on the row the face is on.</summary>
        /// <param name="screen">The frame.</param>
        /// <returns>The number, or -1 when it was not found.</returns>
        public static int MinesLeft(string screen)
        {
            return Counter(screen, first: true);
        }

        /// <summary>What the right-hand counter says, which is the clock.</summary>
        /// <param name="screen">The frame.</param>
        /// <returns>The number of seconds, or -1 when it was not found.</returns>
        public static int Clock(string screen)
        {
            return Counter(screen, first: false);
        }

        /// <summary>Where the face is on screen, or (-1, -1) when it is not.</summary>
        /// <param name="screen">The frame.</param>
        /// <returns>The column and row of the face's first character.</returns>
        public static (int Column, int Row) Smiley(string screen)
        {
            var lines = Lines(screen);

            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var mood in new[] {":)", ":(", "B)"})
                {
                    var at = lines[i].IndexOf(mood, System.StringComparison.Ordinal);
                    if (at >= 0)
                        return (at, i);
                }
            }

            return (-1, -1);
        }

        /// <summary>Whether the board on screen has been either cleared or blown up.</summary>
        /// <param name="screen">The frame.</param>
        /// <returns>True when the game is over.</returns>
        public static bool IsFinished(string screen)
        {
            foreach (var line in Lines(screen))
            {
                if (line.Contains(":(") || line.Contains("B)"))
                    return true;
            }

            return false;
        }

        /// <summary>Reads one of the two counters off the row the face is drawn on.</summary>
        /// <param name="screen">The frame.</param>
        /// <param name="first">True for the left-hand counter, false for the right.</param>
        /// <returns>The number, or -1.</returns>
        private static int Counter(string screen, bool first)
        {
            foreach (var line in Lines(screen))
            {
                if (!line.Contains(":)") && !line.Contains(":(") && !line.Contains("B)"))
                    continue;

                var found = -1;

                for (var i = 0; i + 2 < line.Length; i++)
                {
                    if (!IsDigits(line, i))
                        continue;

                    var value = (line[i] - '0')*100 + (line[i + 1] - '0')*10 + (line[i + 2] - '0');
                    if (first)
                        return value;

                    found = value;
                    i += 2;
                }

                return found;
            }

            return -1;
        }

        /// <summary>Whether three digits start at a position.</summary>
        private static bool IsDigits(string line, int at)
        {
            return char.IsDigit(line[at]) && char.IsDigit(line[at + 1]) && char.IsDigit(line[at + 2]);
        }
    }
}

using System;
using WolfCurses.Core;
using WolfCurses.Games.Minesweeper;
using WolfCurses.Graphics;
using Xunit;

namespace WolfCurses.Games.Tests.Minesweeper
{
    /// <summary>
    ///     The Windows 95 panel: the bevels, the two counters and the face between them.
    /// </summary>
    public class MinesweeperFaceTests
    {
        [Fact]
        public void ThePanelIsAlwaysTheRectangleItSaysItIs()
        {
            // The TextGrid guarantee the click map is built on. A panel that changed width with its contents would
            // put every square somewhere else the moment a number appeared.
            var face = new MinesweeperFace(9, 9);
            var field = Played();

            foreach (var render in new[] {face.Render(field, 0, true), face.Render(field, 999, false)})
            {
                var rows = Rows(render);

                Assert.Equal(face.Rows, rows.Length);
                foreach (var row in rows)
                    Assert.Equal(face.Columns, AnsiText.VisibleLength(row));
            }
        }

        [Fact]
        public void TurningTheCoordinatesOnMovesNothing()
        {
            // THE invariant the mouse depends on. The letters and numbers ride on chrome that is drawn either way,
            // so a click lands on the same square whether or not the terminal has a pointer - and if they ever
            // started shifting the board, every click would be a row or a column out on exactly one of the two
            // terminals, which is the sort of thing nobody reproduces.
            var face = new MinesweeperFace(9, 9);
            var field = Played();

            var labelled = Rows(AnsiText.StripEscapes(face.Render(field, 12, true)));
            var plain = Rows(AnsiText.StripEscapes(face.Render(field, 12, false)));

            Assert.Equal(plain.Length, labelled.Length);

            for (var y = 0; y < face.BoardHeight; y++)
            {
                var row = face.BoardOriginRow + y;
                var from = face.BoardOriginColumn;

                Assert.Equal(plain[row].Substring(from), labelled[row].Substring(from));
            }
        }

        [Fact]
        public void AnUntouchedSquareIsRaisedAndAnOpenedOneIsFlat()
        {
            // Read with the escapes stripped, so this is about the GLYPHS. Raised, flat, flagged and mined have to
            // be four different shapes before they are four different colours, or the game stops working the moment
            // somebody sets NO_COLOR.
            var face = new MinesweeperFace(9, 9);
            var field = new Minefield(9, 9, 10, new Randomizer(4));
            field.Reveal(4, 4);

            var rows = Rows(AnsiText.StripEscapes(face.Render(field, 0, false)));

            var opened = Glyph(face, rows, 4, 4);
            var untouched = FindSquare(face, rows, field, revealed: false);

            Assert.Equal('▏', opened);
            Assert.Equal('▌', untouched);
        }

        [Fact]
        public void AFlagIsDrawnOnTheSquareItWasPlantedOn()
        {
            // Flagged wherever the board still shows a face-down square, asked of the board rather than named.
            // Flagging an OPENED square is correctly a no-op, and the opening cascade reaches a different set of
            // squares on every seed - so a test that flags a fixed corner passes or fails on the shuffle. This one
            // did, on its first run, which is the third time this arcade has learned it.
            var face = new MinesweeperFace(9, 9);
            var field = new Minefield(9, 9, 10, new Randomizer(4));
            field.Reveal(4, 4);

            var (fx, fy) = FirstHidden(field);
            field.ToggleFlag(fx, fy);
            Assert.True(field.IsFlagged(fx, fy), "the flag did not go down at all");

            var rows = Rows(AnsiText.StripEscapes(face.Render(field, 0, false)));

            Assert.Equal('¶', Middle(face, rows, fx, fy));
        }

        [Fact]
        public void TheLeftCounterCountsDownAsFlagsArePlanted()
        {
            var face = new MinesweeperFace(9, 9);
            var field = new Minefield(9, 9, 10, new Randomizer(4));

            Assert.Equal("010", Counter(face, field, 0));

            field.ToggleFlag(0, 0);
            field.ToggleFlag(1, 0);

            Assert.Equal("008", Counter(face, field, 0));
        }

        [Fact]
        public void OverFlaggingTakesTheCounterNegativeRatherThanSittingAtZero()
        {
            // It always did, and it is genuinely useful: a negative counter is the game telling a player they have
            // planted more flags than there are mines, which means one of them is wrong.
            var face = new MinesweeperFace(9, 9);
            var field = new Minefield(9, 9, 10, new Randomizer(4));

            for (var i = 0; i < 12; i++)
                field.ToggleFlag(i%9, i/9);

            Assert.StartsWith("-", Counter(face, field, 0), StringComparison.Ordinal);
        }

        [Fact]
        public void TheClockIsThreeDigitsAndStaysThreeDigits()
        {
            // Three digits is what the panel has room for, so a long game has to clamp rather than push the face
            // sideways - which would move the board with it.
            var face = new MinesweeperFace(9, 9);
            var field = new Minefield(9, 9, 10, new Randomizer(4));

            foreach (var seconds in new[] {0, 7, 999, 4000})
            {
                var rows = Rows(AnsiText.StripEscapes(face.Render(field, seconds, false)));
                Assert.Equal(face.Columns, rows[face.SmileyRow].Length);
            }

            var capped = Rows(AnsiText.StripEscapes(face.Render(field, 4000, false)));
            Assert.Contains("999", capped[face.SmileyRow], StringComparison.Ordinal);
        }

        [Fact]
        public void TheFaceKnowsHowTheGameIsGoing()
        {
            var face = new MinesweeperFace(9, 9);

            var playing = new Minefield(9, 9, 10, new Randomizer(4));
            Assert.Contains(":)", AnsiText.StripEscapes(face.Render(playing, 0, false)), StringComparison.Ordinal);

            var lost = new Minefield(9, 9, 10, new Randomizer(4));
            lost.Reveal(4, 4);
            for (var y = 0; y < 9 && !lost.IsOver; y++)
            for (var x = 0; x < 9 && !lost.IsOver; x++)
            {
                if (lost.IsMine(x, y))
                    lost.Reveal(x, y);
            }

            Assert.True(lost.HitMine);
            Assert.Contains(":(", AnsiText.StripEscapes(face.Render(lost, 0, false)), StringComparison.Ordinal);

            var won = new Minefield(9, 9, 10, new Randomizer(4));
            for (var y = 0; y < 9; y++)
            for (var x = 0; x < 9; x++)
            {
                if (!won.IsMine(x, y))
                    won.Reveal(x, y);
            }

            Assert.True(won.Won);
            Assert.Contains("B)", AnsiText.StripEscapes(face.Render(won, 0, false)), StringComparison.Ordinal);
        }

        [Fact]
        public void EveryMineIsShownOnceTheBoardIsLost()
        {
            var face = new MinesweeperFace(9, 9);
            var field = new Minefield(9, 9, 10, new Randomizer(4));
            field.Reveal(4, 4);

            for (var y = 0; y < 9 && !field.IsOver; y++)
            for (var x = 0; x < 9 && !field.IsOver; x++)
            {
                if (field.IsMine(x, y))
                    field.Reveal(x, y);
            }

            var rows = Rows(AnsiText.StripEscapes(face.Render(field, 0, false)));
            var shown = 0;

            for (var y = 0; y < 9; y++)
            for (var x = 0; x < 9; x++)
            {
                if (field.IsMine(x, y) && Middle(face, rows, x, y) == '*')
                    shown++;
            }

            Assert.Equal(field.MineCount, shown);
        }

        [Fact]
        public void ThePanelIsActuallyPaintedInSeveralColours()
        {
            // Counted rather than "does it contain an escape at all", which is nearly free and stays true after any
            // one of the styles has been flattened - it survived exactly that mutation. The panel is a highlight, a
            // shadow, a face, two readouts and a row of numbers, so half a dozen distinct sequences is the floor.
            var face = new MinesweeperFace(9, 9);
            var field = Played();
            field.Reveal(4, 4);

            Assert.True(DistinctSequences(face.Render(field, 42, false)) >= 6,
                "the panel came out in fewer colours than it has parts");
        }

        [Fact]
        public void TheBevelIsLitFromOneSide()
        {
            // The whole trick, asserted rather than eyeballed: the top edge and the bottom edge have to be drawn in
            // DIFFERENT colours or the panel is a flat grey rectangle with a border. Counting sequences is not
            // enough on its own - flattening one of the two leaves plenty of others behind, and it survived exactly
            // that.
            var face = new MinesweeperFace(9, 9);
            var rows = Rows(face.Render(Played(), 0, false));

            Assert.NotEqual(StyleAt(rows[rows.Length - 1], 0), StyleAt(rows[0], 0));
        }

        [Fact]
        public void TheMineThatWasSteppedOnIsMarkedApartFromTheRest()
        {
            // Losing opens every mine at once, so without the board remembering which one went off they would all
            // be drawn identically - and the one square that matters is the one you actually stood on.
            var face = new MinesweeperFace(9, 9);
            var field = new Minefield(9, 9, 10, new Randomizer(4));
            field.Reveal(4, 4);

            var (mx, my) = FirstMine(field);
            field.Reveal(mx, my);

            Assert.True(field.HitMine);
            Assert.Equal((mx, my), (field.HitX, field.HitY));

            var rows = Rows(face.Render(field, 0, false));
            var hit = StyledCell(face, rows, mx, my);

            var other = FirstMine(field, skipX: mx, skipY: my);
            var quiet = StyledCell(face, rows, other.X, other.Y);

            Assert.NotEqual(quiet, hit);
        }

        [Fact]
        public void AFlagThatTurnedOutToBeRightKeepsItsFlag()
        {
            var face = new MinesweeperFace(9, 9);
            var field = new Minefield(9, 9, 10, new Randomizer(4));
            field.Reveal(4, 4);

            var (mx, my) = FirstMine(field);
            field.ToggleFlag(mx, my);
            Assert.True(field.IsFlagged(mx, my), "the flag did not go down at all");

            var other = FirstMine(field, skipX: mx, skipY: my);
            field.Reveal(other.X, other.Y);
            Assert.True(field.IsOver);

            var rows = Rows(AnsiText.StripEscapes(face.Render(field, 0, false)));

            Assert.Equal('¶', Middle(face, rows, mx, my));
        }

        private static Minefield Played()
        {
            var field = new Minefield(9, 9, 10, new Randomizer(4));
            field.Reveal(4, 4);
            field.ToggleFlag(0, 0);
            return field;
        }

        private static string[] Rows(string render)
        {
            return render.Replace("\r\n", "\n").Split('\n');
        }

        /// <summary>The left-hand cell of a square, which is where the bevel or the grid line lives.</summary>
        private static char Glyph(MinesweeperFace face, string[] rows, int x, int y)
        {
            return rows[face.BoardOriginRow + y][face.BoardOriginColumn + x*MinesweeperFace.TileWidth];
        }

        /// <summary>The middle cell of a square, which is where anything drawn on it lives.</summary>
        private static char Middle(MinesweeperFace face, string[] rows, int x, int y)
        {
            return rows[face.BoardOriginRow + y]
                [face.BoardOriginColumn + x*MinesweeperFace.TileWidth + MinesweeperFace.TileWidth/2];
        }

        /// <summary>
        ///     The left-hand glyph of the first square in whichever state is asked for, found by asking the board
        ///     rather than by naming a square — a cascade reaches different squares on every seed.
        /// </summary>
        private static char FindSquare(MinesweeperFace face, string[] rows, Minefield field, bool revealed)
        {
            for (var y = 0; y < field.Height; y++)
            for (var x = 0; x < field.Width; x++)
            {
                if (field.IsRevealed(x, y) == revealed && !field.IsFlagged(x, y))
                    return Glyph(face, rows, x, y);
            }

            throw new InvalidOperationException("the board had no square in that state at all");
        }

        /// <summary>How many different escape sequences a render put out.</summary>
        private static int DistinctSequences(string render)
        {
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var at = render.IndexOf('');

            while (at >= 0)
            {
                var end = render.IndexOf('m', at);
                if (end < 0)
                    break;

                seen.Add(render.Substring(at, end - at + 1));
                at = render.IndexOf('', end);
            }

            return seen.Count;
        }

        /// <summary>Whatever escape sequence is in force at a visible column of a styled row.</summary>
        private static string StyleAt(string row, int column)
        {
            var visible = 0;
            var opened = string.Empty;

            for (var i = 0; i < row.Length; i++)
            {
                if (row[i] == '')
                {
                    var end = row.IndexOf('m', i);
                    if (end < 0)
                        break;

                    opened = row.Substring(i, end - i + 1);
                    i = end;
                    continue;
                }

                if (visible == column)
                    return opened;

                visible++;
            }

            return opened;
        }

        /// <summary>A square's middle cell together with whatever style was opened just before it.</summary>
        private static string StyledCell(MinesweeperFace face, string[] rows, int x, int y)
        {
            var row = rows[face.BoardOriginRow + y];
            var plain = AnsiText.StripEscapes(row);
            var column = face.BoardOriginColumn + x*MinesweeperFace.TileWidth + MinesweeperFace.TileWidth/2;

            // Walks the styled row counting only visible characters, so the answer is the escape run that is in
            // force at that cell plus the cell itself.
            var visible = 0;
            var opened = string.Empty;

            for (var i = 0; i < row.Length; i++)
            {
                if (row[i] == '')
                {
                    var end = row.IndexOf('m', i);
                    if (end < 0)
                        break;

                    opened = row.Substring(i, end - i + 1);
                    i = end;
                    continue;
                }

                if (visible == column)
                    return opened + row[i];

                visible++;
            }

            return plain[column].ToString();
        }

        /// <summary>The first mine on the board, optionally skipping one that has already been used.</summary>
        private static (int X, int Y) FirstMine(Minefield field, int skipX = -1, int skipY = -1)
        {
            for (var y = 0; y < field.Height; y++)
            for (var x = 0; x < field.Width; x++)
            {
                if (field.IsMine(x, y) && (x != skipX || y != skipY))
                    return (x, y);
            }

            throw new InvalidOperationException("the board had no mine to use");
        }

        /// <summary>The first square the board still has face down, which is the only one worth flagging.</summary>
        private static (int X, int Y) FirstHidden(Minefield field)
        {
            for (var y = 0; y < field.Height; y++)
            for (var x = 0; x < field.Width; x++)
            {
                if (!field.IsRevealed(x, y))
                    return (x, y);
            }

            throw new InvalidOperationException("the whole board was already open");
        }

        /// <summary>The three digits of the left-hand counter.</summary>
        private static string Counter(MinesweeperFace face, Minefield field, int seconds)
        {
            var rows = Rows(AnsiText.StripEscapes(face.Render(field, seconds, false)));
            return rows[face.SmileyRow].Substring(face.BoardOriginColumn, 3);
        }
    }
}

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
            var face = new MinesweeperFace(9, 9, true);
            var field = Played();

            foreach (var render in new[] {face.Render(field, 0), face.Render(field, 999)})
            {
                var rows = Rows(render);

                Assert.Equal(face.Rows, rows.Length);
                foreach (var row in rows)
                    Assert.Equal(face.Columns, AnsiText.VisibleLength(row));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WhereTheBoardIsDrawnIsWhereTheClickMapSaysItIs(bool labelled)
        {
            // THE invariant the mouse depends on, and it is not "the coordinates change nothing" — they legitimately
            // do, since the gutter costs a row and three columns. What has to hold is that the DRAWING and the MAP
            // move together, which they do by both taking the origin from the same two properties. Asserted for
            // every square in both modes: plant a flag, find it on the panel, and ask the map which square that cell
            // belongs to.
            var face = new MinesweeperFace(9, 9, labelled);
            var map = new MinesweeperBoardMap(face.BoardOriginRow, face.BoardOriginColumn, face.BoardWidth,
                face.BoardHeight, MinesweeperFace.TileWidth, MinesweeperFace.TileHeight);

            for (var y = 0; y < face.BoardHeight; y++)
            for (var x = 0; x < face.BoardWidth; x++)
            {
                var field = new Minefield(9, 9, 10, new Randomizer(4));
                field.ToggleFlag(x, y);

                var rows = Rows(AnsiText.StripEscapes(face.Render(field, 0)));
                var (foundColumn, foundRow) = FindGlyph(rows, '¶');

                Assert.True(foundRow >= 0, $"the flag on {x},{y} was not drawn at all");
                Assert.True(map.TryToSquare(foundRow, foundColumn, out var mappedX, out var mappedY),
                    $"the map does not think {foundColumn},{foundRow} is on the board");

                Assert.Equal((x, y), (mappedX, mappedY));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EveryClosedSquareIsACompleteFourSidedBox(bool labelled)
        {
            // THE point of the whole thing, and it took four wrong answers to get here: a box has a line above its
            // contents AND a line below them, which is three vertical positions, and a character cell offers one.
            // Every attempt to fit that into a single row per tile left an edge or a corner missing - rails, then
            // bands, then invisible white-on-silver, then an open corner. Two rows and a shared-edge lattice is the
            // floor, and this asserts the result square by square rather than trusting the drawing code.
            var face = new MinesweeperFace(9, 9, labelled);
            var rows = Rows(AnsiText.StripEscapes(face.Render(new Minefield(9, 9, 10, new Randomizer(4)), 0)));

            for (var y = 0; y < face.BoardHeight; y++)
            for (var x = 0; x < face.BoardWidth; x++)
            {
                var row = face.InteriorRow(y);
                var left = face.BoardOriginColumn + x*MinesweeperFace.TileWidth;
                var right = left + MinesweeperFace.TileWidth;

                Assert.Equal('│', rows[row][left]);
                Assert.Equal('│', rows[row][right]);

                for (var i = 1; i < MinesweeperFace.TileWidth; i++)
                {
                    Assert.Equal('─', rows[row - 1][left + i]);
                    Assert.Equal('─', rows[row + 1][left + i]);
                }
            }
        }

        [Fact]
        public void TheFieldsOwnCornersAreCorners()
        {
            // The outer frame comes out of the same rule as every inner line - anything off the board counts as
            // still closed - so there is no special case for it, and this is what says the rule reaches the edges.
            //
            // Asserted on a board with an EDGE SQUARE ALREADY OPEN, which is the only arrangement that can tell the
            // rule apart from an accident: while every edge square is closed the frame is drawn because the squares
            // themselves ask for it, and dropping the off-the-board rule entirely changes nothing. That mutation
            // survived until this line was added.
            var face = new MinesweeperFace(9, 9, false);
            var field = new Minefield(9, 9, 10, new Randomizer(4));
            field.Reveal(4, 4);

            var (edgeX, edgeY) = FirstOpenedOnTheEdge(field);
            var rows = Rows(AnsiText.StripEscapes(face.Render(field, 0)));

            if (edgeY == 0)
            {
                for (var i = 1; i < MinesweeperFace.TileWidth; i++)
                {
                    Assert.Equal('─',
                        rows[face.BoardOriginRow][face.BoardOriginColumn + edgeX*MinesweeperFace.TileWidth + i]);
                }
            }

            var top = face.BoardOriginRow;
            var bottom = top + face.BoardHeight*MinesweeperFace.TileHeight;
            var left = face.BoardOriginColumn;
            var right = left + face.BoardWidth*MinesweeperFace.TileWidth;

            Assert.Equal('┌', rows[top][left]);
            Assert.Equal('┐', rows[top][right]);
            Assert.Equal('└', rows[bottom][left]);
            Assert.Equal('┘', rows[bottom][right]);
        }

        [Fact]
        public void ACLearedRegionLosesItsLinesAndBecomesOneFlatExpanse()
        {
            // The other half of the lattice rule: a line is drawn only where a square beside it is still closed, so
            // an opened region reads as one flat area rather than as more squares - which is what the original did,
            // and what makes "boxed" and "closed" mean the same thing on screen.
            var face = new MinesweeperFace(9, 9, false);
            var field = new Minefield(9, 9, 10, new Randomizer(4));
            field.Reveal(4, 4);

            var opened = 0;
            for (var y = 0; y < 9; y++)
            for (var x = 0; x < 9; x++)
            {
                if (field.IsRevealed(x, y))
                    opened++;
            }

            Assert.True(opened > 4, "the opening did not cascade, so this proves nothing");

            var rows = Rows(AnsiText.StripEscapes(face.Render(field, 0)));
            var inside = 0;

            // Somewhere in the cleared region two opened squares are side by side, and the line between them has to
            // be gone.
            for (var y = 0; y < 9; y++)
            for (var x = 0; x + 1 < 9; x++)
            {
                if (!field.IsRevealed(x, y) || !field.IsRevealed(x + 1, y))
                    continue;

                inside++;
                var column = face.BoardOriginColumn + (x + 1)*MinesweeperFace.TileWidth;
                Assert.Equal(' ', rows[face.InteriorRow(y)][column]);
            }

            Assert.True(inside > 0, "no two opened squares were side by side, so this proves nothing");

            // And the same downward. Checking only one direction leaves the other rule untested, and a mutation that
            // drew every horizontal line everywhere survived exactly that gap.
            var stacked = 0;

            for (var y = 0; y + 1 < 9; y++)
            for (var x = 0; x < 9; x++)
            {
                if (!field.IsRevealed(x, y) || !field.IsRevealed(x, y + 1))
                    continue;

                stacked++;
                var row = face.InteriorRow(y) + 1;

                for (var i = 1; i < MinesweeperFace.TileWidth; i++)
                    Assert.Equal(' ', rows[row][face.BoardOriginColumn + x*MinesweeperFace.TileWidth + i]);
            }

            Assert.True(stacked > 0, "no two opened squares were stacked, so this proves nothing");
        }

        [Fact]
        public void AnUntouchedSquareIsRaisedAndAnOpenedOneIsFlat()
        {
            // Read with the escapes stripped, so this is about the GLYPHS. Raised, flat, flagged and mined have to
            // be four different shapes before they are four different colours, or the game stops working the moment
            // somebody sets NO_COLOR.
            var face = new MinesweeperFace(9, 9, true);
            var field = new Minefield(9, 9, 10, new Randomizer(4));
            field.Reveal(4, 4);

            var rows = Rows(AnsiText.StripEscapes(face.Render(field, 0)));

            // The highlight hairline just inside a closed square's left edge, which is where Windows put it and is
            // also the one mark that says "closed" without depending on the lines around it. That is a difference in
            // GLYPH rather than in shade, which is what keeps the board playable with no colour at all.
            var opened = Glyph(face, rows, 4, 4);
            var untouched = FindSquare(face, rows, field, revealed: false);

            Assert.Equal(' ', opened);
            Assert.Equal('▏', untouched);
        }

        [Fact]
        public void TheGridIsDrawnInShadowRatherThanInHighlight()
        {
            // Windows lights a button from the top left, so the faithful thing is a white top edge — except that
            // white is #FFFFFF against a #C0C0C0 face and there is almost nothing between them, while #808080
            // against the same face is unmistakable. Drawn "correctly", the vertical grooves came out crisp and the
            // horizontal highlights invisible, and the tiles ran together into strips exactly as if they had no
            // horizontal edges at all. Definition beats direction, and this is what says so.
            // An UNTOUCHED board, so the square being read is certainly still raised. Reading a played one is how
            // this test first failed to catch anything: the opening cascade had opened that corner, so the style at
            // that column was the flat face rather than a grid hairline and the comparison was about nothing.
            var face = new MinesweeperFace(9, 9, false);
            var rows = Rows(face.Render(new Minefield(9, 9, 10, new Randomizer(4)), 0));

            var panelTop = StyleAt(rows[face.SmileyRow - 1], face.BoardOriginColumn);
            var tileEdge = StyleAt(rows[face.BoardOriginRow], face.BoardOriginColumn);

            Assert.NotEqual(panelTop, tileEdge);
        }

        [Fact]
        public void AFlagIsDrawnOnTheSquareItWasPlantedOn()
        {
            // Flagged wherever the board still shows a face-down square, asked of the board rather than named.
            // Flagging an OPENED square is correctly a no-op, and the opening cascade reaches a different set of
            // squares on every seed - so a test that flags a fixed corner passes or fails on the shuffle. This one
            // did, on its first run, which is the third time this arcade has learned it.
            var face = new MinesweeperFace(9, 9, true);
            var field = new Minefield(9, 9, 10, new Randomizer(4));
            field.Reveal(4, 4);

            var (fx, fy) = FirstHidden(field);
            field.ToggleFlag(fx, fy);
            Assert.True(field.IsFlagged(fx, fy), "the flag did not go down at all");

            var rows = Rows(AnsiText.StripEscapes(face.Render(field, 0)));

            Assert.Equal('¶', Middle(face, rows, fx, fy));
        }

        [Fact]
        public void TheLeftCounterCountsDownAsFlagsArePlanted()
        {
            var face = new MinesweeperFace(9, 9, true);
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
            var face = new MinesweeperFace(9, 9, true);
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
            var face = new MinesweeperFace(9, 9, true);
            var field = new Minefield(9, 9, 10, new Randomizer(4));

            foreach (var seconds in new[] {0, 7, 999, 4000})
            {
                var rows = Rows(AnsiText.StripEscapes(face.Render(field, seconds)));
                Assert.Equal(face.Columns, rows[face.SmileyRow].Length);
            }

            var capped = Rows(AnsiText.StripEscapes(face.Render(field, 4000)));
            Assert.Contains("999", capped[face.SmileyRow], StringComparison.Ordinal);
        }

        [Fact]
        public void TheFaceKnowsHowTheGameIsGoing()
        {
            var face = new MinesweeperFace(9, 9, true);

            var playing = new Minefield(9, 9, 10, new Randomizer(4));
            Assert.Contains(":)", AnsiText.StripEscapes(face.Render(playing, 0)), StringComparison.Ordinal);

            var lost = new Minefield(9, 9, 10, new Randomizer(4));
            lost.Reveal(4, 4);
            for (var y = 0; y < 9 && !lost.IsOver; y++)
            for (var x = 0; x < 9 && !lost.IsOver; x++)
            {
                if (lost.IsMine(x, y))
                    lost.Reveal(x, y);
            }

            Assert.True(lost.HitMine);
            Assert.Contains(":(", AnsiText.StripEscapes(face.Render(lost, 0)), StringComparison.Ordinal);

            var won = new Minefield(9, 9, 10, new Randomizer(4));
            for (var y = 0; y < 9; y++)
            for (var x = 0; x < 9; x++)
            {
                if (!won.IsMine(x, y))
                    won.Reveal(x, y);
            }

            Assert.True(won.Won);
            Assert.Contains("B)", AnsiText.StripEscapes(face.Render(won, 0)), StringComparison.Ordinal);
        }

        [Fact]
        public void EveryMineIsShownOnceTheBoardIsLost()
        {
            var face = new MinesweeperFace(9, 9, true);
            var field = new Minefield(9, 9, 10, new Randomizer(4));
            field.Reveal(4, 4);

            for (var y = 0; y < 9 && !field.IsOver; y++)
            for (var x = 0; x < 9 && !field.IsOver; x++)
            {
                if (field.IsMine(x, y))
                    field.Reveal(x, y);
            }

            var rows = Rows(AnsiText.StripEscapes(face.Render(field, 0)));
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
            var face = new MinesweeperFace(9, 9, true);
            var field = Played();
            field.Reveal(4, 4);

            Assert.True(DistinctSequences(face.Render(field, 42)) >= 6,
                "the panel came out in fewer colours than it has parts");
        }

        [Fact]
        public void TheBevelIsLitFromOneSide()
        {
            // The whole trick, asserted rather than eyeballed: the top edge and the bottom edge have to be drawn in
            // DIFFERENT colours or the panel is a flat grey rectangle with a border. Counting sequences is not
            // enough on its own - flattening one of the two leaves plenty of others behind, and it survived exactly
            // that.
            var face = new MinesweeperFace(9, 9, true);
            var rows = Rows(face.Render(Played(), 0));

            // The panel's own top edge against its bottom edge - not row zero, which with the coordinates on is the
            // gutter and is deliberately unstyled.
            var top = rows[face.SmileyRow - 1];
            var bottom = rows[rows.Length - 1];

            Assert.NotEqual(StyleAt(bottom, face.BoardOriginColumn), StyleAt(top, face.BoardOriginColumn));
        }

        [Fact]
        public void TheMineThatWasSteppedOnIsMarkedApartFromTheRest()
        {
            // Losing opens every mine at once, so without the board remembering which one went off they would all
            // be drawn identically - and the one square that matters is the one you actually stood on.
            var face = new MinesweeperFace(9, 9, true);
            var field = new Minefield(9, 9, 10, new Randomizer(4));
            field.Reveal(4, 4);

            var (mx, my) = FirstMine(field);
            field.Reveal(mx, my);

            Assert.True(field.HitMine);
            Assert.Equal((mx, my), (field.HitX, field.HitY));

            var rows = Rows(face.Render(field, 0));
            var hit = StyledCell(face, rows, mx, my);

            var other = FirstMine(field, skipX: mx, skipY: my);
            var quiet = StyledCell(face, rows, other.X, other.Y);

            Assert.NotEqual(quiet, hit);
        }

        [Fact]
        public void AFlagThatTurnedOutToBeRightKeepsItsFlag()
        {
            var face = new MinesweeperFace(9, 9, true);
            var field = new Minefield(9, 9, 10, new Randomizer(4));
            field.Reveal(4, 4);

            var (mx, my) = FirstMine(field);
            field.ToggleFlag(mx, my);
            Assert.True(field.IsFlagged(mx, my), "the flag did not go down at all");

            var other = FirstMine(field, skipX: mx, skipY: my);
            field.Reveal(other.X, other.Y);
            Assert.True(field.IsOver);

            var rows = Rows(AnsiText.StripEscapes(face.Render(field, 0)));

            Assert.Equal('¶', Middle(face, rows, mx, my));
        }

        [Fact]
        public void AFaceNobodyIsPointingAtDrawsExactlyWhatItAlwaysDrew()
        {
            // The invariant that keeps every other test in this file meaning what it meant. Hovering was added
            // afterwards, so at its resting value it has to be as if it were never there - not "nearly", since the
            // bevel and palette tests below read exact cells and exact escape sequences.
            var face = new MinesweeperFace(9, 9, true);
            var field = Played();

            var before = face.Render(field, 0);

            face.HoveredX = 3;
            face.HoveredY = 2;
            face.Render(field, 0);

            face.HoveredX = -1;
            face.HoveredY = -1;

            Assert.Equal(before, face.Render(field, 0));
        }

        [Fact]
        public void PointingAtASquareChangesHowItIsDrawnAndNotWhatItSays()
        {
            // The rule the whole feature has to obey: a hover moves a STYLE and never a GLYPH. The closed-square
            // hairline is what both a reader and MinesweeperScreen use to tell a raised square from an opened one,
            // so a hover that dropped or replaced it would be changing what the board says rather than lighting it.
            var face = new MinesweeperFace(9, 9, true);
            var field = Played();

            var plain = face.Render(field, 0);

            face.HoveredX = 3;
            face.HoveredY = 2;
            var lit = face.Render(field, 0);

            Assert.NotEqual(plain, lit);
            Assert.Equal(AnsiText.StripEscapes(plain), AnsiText.StripEscapes(lit));
        }

        [Fact]
        public void OnlyTheSquareUnderThePointerIsLit()
        {
            // "Something changed" would pass for a version that repainted the whole panel. The change has to be one
            // square's own interior row, so every other row of the panel comes back untouched.
            var face = new MinesweeperFace(9, 9, true);
            var field = Played();

            var plain = Rows(face.Render(field, 0));

            face.HoveredX = 3;
            face.HoveredY = 2;
            var lit = Rows(face.Render(field, 0));

            var hoveredRow = face.InteriorRow(2);

            Assert.NotEqual(plain[hoveredRow], lit[hoveredRow]);

            for (var row = 0; row < plain.Length; row++)
            {
                if (row == hoveredRow)
                    continue;

                Assert.Equal(plain[row], lit[row]);
            }
        }

        [Fact]
        public void PointingOffTheBoardLightsNothing()
        {
            // The screen answers -1 for a pointer that is over the panel but not over a square - the counters, the
            // face, the border - and that has to be as quiet as never having pointed at all.
            var face = new MinesweeperFace(9, 9, true);
            var field = Played();

            var plain = face.Render(field, 0);

            face.HoveredX = 99;
            face.HoveredY = 99;

            Assert.Equal(plain, face.Render(field, 0));
        }

        [Fact]
        public void AFinishedBoardIsNotLitAtAll()
        {
            // A lit square says something is about to happen, and on a board that is over nothing is.
            var face = new MinesweeperFace(9, 9, true);
            var field = new Minefield(9, 9, 10, new Randomizer(4));
            field.Reveal(4, 4);

            for (var x = 0; x < 9 && !field.IsOver; x++)
            for (var y = 0; y < 9 && !field.IsOver; y++)
                field.Reveal(x, y);

            Assert.True(field.IsOver, "the board never finished, so this tests nothing");

            var plain = face.Render(field, 0);

            face.HoveredX = 3;
            face.HoveredY = 2;

            Assert.Equal(plain, face.Render(field, 0));
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

        /// <summary>The cell just inside a square's left edge, which is where the closed highlight lives.</summary>
        private static char Glyph(MinesweeperFace face, string[] rows, int x, int y)
        {
            return rows[face.InteriorRow(y)][face.InteriorColumn(x) - 1];
        }

        /// <summary>The middle cell of a square, which is where anything drawn on it lives.</summary>
        private static char Middle(MinesweeperFace face, string[] rows, int x, int y)
        {
            return rows[face.InteriorRow(y)][face.InteriorColumn(x)];
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

        /// <summary>Where a glyph is on the panel, or (-1, -1) when it is not there at all.</summary>
        private static (int Column, int Row) FindGlyph(string[] rows, char glyph)
        {
            for (var y = 0; y < rows.Length; y++)
            {
                var at = rows[y].IndexOf(glyph);
                if (at >= 0)
                    return (at, y);
            }

            return (-1, -1);
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
            var row = rows[face.InteriorRow(y)];
            var plain = AnsiText.StripEscapes(row);
            var column = face.InteriorColumn(x);

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

        /// <summary>The first opened square that sits against an edge of the board.</summary>
        private static (int X, int Y) FirstOpenedOnTheEdge(Minefield field)
        {
            for (var y = 0; y < field.Height; y++)
            for (var x = 0; x < field.Width; x++)
            {
                var onEdge = x == 0 || y == 0 || x == field.Width - 1 || y == field.Height - 1;
                if (onEdge && field.IsRevealed(x, y))
                    return (x, y);
            }

            throw new InvalidOperationException("the cascade never reached an edge, so this proves nothing");
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
            var rows = Rows(AnsiText.StripEscapes(face.Render(field, seconds)));
            return rows[face.SmileyRow].Substring(face.BoardOriginColumn, 3);
        }
    }
}

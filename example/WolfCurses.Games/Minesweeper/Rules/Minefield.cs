// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Collections.Generic;
using WolfCurses.Core;

namespace WolfCurses.Games.Minesweeper
{
    /// <summary>
    ///     The rules of minesweeper, with no screen attached. <see cref="MinesweeperDialog" /> is what draws it and
    ///     what parses "b4" into a pair of numbers.
    /// </summary>
    public sealed class Minefield
    {
        private readonly int[,] _adjacent;
        private readonly bool[,] _flagged;
        private readonly bool[,] _mine;
        private readonly Randomizer _random;
        private readonly bool[,] _revealed;

        /// <summary>
        ///     How many mines were actually buried. Equal to <see cref="MineCount" /> on any board with room for
        ///     them, and the win condition counts against this rather than the request: the nine squares kept clear
        ///     around the opening move come off the top, so a board asked for more mines than it can hold after that
        ///     would otherwise set itself a target no amount of play could reach.
        /// </summary>
        private int _minesBuried;

        private bool _minesPlaced;
        private int _revealedCount;

        /// <summary>Initializes a new instance of the <see cref="Minefield" /> class.</summary>
        /// <param name="width">Board width in squares.</param>
        /// <param name="height">Board height in squares.</param>
        /// <param name="mineCount">How many mines to bury once the first square is opened.</param>
        /// <param name="random">The simulation's shared random source.</param>
        public Minefield(int width, int height, int mineCount, Randomizer random)
        {
            if (width < 2 || height < 2)
                throw new ArgumentOutOfRangeException(nameof(width), "A board smaller than 2x2 is not a game.");
            if (mineCount < 1 || mineCount >= width*height)
                throw new ArgumentOutOfRangeException(nameof(mineCount),
                    "There must be at least one mine and at least one square that is not one.");

            Width = width;
            Height = height;
            MineCount = mineCount;
            _random = random ?? throw new ArgumentNullException(nameof(random));

            _mine = new bool[height, width];
            _revealed = new bool[height, width];
            _flagged = new bool[height, width];
            _adjacent = new int[height, width];
        }

        /// <summary>Board width in squares.</summary>
        public int Width { get; }

        /// <summary>Board height in squares.</summary>
        public int Height { get; }

        /// <summary>How many mines are buried.</summary>
        public int MineCount { get; }

        /// <summary>How many squares the player has flagged, which is what the mine counter counts down from.</summary>
        public int FlagsPlaced { get; private set; }

        /// <summary>True once the board is either cleared or blown up.</summary>
        public bool IsOver { get; private set; }

        /// <summary>True when the game ended because every square that is not a mine was opened.</summary>
        public bool Won { get; private set; }

        /// <summary>True when the game ended by opening a mine.</summary>
        public bool HitMine { get; private set; }

        /// <summary>
        ///     Which square the losing mine was on, or -1 before there is one.
        ///     <para>
        ///         Kept because a board has always shown that one differently from the mines that were merely
        ///         sitting there, and nothing else can work out which it was: losing opens <i>every</i> mine at
        ///         once, so by the time anything comes to draw the board they are all face up and identical.
        ///     </para>
        /// </summary>
        public int HitX { get; private set; } = -1;

        /// <summary>Which square the losing mine was on, or -1 before there is one.</summary>
        public int HitY { get; private set; } = -1;

        /// <summary>Whether that square has been opened.</summary>
        /// <param name="x">Column, counting from zero.</param>
        /// <param name="y">Row, counting from zero.</param>
        /// <returns>True when the square is face up.</returns>
        public bool IsRevealed(int x, int y) => _revealed[y, x];

        /// <summary>Whether the player has planted a flag on that square.</summary>
        /// <param name="x">Column, counting from zero.</param>
        /// <param name="y">Row, counting from zero.</param>
        /// <returns>True when the square carries a flag.</returns>
        public bool IsFlagged(int x, int y) => _flagged[y, x];

        /// <summary>Whether that square is a mine. Only meaningful to a caller that is drawing the end of a game.</summary>
        /// <param name="x">Column, counting from zero.</param>
        /// <param name="y">Row, counting from zero.</param>
        /// <returns>True when a mine is buried there.</returns>
        public bool IsMine(int x, int y) => _mine[y, x];

        /// <summary>How many of the eight neighbours are mines.</summary>
        /// <param name="x">Column, counting from zero.</param>
        /// <param name="y">Row, counting from zero.</param>
        /// <returns>The neighbour count, zero through eight.</returns>
        public int AdjacentMines(int x, int y) => _adjacent[y, x];

        /// <summary>Whether the coordinates name a square on this board.</summary>
        /// <param name="x">Column, counting from zero.</param>
        /// <param name="y">Row, counting from zero.</param>
        /// <returns>True when the square exists.</returns>
        public bool Contains(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        /// <summary>
        ///     Opens a square, cascading through the blanks around it. A flagged square is left alone — the flag is
        ///     the player saying "not this one", and honouring it is what makes flags worth planting.
        ///     <para>
        ///         <b>The mines are buried by the first call, not by the constructor</b>, and never within one square
        ///         of where that first click landed. Placing them up front would let the first click — made with no
        ///         information at all — lose the game, and every real implementation of this game avoids that. The
        ///         side effect is that the opening move always breaks into an empty region rather than a lone number,
        ///         which is also what makes the board playable rather than a guess.
        ///     </para>
        /// </summary>
        /// <param name="x">Column, counting from zero.</param>
        /// <param name="y">Row, counting from zero.</param>
        public void Reveal(int x, int y)
        {
            if (IsOver || !Contains(x, y) || _revealed[y, x] || _flagged[y, x])
                return;

            if (!_minesPlaced)
                PlaceMines(x, y);

            if (_mine[y, x])
            {
                _revealed[y, x] = true;
                IsOver = true;
                HitMine = true;
                HitX = x;
                HitY = y;
                RevealEveryMine();
                return;
            }

            // Iterative rather than recursive: a board of blanks is a stack frame per square, and while a 9x9 will
            // never come close, the depth is bounded by the board and not by anything sensible.
            var pending = new Stack<(int X, int Y)>();
            pending.Push((x, y));

            while (pending.Count > 0)
            {
                var (cx, cy) = pending.Pop();
                if (_revealed[cy, cx] || _flagged[cy, cx] || _mine[cy, cx])
                    continue;

                _revealed[cy, cx] = true;
                _revealedCount++;

                if (_adjacent[cy, cx] != 0)
                    continue;

                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    var nx = cx + dx;
                    var ny = cy + dy;
                    if (Contains(nx, ny) && !_revealed[ny, nx])
                        pending.Push((nx, ny));
                }
            }

            if (_revealedCount < Width*Height - _minesBuried)
                return;

            IsOver = true;
            Won = true;
        }

        /// <summary>Plants or pulls a flag. Does nothing to a square that is already open.</summary>
        /// <param name="x">Column, counting from zero.</param>
        /// <param name="y">Row, counting from zero.</param>
        public void ToggleFlag(int x, int y)
        {
            if (IsOver || !Contains(x, y) || _revealed[y, x])
                return;

            _flagged[y, x] = !_flagged[y, x];
            FlagsPlaced += _flagged[y, x] ? 1 : -1;
        }

        /// <summary>
        ///     Buries the mines anywhere but the nine squares around the opening move, then counts each square's
        ///     neighbours once so drawing never has to.
        /// </summary>
        /// <param name="safeX">Column of the first square opened.</param>
        /// <param name="safeY">Row of the first square opened.</param>
        private void PlaceMines(int safeX, int safeY)
        {
            _minesPlaced = true;

            var candidates = new List<(int X, int Y)>(Width*Height);
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                if (Math.Abs(x - safeX) <= 1 && Math.Abs(y - safeY) <= 1)
                    continue;

                candidates.Add((x, y));
            }

            // A partial Fisher-Yates: swap a random survivor to the front, take it, repeat. Drawing without
            // replacement, so the same square can never be picked twice — the bug a "pick a random square, retry if
            // it already has a mine" loop hides until the board is dense.
            _minesBuried = Math.Min(MineCount, candidates.Count);
            for (var i = 0; i < _minesBuried; i++)
            {
                var pick = i + _random.Next(candidates.Count - i);
                (candidates[i], candidates[pick]) = (candidates[pick], candidates[i]);

                var (mx, my) = candidates[i];
                _mine[my, mx] = true;
            }

            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                _adjacent[y, x] = CountNeighbouringMines(x, y);
        }

        /// <summary>How many mines sit in the eight squares around this one.</summary>
        /// <param name="x">Column, counting from zero.</param>
        /// <param name="y">Row, counting from zero.</param>
        /// <returns>The neighbour count, zero through eight.</returns>
        private int CountNeighbouringMines(int x, int y)
        {
            var count = 0;
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                if (Contains(x + dx, y + dy) && _mine[y + dy, x + dx])
                    count++;
            }

            return count;
        }

        /// <summary>Turns every mine face up, which is what a lost board looks like.</summary>
        private void RevealEveryMine()
        {
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                if (_mine[y, x])
                    _revealed[y, x] = true;
            }
        }
    }
}

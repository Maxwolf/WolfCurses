// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Collections.Generic;
using WolfCurses.Core;

namespace WolfCurses.Games.Snake
{
    /// <summary>
    ///     The rules of snake, and nothing else. No console, no window, no form: it is handed a size and a random
    ///     source, and answers questions about where things are. <see cref="SnakeDialog" /> is what draws it.
    ///     <para>
    ///         Keeping the game separate from the screen is worth the extra file. It means the interesting half can be
    ///         exercised without a terminal — walked through a scripted set of moves and asserted on — which is the
    ///         same reason the library keeps <c>MenuLayout</c> and <c>FileDialogListing</c> apart from the things that
    ///         draw them.
    ///     </para>
    /// </summary>
    public sealed class SnakeBoard
    {
        /// <summary>How many segments the snake starts with, laid out along the middle row.</summary>
        private const int StartingLength = 4;

        /// <summary>Where the snake is, head first. A list rather than a queue because drawing walks all of it.</summary>
        private readonly List<(int X, int Y)> _body = new();

        private readonly Randomizer _random;

        /// <summary>
        ///     The direction the next <see cref="Advance" /> will use. Held apart from <see cref="Direction" /> because
        ///     of the oldest bug in this game — see <see cref="Steer" />.
        /// </summary>
        private DirectionEnum _pending;

        /// <summary>Initializes a new instance of the <see cref="SnakeBoard" /> class and lays out the opening snake.</summary>
        /// <param name="width">Playfield width in cells.</param>
        /// <param name="height">Playfield height in cells.</param>
        /// <param name="random">The simulation's shared random source.</param>
        public SnakeBoard(int width, int height, Randomizer random)
        {
            if (width < 8 || height < 6)
                throw new ArgumentOutOfRangeException(nameof(width), "A playfield smaller than 8x6 has no room to play in.");

            Width = width;
            Height = height;
            _random = random ?? throw new ArgumentNullException(nameof(random));

            // Facing right, along the middle, with the head far enough from the wall to react.
            var row = height / 2;
            for (var i = 0; i < StartingLength; i++)
                _body.Add((StartingLength - i, row));

            Direction = DirectionEnum.Right;
            _pending = DirectionEnum.Right;
            PlaceFood();
        }

        /// <summary>Playfield width in cells.</summary>
        public int Width { get; }

        /// <summary>Playfield height in cells.</summary>
        public int Height { get; }

        /// <summary>How much food has been eaten, which is also how far the snake has grown past its start.</summary>
        public int Score { get; private set; }

        /// <summary>True once the snake has hit a wall or itself, or has filled the board.</summary>
        public bool IsOver { get; private set; }

        /// <summary>True when the game ended by filling every cell rather than by crashing.</summary>
        public bool Won { get; private set; }

        /// <summary>Where the next piece of food is sitting.</summary>
        public (int X, int Y) Food { get; private set; }

        /// <summary>The direction actually travelled on the last <see cref="Advance" />.</summary>
        public DirectionEnum Direction { get; private set; }

        /// <summary>The whole snake, head first.</summary>
        public IReadOnlyList<(int X, int Y)> Body => _body;

        /// <summary>
        ///     Points the snake somewhere for its next move. A reversal is ignored rather than fatal.
        ///     <para>
        ///         <b>The comparison is against <see cref="Direction" /> — the direction last actually travelled — and
        ///         not against <see cref="_pending" />, which is the whole point of there being two fields.</b> Keys
        ///         arrive far faster than the snake moves, so a player travelling right who taps up and then left
        ///         inside one move has, if each key is merely checked against the one before it, just steered into
        ///         their own neck: up is legal after right, and left is legal after up, but the snake never travelled
        ///         up. Checking every key against the last move actually made rejects that second key and the snake
        ///         survives, at the cost of one input in a situation the player did not really mean.
        ///     </para>
        /// </summary>
        /// <param name="direction">Where the player wants to go.</param>
        public void Steer(DirectionEnum direction)
        {
            if (direction == DirectionEnum.None || IsOver)
                return;

            if (direction == Opposite(Direction))
                return;

            _pending = direction;
        }

        /// <summary>
        ///     Moves the snake one cell, eating, growing, and ending the game as required. Does nothing once the game
        ///     is over, so a caller that keeps ticking is not a special case.
        /// </summary>
        public void Advance()
        {
            if (IsOver)
                return;

            Direction = _pending;

            var (dx, dy) = Direction switch
            {
                DirectionEnum.Up => (0, -1),
                DirectionEnum.Down => (0, 1),
                DirectionEnum.Left => (-1, 0),
                _ => (1, 0)
            };

            var head = _body[0];
            var next = (X: head.X + dx, Y: head.Y + dy);

            if (next.X < 0 || next.X >= Width || next.Y < 0 || next.Y >= Height)
            {
                IsOver = true;
                return;
            }

            // The tail cell is about to be vacated, so running into it is legal unless the snake is about to grow
            // into it. Checking the whole body first and then correcting for that is how the "chasing your own tail
            // is fine" rule everybody expects actually gets implemented.
            var eating = next == Food;
            var lastIndex = _body.Count - 1;
            for (var i = 0; i < _body.Count; i++)
            {
                if (i == lastIndex && !eating)
                    break;

                if (_body[i] != next)
                    continue;

                IsOver = true;
                return;
            }

            _body.Insert(0, next);

            if (!eating)
            {
                _body.RemoveAt(_body.Count - 1);
                return;
            }

            Score++;

            if (_body.Count >= Width*Height)
            {
                IsOver = true;
                Won = true;
                return;
            }

            PlaceFood();
        }

        /// <summary>Which way is backwards from here.</summary>
        private static DirectionEnum Opposite(DirectionEnum direction)
        {
            return direction switch
            {
                DirectionEnum.Up => DirectionEnum.Down,
                DirectionEnum.Down => DirectionEnum.Up,
                DirectionEnum.Left => DirectionEnum.Right,
                DirectionEnum.Right => DirectionEnum.Left,
                _ => DirectionEnum.None
            };
        }

        /// <summary>
        ///     Drops food on a cell the snake is not occupying.
        ///     <para>
        ///         Guessing at random is right for almost the whole game and hopeless at the end of it, where a long
        ///         snake leaves a handful of free cells and a guess almost always lands on the snake. So it guesses a
        ///         bounded number of times and then counts the free cells and takes one directly, which cannot fail
        ///         and cannot loop. Rejection sampling with no bound is how this function hangs on a nearly-won game.
        ///     </para>
        /// </summary>
        private void PlaceFood()
        {
            const int guesses = 64;
            for (var attempt = 0; attempt < guesses; attempt++)
            {
                var candidate = (_random.Next(Width), _random.Next(Height));
                if (!_body.Contains(candidate))
                {
                    Food = candidate;
                    return;
                }
            }

            var free = new List<(int X, int Y)>();
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                if (!_body.Contains((x, y)))
                    free.Add((x, y));
            }

            // Only reachable while at least one cell is free: Advance ends the game before calling this when the
            // snake has filled the board.
            Food = free[_random.Next(free.Count)];
        }
    }
}

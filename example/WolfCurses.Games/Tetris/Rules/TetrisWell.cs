// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;
using System.Collections.Generic;
using WolfCurses.Core;

namespace WolfCurses.Games.Tetris
{
    /// <summary>
    ///     The rules of tetris, with no screen attached. <see cref="TetrisDialog" /> is what draws it.
    /// </summary>
    public sealed class TetrisWell
    {
        /// <summary>What a clear is worth, by how many rows went at once, before the level multiplier.</summary>
        private static readonly int[] _lineScores = {0, 100, 300, 500, 800};

        /// <summary>
        ///     How far sideways or up a rotation is allowed to shove the piece to make itself fit, tried in order.
        ///     <para>
        ///         This is a <b>simplified</b> wall kick, not the Super Rotation System's table — that one is
        ///         five offsets per piece per rotation pair, and the difference only shows up in the corner cases
        ///         competitive players build their technique on. What this gets right is the part everybody notices:
        ///         a piece turning against a wall or against the stack slides clear instead of refusing to turn.
        ///     </para>
        /// </summary>
        private static readonly (int X, int Y)[] _kicks =
        {
            (0, 0), (-1, 0), (1, 0), (-2, 0), (2, 0), (0, -1)
        };

        /// <summary>What has landed. Null is an empty cell; anything else is the piece that left it there.</summary>
        private readonly TetrominoEnum?[,] _settled;

        private readonly Randomizer _random;

        /// <summary>
        ///     The pieces still to come out of the current bag. See <see cref="TakeFromBag" /> for why there is a bag
        ///     at all rather than a die.
        /// </summary>
        private readonly Queue<TetrominoEnum> _bag = new();

        /// <summary>Initializes a new instance of the <see cref="TetrisWell" /> class and starts the first piece.</summary>
        /// <param name="width">Well width in cells.</param>
        /// <param name="height">Well height in cells.</param>
        /// <param name="random">The simulation's shared random source.</param>
        public TetrisWell(int width, int height, Randomizer random)
        {
            if (width < 6 || height < 8)
                throw new ArgumentOutOfRangeException(nameof(width), "A well smaller than 6x8 cannot hold a piece.");

            Width = width;
            Height = height;
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _settled = new TetrominoEnum?[height, width];

            Level = 1;
            NextKind = TakeFromBag();
            Spawn();
        }

        /// <summary>Well width in cells.</summary>
        public int Width { get; }

        /// <summary>Well height in cells.</summary>
        public int Height { get; }

        /// <summary>The piece under the player's control, or null once the game is over.</summary>
        public Tetromino Active { get; private set; }

        /// <summary>Where the active piece's box sits, as a column.</summary>
        public int ActiveX { get; private set; }

        /// <summary>Where the active piece's box sits, as a row.</summary>
        public int ActiveY { get; private set; }

        /// <summary>The piece the preview panel is showing.</summary>
        public TetrominoEnum NextKind { get; private set; }

        /// <summary>Points earned.</summary>
        public int Score { get; private set; }

        /// <summary>Rows cleared, which is also what drives <see cref="Level" />.</summary>
        public int Lines { get; private set; }

        /// <summary>How fast things are falling, counting from one.</summary>
        public int Level { get; private set; }

        /// <summary>True once a new piece had nowhere to appear.</summary>
        public bool IsOver { get; private set; }

        /// <summary>What has settled in that cell, or null when it is empty.</summary>
        /// <param name="x">Column, counting from zero.</param>
        /// <param name="y">Row, counting from zero.</param>
        /// <returns>The piece that landed there, or null.</returns>
        public TetrominoEnum? SettledAt(int x, int y) => _settled[y, x];

        /// <summary>
        ///     The row the active piece's box would come to rest on if it were dropped now, which is what the ghost
        ///     outline is drawn at. Equal to <see cref="ActiveY" /> when the piece is already resting.
        /// </summary>
        public int GhostY
        {
            get
            {
                if (Active == null)
                    return ActiveY;

                var y = ActiveY;
                while (Fits(Active, ActiveX, y + 1))
                    y++;

                return y;
            }
        }

        /// <summary>How long the current level lets a piece hang before gravity takes it down one row.</summary>
        public TimeSpan FallInterval =>
            TimeSpan.FromMilliseconds(Math.Max(70, 800 - (Level - 1)*65));

        /// <summary>Shifts the active piece one column, if there is room.</summary>
        /// <param name="dx">How far to shift; negative is left.</param>
        /// <returns>True when the piece moved.</returns>
        public bool Move(int dx)
        {
            if (IsOver || !Fits(Active, ActiveX + dx, ActiveY))
                return false;

            ActiveX += dx;
            return true;
        }

        /// <summary>
        ///     Turns the active piece a quarter turn, shoving it sideways or up if that is what it takes to fit.
        /// </summary>
        /// <param name="clockwise">True for a quarter turn clockwise.</param>
        /// <returns>True when the piece turned.</returns>
        public bool Rotate(bool clockwise)
        {
            if (IsOver)
                return false;

            var turned = Active.Rotate(clockwise);
            foreach (var (kx, ky) in _kicks)
            {
                if (!Fits(turned, ActiveX + kx, ActiveY + ky))
                    continue;

                Active = turned;
                ActiveX += kx;
                ActiveY += ky;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Takes the piece down one row, locking it where it stands if it cannot go. The player's soft drop and
        ///     gravity's own tick are the same move; only the scoring differs, which is why the caller says which it
        ///     was.
        /// </summary>
        /// <param name="scored">True when the player asked for this, which is worth a point a row.</param>
        /// <returns>True when the piece fell, false when it locked instead.</returns>
        public bool Drop(bool scored)
        {
            if (IsOver)
                return false;

            if (!Fits(Active, ActiveX, ActiveY + 1))
            {
                Lock();
                return false;
            }

            ActiveY++;
            if (scored)
                Score++;

            return true;
        }

        /// <summary>Slams the active piece straight down and locks it, worth two points a row.</summary>
        /// <returns>How many rows it fell.</returns>
        public int HardDrop()
        {
            if (IsOver)
                return 0;

            var fallen = 0;
            while (Fits(Active, ActiveX, ActiveY + 1))
            {
                ActiveY++;
                fallen++;
            }

            Score += fallen*2;
            Lock();
            return fallen;
        }

        /// <summary>Whether a piece would sit legally at that spot: on the board, and on nothing already there.</summary>
        /// <param name="piece">The piece to try.</param>
        /// <param name="px">The column its box would start at.</param>
        /// <param name="py">The row its box would start at.</param>
        /// <returns>True when it fits.</returns>
        private bool Fits(Tetromino piece, int px, int py)
        {
            if (piece == null)
                return false;

            for (var y = 0; y < piece.Size; y++)
            for (var x = 0; x < piece.Size; x++)
            {
                if (!piece.IsFilled(x, y))
                    continue;

                var wx = px + x;
                var wy = py + y;

                if (wx < 0 || wx >= Width || wy < 0 || wy >= Height)
                    return false;

                if (_settled[wy, wx] != null)
                    return false;
            }

            return true;
        }

        /// <summary>Writes the active piece into the well, clears whatever rows that completed, and brings the next one.</summary>
        private void Lock()
        {
            for (var y = 0; y < Active.Size; y++)
            for (var x = 0; x < Active.Size; x++)
            {
                if (Active.IsFilled(x, y))
                    _settled[ActiveY + y, ActiveX + x] = Active.Kind;
            }

            var cleared = ClearFullRows();
            if (cleared > 0)
            {
                Lines += cleared;
                Score += _lineScores[cleared]*Level;
                Level = 1 + Lines/10;
            }

            Spawn();
        }

        /// <summary>
        ///     Removes every full row and drops what was above it.
        ///     <para>
        ///         Walking from the bottom upward and only moving the write cursor when a row survives collapses the
        ///         whole thing into one pass, and — the part worth having — handles rows that are not next to each
        ///         other without a special case, which a "find a full row, shift everything, start again" loop gets
        ///         wrong precisely when four rows go at once.
        ///     </para>
        /// </summary>
        /// <returns>How many rows went.</returns>
        private int ClearFullRows()
        {
            var write = Height - 1;

            for (var read = Height - 1; read >= 0; read--)
            {
                var full = true;
                for (var x = 0; x < Width; x++)
                {
                    if (_settled[read, x] != null)
                        continue;

                    full = false;
                    break;
                }

                if (full)
                    continue;

                if (write != read)
                {
                    for (var x = 0; x < Width; x++)
                        _settled[write, x] = _settled[read, x];
                }

                write--;
            }

            var cleared = write + 1;
            for (var y = write; y >= 0; y--)
            for (var x = 0; x < Width; x++)
                _settled[y, x] = null;

            return cleared;
        }

        /// <summary>Brings the previewed piece into play at the top, and ends the game if it has nowhere to go.</summary>
        private void Spawn()
        {
            var piece = Tetromino.Create(NextKind);
            NextKind = TakeFromBag();

            var x = (Width - piece.Size)/2;
            if (!Fits(piece, x, 0))
            {
                Active = null;
                IsOver = true;
                return;
            }

            Active = piece;
            ActiveX = x;
            ActiveY = 0;
        }

        /// <summary>
        ///     The next piece, dealt from a shuffled bag of all seven rather than picked at random.
        ///     <para>
        ///         Rolling a seven-sided die every time is the obvious implementation and is the one that makes the
        ///         game feel unfair: it can hand out four S pieces in a row, and it can withhold the I piece for
        ///         thirty pieces at a stretch, which is a loss the player cannot play around. Dealing from a shuffled
        ///         bag gives the same seven every seven pieces in a different order each time, so the longest possible
        ///         wait for any piece is bounded — this is what every version since about 2001 does, and it is four
        ///         lines.
        ///     </para>
        /// </summary>
        /// <returns>The next piece to preview.</returns>
        private TetrominoEnum TakeFromBag()
        {
            if (_bag.Count == 0)
            {
                var kinds = (TetrominoEnum[]) Enum.GetValues(typeof (TetrominoEnum));
                for (var i = kinds.Length - 1; i > 0; i--)
                {
                    var j = _random.Next(i + 1);
                    (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
                }

                foreach (var kind in kinds)
                    _bag.Enqueue(kind);
            }

            return _bag.Dequeue();
        }
    }
}

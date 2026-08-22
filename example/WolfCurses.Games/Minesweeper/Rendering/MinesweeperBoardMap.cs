// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.Minesweeper
{
    /// <summary>
    ///     Where the field landed in the frame the player is looking at, so a click can be turned back into a
    ///     square.
    ///     <para>
    ///         <b>It is arithmetic rather than hit-testing</b> — because the board is drawn as <i>characters</i>,
    ///         every square occupies an exact, known rectangle of cells and a click is a division. That is the quiet
    ///         argument for keeping this game's board out of a <c>PixelBuffer</c>: a sixel or kitty picture is drawn
    ///         by the terminal against a cell size the renderer only <i>assumes</i> and the terminal never confirms,
    ///         so Missile Command has to force half blocks the moment its mouse is switched on. Here there is
    ///         nothing to force and nothing to be wrong about.
    ///     </para>
    ///     <para>
    ///         <b>The same answer serves both a click and a hover</b>, which is worth knowing before anybody
    ///         optimises one of them: a pointer moving reports one event for every cell it crosses, so this is asked
    ///         far more often than it was written for. It stays a division and holds no state, so the cost is the
    ///         same either way; what the screen does with the answer is where the redraw is skipped.
    ///     </para>
    ///     <para>
    ///         It is rebuilt every time the screen is composed rather than worked out once, for the same reason
    ///         Missile Command rebuilds its own: the row the panel starts on depends on how many lines the status
    ///         above it took, and a constant written down here would be quietly wrong the first time that changed.
    ///     </para>
    /// </summary>
    public readonly struct MinesweeperBoardMap
    {
        /// <summary>Initializes a new instance of the <see cref="MinesweeperBoardMap" /> struct.</summary>
        /// <param name="originRow">Which screen row the top row of squares is drawn on.</param>
        /// <param name="originColumn">Which screen column the left edge of the first square is drawn on.</param>
        /// <param name="width">How many squares across.</param>
        /// <param name="height">How many squares down.</param>
        /// <param name="tileWidth">How many columns one square advances, its shared side line included.</param>
        /// <param name="tileHeight">How many rows one square advances, its shared top line included.</param>
        public MinesweeperBoardMap(int originRow, int originColumn, int width, int height, int tileWidth,
            int tileHeight)
        {
            OriginRow = originRow;
            OriginColumn = originColumn;
            Width = width;
            Height = height;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
        }

        /// <summary>Which screen row the top row of squares is drawn on.</summary>
        public int OriginRow { get; }

        /// <summary>Which screen column the left edge of the first square is drawn on.</summary>
        public int OriginColumn { get; }

        /// <summary>How many squares across.</summary>
        public int Width { get; }

        /// <summary>How many squares down.</summary>
        public int Height { get; }

        /// <summary>How many columns one square advances, its shared side line included.</summary>
        public int TileWidth { get; }

        /// <summary>How many rows one square advances, its shared top line included.</summary>
        public int TileHeight { get; }

        /// <summary>Whether this map describes a board at all.</summary>
        public bool IsUsable => Width > 0 && Height > 0 && TileWidth > 0 && TileHeight > 0;

        /// <summary>
        ///     Turns a clicked cell into a square, or reports that the click missed the field.
        /// </summary>
        /// <param name="row">The clicked row, counted from the top of the window.</param>
        /// <param name="column">The clicked column, counted from the left of the window.</param>
        /// <param name="x">The square's column, counting from zero.</param>
        /// <param name="y">The square's row, counting from zero.</param>
        /// <returns>True when the click landed on a square.</returns>
        public bool TryToSquare(int row, int column, out int x, out int y)
        {
            x = 0;
            y = 0;

            if (!IsUsable)
                return false;

            var cy = row - OriginRow;
            var cx = column - OriginColumn;

            // Refused rather than clamped. A click on the chrome — the counters, the smiley, the panel edge — is not
            // a near miss on the nearest square, and treating it as one would open a corner every time somebody
            // reached for the frame.
            if (cx < 0 || cy < 0)
                return false;

            // The subtraction is what puts a click on a shared LINE onto the square above or left of it rather than
            // below or right. Every line belongs to two squares, so it has to be given to one of them on purpose;
            // leaving it out instead makes the whole top and left frame of the field open the wrong square.
            var tileX = (cx - 1)/TileWidth;
            var tileY = (cy - 1)/TileHeight;

            if (tileX < 0 || tileY < 0 || tileX >= Width || tileY >= Height)
                return false;

            x = tileX;
            y = tileY;
            return true;
        }
    }
}

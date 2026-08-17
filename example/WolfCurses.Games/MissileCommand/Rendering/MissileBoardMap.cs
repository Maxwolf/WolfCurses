// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses.Games.MissileCommand
{
    /// <summary>
    ///     Where the board landed on screen last frame, and how to turn a character cell back into a world position.
    ///     <para>
    ///         <b>Recorded when the frame is composed, never recomputed when a click arrives.</b> The board's shape
    ///         changes for three separate reasons — the terminal being resized, TAB flipping between the picture and
    ///         the characters, and the picture becoming legible or illegible as rows come and go — and a click is
    ///         answering the frame the player was actually looking at. Recomputing at click time silently aims at a
    ///         board that may no longer exist.
    ///     </para>
    ///     <para>
    ///         <b>The two boards do not share a mapping, and the difference is half a cell.</b>
    ///         <see cref="MissileFieldText" /> point-samples: it asks which cell a world position rounds to, so cell
    ///         <c>c</c> of <c>n</c> means exactly <c>c / (n - 1)</c> of the way across and the last cell is the far
    ///         edge. The picture is area-averaged on its way down to character cells, so a cell covers a band and its
    ///         <i>centre</i> is the honest answer — <c>(c + 0.5) / n</c>. Using either formula for both is wrong by
    ///         half a cell, which at eighteen rows is about forty per cent of a blast radius: every shot lands
    ///         slightly high and it reads as being bad at the game rather than as a bug.
    ///     </para>
    /// </summary>
    public readonly struct MissileBoardMap
    {
        private MissileBoardMap(int originRow, int originColumn, int columns, int rows, bool areaSampled)
        {
            OriginRow = originRow;
            OriginColumn = originColumn;
            Columns = columns;
            Rows = rows;
            AreaSampled = areaSampled;
        }

        /// <summary>The screen row the top of the board is drawn on.</summary>
        public int OriginRow { get; }

        /// <summary>The screen column the left of the board is drawn at.</summary>
        public int OriginColumn { get; }

        /// <summary>How many character columns the board covers.</summary>
        public int Columns { get; }

        /// <summary>How many character rows the board covers.</summary>
        public int Rows { get; }

        /// <summary>True for the picture, whose cells are averages of a band; false for the character grid.</summary>
        public bool AreaSampled { get; }

        /// <summary>True when this map describes a board big enough to aim at.</summary>
        public bool IsUsable => Columns > 1 && Rows > 1;

        /// <summary>Records where the character board was drawn.</summary>
        /// <param name="originRow">The screen row its first row occupies.</param>
        /// <param name="originColumn">The screen column its first column occupies.</param>
        /// <param name="columns">How many columns it covers.</param>
        /// <param name="rows">How many rows it covers.</param>
        /// <returns>The map.</returns>
        public static MissileBoardMap ForCharacters(int originRow, int originColumn, int columns, int rows)
        {
            return new MissileBoardMap(originRow, originColumn, columns, rows, false);
        }

        /// <summary>Records where the picture board was drawn, measured from what the renderer actually returned.</summary>
        /// <param name="originRow">The screen row its first row occupies.</param>
        /// <param name="originColumn">The screen column its first column occupies.</param>
        /// <param name="columns">How many columns it covers.</param>
        /// <param name="rows">How many rows it covers.</param>
        /// <returns>The map.</returns>
        public static MissileBoardMap ForPicture(int originRow, int originColumn, int columns, int rows)
        {
            return new MissileBoardMap(originRow, originColumn, columns, rows, true);
        }

        /// <summary>
        ///     Turns a screen cell into a world position, or answers false when that cell is not on the board at all.
        ///     <para>
        ///         Refusing rather than clamping is the point: a click on the status line above the board or on the
        ///         message below it must not be treated as a shot at the top or bottom of the sky.
        ///     </para>
        /// </summary>
        /// <param name="row">Screen row of the click.</param>
        /// <param name="column">Screen column of the click.</param>
        /// <param name="worldX">Where that lands in world units.</param>
        /// <param name="worldY">Where that lands in world units.</param>
        /// <returns>TRUE when the cell was on the board.</returns>
        public bool TryToWorld(int row, int column, out double worldX, out double worldY)
        {
            worldX = 0.0;
            worldY = 0.0;

            if (!IsUsable)
                return false;

            var cx = column - OriginColumn;
            var cy = row - OriginRow;
            if (cx < 0 || cx >= Columns || cy < 0 || cy >= Rows)
                return false;

            if (AreaSampled)
            {
                worldX = (cx + 0.5)/Columns*MissileField.Aspect;
                worldY = 1.0 - (cy + 0.5)/Rows;
                return true;
            }

            // The exact algebraic inverse of MissileFieldText's forward map. Note the minus one: dividing by Columns
            // instead would make the rightmost cell unreachable and pull every shot slightly left.
            worldX = (double) cx/(Columns - 1)*MissileField.Aspect;
            worldY = 1.0 - (double) cy/(Rows - 1);
            return true;
        }
    }
}

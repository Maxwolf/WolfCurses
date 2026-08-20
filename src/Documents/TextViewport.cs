// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;

namespace WolfCurses.Documents
{
    /// <summary>
    ///     The window a screen shows onto a document that is bigger than it: which line and column the top-left cell
    ///     is, and the arithmetic for keeping the caret inside, turning a mouse click back into a position, and
    ///     working out where a position lands on screen.
    ///     <para>
    ///         This is the part everyone writes by hand and gets subtly wrong. The library already learned the same
    ///         lesson about grids, where <c>TextGrid.Render(originX, originY, columns, rows)</c> became the camera a
    ///         maze bigger than the terminal needed; this is that idea for text, where the cells are characters of a
    ///         <see cref="TextBuffer" /> rather than a plotted grid.
    ///     </para>
    ///     <para>
    ///         Pure arithmetic, no console. It does not hold the document either, so the two numbers that depend on
    ///         the document's size (how far down you may scroll, and whether a position exists) are passed in by the
    ///         caller who has it. That keeps a viewport usable over anything with lines, not only a
    ///         <see cref="TextBuffer" />.
    ///     </para>
    /// </summary>
    public sealed class TextViewport
    {
        /// <summary>Initializes a viewport of the given size, parked at the top-left of the document.</summary>
        /// <param name="width">Visible columns; at least one.</param>
        /// <param name="height">Visible rows; at least one.</param>
        public TextViewport(int width = 1, int height = 1)
        {
            Resize(width, height);
        }

        /// <summary>The document line drawn on the viewport's first row.</summary>
        public int FirstLine { get; private set; }

        /// <summary>
        ///     The column drawn in the viewport's first cell, for horizontal scrolling.
        ///     <para>
        ///         <b>A screen column, not a character index.</b> The two are the same until a line contains a tab,
        ///         after which they part company and only one of them scrolls evenly: stepping sideways by character
        ///         index over an indented line would jump the view by a tab's width at a time. Callers translate with
        ///         <see cref="TabStops" />, which is also what keeps this class free of the document.
        ///     </para>
        /// </summary>
        public int FirstColumn { get; private set; }

        /// <summary>How many columns are visible; at least one.</summary>
        public int Width { get; private set; }

        /// <summary>How many rows are visible; at least one.</summary>
        public int Height { get; private set; }

        /// <summary>The document line one past the last visible one.</summary>
        public int LastLineExclusive => FirstLine + Height;

        /// <summary>
        ///     Changes the visible size. Zero or negative becomes one, because a viewport with no rows has no
        ///     arithmetic that means anything and a headless host really does report zero.
        /// </summary>
        /// <param name="width">Visible columns.</param>
        /// <param name="height">Visible rows.</param>
        public void Resize(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
        }

        /// <summary>Scrolls to an absolute origin, never above or left of the document's start.</summary>
        /// <param name="firstLine">The line to put on the first row.</param>
        /// <param name="firstColumn">The column to put in the first cell.</param>
        public void ScrollTo(int firstLine, int firstColumn)
        {
            FirstLine = Math.Max(0, firstLine);
            FirstColumn = Math.Max(0, firstColumn);
        }

        /// <summary>Scrolls by a delta, never above or left of the document's start.</summary>
        /// <param name="lines">Rows to move down; negative moves up.</param>
        /// <param name="columns">Columns to move right; negative moves left.</param>
        public void ScrollBy(int lines, int columns)
        {
            ScrollTo(FirstLine + lines, FirstColumn + columns);
        }

        /// <summary>
        ///     Pulls the origin back so the last screenful of a document is the furthest you can scroll.
        ///     <para>
        ///         Deliberately allows the final screen to be shown in full rather than stopping when the last line
        ///         reaches the bottom row: without this, PageDown at the end of a long document walks the origin off
        ///         into empty space and the text scrolls away entirely, which is the classic version of this bug.
        ///     </para>
        /// </summary>
        /// <param name="lineCount">How many lines the document has.</param>
        public void ClampToDocument(int lineCount)
        {
            FirstLine = Math.Clamp(FirstLine, 0, Math.Max(0, lineCount - Height));
        }

        /// <summary>
        ///     Scrolls the least amount that brings a position inside the window, and reports whether it had to move.
        ///     This is what every caret movement calls, so the caret is never off screen after a key press.
        /// </summary>
        /// <param name="position">The position to reveal, usually the caret.</param>
        /// <returns>TRUE when the origin changed, which a caller can use to skip redrawing.</returns>
        public bool EnsureVisible(TextPosition position)
        {
            var line = FirstLine;
            var column = FirstColumn;

            if (position.Line < FirstLine)
                line = position.Line;
            else if (position.Line >= FirstLine + Height)
                line = position.Line - Height + 1;

            if (position.Column < FirstColumn)
                column = position.Column;
            else if (position.Column >= FirstColumn + Width)
                column = position.Column - Width + 1;

            if (line == FirstLine && column == FirstColumn)
                return false;

            ScrollTo(line, column);
            return true;
        }

        /// <summary>
        ///     Turns a cell inside the viewport into a document position, which is the whole of a mouse hit test.
        ///     The result is not clamped to the document, because the viewport does not have it; hand the result to
        ///     <see cref="TextBuffer.Clamp" /> and clicking past the last line lands on the last line, which is what
        ///     a person expects.
        /// </summary>
        /// <param name="row">Row within the viewport, zero at the top.</param>
        /// <param name="column">Column within the viewport, zero at the left.</param>
        /// <returns>The document position under that cell.</returns>
        public TextPosition ToDocument(int row, int column)
        {
            return new TextPosition(FirstLine + row, FirstColumn + column);
        }

        /// <summary>
        ///     Where a document position falls inside the viewport, when it falls inside it at all. The false return
        ///     is the useful half: a caret scrolled out of view has no cell to draw in, and a renderer that assumes
        ///     one paints the cursor onto whatever row happens to be there.
        /// </summary>
        /// <param name="position">The document position.</param>
        /// <param name="row">Row within the viewport.</param>
        /// <param name="column">Column within the viewport.</param>
        /// <returns>TRUE when the position is currently visible.</returns>
        public bool TryToScreen(TextPosition position, out int row, out int column)
        {
            row = position.Line - FirstLine;
            column = position.Column - FirstColumn;

            return row >= 0 && row < Height && column >= 0 && column < Width;
        }
    }
}

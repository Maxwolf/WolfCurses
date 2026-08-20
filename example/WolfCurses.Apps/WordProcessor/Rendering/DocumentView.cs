// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using WolfCurses.Documents;
using WolfCurses.Graphics;

namespace WolfCurses.Apps.WordProcessor
{
    /// <summary>
    ///     Draws the visible part of a document: the lines the viewport is over, with the selection and the caret
    ///     marked. Pure, taking a buffer and a viewport and returning rows, so what the editor looks like can be
    ///     asserted without a console.
    ///     <para>
    ///         <b>Every row comes back exactly the viewport's width.</b> That is what lets the frame around it stay a
    ///         rectangle and what makes the blue field cover the whole page rather than stopping after each line's
    ///         last word. It is the same rectangle invariant <c>TextGrid</c> keeps, and for the same reason.
    ///     </para>
    ///     <para>
    ///         <b>The caret is drawn rather than placed.</b> The library parks the terminal's real cursor at the end
    ///         of the input prompt, so there is no cursor to put inside the document. The caret is a cell in the
    ///         highlight style instead, which is what a block cursor looks like anyway; with a selection it merges
    ///         into the highlighted run, which is also what a terminal editor does.
    ///     </para>
    /// </summary>
    internal static class DocumentView
    {
        /// <summary>Renders the visible rows of a document, each padded to the viewport's exact width.</summary>
        /// <param name="buffer">The document.</param>
        /// <param name="viewport">The window onto it.</param>
        /// <param name="field">How ordinary text is painted.</param>
        /// <param name="highlight">How the caret and the selection are painted.</param>
        /// <param name="showCaret">FALSE while something else owns the cursor, such as an open menu.</param>
        /// <returns>One string per viewport row.</returns>
        public static string[] Render(TextBuffer buffer, TextViewport viewport, TextStyle field, TextStyle highlight,
            bool showCaret = true)
        {
            var rows = new string[viewport.Height];

            for (var row = 0; row < viewport.Height; row++)
            {
                var lineIndex = viewport.FirstLine + row;

                rows[row] = lineIndex < buffer.LineCount
                    ? RenderLine(buffer, viewport, lineIndex, field, highlight, showCaret)
                    : field.Apply(new string(' ', viewport.Width));
            }

            return rows;
        }

        /// <summary>Renders one document line, clipped to the viewport and padded back out to its full width.</summary>
        private static string RenderLine(TextBuffer buffer, TextViewport viewport, int lineIndex, TextStyle field,
            TextStyle highlight, bool showCaret)
        {
            var stored = buffer.GetLine(lineIndex);
            var (documentStart, documentEnd) = HighlightRange(buffer, lineIndex, showCaret);

            // A highlight is a range of characters and the line is drawn in screen columns, which stop being the
            // same thing as soon as the line contains a tab. Translating the range as well as expanding the text is
            // the half that is easy to forget, and forgetting it puts the caret on the wrong character of exactly
            // the indented lines somebody is most likely to be editing.
            var highlightStart = TabStops.ToDisplayColumn(stored, documentStart, buffer.TabWidth);
            var highlightEnd = TabStops.ToDisplayColumn(stored, documentEnd, buffer.TabWidth);
            var line = TabStops.Expand(stored, buffer.TabWidth);

            // Padded to cover the whole row before anything is clipped, so the caret has a cell to sit in past the
            // end of the text and the field colour reaches the frame on the right.
            var needed = Math.Max(viewport.FirstColumn + viewport.Width, highlightEnd);
            if (line.Length < needed)
                line = line.PadRight(needed);

            var visible = line.Substring(viewport.FirstColumn, viewport.Width);

            var from = Math.Clamp(highlightStart - viewport.FirstColumn, 0, visible.Length);
            var to = Math.Clamp(highlightEnd - viewport.FirstColumn, 0, visible.Length);

            if (to <= from)
                return field.Apply(visible);

            return field.Apply(visible.Substring(0, from)) +
                   highlight.Apply(visible.Substring(from, to - from)) +
                   field.Apply(visible.Substring(to));
        }

        /// <summary>
        ///     Which columns of a line are highlighted: the part of the selection that falls on it, or the single
        ///     cell under the caret when nothing is selected. An empty range means nothing on this line is marked.
        /// </summary>
        private static (int Start, int End) HighlightRange(TextBuffer buffer, int lineIndex, bool showCaret)
        {
            if (buffer.HasSelection)
            {
                var start = buffer.SelectionStart;
                var end = buffer.SelectionEnd;

                if (lineIndex < start.Line || lineIndex > end.Line)
                    return (0, 0);

                var from = lineIndex == start.Line ? start.Column : 0;

                // A line wholly inside the selection is highlighted one column past its text, which is how the
                // selected line break shows: without it a run of selected lines looks like a ragged right edge and
                // there is no sign the newlines are going too.
                var to = lineIndex == end.Line ? end.Column : buffer.GetLine(lineIndex).Length + 1;

                return (from, to);
            }

            if (!showCaret || lineIndex != buffer.Caret.Line)
                return (0, 0);

            return (buffer.Caret.Column, buffer.Caret.Column + 1);
        }
    }
}

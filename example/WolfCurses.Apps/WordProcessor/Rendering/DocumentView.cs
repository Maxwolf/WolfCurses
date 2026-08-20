// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Text;
using WolfCurses.Documents;
using WolfCurses.Window.Control;

namespace WolfCurses.Apps.WordProcessor
{
    /// <summary>
    ///     Draws the visible part of a document: the lines the viewport is over, with the selection and the caret in
    ///     inverse video. Pure, taking a buffer and a viewport and returning a string, so what the editor looks like
    ///     can be asserted without a console.
    ///     <para>
    ///         <b>The caret is drawn rather than placed.</b> The library parks the terminal's real cursor at the end
    ///         of the input prompt, which is right for a prompt and means there is no cursor to put inside the
    ///         document. So the caret is a cell in inverse video, which is what a block cursor looks like anyway.
    ///         With no selection it is exactly one cell; with a selection it merges into the highlighted run, which
    ///         is also what a terminal editor does.
    ///     </para>
    ///     <para>
    ///         Emphasis goes through <see cref="ListNavigator.Emphasize" />, the same gate every highlight in the
    ///         library uses, so <c>NO_COLOR</c> and a forced colour mode of none reach this screen too and the
    ///         output degrades to plain text rather than to escape sequences nobody asked for.
    ///     </para>
    ///     <para>
    ///         <b>Known limitation: tabs are drawn as they arrive.</b> A tab is one character to the document model
    ///         and several columns to the terminal, so a file containing them renders with the caret column out of
    ///         step with where the cursor appears. None of the shipped samples contain a tab. Fixing it properly
    ///         means an expansion layer between document columns and screen columns, which is a real feature and
    ///         belongs in the library beside <see cref="TextViewport" /> rather than being faked here.
    ///     </para>
    /// </summary>
    internal static class DocumentView
    {
        /// <summary>Renders the visible rows of a document.</summary>
        /// <param name="buffer">The document.</param>
        /// <param name="viewport">The window onto it.</param>
        /// <param name="showCaret">FALSE while something else owns the cursor, such as an open dialog.</param>
        /// <returns>One line of text per viewport row, newline separated.</returns>
        public static string Render(TextBuffer buffer, TextViewport viewport, bool showCaret = true)
        {
            var sb = new StringBuilder();

            for (var row = 0; row < viewport.Height; row++)
            {
                var lineIndex = viewport.FirstLine + row;

                if (lineIndex < buffer.LineCount)
                    sb.Append(RenderLine(buffer, viewport, lineIndex, showCaret));

                sb.Append(Environment.NewLine);
            }

            return sb.ToString();
        }

        /// <summary>Renders one document line, clipped to the viewport and with any highlight applied.</summary>
        /// <param name="buffer">The document.</param>
        /// <param name="viewport">The window onto it.</param>
        /// <param name="lineIndex">Which document line to draw.</param>
        /// <param name="showCaret">Whether the caret should be drawn.</param>
        /// <returns>The drawn line.</returns>
        private static string RenderLine(TextBuffer buffer, TextViewport viewport, int lineIndex, bool showCaret)
        {
            var line = buffer.GetLine(lineIndex);
            var (highlightStart, highlightEnd) = HighlightRange(buffer, lineIndex, showCaret);

            // The caret and the end of a selected line both sit one past the last character, where there is nothing
            // to put in inverse video. Pad so the highlight has a cell to live in; the padding is trimmed off again
            // by the clip below whenever it is not needed.
            if (highlightEnd > line.Length)
                line = line.PadRight(highlightEnd);

            var visible = Clip(line, viewport.FirstColumn, viewport.Width);
            if (highlightEnd <= highlightStart)
                return visible.TrimEnd();

            var from = Math.Clamp(highlightStart - viewport.FirstColumn, 0, visible.Length);
            var to = Math.Clamp(highlightEnd - viewport.FirstColumn, 0, visible.Length);
            if (to <= from)
                return visible.TrimEnd();

            return visible.Substring(0, from) +
                   ListNavigator.Emphasize(visible.Substring(from, to - from)) +
                   visible.Substring(to).TrimEnd();
        }

        /// <summary>
        ///     Which columns of a line are highlighted: the part of the selection that falls on it, or the single
        ///     cell under the caret when nothing is selected. An empty range means nothing on this line is marked.
        /// </summary>
        /// <param name="buffer">The document.</param>
        /// <param name="lineIndex">The line being drawn.</param>
        /// <param name="showCaret">Whether the caret counts as a highlight.</param>
        /// <returns>The half-open column range to emphasize.</returns>
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

        /// <summary>Takes the visible window out of a line, tolerating a start past its end.</summary>
        /// <param name="line">The whole line.</param>
        /// <param name="start">First visible column.</param>
        /// <param name="width">How many columns are visible.</param>
        /// <returns>The visible slice, which may be empty.</returns>
        private static string Clip(string line, int start, int width)
        {
            if (start >= line.Length)
                return string.Empty;

            var available = line.Length - start;
            return line.Substring(start, Math.Min(width, available));
        }
    }
}

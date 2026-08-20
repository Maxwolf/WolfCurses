// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Collections.Generic;
using System.Text;

namespace WolfCurses.Documents
{
    /// <summary>
    ///     An editable document: lines of text, a caret in them, and an optional selection. This is the thing the
    ///     library had no answer for. <see cref="Core.InputManager" />'s buffer is append-only and has no caret by
    ///     design, which is right for a command prompt and useless for anything you can move around inside, so every
    ///     editor-shaped screen would otherwise start by writing this.
    ///     <para>
    ///         Pure state and arithmetic, like <see cref="Window.Control.ListNavigator" /> and for the same reason:
    ///         it never touches a console, never renders, and knows nothing about keys, so it is unit-tested directly
    ///         and a caller is free to drive it from a keyboard, a mouse, a script or a test.
    ///     </para>
    ///     <para>
    ///         <b>The buffer owns the caret</b>, which makes the common case small: one document, one cursor, one
    ///         object to ask. The cost is that two views onto the same document cannot have separate cursors, so a
    ///         split-pane editor would want the caret lifted out. Nothing needs that yet and the shape is easy to
    ///         change later; the alternative today is every caller carrying a caret beside a buffer and keeping the
    ///         two in step by hand, which is exactly the bookkeeping this exists to remove.
    ///     </para>
    ///     <para>
    ///         <b>There is no undo here yet.</b> Undo is a stack of edits rather than a property of the text, and
    ///         bolting it on badly (snapshot the whole document per keystroke) is worse than not having it.
    ///     </para>
    /// </summary>
    public sealed class TextBuffer
    {
        /// <summary>The lines. Never empty: an empty document is one empty line, so there is always somewhere to be.</summary>
        private readonly List<string> _lines = new() {string.Empty};

        /// <summary>Where the selection was started, or null when there is no selection.</summary>
        private TextPosition? _anchor;

        /// <summary>
        ///     The column vertical movement is trying to get back to.
        ///     <para>
        ///         <b>This is the detail that separates an editor from a toy.</b> Walk the caret down from column 40
        ///         through a short line and back, and without this it comes to rest wherever the short line ended,
        ///         having quietly forgotten where it was. Real editors remember the column you were in and return to
        ///         it as soon as a line is long enough, so vertical movement over ragged text is reversible. Every
        ///         horizontal move and every edit re-aims it at wherever the caret actually is; vertical moves read
        ///         it and never write it.
        ///     </para>
        /// </summary>
        private int _desiredColumn;

        /// <summary>Initializes an empty document.</summary>
        public TextBuffer()
        {
            NewLine = Environment.NewLine;
        }

        /// <summary>The caret. Always a real place in the document, never past the end of its line.</summary>
        public TextPosition Caret { get; private set; }

        /// <summary>How many lines the document has; at least one.</summary>
        public int LineCount => _lines.Count;

        /// <summary>The lines, for a renderer to walk without copying the document.</summary>
        public IReadOnlyList<string> Lines => _lines;

        /// <summary>
        ///     The line ending this document was loaded with, and the one <see cref="GetText" /> writes back.
        ///     Remembered rather than normalized so that opening a file and saving it unchanged really does produce
        ///     the same bytes, which is the difference between an editor and a reformatter.
        /// </summary>
        public string NewLine { get; private set; }

        /// <summary>Whether the document has been edited since it was loaded or last marked saved.</summary>
        public bool IsModified { get; private set; }

        /// <summary>
        ///     Columns between tab stops for this document, which is what a renderer needs to know to draw its lines
        ///     and place its caret. It lives here rather than on the viewport because it is a property of the file
        ///     being edited (what its author indented with) rather than of the window looking at it, and because
        ///     this is where the lines are; the arithmetic itself is <see cref="TabStops" />.
        /// </summary>
        public int TabWidth { get; set; } = TabStops.DefaultWidth;

        /// <summary>Whether any text is selected.</summary>
        public bool HasSelection => _anchor.HasValue && _anchor.Value != Caret;

        /// <summary>The earlier end of the selection in reading order, or the caret when there is no selection.</summary>
        public TextPosition SelectionStart => HasSelection && _anchor.Value < Caret ? _anchor.Value : Caret;

        /// <summary>The later end of the selection in reading order, or the caret when there is no selection.</summary>
        public TextPosition SelectionEnd => HasSelection && _anchor.Value > Caret ? _anchor.Value : Caret;

        /// <summary>Builds a document from text, splitting on any of CRLF, LF or CR.</summary>
        /// <param name="text">The text to load; null is an empty document.</param>
        /// <returns>The loaded buffer, unmodified, with the caret at the start.</returns>
        public static TextBuffer FromText(string text)
        {
            var buffer = new TextBuffer();
            buffer.SetText(text);
            return buffer;
        }

        /// <summary>Whether a character counts as part of a word, for word movement and double-click selection.</summary>
        /// <param name="character">The character to test.</param>
        /// <returns>TRUE for letters, digits and the underscore.</returns>
        public static bool IsWordCharacter(char character)
        {
            return char.IsLetterOrDigit(character) || character == '_';
        }

        /// <summary>
        ///     Replaces the whole document, resets the caret and selection, and clears the modified flag. The line
        ///     ending is taken from whatever dominates the incoming text, so a CRLF file stays a CRLF file.
        /// </summary>
        /// <param name="text">The text to load; null is an empty document.</param>
        public void SetText(string text)
        {
            text ??= string.Empty;

            NewLine = DetectNewLine(text);

            _lines.Clear();
            _lines.AddRange(SplitLines(text));

            Caret = TextPosition.Start;
            _anchor = null;
            _desiredColumn = 0;
            IsModified = false;
        }

        /// <summary>The whole document as text, joined with the line ending it was loaded with.</summary>
        /// <returns>The document text.</returns>
        public string GetText()
        {
            return string.Join(NewLine, _lines);
        }

        /// <summary>One line, or empty for an index outside the document.</summary>
        /// <param name="index">The zero-based line index.</param>
        /// <returns>The line's text.</returns>
        public string GetLine(int index)
        {
            return index >= 0 && index < _lines.Count ? _lines[index] : string.Empty;
        }

        /// <summary>The selected text, or empty when nothing is selected.</summary>
        /// <returns>The selection as text.</returns>
        public string GetSelectedText()
        {
            if (!HasSelection)
                return string.Empty;

            var start = SelectionStart;
            var end = SelectionEnd;

            if (start.Line == end.Line)
                return _lines[start.Line].Substring(start.Column, end.Column - start.Column);

            var sb = new StringBuilder();
            sb.Append(_lines[start.Line].Substring(start.Column));
            for (var line = start.Line + 1; line < end.Line; line++)
                sb.Append(NewLine).Append(_lines[line]);

            return sb.Append(NewLine).Append(_lines[end.Line].Substring(0, end.Column)).ToString();
        }

        /// <summary>Declares the document saved, so <see cref="IsModified" /> reads false again.</summary>
        public void MarkSaved()
        {
            IsModified = false;
        }

        /// <summary>Pulls a position inside the document: a real line, and a column no further than that line's end.</summary>
        /// <param name="position">The position to clamp.</param>
        /// <returns>A position that really exists.</returns>
        public TextPosition Clamp(TextPosition position)
        {
            var line = Math.Clamp(position.Line, 0, _lines.Count - 1);
            return new TextPosition(line, Math.Clamp(position.Column, 0, _lines[line].Length));
        }

        /// <summary>The position one past the very last character.</summary>
        /// <returns>The end of the document.</returns>
        public TextPosition EndPosition()
        {
            return new TextPosition(_lines.Count - 1, _lines[_lines.Count - 1].Length);
        }

        /// <summary>Drops the selection, leaving the caret where it is.</summary>
        public void ClearSelection()
        {
            _anchor = null;
        }

        /// <summary>Selects the whole document, leaving the caret at the end of it.</summary>
        public void SelectAll()
        {
            _anchor = TextPosition.Start;
            Caret = EndPosition();
            _desiredColumn = Caret.Column;
        }

        /// <summary>
        ///     Moves the caret somewhere, optionally dragging a selection behind it. This is the one place a caller
        ///     names a position, so every other movement method is a way of working one out.
        /// </summary>
        /// <param name="position">Where to put the caret; clamped into the document.</param>
        /// <param name="extendSelection">TRUE to keep the selection anchor and select up to the new position.</param>
        public void MoveTo(TextPosition position, bool extendSelection = false)
        {
            SetCaret(Clamp(position), extendSelection, false);
        }

        /// <summary>One character left, stepping onto the end of the previous line at a line start.</summary>
        /// <param name="extendSelection">TRUE to select over the move.</param>
        public void MoveLeft(bool extendSelection = false)
        {
            var caret = Caret;
            if (caret.Column > 0)
                SetCaret(new TextPosition(caret.Line, caret.Column - 1), extendSelection, false);
            else if (caret.Line > 0)
                SetCaret(new TextPosition(caret.Line - 1, _lines[caret.Line - 1].Length), extendSelection, false);
        }

        /// <summary>One character right, stepping onto the start of the next line at a line end.</summary>
        /// <param name="extendSelection">TRUE to select over the move.</param>
        public void MoveRight(bool extendSelection = false)
        {
            var caret = Caret;
            if (caret.Column < _lines[caret.Line].Length)
                SetCaret(new TextPosition(caret.Line, caret.Column + 1), extendSelection, false);
            else if (caret.Line < _lines.Count - 1)
                SetCaret(new TextPosition(caret.Line + 1, 0), extendSelection, false);
        }

        /// <summary>Up some number of lines, returning to the column vertical movement is aiming for.</summary>
        /// <param name="lines">How many lines to rise; one by default.</param>
        /// <param name="extendSelection">TRUE to select over the move.</param>
        public void MoveUp(int lines = 1, bool extendSelection = false)
        {
            MoveVertically(-lines, extendSelection);
        }

        /// <summary>Down some number of lines, returning to the column vertical movement is aiming for.</summary>
        /// <param name="lines">How many lines to fall; one by default.</param>
        /// <param name="extendSelection">TRUE to select over the move.</param>
        public void MoveDown(int lines = 1, bool extendSelection = false)
        {
            MoveVertically(lines, extendSelection);
        }

        /// <summary>To the first column of the current line.</summary>
        /// <param name="extendSelection">TRUE to select over the move.</param>
        public void MoveToLineStart(bool extendSelection = false)
        {
            SetCaret(new TextPosition(Caret.Line, 0), extendSelection, false);
        }

        /// <summary>To just past the last character of the current line.</summary>
        /// <param name="extendSelection">TRUE to select over the move.</param>
        public void MoveToLineEnd(bool extendSelection = false)
        {
            SetCaret(new TextPosition(Caret.Line, _lines[Caret.Line].Length), extendSelection, false);
        }

        /// <summary>To the very start of the document.</summary>
        /// <param name="extendSelection">TRUE to select over the move.</param>
        public void MoveToStart(bool extendSelection = false)
        {
            SetCaret(TextPosition.Start, extendSelection, false);
        }

        /// <summary>To the very end of the document.</summary>
        /// <param name="extendSelection">TRUE to select over the move.</param>
        public void MoveToEnd(bool extendSelection = false)
        {
            SetCaret(EndPosition(), extendSelection, false);
        }

        /// <summary>
        ///     Back to the start of the word the caret is in, or of the previous one when it is already there. Steps
        ///     over the run of separators first, so repeated presses walk word to word rather than stalling on the
        ///     spaces between them.
        /// </summary>
        /// <param name="extendSelection">TRUE to select over the move.</param>
        public void MoveWordLeft(bool extendSelection = false)
        {
            var caret = Caret;
            if (caret.Column == 0)
            {
                if (caret.Line > 0)
                    SetCaret(new TextPosition(caret.Line - 1, _lines[caret.Line - 1].Length), extendSelection, false);

                return;
            }

            var line = _lines[caret.Line];
            var column = caret.Column;
            while (column > 0 && !IsWordCharacter(line[column - 1]))
                column--;

            while (column > 0 && IsWordCharacter(line[column - 1]))
                column--;

            SetCaret(new TextPosition(caret.Line, column), extendSelection, false);
        }

        /// <summary>Forward to the start of the next word: over the rest of this one, then over the separators after it.</summary>
        /// <param name="extendSelection">TRUE to select over the move.</param>
        public void MoveWordRight(bool extendSelection = false)
        {
            var caret = Caret;
            var line = _lines[caret.Line];
            if (caret.Column >= line.Length)
            {
                if (caret.Line < _lines.Count - 1)
                    SetCaret(new TextPosition(caret.Line + 1, 0), extendSelection, false);

                return;
            }

            var column = caret.Column;
            while (column < line.Length && IsWordCharacter(line[column]))
                column++;

            while (column < line.Length && !IsWordCharacter(line[column]))
                column++;

            SetCaret(new TextPosition(caret.Line, column), extendSelection, false);
        }

        /// <summary>
        ///     Selects the word around a position, which is what a double-click means. A position on a separator
        ///     selects just that one character rather than the whitespace run, so double-clicking a gap between words
        ///     does something small and obvious instead of swallowing the layout.
        /// </summary>
        /// <param name="position">Where the click landed.</param>
        public void SelectWordAt(TextPosition position)
        {
            var at = Clamp(position);
            var line = _lines[at.Line];

            if (line.Length == 0)
            {
                MoveTo(at);
                return;
            }

            // A caret sitting just past the last character belongs to the character before it, or clicking off the
            // end of a line would never select anything.
            var index = Math.Min(at.Column, line.Length - 1);

            if (!IsWordCharacter(line[index]))
            {
                _anchor = new TextPosition(at.Line, index);
                SetCaret(new TextPosition(at.Line, index + 1), true, false);
                return;
            }

            var start = index;
            while (start > 0 && IsWordCharacter(line[start - 1]))
                start--;

            var end = index;
            while (end < line.Length && IsWordCharacter(line[end]))
                end++;

            _anchor = new TextPosition(at.Line, start);
            SetCaret(new TextPosition(at.Line, end), true, false);
        }

        /// <summary>
        ///     Selects a whole line, which is what a triple-click means. The selection runs to the start of the next
        ///     line rather than to this line's end, so deleting it takes the line break with it and the lines below
        ///     really do move up.
        /// </summary>
        /// <param name="lineIndex">The line to select.</param>
        public void SelectLine(int lineIndex)
        {
            var line = Math.Clamp(lineIndex, 0, _lines.Count - 1);
            _anchor = new TextPosition(line, 0);

            var end = line < _lines.Count - 1
                ? new TextPosition(line + 1, 0)
                : new TextPosition(line, _lines[line].Length);

            SetCaret(end, true, false);
        }

        /// <summary>Types one character, replacing the selection if there is one.</summary>
        /// <param name="character">The character to insert.</param>
        public void Insert(char character)
        {
            Insert(character.ToString());
        }

        /// <summary>
        ///     Inserts text at the caret, replacing the selection if there is one. Newlines inside the text are
        ///     honoured, which is what makes this the paste path as well as the typing one.
        /// </summary>
        /// <param name="text">The text to insert; null or empty does nothing.</param>
        public void Insert(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            DeleteSelection();

            var caret = Caret;
            var line = _lines[caret.Line];
            var before = line.Substring(0, caret.Column);
            var after = line.Substring(caret.Column);

            var pieces = SplitLines(text);
            if (pieces.Count == 1)
            {
                _lines[caret.Line] = before + pieces[0] + after;
                SetCaret(new TextPosition(caret.Line, caret.Column + pieces[0].Length), false, false);
            }
            else
            {
                _lines[caret.Line] = before + pieces[0];
                for (var i = 1; i < pieces.Count; i++)
                    _lines.Insert(caret.Line + i, pieces[i]);

                var lastLine = caret.Line + pieces.Count - 1;
                var lastColumn = pieces[pieces.Count - 1].Length;
                _lines[lastLine] += after;
                SetCaret(new TextPosition(lastLine, lastColumn), false, false);
            }

            IsModified = true;
        }

        /// <summary>Splits the current line at the caret, which is what ENTER does.</summary>
        public void InsertNewLine()
        {
            DeleteSelection();

            var caret = Caret;
            var line = _lines[caret.Line];
            _lines[caret.Line] = line.Substring(0, caret.Column);
            _lines.Insert(caret.Line + 1, line.Substring(caret.Column));

            SetCaret(new TextPosition(caret.Line + 1, 0), false, false);
            IsModified = true;
        }

        /// <summary>
        ///     Deletes the selection, or the character before the caret, or joins this line onto the previous one
        ///     when the caret is at a line start.
        /// </summary>
        public void Backspace()
        {
            if (HasSelection)
            {
                DeleteSelection();
                return;
            }

            var caret = Caret;
            if (caret.Column > 0)
            {
                DeleteRange(new TextPosition(caret.Line, caret.Column - 1), caret);
            }
            else if (caret.Line > 0)
            {
                var previousLength = _lines[caret.Line - 1].Length;
                DeleteRange(new TextPosition(caret.Line - 1, previousLength), caret);
            }
        }

        /// <summary>
        ///     Deletes the selection, or the character at the caret, or pulls the next line up when the caret is at a
        ///     line end.
        /// </summary>
        public void Delete()
        {
            if (HasSelection)
            {
                DeleteSelection();
                return;
            }

            var caret = Caret;
            if (caret.Column < _lines[caret.Line].Length)
                DeleteRange(caret, new TextPosition(caret.Line, caret.Column + 1));
            else if (caret.Line < _lines.Count - 1)
                DeleteRange(caret, new TextPosition(caret.Line + 1, 0));
        }

        /// <summary>Deletes the selection and leaves the caret where it started. Does nothing when nothing is selected.</summary>
        public void DeleteSelection()
        {
            if (!HasSelection)
                return;

            DeleteRange(SelectionStart, SelectionEnd);
        }

        /// <summary>Removes everything between two positions and puts the caret at the start of the hole.</summary>
        /// <param name="start">The earlier position.</param>
        /// <param name="end">The later position.</param>
        private void DeleteRange(TextPosition start, TextPosition end)
        {
            var head = _lines[start.Line].Substring(0, start.Column);
            var tail = _lines[end.Line].Substring(end.Column);

            _lines[start.Line] = head + tail;
            if (end.Line > start.Line)
                _lines.RemoveRange(start.Line + 1, end.Line - start.Line);

            _anchor = null;
            SetCaret(start, false, false);
            IsModified = true;
        }

        /// <summary>Vertical movement, which is the only kind that reads the desired column instead of setting it.</summary>
        /// <param name="delta">Lines to move; negative is up.</param>
        /// <param name="extendSelection">TRUE to select over the move.</param>
        private void MoveVertically(int delta, bool extendSelection)
        {
            var line = Math.Clamp(Caret.Line + delta, 0, _lines.Count - 1);
            var column = Math.Min(_desiredColumn, _lines[line].Length);
            SetCaret(new TextPosition(line, column), extendSelection, true);
        }

        /// <summary>
        ///     The single place the caret is assigned, so the selection anchor and the desired column cannot get out
        ///     of step with it.
        /// </summary>
        /// <param name="position">Where the caret is going; already clamped by the caller.</param>
        /// <param name="extendSelection">TRUE to keep or start a selection anchor.</param>
        /// <param name="keepDesiredColumn">TRUE for vertical movement, which must not re-aim the desired column.</param>
        private void SetCaret(TextPosition position, bool extendSelection, bool keepDesiredColumn)
        {
            if (extendSelection)
                _anchor ??= Caret;
            else
                _anchor = null;

            Caret = position;

            if (!keepDesiredColumn)
                _desiredColumn = position.Column;
        }

        /// <summary>
        ///     Splits text on any line ending, without deciding what the document's own ending is. A CRLF pair is one
        ///     ending rather than two, or every line would be followed by a phantom empty one.
        /// </summary>
        /// <param name="text">The text to split.</param>
        /// <returns>The pieces, one per line; always at least one.</returns>
        private static List<string> SplitLines(string text)
        {
            var pieces = new List<string>();
            var start = 0;

            for (var i = 0; i < text.Length; i++)
            {
                var character = text[i];
                if (character != '\n' && character != '\r')
                    continue;

                pieces.Add(text.Substring(start, i - start));
                if (character == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;

                start = i + 1;
            }

            pieces.Add(text.Substring(start));
            return pieces;
        }

        /// <summary>
        ///     Which line ending a document uses. Whichever of CRLF and bare LF appears more often wins, because a
        ///     file with a stray ending of the other kind is common and should not flip the whole document when it is
        ///     saved. A document with no line breaks at all takes the platform's.
        /// </summary>
        /// <param name="text">The text to inspect.</param>
        /// <returns>The line ending to write back.</returns>
        private static string DetectNewLine(string text)
        {
            var crlf = 0;
            var lf = 0;

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '\n')
                    continue;

                if (i > 0 && text[i - 1] == '\r')
                    crlf++;
                else
                    lf++;
            }

            if (crlf == 0 && lf == 0)
                return Environment.NewLine;

            return crlf >= lf ? "\r\n" : "\n";
        }
    }
}

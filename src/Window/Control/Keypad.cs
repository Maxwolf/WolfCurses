// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Text;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     A grid of labelled, clickable cells: a calculator's keys, a dialog's row of buttons, a game's on-screen
    ///     controls, a palette of characters to pick from.
    ///     <para>
    ///         <b>It exists for the same reason <see cref="MenuBar" /> does: it keeps its layout.</b> The geometry
    ///         is worked out once and read by <i>both</i> the drawing and the hit test, so the button a click lands
    ///         on cannot be a different button from the one drawn there. Every hand-rolled keypad computes that
    ///         arithmetic twice, in two places, and the two drift the first time a key is widened.
    ///     </para>
    ///     <para>
    ///         <b>Spans are horizontal only</b>, the same limit and the same reason as a spreadsheet's merged
    ///         cells: a button spanning rows would mean no row could be drawn without knowing which columns an
    ///         earlier row had already claimed, and every hit test would have to ask the same question. A wide zero
    ///         key is a row's business; a tall equals key is the whole pad's.
    ///     </para>
    ///     <para>
    ///         The rules between the keys are drawn through <see cref="BoxDrawing.Junction" />, which is what makes
    ///         a spanning button come out right: where a button absorbs the rule that would have run beside it, the
    ///         junctions above and below it change shape on their own, because each is chosen from the lines that
    ///         actually meet there rather than from a fixed table of corners.
    ///     </para>
    /// </summary>
    public sealed class Keypad
    {
        /// <summary>The rows, top to bottom.</summary>
        private readonly List<KeypadRow> _rows;

        /// <summary>Which row the pointer is over, or -1.</summary>
        private int _hoveredRow = -1;

        /// <summary>Which button of that row the pointer is over, or -1.</summary>
        private int _hoveredIndex = -1;

        /// <summary>Initializes a new instance of the <see cref="Keypad" /> class.</summary>
        /// <param name="rows">The rows, top to bottom.</param>
        public Keypad(params KeypadRow[] rows)
        {
            _rows = new List<KeypadRow>(rows ?? Array.Empty<KeypadRow>());
        }

        /// <summary>The rows, top to bottom.</summary>
        public IReadOnlyList<KeypadRow> Rows => _rows;

        /// <summary>How many columns wide one unspanned button is drawn, not counting the rules beside it.</summary>
        public int ButtonWidth { get; set; } = 5;

        /// <summary>
        ///     The screen row the pad's top border is drawn on, which is what a hit test is measured against. Told
        ///     rather than inferred, for the reason <see cref="MenuBar.BarRow" /> is: an off-by-one hit test lands
        ///     on the button above the pointer, which is worse than not working.
        /// </summary>
        public int Row { get; set; }

        /// <summary>The screen column the pad's left border is drawn in.</summary>
        public int Column { get; set; }

        /// <summary>Which line style the rules are drawn in.</summary>
        public BoxBorderEnum Border { get; set; } = BoxBorderEnum.Single;

        /// <summary>How the rules between the keys are painted.</summary>
        public TextStyle BorderStyle { get; set; } = TextStyle.None;

        /// <summary>How a key's face is painted.</summary>
        public TextStyle ButtonStyle { get; set; } = TextStyle.None;

        /// <summary>How the key under the pointer is painted; falls back to inverse video when left unset.</summary>
        public TextStyle HoverStyle { get; set; } = TextStyle.None;

        /// <summary>
        ///     How a key that cannot be pressed is painted.
        ///     <para>
        ///         <b>Nullable, and null means "not specified" rather than "no colour".</b> It falls back to
        ///         <see cref="ButtonStyle" />, so a pad that never sets it draws what it always drew, where an empty
        ///         style would paint dead keys unstyled and punch holes in a coloured pad. Leaving it unset on a pad
        ///         that uses <see cref="KeypadButton.EnabledWhen" /> is how a key ends up inert and looking live,
        ///         which reads as a broken pad rather than as a greyed key.
        ///     </para>
        /// </summary>
        public TextStyle? DisabledStyle { get; set; }

        /// <summary>Which colour vocabulary the styles resolve through; pinnable per instance for tests.</summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>How many columns the widest row divides into.</summary>
        public int Columns
        {
            get
            {
                var columns = 0;

                foreach (var row in _rows)
                    columns = Math.Max(columns, row.Columns);

                return columns;
            }
        }

        /// <summary>How many screen columns the whole pad occupies, its outer rules included.</summary>
        public int Width => Columns <= 0 ? 0 : Columns * ButtonWidth + Columns + 1;

        /// <summary>How many screen rows the whole pad occupies: one per key row, plus a rule between and around.</summary>
        public int Height => _rows.Count <= 0 ? 0 : _rows.Count * 2 + 1;

        /// <summary>The key the pointer is over, or null when it is somewhere else.</summary>
        public KeypadButton Hovered =>
            _hoveredRow >= 0 && _hoveredRow < _rows.Count && _hoveredIndex >= 0 &&
            _hoveredIndex < _rows[_hoveredRow].Buttons.Count
                ? _rows[_hoveredRow].Buttons[_hoveredIndex]
                : null;

        /// <summary>
        ///     Moves the lit key to whatever the pointer is over, and reports whether it moved.
        ///     <para>
        ///         Moving off the pad puts the light out, which is the half that is easy to leave out: a key that
        ///         stays lit after the pointer has gone is a key the user believes they are about to press.
        ///     </para>
        ///     <para>
        ///         A key that cannot be pressed does not light up either, matching what
        ///         <see cref="MenuBar.HandleMouseMove" /> does with a dead entry: lighting one would say it is about
        ///         to do something.
        ///     </para>
        /// </summary>
        /// <param name="row">The row the pointer is over, in the same coordinates as <see cref="Row" />.</param>
        /// <param name="column">The column the pointer is over.</param>
        /// <returns>TRUE when the lit key changed, so the caller knows the screen moved.</returns>
        public bool Hover(int row, int column)
        {
            var found = Locate(row, column, out var rowIndex, out var index);

            if (found == null || !found.IsPressable)
            {
                rowIndex = -1;
                index = -1;
            }

            if (rowIndex == _hoveredRow && index == _hoveredIndex)
                return false;

            _hoveredRow = rowIndex;
            _hoveredIndex = index;

            return true;
        }

        /// <summary>Puts the light out, which is what a screen does when the pointer leaves it entirely.</summary>
        /// <returns>TRUE when a key had been lit.</returns>
        public bool ClearHover()
        {
            if (_hoveredRow < 0 && _hoveredIndex < 0)
                return false;

            _hoveredRow = -1;
            _hoveredIndex = -1;

            return true;
        }

        /// <summary>
        ///     Presses whatever key is under a cell and runs it, reporting whether the press was spent. The false
        ///     return is the useful half: a screen offers every press here first and then handles whatever is left.
        /// </summary>
        /// <param name="row">The pressed row.</param>
        /// <param name="column">The pressed column.</param>
        /// <returns>TRUE when a key was pressed and its action ran.</returns>
        public bool Press(int row, int column)
        {
            var button = Locate(row, column, out _, out _);

            if (button == null || !button.IsPressable)
                return false;

            button.Action();
            return true;
        }

        /// <summary>The key drawn at a cell, or null for the rules between them and everywhere off the pad.</summary>
        /// <param name="row">The row to test.</param>
        /// <param name="column">The column to test.</param>
        /// <returns>The key, or null.</returns>
        public KeypadButton ButtonAt(int row, int column)
        {
            return Locate(row, column, out _, out _);
        }

        /// <summary>
        ///     Draws the pad, one string per screen row.
        ///     <para>
        ///         Returned as rows rather than one string because a caller almost always has something to put
        ///         beside it, and splitting a styled block back apart by newline is the sort of thing that works
        ///         until a style contains one.
        ///     </para>
        /// </summary>
        /// <returns>The pad's rows, top to bottom.</returns>
        public IReadOnlyList<string> Render()
        {
            var lines = new List<string>();

            if (_rows.Count == 0 || Columns <= 0)
                return lines;

            var cuts = new List<HashSet<int>>(_rows.Count);

            foreach (var row in _rows)
                cuts.Add(CutsOf(row));

            lines.Add(Rule(null, cuts[0]));

            for (var i = 0; i < _rows.Count; i++)
            {
                lines.Add(Faces(i));
                lines.Add(i + 1 < _rows.Count ? Rule(cuts[i], cuts[i + 1]) : Rule(cuts[i], null));
            }

            return lines;
        }

        /// <summary>
        ///     The columns a row draws a vertical rule in: its left edge, and the right-hand edge of every key.
        ///     <para>
        ///         This is the whole of how a span works. A key covering two columns simply does not put a cut where
        ///         the rule between them would have gone, and everything else follows: the junctions above and below
        ///         see no line arriving there and choose a straight run instead of a tee.
        ///     </para>
        /// </summary>
        /// <param name="row">The row to measure.</param>
        /// <returns>The columns carrying a rule.</returns>
        private HashSet<int> CutsOf(KeypadRow row)
        {
            var cuts = new HashSet<int> {0};
            var x = 0;

            foreach (var button in row.Buttons)
            {
                x += ContentWidth(button) + 1;
                cuts.Add(x);
            }

            // A row that does not account for the full width still has its right-hand edge drawn, or the pad would
            // be open along that row and the border would not close.
            cuts.Add(Width - 1);

            return cuts;
        }

        /// <summary>How many columns a key's face occupies, absorbing the rules its span swallowed.</summary>
        /// <param name="button">The key.</param>
        /// <returns>The face width.</returns>
        private int ContentWidth(KeypadButton button)
        {
            return button.Span * ButtonWidth + (button.Span - 1);
        }

        /// <summary>Draws one horizontal rule, choosing every glyph from the lines that actually meet at it.</summary>
        /// <param name="above">The cuts of the row above, or null at the top of the pad.</param>
        /// <param name="below">The cuts of the row below, or null at the bottom.</param>
        /// <returns>The drawn rule.</returns>
        private string Rule(HashSet<int> above, HashSet<int> below)
        {
            var sb = new StringBuilder(Width);

            for (var x = 0; x < Width; x++)
            {
                sb.Append(BoxDrawing.Junction(
                    above != null && above.Contains(x),
                    below != null && below.Contains(x),
                    x > 0,
                    x < Width - 1,
                    Border));
            }

            return Paint(sb.ToString(), BorderStyle);
        }

        /// <summary>Draws one row of key faces, with its rules between them.</summary>
        /// <param name="rowIndex">Which row.</param>
        /// <returns>The drawn row.</returns>
        private string Faces(int rowIndex)
        {
            var row = _rows[rowIndex];
            var vertical = BoxDrawing.Junction(true, true, false, false, Border).ToString();

            var line = new TextRow {ColorMode = ColorMode};
            var drawn = 0;

            for (var i = 0; i < row.Buttons.Count; i++)
            {
                var button = row.Buttons[i];
                var width = ContentWidth(button);

                line.Append(vertical, BorderStyle);
                line.Append(AnsiText.Fit(button.Label, width, AnsiHorizontalAlignmentEnum.Center),
                    FaceStyle(button, rowIndex, i));

                drawn += width + 1;
            }

            // Whatever a short row left over is drawn as empty pad rather than left ragged, so a background colour
            // covers the whole of it and the right-hand border still lands where every other row's does.
            if (drawn < Width - 1)
                line.Append(' ', Width - 1 - drawn, ButtonStyle);

            line.Append(vertical, BorderStyle);

            return line.Render();
        }

        /// <summary>How one key's face is painted: lit, dead, or at rest.</summary>
        /// <param name="button">The key.</param>
        /// <param name="rowIndex">Which row it is in.</param>
        /// <param name="index">Which key of that row it is.</param>
        /// <returns>Its style.</returns>
        private TextStyle FaceStyle(KeypadButton button, int rowIndex, int index)
        {
            if (!button.IsPressable)
                return DisabledStyle ?? ButtonStyle;

            if (rowIndex != _hoveredRow || index != _hoveredIndex)
                return ButtonStyle;

            // Inverse video where nobody chose a colour, through the same gate every other highlight in this
            // namespace uses, so an unstyled pad still shows which key the pointer is on and still emits nothing
            // at all when colour is switched off.
            if (!HoverStyle.IsEmpty)
                return HoverStyle;

            return AnsiConsole.DetectColorMode() == AnsiColorModeEnum.None
                ? ButtonStyle
                : ButtonStyle.WithForeground(ConsoleColor.Black).WithBackground(ConsoleColor.Gray);
        }

        /// <summary>Which key covers a cell, and where it sits in the pad.</summary>
        /// <param name="row">The row to test.</param>
        /// <param name="column">The column to test.</param>
        /// <param name="rowIndex">Which row it is in, or -1.</param>
        /// <param name="index">Which key of that row, or -1.</param>
        /// <returns>The key, or null.</returns>
        private KeypadButton Locate(int row, int column, out int rowIndex, out int index)
        {
            rowIndex = -1;
            index = -1;

            var local = row - Row;

            // Key faces are on the odd rows; the even ones are the rules between them and belong to no key.
            if (local < 1 || local >= Height || local % 2 == 0)
                return null;

            rowIndex = (local - 1) / 2;

            if (rowIndex >= _rows.Count)
            {
                rowIndex = -1;
                return null;
            }

            var x = column - Column;
            var at = 0;

            for (var i = 0; i < _rows[rowIndex].Buttons.Count; i++)
            {
                var button = _rows[rowIndex].Buttons[i];

                // The rule is at `at`, so the face runs from the next column for its whole width. A press on the
                // rule itself belongs to nobody, the same answer TableViewport gives for the ground past its last
                // column: rounding it to a neighbour presses a key nobody pointed at.
                if (x > at && x <= at + ContentWidth(button))
                {
                    index = i;
                    return button;
                }

                at += ContentWidth(button) + 1;
            }

            rowIndex = -1;
            return null;
        }

        /// <summary>Paints text in a style, or hands it back untouched when there is no style.</summary>
        /// <param name="text">The text to paint.</param>
        /// <param name="style">The style to paint it in.</param>
        /// <returns>The painted text.</returns>
        private string Paint(string text, TextStyle style)
        {
            return style.IsEmpty ? text : style.Apply(text, ColorMode);
        }
    }
}

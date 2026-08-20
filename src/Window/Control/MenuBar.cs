// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Collections.Generic;
using System.Text;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     A pull-down menu bar of the kind every text-mode application had: a row of titles, one of which drops open
    ///     into a panel of choices, driven by the keyboard or by a pointer.
    ///     <para>
    ///         <b>This is the control that keeps its layout, and that is the whole point of it.</b> The library's
    ///         numbered menus deliberately do not: <c>MenuLayout</c> may reflow items into columns it does not
    ///         retain, so nothing afterwards knows which cell holds which item, which is exactly why clicking a menu
    ///         was never supported. Here the geometry is computed once by <see cref="Layout" /> and used by both the
    ///         drawing and the hit test, so the menu on screen and the menu a click lands in cannot be different
    ///         menus.
    ///     </para>
    ///     <para>
    ///         Pure state, arithmetic and strings, like every other control in this namespace. It reads no console
    ///         and draws nothing itself: it returns text and answers questions about coordinates, and the screen that
    ///         owns it decides where that text goes. The one thing it must be told is <see cref="BarRow" />, since
    ///         only the owner knows how far down its own layout the bar ended up.
    ///     </para>
    /// </summary>
    public sealed class MenuBar
    {
        /// <summary>Blank columns either side of a title on the bar.</summary>
        private const int TitlePadding = 1;

        /// <summary>The menus, left to right.</summary>
        private readonly List<MenuBarMenu> _menus = new();

        /// <summary>Where each title starts on the bar, recomputed by <see cref="Layout" />.</summary>
        private readonly List<int> _titleColumns = new();

        /// <summary>Initializes a new instance of the <see cref="MenuBar" /> class.</summary>
        /// <param name="menus">The menus, left to right.</param>
        public MenuBar(params MenuBarMenu[] menus)
        {
            if (menus != null)
                _menus.AddRange(menus);

            Layout();
        }

        /// <summary>The menus, left to right.</summary>
        public IReadOnlyList<MenuBarMenu> Menus => _menus;

        /// <summary>Which menu is dropped open, or -1 when none is.</summary>
        public int OpenIndex { get; private set; } = -1;

        /// <summary>Whether a menu is dropped open.</summary>
        public bool IsOpen => OpenIndex >= 0;

        /// <summary>Which entry of the open menu the cursor is on, or -1.</summary>
        public int HighlightIndex { get; private set; } = -1;

        /// <summary>
        ///     Which screen row the bar itself is drawn on, counting from the top of the window the pointer reports
        ///     against. Set by the owner, because only it knows what is above the bar; a hit test against the wrong
        ///     row is off by exactly however much chrome the owner drew and is silently wrong rather than broken.
        /// </summary>
        public int BarRow { get; set; }

        /// <summary>
        ///     How wide the bar is, remembered from the last <see cref="Render" /> so that hit-testing a
        ///     right-aligned title uses the same edge the drawing did. Set it directly only when hit-testing before
        ///     anything has been drawn.
        /// </summary>
        public int Width { get; set; } = 80;

        /// <summary>How the bar itself is painted. Left at none, the bar is plain text exactly as before.</summary>
        public TextStyle BarStyle { get; set; } = TextStyle.None;

        /// <summary>How the open menu's title is painted; falls back to inverse video when left unset.</summary>
        public TextStyle HighlightStyle { get; set; } = TextStyle.None;

        /// <summary>How the dropped panel is painted.</summary>
        public TextStyle PanelStyle { get; set; } = TextStyle.None;

        /// <summary>How the entry under the cursor is painted; falls back to inverse video when left unset.</summary>
        public TextStyle PanelHighlightStyle { get; set; } = TextStyle.None;

        /// <summary>
        ///     Which colour vocabulary the styles resolve through, pinnable per instance for the same reason every
        ///     other styled control here has one: <see cref="AnsiColorModeEnum.Auto" /> consults a process-wide
        ///     cache that a test must not move.
        /// </summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>How many rows the open menu's panel occupies, or zero when nothing is open.</summary>
        public int DropdownHeight => IsOpen ? _menus[OpenIndex].Entries.Count + 2 : 0;

        /// <summary>The entry the cursor is on, or null.</summary>
        public MenuBarEntry Highlighted =>
            IsOpen && HighlightIndex >= 0 && HighlightIndex < _menus[OpenIndex].Entries.Count
                ? _menus[OpenIndex].Entries[HighlightIndex]
                : null;

        /// <summary>Drops a menu open with the cursor on its first choice.</summary>
        /// <param name="menuIndex">Which menu; out of range closes instead.</param>
        public void Open(int menuIndex)
        {
            if (menuIndex < 0 || menuIndex >= _menus.Count)
            {
                Close();
                return;
            }

            OpenIndex = menuIndex;

            // Start on the first thing that can actually be chosen, so a menu beginning with a disabled entry does
            // not open with the cursor on a dead line.
            HighlightIndex = _menus[menuIndex].NextSelectable(-1, 1);
        }

        /// <summary>Shuts whatever is open.</summary>
        public void Close()
        {
            OpenIndex = -1;
            HighlightIndex = -1;
        }

        /// <summary>
        ///     Offers a key to the menu bar and reports whether it was spent.
        ///     <para>
        ///         The false return is the important half: a screen hands every key here first and then handles
        ///         whatever is left, so an editor's typing still reaches the document while a menu is shut, and
        ///         nothing reaches the document while one is open.
        ///     </para>
        /// </summary>
        /// <param name="keyInfo">The key that was pressed.</param>
        /// <returns>TRUE when the menu bar consumed the key.</returns>
        public bool HandleKey(ConsoleKeyInfo keyInfo)
        {
            var alt = (keyInfo.Modifiers & ConsoleModifiers.Alt) != 0;

            if (alt && TryOpenByAccessKey(keyInfo.Key))
                return true;

            if (!IsOpen)
                return false;

            switch (keyInfo.Key)
            {
                case ConsoleKey.Escape:
                    Close();
                    return true;
                case ConsoleKey.LeftArrow:
                    Open(Wrap(OpenIndex - 1));
                    return true;
                case ConsoleKey.RightArrow:
                    Open(Wrap(OpenIndex + 1));
                    return true;
                case ConsoleKey.UpArrow:
                    HighlightIndex = _menus[OpenIndex].NextSelectable(HighlightIndex, -1);
                    return true;
                case ConsoleKey.DownArrow:
                    HighlightIndex = _menus[OpenIndex].NextSelectable(HighlightIndex, 1);
                    return true;
                case ConsoleKey.Home:
                    HighlightIndex = _menus[OpenIndex].NextSelectable(-1, 1);
                    return true;
                case ConsoleKey.End:
                    HighlightIndex = _menus[OpenIndex].NextSelectable(_menus[OpenIndex].Entries.Count, -1);
                    return true;
                case ConsoleKey.Enter:
                    Activate();
                    return true;
                default:
                    // Everything else is swallowed while a menu is open, which is what stops a stray keystroke
                    // landing in the document behind it.
                    return true;
            }
        }

        /// <summary>
        ///     Offers a pointer press to the menu bar and reports whether it was spent. A press on a title opens or
        ///     shuts that menu, a press on an entry chooses it, and a press anywhere else while a menu is open shuts
        ///     it and is swallowed, because dismissing a menu should not also do whatever was underneath it.
        /// </summary>
        /// <param name="row">The pressed row, in the same coordinates as <see cref="BarRow" />.</param>
        /// <param name="column">The pressed column.</param>
        /// <returns>TRUE when the menu bar consumed the press.</returns>
        public bool HandleMouse(int row, int column)
        {
            if (row == BarRow)
            {
                var hit = TitleAt(column);
                if (hit < 0)
                {
                    if (!IsOpen)
                        return false;

                    Close();
                    return true;
                }

                if (OpenIndex == hit)
                    Close();
                else
                    Open(hit);

                return true;
            }

            if (!IsOpen)
                return false;

            var entry = EntryAt(row, column);
            if (entry < 0)
            {
                Close();
                return true;
            }

            HighlightIndex = entry;
            Activate();
            return true;
        }

        /// <summary>
        ///     Runs the highlighted entry and shuts the menu.
        ///     <para>
        ///         The menu is shut <b>before</b> the action runs, not after. An action is free to open a dialog, or
        ///         to rebuild this very menu, and a close that happened afterwards would either reach into whatever
        ///         the action put up or undo it.
        ///     </para>
        /// </summary>
        public void Activate()
        {
            var entry = Highlighted;
            if (entry == null || !entry.IsSelectable)
                return;

            Close();
            entry.Action();
        }

        /// <summary>Which menu title covers a column of the bar, or -1 for the gaps and the space past the end.</summary>
        /// <param name="column">The column to test.</param>
        /// <returns>The menu index, or -1.</returns>
        public int TitleAt(int column)
        {
            Layout();

            for (var i = 0; i < _menus.Count; i++)
            {
                var start = _titleColumns[i];
                if (column >= start && column < start + CellWidth(_menus[i]))
                    return i;
            }

            return -1;
        }

        /// <summary>
        ///     Which entry of the open panel a cell falls on, or -1 for its border, its separators and everywhere
        ///     outside it.
        /// </summary>
        /// <param name="row">The row to test.</param>
        /// <param name="column">The column to test.</param>
        /// <returns>The entry index, or -1.</returns>
        public int EntryAt(int row, int column)
        {
            if (!IsOpen)
                return -1;

            Layout();

            var menu = _menus[OpenIndex];
            var left = _titleColumns[OpenIndex];
            var width = menu.ContentWidth + 4;

            if (column < left || column >= left + width)
                return -1;

            // Row BarRow + 1 is the panel's top border, so the first entry is two rows below the bar.
            var index = row - BarRow - 2;
            if (index < 0 || index >= menu.Entries.Count)
                return -1;

            return menu.Entries[index].IsSelectable ? index : -1;
        }

        /// <summary>
        ///     Draws the bar and, below it, the open panel. The owner appends this above whatever else it draws and
        ///     shortens its own content by <see cref="DropdownHeight" /> so nothing is pushed off the bottom.
        /// </summary>
        /// <param name="width">How many columns the bar spans.</param>
        /// <returns>The bar row followed by the panel's rows, each newline terminated.</returns>
        public string Render(int width)
        {
            Width = width;
            Layout();

            var sb = new StringBuilder();
            sb.Append(RenderBar(width)).Append(Environment.NewLine);

            if (!IsOpen)
                return sb.ToString();

            foreach (var row in RenderDropdown())
                sb.Append(row).Append(Environment.NewLine);

            return sb.ToString();
        }

        /// <summary>Draws the row of titles, with the open one emphasized.</summary>
        /// <param name="width">How many columns the bar spans.</param>
        /// <returns>The bar row.</returns>
        private string RenderBar(int width)
        {
            // Walked in column order rather than in declared order, because a right-aligned title is drawn after the
            // left-hand ones but sits past them; and emitted as runs rather than spliced into a padded string,
            // because a styled title is far longer than it is wide and indexing into it by column would cut an
            // escape in half.
            var order = new List<int>();
            for (var i = 0; i < _menus.Count; i++)
                order.Add(i);

            order.Sort((a, b) => _titleColumns[a].CompareTo(_titleColumns[b]));

            var sb = new StringBuilder();
            var column = 0;

            foreach (var index in order)
            {
                var start = _titleColumns[index];
                if (start > column)
                {
                    sb.Append(Paint(new string(' ', start - column), BarStyle));
                    column = start;
                }

                var title = new string(' ', TitlePadding) + _menus[index].Title + new string(' ', TitlePadding);
                sb.Append(index == OpenIndex ? Emphasis(title, HighlightStyle) : Paint(title, BarStyle));
                column += title.Length;
            }

            // Padded to the full width so the bar reads as a bar rather than as a few words floating on the top row,
            // and so a background colour covers the whole row rather than stopping after the last word.
            if (column < width)
                sb.Append(Paint(new string(' ', width - column), BarStyle));

            return sb.ToString();
        }

        /// <summary>
        ///     Paints text in a style, or hands it back untouched when there is no style. The untouched path is the
        ///     compatibility one: a menu bar nobody has coloured renders as plain text with no escapes at all, the
        ///     same stance every other control in this namespace takes.
        /// </summary>
        /// <param name="text">The text to paint.</param>
        /// <param name="style">The style to paint it in.</param>
        /// <returns>The painted text.</returns>
        private string Paint(string text, TextStyle style)
        {
            return style.IsEmpty ? text : style.Apply(text, ColorMode);
        }

        /// <summary>
        ///     Marks the highlighted title or entry: in the given style when one was set, and otherwise in inverse
        ///     video through the same gate every other highlight in the library uses, so a bar nobody styled still
        ///     shows which menu is open and still says nothing at all when colour is switched off.
        /// </summary>
        /// <param name="text">The text to emphasize.</param>
        /// <param name="style">The style to use, when there is one.</param>
        /// <returns>The emphasized text.</returns>
        private string Emphasis(string text, TextStyle style)
        {
            return style.IsEmpty ? ListNavigator.Emphasize(text) : style.Apply(text, ColorMode);
        }

        /// <summary>Draws the open panel, indented to sit under its own title.</summary>
        /// <returns>The panel's rows.</returns>
        private IEnumerable<string> RenderDropdown()
        {
            var menu = _menus[OpenIndex];
            var indent = new string(' ', _titleColumns[OpenIndex]);
            var inner = menu.ContentWidth + 2;

            yield return indent + Paint("┌" + new string('─', inner) + "┐", PanelStyle);

            for (var i = 0; i < menu.Entries.Count; i++)
            {
                var entry = menu.Entries[i];

                if (entry.IsSeparator)
                {
                    yield return indent + Paint("├" + new string('─', inner) + "┤", PanelStyle);
                    continue;
                }

                var text = " " + entry.Label.PadRight(menu.ContentWidth - entry.Shortcut.Length) + entry.Shortcut + " ";
                var body = i == HighlightIndex ? Emphasis(text, PanelHighlightStyle) : Paint(text, PanelStyle);

                yield return indent + Paint("│", PanelStyle) + body + Paint("│", PanelStyle);
            }

            yield return indent + Paint("└" + new string('─', inner) + "┘", PanelStyle);
        }

        /// <summary>
        ///     Recomputes where each title starts. Called by everything that needs the geometry rather than cached
        ///     behind a dirty flag, so a menu whose titles changed cannot be drawn in one place and hit-tested in
        ///     another; it is a handful of additions over a handful of menus.
        /// </summary>
        private void Layout()
        {
            _titleColumns.Clear();
            for (var i = 0; i < _menus.Count; i++)
                _titleColumns.Add(0);

            var left = 0;
            var right = Math.Max(0, Width);

            for (var i = 0; i < _menus.Count; i++)
            {
                var cells = CellWidth(_menus[i]);

                if (_menus[i].AlignRight)
                {
                    // Laid from the right edge inward, so several right-aligned menus keep their declared order
                    // reading left to right rather than coming out reversed.
                    right -= cells;
                    _titleColumns[i] = Math.Max(left, right);
                }
                else
                {
                    _titleColumns[i] = left;
                    left += cells;
                }
            }
        }

        /// <summary>How many columns a title occupies on the bar, padding included.</summary>
        /// <param name="menu">The menu whose title to measure.</param>
        /// <returns>The width of its cell on the bar.</returns>
        private static int CellWidth(MenuBarMenu menu)
        {
            return menu.Title.Length + TitlePadding * 2;
        }

        /// <summary>Opens whichever menu answers to a letter, when one does.</summary>
        /// <param name="key">The key pressed with ALT held.</param>
        /// <returns>TRUE when a menu was opened.</returns>
        private bool TryOpenByAccessKey(ConsoleKey key)
        {
            for (var i = 0; i < _menus.Count; i++)
            {
                if (_menus[i].AccessKey != '\0' && (int) key == _menus[i].AccessKey)
                {
                    Open(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Wraps a menu index round the ends of the bar.</summary>
        /// <param name="index">The index to wrap.</param>
        /// <returns>An index inside the bar.</returns>
        private int Wrap(int index)
        {
            if (_menus.Count == 0)
                return -1;

            return (index % _menus.Count + _menus.Count) % _menus.Count;
        }
    }
}

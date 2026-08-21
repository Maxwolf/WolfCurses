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
        ///     How an entry that cannot be chosen is painted, which is what makes
        ///     <see cref="MenuBarEntry.EnabledWhen" /> visible at all.
        ///     <para>
        ///         <b>Without this the predicate is invisible and the menu reads as broken.</b> An entry that is
        ///         switched off still draws exactly like a live one, so it does not answer the pointer, does not
        ///         take the highlight and does nothing when clicked, with nothing on screen saying why. A menu with
        ///         four such entries out of six looks like a menu that has stopped working.
        ///     </para>
        ///     <para>
        ///         <b>Nullable, unlike its siblings, and the difference is the point.</b> Null means "not specified"
        ///         and falls back to <see cref="PanelStyle" />, so a bar that never sets it draws exactly what it
        ///         drew before this existed. A non-nullable default of <see cref="TextStyle.None" /> would instead
        ///         paint disabled entries with no style at all, punching a hole in a panel that has a background,
        ///         which is worse than the problem.
        ///     </para>
        ///     <para>
        ///         What is drawn in it is exactly what cannot be chosen: the same <c>IsSelectable</c> the hit test
        ///         and the arrow keys consult, so the greying and the behaviour can never disagree.
        ///     </para>
        /// </summary>
        public TextStyle? DisabledStyle { get; set; }

        /// <summary>
        ///     Which colour vocabulary the styles resolve through, pinnable per instance for the same reason every
        ///     other styled control here has one: <see cref="AnsiColorModeEnum.Auto" /> consults a process-wide
        ///     cache that a test must not move.
        /// </summary>
        public AnsiColorModeEnum ColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>
        ///     Whether the letter that opens each menu is underlined. On by default, and always on rather than only
        ///     while ALT is held: a terminal reports no key-up, so "while held" is not a state anything here can
        ///     know it has left. Showing them permanently is also what the editors this imitates actually did.
        /// </summary>
        public bool ShowAccessKeys { get; set; } = true;

        /// <summary>
        ///     What is drawn beside a ticked entry. Settable because which glyph reads as a tick is a question about
        ///     the look being imitated and about the font it will land in, not about menus: an MS-DOS editor drew a
        ///     square root sign and an ASCII-only terminal would rather have an <c>x</c>.
        /// </summary>
        public char CheckMark { get; set; } = '\u2713';

        /// <summary>
        ///     Which screen row the open panel's own top border is drawn on. Negative means "directly under the
        ///     bar", which is where it hangs when nothing is between them.
        ///     <para>
        ///         It is settable because a caller drawing the panel <i>over</i> something usually has a row of its
        ///         own in between, such as a frame's top edge. Inferring it from <see cref="BarRow" /> would then be
        ///         off by exactly that much, and a hit test that is off by one lands on the entry above the one
        ///         under the pointer, which is worse than not working at all.
        ///     </para>
        /// </summary>
        public int PanelRow { get; set; } = -1;

        /// <summary>Where the panel's top border really is, resolved against <see cref="BarRow" />.</summary>
        private int PanelTop => PanelRow >= 0 ? PanelRow : BarRow + 1;

        /// <summary>How many rows the open menu's panel occupies, or zero when nothing is open.</summary>
        public int DropdownHeight => IsOpen ? _menus[OpenIndex].Entries.Count + 2 : 0;

        /// <summary>Which column the open panel starts at, which is the left edge of its own title.</summary>
        public int DropdownColumn
        {
            get
            {
                if (!IsOpen)
                    return 0;

                Layout();
                return _titleColumns[OpenIndex];
            }
        }

        /// <summary>How many columns the open panel occupies, borders included.</summary>
        public int DropdownWidth => IsOpen ? _menus[OpenIndex].ContentWidth + 4 : 0;

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

            // F10 is the traditional way into a menu bar and the reason every text-mode application had it: ALT is
            // not reliably delivered as a modifier. Plenty of terminals swallow it, send an escape prefix instead,
            // or hand it to the window manager, and a menu bar reachable only by ALT is one that simply does not
            // open on those. F10 arrives as an ordinary key everywhere.
            if (keyInfo.Key == ConsoleKey.F10)
            {
                if (IsOpen)
                    Close();
                else
                    Open(0);

                return true;
            }

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
        ///     Offers the pointer's new position to the menu bar, moving the highlight to whatever it is over.
        ///     <para>
        ///         <b>Only while a menu is already open.</b> Hovering a shut bar deliberately opens nothing: a menu
        ///         that drops down because the pointer crossed it on the way somewhere else is a menu that gets in
        ///         the way, and the press that would have opened it is one keystroke of effort. Once one <i>is</i>
        ///         open, though, sliding along the bar opens each menu in turn, which is what every menu bar since
        ///         the first one has done and is how somebody looks through them.
        ///     </para>
        ///     <para>
        ///         Separators and disabled entries do not take the highlight, matching what the arrow keys do:
        ///         <see cref="EntryAt" /> already answers -1 for both, so the highlight simply stays where it was
        ///         rather than landing on a line that cannot be chosen.
        ///     </para>
        /// </summary>
        /// <param name="row">The row the pointer is over, in the same coordinates as <see cref="BarRow" />.</param>
        /// <param name="column">The column the pointer is over.</param>
        /// <returns>TRUE when the highlight or the open menu changed, so the caller knows the screen moved.</returns>
        public bool HandleMouseMove(int row, int column)
        {
            if (!IsOpen)
                return false;

            if (row == BarRow)
            {
                var title = TitleAt(column);

                if (title < 0 || title == OpenIndex)
                    return false;

                Open(title);
                return true;
            }

            var entry = EntryAt(row, column);

            if (entry < 0 || entry == HighlightIndex)
                return false;

            HighlightIndex = entry;
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

            // The panel's own top border is the first of its rows, so the first entry is the one after it.
            var index = row - PanelTop - 1;
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
                sb.Append(RenderTitle(_menus[index].Title, index == OpenIndex));
                column += title.Length;
            }

            // Padded to the full width so the bar reads as a bar rather than as a few words floating on the top row,
            // and so a background colour covers the whole row rather than stopping after the last word.
            if (column < width)
                sb.Append(Paint(new string(' ', width - column), BarStyle));

            return sb.ToString();
        }

        /// <summary>
        ///     Draws one title, with the letter that opens it underlined. Emitted as three runs rather than one so
        ///     the underline covers exactly the access key: it is a text attribute, not a colour, and a whole title
        ///     underlined says something different from one letter of it.
        /// </summary>
        /// <param name="title">The menu's title.</param>
        /// <param name="open">Whether this menu is the open one.</param>
        /// <returns>The drawn title, padding included.</returns>
        private string RenderTitle(string title, bool open)
        {
            var pad = new string(' ', TitlePadding);
            var style = open ? HighlightStyle : BarStyle;

            if (!ShowAccessKeys || title.Length == 0)
                return open ? Emphasis(pad + title + pad, HighlightStyle) : Paint(pad + title + pad, BarStyle);

            var head = pad + title.Substring(0, 1);
            var tail = title.Substring(1) + pad;

            if (style.IsEmpty)
                return Paint(pad, style) + Emphasis(title.Substring(0, 1), style) + Paint(tail, style);

            return Paint(pad, style) +
                   style.WithUnderline(true).Apply(title.Substring(0, 1), ColorMode) +
                   Paint(tail, style);
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

        /// <summary>
        ///     The open panel's rows on their own, with no indent, so a caller can draw them <i>over</i> whatever is
        ///     behind them rather than pushing it down the screen. A dropped menu that displaces the document is the
        ///     tell that a screen is being stacked rather than composited.
        /// </summary>
        /// <returns>The panel's rows, each exactly <see cref="DropdownWidth" /> columns wide.</returns>
        public IReadOnlyList<string> DropdownRows()
        {
            if (!IsOpen)
                return Array.Empty<string>();

            Layout();
            return new List<string>(PanelRows());
        }

        /// <summary>Draws the bar on its own, for a caller composing the panel separately.</summary>
        /// <param name="width">How many columns the bar spans.</param>
        /// <returns>The bar row.</returns>
        public string RenderTitleBar(int width)
        {
            Width = width;
            Layout();
            return RenderBar(width);
        }

        /// <summary>Draws the open panel, indented to sit under its own title.</summary>
        /// <returns>The panel's rows.</returns>
        private IEnumerable<string> RenderDropdown()
        {
            var indent = new string(' ', _titleColumns[OpenIndex]);
            foreach (var row in PanelRows())
                yield return indent + row;
        }

        /// <summary>The panel itself, one row at a time.</summary>
        /// <returns>The panel's rows.</returns>
        private IEnumerable<string> PanelRows()
        {
            var menu = _menus[OpenIndex];
            var inner = menu.ContentWidth + 2;

            yield return Paint("┌" + new string('─', inner) + "┐", PanelStyle);

            for (var i = 0; i < menu.Entries.Count; i++)
            {
                var entry = menu.Entries[i];

                if (entry.IsSeparator)
                {
                    yield return Paint("├" + new string('─', inner) + "┤", PanelStyle);
                    continue;
                }

                // The check column is reserved for the whole menu or for none of it, so an entry that happens to
                // be unticked still gets the space and every label in the panel starts at the same indent.
                var mark = menu.HasCheckMarks
                    ? (entry.IsChecked ? CheckMark + " " : "  ")
                    : string.Empty;

                var label = entry.Label.PadRight(menu.ContentWidth - mark.Length - entry.Shortcut.Length);
                var text = " " + mark + label + entry.Shortcut + " ";
                // Greyed by the same rule the arrow keys and the hit test follow, so what is drawn dead is
                // exactly what cannot be chosen. The highlight never lands on one of these, since both ways of
                // moving it refuse them, so the two styles cannot collide.
                var resting = entry.IsSelectable ? PanelStyle : DisabledStyle ?? PanelStyle;
                var body = i == HighlightIndex ? Emphasis(text, PanelHighlightStyle) : Paint(text, resting);

                yield return Paint("│", PanelStyle) + body + Paint("│", PanelStyle);
            }

            yield return Paint("└" + new string('─', inner) + "┘", PanelStyle);
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

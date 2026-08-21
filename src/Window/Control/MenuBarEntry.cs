// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     One line of a pull-down menu: what it says, what it does, and optionally the key that does it without
    ///     opening the menu at all.
    ///     <para>
    ///         The action is an <see cref="Action" /> for the same reason <c>Window.AddCommand</c> takes one: a menu
    ///         choice is a thing to run, and handing it over at construction is what lets the menu invoke it without
    ///         anybody switching on an identifier afterwards.
    ///     </para>
    /// </summary>
    public sealed class MenuBarEntry
    {
        /// <summary>What <see cref="IsEnabled" /> answers when no <see cref="EnabledWhen" /> was given.</summary>
        private bool _isEnabled = true;

        /// <summary>Initializes a new instance of the <see cref="MenuBarEntry" /> class.</summary>
        /// <param name="label">What the line reads.</param>
        /// <param name="action">What choosing it does; null makes the entry unselectable.</param>
        /// <param name="shortcut">The key that also does it, shown right-aligned as a reminder.</param>
        public MenuBarEntry(string label, Action action, string shortcut = null)
        {
            Label = label ?? string.Empty;
            Action = action;
            Shortcut = shortcut ?? string.Empty;
        }

        /// <summary>Private constructor for the separator, which is the one entry with no label and no action.</summary>
        private MenuBarEntry()
        {
            Label = string.Empty;
            Shortcut = string.Empty;
            IsSeparator = true;
        }

        /// <summary>What the line reads.</summary>
        public string Label { get; }

        /// <summary>The key that also runs it, shown as a reminder; empty when there is not one.</summary>
        public string Shortcut { get; }

        /// <summary>What choosing it does.</summary>
        public Action Action { get; }

        /// <summary>Whether this is a horizontal rule rather than a choice.</summary>
        public bool IsSeparator { get; }

        /// <summary>
        ///     Whether the entry can be chosen right now. A disabled entry is still drawn, because a menu whose items
        ///     come and go is one nobody can learn the shape of.
        ///     <para>
        ///         Reading this consults <see cref="EnabledWhen" /> when one was given, so an entry whose validity
        ///         moves is answered fresh every time rather than remembered from whenever somebody last thought to
        ///         set it. Assigning still works and is the right thing for an entry that is switched on once.
        ///     </para>
        /// </summary>
        public bool IsEnabled
        {
            get => EnabledWhen?.Invoke() ?? _isEnabled;
            set => _isEnabled = value;
        }

        /// <summary>
        ///     Asked, each time, whether the entry can be chosen: a Cut that means nothing without a selection, a
        ///     Paste that means nothing with an empty clipboard, a Save that only applies to a file already open.
        ///     <para>
        ///         A predicate rather than a flag the owner keeps up to date, because <see cref="IsEnabled" /> is
        ///         read at three separate moments - drawing the panel, walking it with the arrow keys, and choosing
        ///         an entry - and an owner refreshing a flag has to do so before all three. Missing one draws a live
        ///         entry as dead, or runs a dead one.
        ///     </para>
        ///     <para>
        ///         Only consulted while a menu is open, but it is consulted per row per frame while it is, so keep
        ///         it to reading state rather than working anything out.
        ///     </para>
        /// </summary>
        public Func<bool> EnabledWhen { get; set; }

        /// <summary>
        ///     Asked, each time, whether the entry should be drawn with a mark beside it: word wrap being on, which
        ///     of several tab widths is in force, whether a search is case sensitive.
        ///     <para>
        ///         There is no settable <c>IsChecked</c> to go with it, deliberately. A mark that never changes says
        ///         nothing, so an entry worth marking is one whose answer moves, and "always ticked" is still
        ///         expressible as a predicate that returns true. One way to express it means no question about which
        ///         of two wins.
        ///     </para>
        /// </summary>
        public Func<bool> CheckedWhen { get; set; }

        /// <summary>Whether a mark is drawn beside this entry right now.</summary>
        public bool IsChecked => CheckedWhen != null && CheckedWhen();

        /// <summary>
        ///     Whether this entry can carry a mark at all, which is what makes its whole menu reserve the column
        ///     they are drawn in. An entry that is merely unticked still needs the space, or the labels in one menu
        ///     would sit at two different indents depending on the answer.
        /// </summary>
        public bool IsCheckable => CheckedWhen != null;

        /// <summary>
        ///     Whether the highlight may land here at all. Separators and entries with nothing to run are skipped
        ///     over by the arrow keys rather than being selectable dead ends.
        /// </summary>
        public bool IsSelectable => !IsSeparator && IsEnabled && Action != null;

        /// <summary>A horizontal rule between groups of choices.</summary>
        /// <returns>A new separator entry.</returns>
        public static MenuBarEntry Separator()
        {
            return new MenuBarEntry();
        }
    }
}

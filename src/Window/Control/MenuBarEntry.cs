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
        /// </summary>
        public bool IsEnabled { get; set; } = true;

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

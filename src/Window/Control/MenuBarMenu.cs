// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Collections.Generic;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     One pull-down on a <see cref="MenuBar" />: a title on the bar and the entries that drop out of it.
    /// </summary>
    public sealed class MenuBarMenu
    {
        /// <summary>The entries, in the order they are drawn.</summary>
        private readonly List<MenuBarEntry> _entries = new();

        /// <summary>Initializes a new instance of the <see cref="MenuBarMenu" /> class.</summary>
        /// <param name="title">The word on the bar.</param>
        /// <param name="entries">The lines that drop out of it.</param>
        public MenuBarMenu(string title, params MenuBarEntry[] entries)
        {
            Title = title ?? string.Empty;

            if (entries != null)
                _entries.AddRange(entries);
        }

        /// <summary>The word on the bar.</summary>
        public string Title { get; }

        /// <summary>The lines that drop out of it.</summary>
        public IReadOnlyList<MenuBarEntry> Entries => _entries;

        /// <summary>
        ///     The letter that opens this menu, which is its first. Deliberately not a marked letter elsewhere in the
        ///     title: a menu bar whose access keys are scattered through the words needs every title to declare one,
        ///     and getting that wrong gives two menus the same key with no complaint from anything.
        /// </summary>
        public char AccessKey => Title.Length > 0 ? char.ToUpperInvariant(Title[0]) : '\0';

        /// <summary>
        ///     How wide the dropped panel is: the widest entry, counting the gap between a label and its shortcut
        ///     reminder, and never narrower than the title above it.
        /// </summary>
        public int ContentWidth
        {
            get
            {
                var widest = Title.Length;
                foreach (var entry in _entries)
                {
                    var width = entry.IsSeparator
                        ? 0
                        : entry.Label.Length + (entry.Shortcut.Length > 0 ? entry.Shortcut.Length + 2 : 0);

                    widest = Math.Max(widest, width);
                }

                return widest;
            }
        }

        /// <summary>Adds an entry after construction, for a menu whose contents depend on what is loaded.</summary>
        /// <param name="entry">The entry to add.</param>
        public void Add(MenuBarEntry entry)
        {
            if (entry != null)
                _entries.Add(entry);
        }

        /// <summary>
        ///     The next selectable entry from a starting point, skipping separators and anything disabled, wrapping
        ///     round the ends. Returns -1 when the menu has nothing that can be chosen at all, which a caller has to
        ///     handle rather than assume away.
        /// </summary>
        /// <param name="from">Where to start looking; may be outside the menu.</param>
        /// <param name="step">+1 to look downward, -1 upward.</param>
        /// <returns>The index of the next selectable entry, or -1.</returns>
        public int NextSelectable(int from, int step)
        {
            if (_entries.Count == 0)
                return -1;

            var index = from;
            for (var attempt = 0; attempt < _entries.Count; attempt++)
            {
                index += step;

                if (index < 0)
                    index = _entries.Count - 1;
                else if (index >= _entries.Count)
                    index = 0;

                if (_entries[index].IsSelectable)
                    return index;
            }

            return -1;
        }
    }
}

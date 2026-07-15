// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.Collections.Generic;
using System.Linq;
using WolfCurses.Window;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     Holds the configuration and current state of a <see cref="SelectList" />: the options to show, whether more
    ///     than one may be chosen, the current page, the multi-select checkbox state, and the result callbacks. It is
    ///     the <see cref="WindowData" /> of the <see cref="SelectListWindow" />, so the options are kept as display
    ///     strings (the generic <see cref="SelectList" /> entry points map items to labels and translate the chosen
    ///     indices back to items).
    /// </summary>
    public sealed class SelectListData : WindowData
    {
        private readonly HashSet<int> _selected = new();

        /// <summary>Heading shown above the list.</summary>
        public string Title { get; private set; }

        /// <summary>The option labels, in order.</summary>
        public IReadOnlyList<string> Options { get; private set; } = Array.Empty<string>();

        /// <summary>True when more than one option may be selected before confirming.</summary>
        public bool MultiSelect { get; private set; }

        /// <summary>Zero-based index of the page currently displayed.</summary>
        public int PageIndex { get; set; }

        /// <summary>Invoked with the chosen option indices (one for single-select) when the user confirms.</summary>
        public Action<IReadOnlyList<int>> OnChosen { get; private set; }

        /// <summary>Invoked if the user cancels. Optional.</summary>
        public Action OnCancelled { get; private set; }

        /// <summary>True once <see cref="Initialize" /> has run.</summary>
        public bool Initialized { get; private set; }

        /// <summary>
        ///     Configures the dialog. Null options become an empty list; null labels become empty strings.
        ///     <paramref name="initiallySelected" /> pre-checks those option indices (multi-select only); out-of-range
        ///     and duplicate indices are ignored, and it has no effect for single-select.
        /// </summary>
        public void Initialize(string title, IEnumerable<string> options, bool multiSelect,
            Action<IReadOnlyList<int>> onChosen, Action onCancelled, IEnumerable<int> initiallySelected = null)
        {
            Title = title;
            Options = options?.Select(o => o ?? string.Empty).ToList() ?? new List<string>();
            MultiSelect = multiSelect;
            OnChosen = onChosen;
            OnCancelled = onCancelled;
            PageIndex = 0;
            _selected.Clear();

            // Pre-check the caller's starting set so the confirmed set can be treated as the new state. Only meaningful
            // for multi-select (single-select confirms on the first number press and never reads _selected); filter to
            // valid indices so a stale or out-of-range set can't smuggle in a phantom selection.
            if (multiSelect && initiallySelected != null)
                foreach (var index in initiallySelected)
                    if (index >= 0 && index < Options.Count)
                        _selected.Add(index);

            Initialized = true;
        }

        /// <summary>Whether the option at <paramref name="index" /> is currently checked (multi-select).</summary>
        public bool IsSelected(int index)
        {
            return _selected.Contains(index);
        }

        /// <summary>Toggles the checkbox for the option at <paramref name="index" /> (multi-select).</summary>
        public void Toggle(int index)
        {
            if (index < 0 || index >= Options.Count)
                return;

            if (!_selected.Add(index))
                _selected.Remove(index);
        }

        /// <summary>Checks every option (multi-select).</summary>
        public void SelectAll()
        {
            for (var i = 0; i < Options.Count; i++)
                _selected.Add(i);
        }

        /// <summary>Clears every checkbox (multi-select).</summary>
        public void SelectNone()
        {
            _selected.Clear();
        }

        /// <summary>The currently checked option indices, ascending.</summary>
        public IReadOnlyList<int> SelectedIndices()
        {
            var list = _selected.ToList();
            list.Sort();
            return list;
        }
    }
}

// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/17/2026

using System;
using WolfCurses.Graphics;

namespace WolfCurses.Window.Control
{
    /// <summary>
    ///     The moving highlight behind every arrow-key-driven list in the library: menus, pickers, the file browser,
    ///     a message box's buttons. Pure state and arithmetic — it holds an index into a list it never sees, moves it
    ///     for navigation keys handed to <see cref="HandleKey" />, and leaves drawing to its owner (usually via
    ///     <see cref="DecorateRow" />). Like the other <see cref="WolfCurses.Window.Control" /> widgets it knows
    ///     nothing about windows, so it is unit-tested directly.
    ///     <para>
    ///         The highlight starts hidden (<see cref="HasSelection" /> false) and appears on the first movement key:
    ///         the next-key selects the first item and the previous-key the last, which is what those keys mean when
    ///         nothing is selected yet. This is also the library's compatibility stance — a menu rendered before
    ///         anyone touches an arrow key is byte-identical to one from before this class existed, so an application
    ///         (and its tests) that never uses arrows never sees a difference. Owners that want the ncurses look of a
    ///         cursor visible from the first frame call <see cref="Select" /> up front, which is what the built-in
    ///         modal controls do.
    ///     </para>
    ///     <para>
    ///         Navigation deliberately owns only one axis (<see cref="Horizontal" /> picks which), so the other axis
    ///         stays free for the owner — a vertical list can spend Left/Right on paging, a horizontal button bar
    ///         leaves Up/Down alone. Home/End always work; PageUp/PageDown work when <see cref="PageSize" /> is set
    ///         and clamp rather than wrap, because a page jump that teleports past an end is disorienting where a
    ///         single step wrapping around it is expected.
    ///     </para>
    /// </summary>
    public sealed class ListNavigator
    {
        /// <summary>Inverse video on: how the highlighted row is emphasized when escapes are allowed at all.</summary>
        private const string InverseOn = "\x1b[7m";

        /// <summary>Attribute reset, ending the inverse span at the end of the highlighted row.</summary>
        private const string InverseOff = "\x1b[0m";

        /// <summary>
        ///     Initializes a new instance of the <see cref="ListNavigator" /> class.
        /// </summary>
        /// <param name="count">How many items the list currently holds; grow or shrink later with <see cref="Resize" />.</param>
        /// <param name="horizontal">
        ///     True to navigate with Left/Right (a button bar), false for Up/Down (a vertical list, the default).
        /// </param>
        public ListNavigator(int count = 0, bool horizontal = false)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "A list cannot hold a negative number of items.");

            Count = count;
            Horizontal = horizontal;
        }

        /// <summary>How many items the list currently holds.</summary>
        public int Count { get; private set; }

        /// <summary>True when Left/Right move the highlight instead of Up/Down.</summary>
        public bool Horizontal { get; }

        /// <summary>
        ///     Whether a single step past either end comes out the other side (the default) or stops. Page jumps
        ///     always stop at the ends regardless.
        /// </summary>
        public bool Wrap { get; set; } = true;

        /// <summary>
        ///     How far PageUp/PageDown jump. Zero (the default) leaves those keys unhandled, for lists that are not
        ///     paged.
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        ///     True once anything is highlighted. False until the first movement key (or <see cref="Select" />), and
        ///     false again whenever the list becomes empty.
        /// </summary>
        public bool HasSelection { get; private set; }

        /// <summary>The highlighted index. Only meaningful while <see cref="HasSelection" /> is true.</summary>
        public int Index { get; private set; }

        /// <summary>
        ///     Moves the highlight for a navigation key and reports whether the key was one. A false return means the
        ///     key is not this navigator's business — the owner is free to spend it on something else — and an empty
        ///     list handles nothing at all.
        /// </summary>
        /// <param name="key">The key the focused window or form was handed.</param>
        /// <returns>TRUE if the key was a navigation key this navigator consumed.</returns>
        public bool HandleKey(ConsoleKey key)
        {
            if (Count <= 0)
                return false;

            var previousKey = Horizontal ? ConsoleKey.LeftArrow : ConsoleKey.UpArrow;
            var nextKey = Horizontal ? ConsoleKey.RightArrow : ConsoleKey.DownArrow;

            if (key == nextKey)
            {
                // The first movement key is what summons the highlight; "next" from nowhere means the first item.
                if (!HasSelection)
                    Select(0);
                else
                    StepBy(1);
                return true;
            }

            if (key == previousKey)
            {
                if (!HasSelection)
                    Select(Count - 1);
                else
                    StepBy(-1);
                return true;
            }

            switch (key)
            {
                case ConsoleKey.Home:
                    Select(0);
                    return true;
                case ConsoleKey.End:
                    Select(Count - 1);
                    return true;
                case ConsoleKey.PageUp when PageSize > 0:
                    Select(Math.Max(0, (HasSelection ? Index : 0) - PageSize));
                    return true;
                case ConsoleKey.PageDown when PageSize > 0:
                    Select(Math.Min(Count - 1, (HasSelection ? Index : 0) + PageSize));
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        ///     Highlights the given index, clamped into range. Does nothing on an empty list. This is also how an
        ///     owner shows the cursor from the first frame instead of waiting for an arrow key.
        /// </summary>
        /// <param name="index">The index to highlight; values past either end land on that end.</param>
        public void Select(int index)
        {
            if (Count <= 0)
                return;

            Index = Math.Clamp(index, 0, Count - 1);
            HasSelection = true;
        }

        /// <summary>
        ///     Tells the navigator the list changed size. The highlight survives when it still points at a real item,
        ///     clamps onto the new last item when the list shrank past it, and disappears when the list is empty —
        ///     never left dangling past the end for the owner to index with.
        /// </summary>
        /// <param name="count">The list's new item count.</param>
        public void Resize(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "A list cannot hold a negative number of items.");

            Count = count;
            if (count <= 0)
            {
                HasSelection = false;
                Index = 0;
                return;
            }

            if (Index >= count)
                Index = count - 1;
        }

        /// <summary>
        ///     Prefixes a rendered row with the highlight cursor — <c>"&gt; "</c> when highlighted, the same two
        ///     columns of space when not, so rows stay aligned — and wraps the highlighted row in inverse video when
        ///     the environment allows escapes at all (<see cref="AnsiConsole.DetectColorMode" /> not
        ///     <see cref="AnsiColorModeEnum.None" />; inverse is an attribute rather than a color, but an environment
        ///     that asked for no color escapes gets no escapes of any kind). The marker is therefore the part of the
        ///     contract that is always present, and what a highlight means on a terminal that cannot emphasize.
        /// </summary>
        /// <param name="row">The row text, without any leading indent for the cursor.</param>
        /// <param name="highlighted">Whether this row is the highlighted one.</param>
        /// <returns>The row with its two-column cursor prefix, emphasized when highlighted.</returns>
        public static string DecorateRow(string row, bool highlighted)
        {
            return highlighted ? Emphasize("> " + row) : "  " + row;
        }

        /// <summary>
        ///     Wraps text in inverse video when the environment allows escapes at all, and returns it untouched when
        ///     it does not (<see cref="AnsiConsole.DetectColorMode" /> of <see cref="AnsiColorModeEnum.None" /> —
        ///     inverse is an attribute rather than a color, but an environment that asked for no color escapes gets
        ///     no escapes of any kind). Shared so every highlight in the library dims through the same gate; owners
        ///     whose shape is not a row — a message box's button bar — call this directly with markers of their own.
        /// </summary>
        /// <param name="text">The text to emphasize.</param>
        /// <returns>The text, emphasized as hard as the environment allows.</returns>
        public static string Emphasize(string text)
        {
            return AnsiConsole.DetectColorMode() == AnsiColorModeEnum.None
                ? text
                : InverseOn + text + InverseOff;
        }

        /// <summary>Takes one step, wrapping or stopping at the ends per <see cref="Wrap" />.</summary>
        private void StepBy(int delta)
        {
            var moved = Index + delta;
            if (Wrap)
                Index = ((moved % Count) + Count) % Count;
            else
                Index = Math.Clamp(moved, 0, Count - 1);
        }
    }
}

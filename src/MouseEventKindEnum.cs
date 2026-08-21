// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

namespace WolfCurses
{
    /// <summary>
    ///     What a <see cref="MouseEvent" /> is reporting: a button going down, the pointer moving, or a button
    ///     coming back up.
    ///     <para>
    ///         The library shipped presses only for a long time, and deliberately: a press is the whole of what a
    ///         grid game needs, and the other two are a firehose nobody was paying for. What they buy is everything
    ///         that has a <i>duration</i> rather than an instant. A pointer you can see needs to know where the mouse
    ///         is when no button is down; dragging a scrollbar thumb or sweeping a selection needs to know a button
    ///         is still held and then that it is not. None of those can be built out of presses, however many of
    ///         them arrive.
    ///     </para>
    ///     <para>
    ///         Still no wheel member, for the reason it never had one: every protocol encodes a wheel notch as a
    ///         press, and leaving it unrepresentable is what stops a fire-on-click game emptying its magazine on a
    ///         scroll.
    ///     </para>
    /// </summary>
    public enum MouseEventKindEnum
    {
        /// <summary>A button went down. The only kind the library reported before motion was added.</summary>
        Press = 0,

        /// <summary>
        ///     The pointer moved. <see cref="MouseEvent.Button" /> says which button is still held, so a drag is a
        ///     move with a button on it and a bare hover is a move with <see cref="MouseButtonEnum.None" />.
        /// </summary>
        Move = 1,

        /// <summary>A button came back up.</summary>
        Release = 2
    }
}

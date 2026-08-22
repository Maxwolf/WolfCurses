// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;

namespace WolfCurses
{
    /// <summary>
    ///     One thing the mouse did at one character cell: a button going down, the pointer moving, a button coming
    ///     back up, or the wheel turning. <see cref="Kind" /> says which.
    ///     <para>
    ///         <b><see cref="Column" /> and <see cref="Row" /> are cells counted from the top-left of the terminal
    ///         <i>window</i></b>, which is not the same thing as the console's screen buffer: on Windows the buffer
    ///         can be taller than the window and scrolled, so the producer subtracts <c>Console.WindowTop</c> before
    ///         building one of these. That is the same buffer-versus-window correction
    ///         <see cref="ConsolePresenter" /> already applies in the other direction, and getting it wrong puts
    ///         every click a screenful away from where the user pointed without anything appearing to fail.
    ///     </para>
    ///     <para>
    ///         With the built-in presenter drawing, row N happens to be line N of the last rendered frame — but that
    ///         is a property of that presenter, which a host may replace by subscribing to
    ///         <c>SceneGraph.ScreenBufferDirtyEvent</c>, and never a promise of this type.
    ///     </para>
    ///     <para>
    ///         <b>For a long time a press was the only kind there was</b>, because a press is the whole of what a
    ///         grid game or a menu acts on and the others are a flood every caller would immediately have to
    ///         throttle. What they buy is everything with a <i>duration</i> rather than an instant: a pointer you
    ///         can see, a thumb being dragged, a selection swept across a document. So motion stays opt-in per app
    ///         (<c>InputManager.ReportsMouseMotion</c>, default false, dropped inside the reader when unwanted, so
    ///         an app that does not use it pays nothing), and the wheel arrives as its own
    ///         <see cref="MouseEventKindEnum.Wheel" /> rather than as a button. See <see cref="MouseButtonEnum" />
    ///         for why that last distinction is not cosmetic.
    ///     </para>
    /// </summary>
    public readonly struct MouseEvent : IEquatable<MouseEvent>
    {
        /// <summary>Initializes a new instance of the <see cref="MouseEvent" /> struct.</summary>
        /// <param name="column">Cell column, counted from the left edge of the terminal window.</param>
        /// <param name="row">Cell row, counted from the top of the terminal window.</param>
        /// <param name="button">Which button went down.</param>
        /// <param name="modifiers">Which of shift, alt and control were held.</param>
        /// <param name="kind">Whether a button went down, the pointer moved, a button came up, or the wheel turned.</param>
        /// <param name="wheelDelta">How many notches the wheel turned; positive is away from the user.</param>
        public MouseEvent(int column, int row, MouseButtonEnum button, ConsoleModifiers modifiers = 0,
            MouseEventKindEnum kind = MouseEventKindEnum.Press, int wheelDelta = 0)
        {
            Column = column;
            Row = row;
            Button = button;
            Modifiers = modifiers;
            Kind = kind;
            WheelDelta = wheelDelta;
        }

        /// <summary>Cell column, counted from the left edge of the terminal window.</summary>
        public int Column { get; }

        /// <summary>Cell row, counted from the top of the terminal window.</summary>
        public int Row { get; }

        /// <summary>
        ///     Which button this is about: the one that went down or came up, and for a
        ///     <see cref="MouseEventKindEnum.Move" /> whichever buttons are still <i>held</i>. That last reading is
        ///     the whole of what tells a drag from a hover without a further kind, and it is why a bare hover
        ///     arrives as a move carrying <see cref="MouseButtonEnum.None" />.
        /// </summary>
        public MouseButtonEnum Button { get; }

        /// <summary>
        ///     Whether this is a button going down, the pointer moving, a button coming up, or the wheel turning.
        ///     Defaults to <see cref="MouseEventKindEnum.Press" />, which is what every event was before the others
        ///     existed and is what keeps code written against the old shape meaning what it meant.
        /// </summary>
        public MouseEventKindEnum Kind { get; }

        /// <summary>
        ///     How far the wheel turned, in notches, and which way: positive is away from the user, which every
        ///     platform treats as scrolling up. Zero for every kind that is not
        ///     <see cref="MouseEventKindEnum.Wheel" />.
        ///     <para>
        ///         Notches rather than raw units, because the raw number is a platform detail (Windows counts in
        ///         120ths of a notch) and every caller would otherwise divide it by the same constant.
        ///     </para>
        /// </summary>
        public int WheelDelta { get; }

        /// <summary>
        ///     Which of shift, alt and control were held. The BCL's own <see cref="ConsoleModifiers" /> rather than a
        ///     fourth new type: terminals report exactly those three, which is that enum's entire membership, and a
        ///     shifted click should compare against a shifted key press using the same type.
        /// </summary>
        public ConsoleModifiers Modifiers { get; }

        /// <inheritdoc />
        public bool Equals(MouseEvent other)
        {
            return Column == other.Column && Row == other.Row &&
                   Button == other.Button && Modifiers == other.Modifiers &&
                   Kind == other.Kind && WheelDelta == other.WheelDelta;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is MouseEvent other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Column, Row, (int) Button, (int) Modifiers, (int) Kind, WheelDelta);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var what = Kind == MouseEventKindEnum.Wheel
                ? $"Wheel {WheelDelta:+0;-0;0}"
                : $"{Kind} {Button}";

            return $"{what} at {Column},{Row}" + (Modifiers == 0 ? string.Empty : $" ({Modifiers})");
        }

        /// <summary>
        ///     Whether two mouse events describe the same thing happening at the same cell.
        ///     <para>
        ///         <b><see cref="Kind" /> and <see cref="WheelDelta" /> are part of that and were once left out</b>,
        ///         which made a release equal to the press that preceded it and a wheel notch equal to the same
        ///         notch turned the other way. Nothing in the library compares these - the queue keeps every event
        ///         rather than deduplicating, deliberately - so it went unnoticed, and it is exactly the sort of
        ///         thing a caller building an "only redraw when it moved" gate would have leaned on.
        ///     </para>
        /// </summary>
        /// <param name="left">The first event.</param>
        /// <param name="right">The second event.</param>
        /// <returns>TRUE when they match.</returns>
        public static bool operator ==(MouseEvent left, MouseEvent right) => left.Equals(right);

        /// <summary>Whether two mouse events differ.</summary>
        /// <param name="left">The first event.</param>
        /// <param name="right">The second event.</param>
        /// <returns>TRUE when they differ.</returns>
        public static bool operator !=(MouseEvent left, MouseEvent right) => !left.Equals(right);
    }
}

// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

using System;

namespace WolfCurses
{
    /// <summary>
    ///     One mouse button going down at one character cell — the whole of what this library reports about the
    ///     mouse, on purpose.
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
    ///         There is no release, no motion and no wheel. A press is what a game or a menu acts on, and each of
    ///         the others costs public surface plus a flood of events that every caller would immediately have to
    ///         throttle — see <see cref="MouseButtonEnum" /> for why the wheel in particular is not merely
    ///         unreported but unrepresentable.
    ///     </para>
    /// </summary>
    public readonly struct MouseEvent : IEquatable<MouseEvent>
    {
        /// <summary>Initializes a new instance of the <see cref="MouseEvent" /> struct.</summary>
        /// <param name="column">Cell column, counted from the left edge of the terminal window.</param>
        /// <param name="row">Cell row, counted from the top of the terminal window.</param>
        /// <param name="button">Which button went down.</param>
        /// <param name="modifiers">Which of shift, alt and control were held.</param>
        public MouseEvent(int column, int row, MouseButtonEnum button, ConsoleModifiers modifiers = 0)
        {
            Column = column;
            Row = row;
            Button = button;
            Modifiers = modifiers;
        }

        /// <summary>Cell column, counted from the left edge of the terminal window.</summary>
        public int Column { get; }

        /// <summary>Cell row, counted from the top of the terminal window.</summary>
        public int Row { get; }

        /// <summary>Which button went down.</summary>
        public MouseButtonEnum Button { get; }

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
                   Button == other.Button && Modifiers == other.Modifiers;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is MouseEvent other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Column, Row, (int) Button, (int) Modifiers);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Button} at {Column},{Row}" + (Modifiers == 0 ? string.Empty : $" ({Modifiers})");
        }

        /// <summary>Whether two mouse events describe the same press.</summary>
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

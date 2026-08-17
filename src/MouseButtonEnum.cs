// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/17/2026

namespace WolfCurses
{
    /// <summary>
    ///     Which mouse button went down.
    ///     <para>
    ///         <b>There is deliberately no wheel member, and that omission is doing real work.</b> Every mouse
    ///         protocol in use encodes a wheel notch as a button <i>press</i> — SGR reports it as
    ///         <c>ESC[&lt;64;x;yM</c>, with the same final byte a real press uses, and the specification says a wheel
    ///         never reports a release at all. So the obvious handler, "on a press, do the thing", empties a
    ///         magazine when the player scrolls. With no value this type can carry for a wheel, the reader has to
    ///         drop those records at the mask and the bug becomes unrepresentable rather than merely guarded against.
    ///     </para>
    /// </summary>
    public enum MouseButtonEnum
    {
        /// <summary>No button — what <c>default(MouseEvent)</c> carries, so an empty value cannot read as a click.</summary>
        None = 0,

        /// <summary>The primary button.</summary>
        Left = 1,

        /// <summary>The wheel button, pressed rather than rolled.</summary>
        Middle = 2,

        /// <summary>The secondary button.</summary>
        Right = 3
    }
}

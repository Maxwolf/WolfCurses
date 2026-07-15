// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

namespace WolfCurses.Window.Control
{
    /// <summary>The line style used to draw a <see cref="Box" />.</summary>
    public enum BoxBorderEnum
    {
        /// <summary>Single line: <c>┌─┐│└┘</c>.</summary>
        Single,

        /// <summary>Double line: <c>╔═╗║╚╝</c>.</summary>
        Double,

        /// <summary>Single line with rounded corners: <c>╭─╮│╰╯</c>.</summary>
        Rounded,

        /// <summary>Plain ASCII: <c>+-+|++</c>, for terminals without box-drawing glyphs.</summary>
        Ascii,

        /// <summary>No border at all — the content is just padded into a rectangular block.</summary>
        None
    }
}

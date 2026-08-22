// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/22/2026

using System;
using System.Text;

namespace WolfCurses.Games
{
    /// <summary>
    ///     The one-line control legend a steered game draws as its own last row.
    ///     <para>
    ///         <b>It cannot live on the window's prompt, and for eight screens here it did.</b> A game that is
    ///         steered rather than typed at overrides <c>InputFillsBuffer</c> to false, and
    ///         <c>WindowManager.AcceptingInput</c> answers false for exactly that shape, so <c>SceneGraph</c> draws
    ///         no prompt row at all while the game is up. Every <c>ParentWindow.PromptText</c> those screens set was
    ///         therefore written to a row nobody could ever see. That is how Labyrinth came to keep its whole control
    ///         list somewhere invisible, and how Battlezone came to trim its own to fit a width that did not apply.
    ///     </para>
    ///     <para>
    ///         <b>The row is free.</b> <c>ConsolePresenter</c> clips the frame to one row less than the console's
    ///         height, keeping the last for the prompt, so a screen whose prompt is suppressed has that row spare and
    ///         spending it here costs nothing it was not already paying.
    ///     </para>
    ///     <para>
    ///         <b>Keep a legend inside about seventy-six columns.</b> Auto-wrap is off for the frame, so an over-wide
    ///         row is truncated rather than wrapped, and the end of a legend is where ESC usually is. As prompt text
    ///         this did not matter, because none of it was drawn; two of these were well over a hundred characters
    ///         long by the time anybody looked.
    ///     </para>
    /// </summary>
    internal static class GameControls
    {
        /// <summary>Appends the legend as the frame's final row.</summary>
        /// <param name="body">The frame being composed.</param>
        /// <param name="controls">What the keys do.</param>
        internal static void Append(StringBuilder body, string controls)
        {
            body.Append(Environment.NewLine).Append(controls);
        }
    }
}

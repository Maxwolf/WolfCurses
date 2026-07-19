// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 12/31/2015@4:49 AM

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace WolfCurses.Window.Form
{
    /// <summary>
    ///     Defines interface for game mode state which can show data, accept input, add new game modes, set new state, and
    ///     have user data custom per implementation.
    /// </summary>
    public interface IForm : IComparer<IForm>, IComparable<IForm>, ITick
    {
        /// <summary>
        ///     Determines if user input is currently allowed to be typed and filled into the input buffer.
        /// </summary>
        /// <remarks>Default is FALSE. Setting to TRUE allows characters and input buffer to be read when submitted.</remarks>
        bool InputFillsBuffer { get; }

        /// <summary>
        ///     Determines if this dialog state is allowed to receive any input at all, even empty line returns. This is useful for
        ///     preventing the player from leaving a particular dialog until you are ready or finished processing some data.
        /// </summary>
        bool AllowInput { get; }

        /// <summary>
        ///     Intended to be overridden in abstract class by generics to provide method to return object that contains all the
        ///     data for parent game Windows.
        /// </summary>
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        WindowData UserData { get; }

        /// <summary>
        ///     Returns a text only representation of the current game Windows state. Could be a statement, information, question
        ///     waiting input, etc.
        /// </summary>
        /// <returns>
        ///     The text user interface.<see cref="string" />.
        /// </returns>
        string OnRenderForm();

        /// <summary>Fired when the game Windows current state is not null and input buffer does not match any known command.</summary>
        /// <param name="input">Contents of the input buffer which didn't match any known command in parent game Windows.</param>
        void OnInputBufferReturned(string input);

        /// <summary>
        ///     Fired when the host reports a key press and this form is the focused window's current one. See
        ///     <see cref="IWindow.OnKeyPressed(ConsoleKey)" /> for why a key press is a separate thing from the input
        ///     buffer: an arrow key has no character, so it cannot be typed and would otherwise go unheard. Implemented
        ///     as a default interface member so existing forms need not change.
        ///     <para>
        ///         ENTER and BACKSPACE never arrive here: the standard routing consumes both as buffer control before
        ///         any key press is reported. ENTER reaches this form as <see cref="OnInputBufferReturned" /> instead,
        ///         and BACKSPACE only ever edits the buffer.
        ///     </para>
        /// </summary>
        /// <param name="key">The key that was pressed.</param>
        void OnKeyPressed(ConsoleKey key)
        {
        }

        /// <summary>
        ///     Fired when the host reports a key press with the whole <see cref="ConsoleKeyInfo" /> attached — the
        ///     overload the parent window dispatches. The default implementation forwards to
        ///     <see cref="OnKeyPressed(ConsoleKey)" />, so a form that only knows the older member behaves exactly as
        ///     before; a form that needs to tell shifted keys apart implements this one and reads
        ///     <see cref="ConsoleKeyInfo.KeyChar" /> or <see cref="ConsoleKeyInfo.Modifiers" />.
        /// </summary>
        /// <param name="keyInfo">The key press exactly as the host saw it.</param>
        void OnKeyPressed(ConsoleKeyInfo keyInfo)
        {
            OnKeyPressed(keyInfo.Key);
        }

        /// <summary>
        ///     Fired after the state has been completely attached to the simulation letting the state know it can browse the user
        ///     data and other properties below it.
        /// </summary>
        void OnFormPostCreate();

        /// <summary>
        ///     Fired when the window is activated and or refocused after another window was removed from being on-top of it.
        ///     Useful for re-initializing form data after something like a random event runs which might kill people or alter the
        ///     vehicle inventory.
        /// </summary>
        void OnFormActivate();
    }
}
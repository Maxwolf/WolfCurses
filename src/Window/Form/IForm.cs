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
        ///     When true, ENTER and BACKSPACE arrive as ordinary key presses on <see cref="OnKeyPressed(ConsoleKeyInfo)" />
        ///     instead of being spent on the input buffer. Default is FALSE, so every existing screen keeps the
        ///     routing it has always had.
        ///     <para>
        ///         <b>This exists because those two keys are otherwise unreachable</b>, and an editor cannot be
        ///         written without them.
        ///         <see cref="WolfCurses.Core.InputManager.SendConsoleKey" /> treats ENTER as "submit the buffer" and
        ///         BACKSPACE as "rub a character out of it" before any key press is queued, which is exactly right
        ///         for a prompt and leaves a screen editing a document with no way to see a backspace at all.
        ///     </para>
        ///     <para>
        ///         Opting in means giving up the buffer's meaning of those keys, which is the point rather than a
        ///         cost: a screen editing text is not collecting a command. Such a screen almost always also returns
        ///         false from <see cref="InputFillsBuffer" />, so typed characters do not pile up in a prompt nobody
        ///         is reading.
        ///     </para>
        /// </summary>
        bool EditsText => false;

        /// <summary>
        ///     Intended to be overridden in abstract class by generics to provide method to return object that contains all the
        ///     data for parent game Windows.
        /// </summary>
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        WindowData UserData { get; }

        /// <summary>
        ///     Returns a text only representation of the current game Windows state. Could be a statement, information, question
        ///     waiting input, etc.
        ///     <para>
        ///         <b>This is called on every system tick — roughly a thousand times a second — and not once per
        ///         frame.</b> <see cref="Core.SceneGraph" /> asks the focused window for the whole screen on every
        ///         tick and compares the answer against the last one, so a form that <i>builds</i> its text in here
        ///         pays for that a thousand times a second in order to show perhaps thirty of the results. Anything
        ///         costing real work — laying out a playfield, compositing a picture, resampling an image — belongs in
        ///         <see cref="ITick.OnTick" />, paced by an <see cref="IntervalTimer" /> and stored in a field that
        ///         this method hands straight back. A form whose text is genuinely cheap, or which only changes when
        ///         something else changed it, can compose here and loses nothing.
        ///     </para>
        ///     <para>
        ///         The other half of that pattern is <see cref="Form{TData}.RestartOnActivate" />: a form stops being
        ///         ticked while a modal window sits on top of it, but its clock does not stop measuring — so without
        ///         registering the timer, the form comes back owing every step that fell due and takes them all at
        ///         once.
        ///     </para>
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
        ///     Fired when a mouse button goes down somewhere on the terminal, if the host asked for the mouse at all.
        ///     <para>
        ///         A mouse press never touches the input buffer — not as content, since a click has no character to
        ///         contribute, and not as buffer control either, since <c>InputBuffer</c> is append-only and has no
        ///         caret to move. So unlike ENTER and BACKSPACE nothing about a click is consumed before it gets
        ///         here.
        ///     </para>
        /// </summary>
        /// <param name="mouse">Where the press landed and which button it was.</param>
        void OnMousePressed(MouseEvent mouse)
        {
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
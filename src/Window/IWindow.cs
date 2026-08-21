// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 12/31/2015@4:49 AM

using System;
using System.Collections.Generic;
using WolfCurses.Window.Form;

namespace WolfCurses.Window
{
    /// <summary>
    ///     Underlying game Windows interface, used by base simulation to keep track of what data should currently have control
    ///     over the simulation details. Only top most game Windows will ever be ticked.
    /// </summary>
    public interface IWindow :
        IComparer<IWindow>,
        IComparable<IWindow>,
        ITick
    {
        /// <summary>
        ///     Determines if the game Windows should not be ticked if it is active but instead removed. The Windows when set to
        ///     being
        ///     removed will not actually be removed until the simulation attempts to tick it and realizes that this is set to true
        ///     and then it will be removed.
        /// </summary>
        bool ShouldRemoveMode { get; }

        /// <summary>
        ///     Determines if user input is currently allowed to be typed and filled into the input buffer.
        /// </summary>
        /// <remarks>Default is FALSE. Setting to TRUE allows characters and input buffer to be read when submitted.</remarks>
        bool AcceptsInput { get; }

        /// <summary>
        ///     When true, ENTER and BACKSPACE are delivered to this window as key presses rather than being spent as
        ///     input-buffer control. Default is false, and a default interface member so existing windows need not
        ///     change. <see cref="Window{TCommands,TData}" /> answers with whatever its attached form says, since a
        ///     screen that edits text is nearly always a form; see <see cref="Form.IForm.EditsText" />.
        /// </summary>
        bool EditsText => false;

        /// <summary>
        ///     Holds the current state which this Windows is in, a Windows will cycle through available states until it is
        ///     finished and
        ///     then detach.
        /// </summary>
        IForm CurrentForm { get; }

        /// <summary>
        ///     Intended to be overridden in abstract class by generics to provide method to return object that contains all the
        ///     data for parent game Windows.
        /// </summary>
        WindowData UserData { get; }

        /// <summary>
        ///     Determines what is asked at the bottom of a windows menu system. By default this is "What is your choice?" and can
        ///     be changed per window, and by any active forms.
        /// </summary>
        string PromptText { get; set; }

        /// <summary>
        ///     When true, the renderer shows the echoed input buffer as asterisks instead of the typed characters, for
        ///     password-style prompts. Default is false. Implemented as a default interface member so existing windows
        ///     need not change.
        /// </summary>
        bool MaskInput => false;

        /// <summary>
        ///     The running simulation this window belongs to, or null for an implementation that never learned it.
        ///     <para>
        ///         This is the third of the small enablers that let forms and library-provided controls do real work
        ///         (after form discovery scanning the library assembly, and <c>Window</c> exposing its protected
        ///         <c>SimUnit</c> to command handlers): a form holds only its parent window, and some of what a form
        ///         legitimately needs lives on the simulation — the input manager, say, so a form that treats SPACE as
        ///         a toggle key can clear the space the input buffer just swallowed. Implemented as a default interface
        ///         member so existing windows need not change; <c>Window</c> supplies the real value.
        ///     </para>
        /// </summary>
        SimulationApp SimUnit => null;

        /// <summary>
        ///     Fired when the host reports a key press, before and apart from anything the input buffer does with it.
        ///     <para>
        ///         This is the only way to hear a key that is not text. The input buffer holds a line the user is typing,
        ///         so it takes characters — and an arrow key, a function key or Home has no character to give it, which
        ///         means it is dropped. That is the right answer for a prompt and the wrong one for anything that wants
        ///         to be steered, so both are offered: printable keys still fill the buffer exactly as they always have,
        ///         and every key is also reported here for whoever cares.
        ///     </para>
        ///     <para>
        ///         ENTER and BACKSPACE never arrive here: the standard routing
        ///         (<see cref="WolfCurses.Core.InputManager.SendConsoleKey" />) consumes both as buffer control before
        ///         a key press is ever reported, so a <c>case ConsoleKey.Enter:</c> in an override is dead code that
        ///         compiles and silently never runs. ENTER reaches a form as
        ///         <see cref="Form.IForm.OnInputBufferReturned" /> (the submitted buffer, possibly empty), and
        ///         BACKSPACE only ever edits the buffer. Implemented as a default interface member so existing windows
        ///         need not change.
        ///     </para>
        /// </summary>
        /// <param name="key">The key that was pressed.</param>
        void OnKeyPressed(ConsoleKey key)
        {
        }

        /// <summary>
        ///     Fired when the host reports a key press, carrying the whole <see cref="ConsoleKeyInfo" /> rather than
        ///     the bare key — this is the overload the simulation dispatches. The default implementation forwards to
        ///     <see cref="OnKeyPressed(ConsoleKey)" />, so an implementation that only knows the older member behaves
        ///     exactly as before.
        ///     <para>
        ///         The full info exists because <see cref="ConsoleKey" /> alone cannot tell shifted punctuation apart:
        ///         ',' and '&lt;' both report <see cref="ConsoleKey.OemComma" />, and only
        ///         <see cref="ConsoleKeyInfo.KeyChar" /> (or the shift modifier) knows which was pressed. The same
        ///         ENTER/BACKSPACE routing note as the other overload applies: neither ever arrives here.
        ///     </para>
        /// </summary>
        /// <param name="keyInfo">The key press exactly as the host saw it.</param>
        void OnKeyPressed(ConsoleKeyInfo keyInfo)
        {
            OnKeyPressed(keyInfo.Key);
        }

        /// <summary>
        ///     Fired when a mouse button goes down somewhere on the terminal, if the host asked for the mouse at all
        ///     with <see cref="Graphics.AnsiConsole.EnableMouse" />. Does nothing unless overridden, so every window
        ///     written before the mouse existed compiles and behaves exactly as it did.
        /// </summary>
        /// <summary>
        ///     Fired for every mouse event the host reports: a press, a release, or a move. Presses are forwarded to
        ///     <see cref="OnMousePressed" /> by the default implementation, so a window written before motion
        ///     existed keeps working untouched and never sees the other two.
        ///     <para>
        ///         Releases and moves are what anything with a <i>duration</i> is built from: a pointer you can see,
        ///         a scrollbar thumb being dragged, a selection swept across a document. None of those can be made
        ///         out of presses however many arrive. Moves are only delivered when the application asked for them
        ///         through <see cref="Core.InputManager.ReportsMouseMotion" />, because they arrive once per cell
        ///         the pointer crosses.
        ///     </para>
        /// </summary>
        /// <param name="mouse">What happened, and where.</param>
        void OnMouseEvent(MouseEvent mouse)
        {
            if (mouse.Kind == MouseEventKindEnum.Press)
                OnMousePressed(mouse);
        }

        /// <param name="mouse">Where the press landed and which button it was.</param>
        void OnMousePressed(MouseEvent mouse)
        {
        }

        /// <summary>Creates and adds the specified type of state to currently active game Windows.</summary>
        /// <param name="stateType">The state Type.</param>
        void SetForm(Type stateType);

        /// <summary>
        ///     Removes the current state from the active game Windows.
        /// </summary>
        void ClearForm();

        /// <summary>
        ///     Sets the flag for this game Windows to be removed the next time it is ticked by the simulation.
        /// </summary>
        void RemoveWindowNextTick();

        /// <summary>
        ///     Grabs the text user interface string that will be used for debugging on console application.
        /// </summary>
        /// <returns>
        ///     The <see cref="string" />.
        /// </returns>
        string OnRenderWindow();

        /// <summary>
        ///     Intended to be used by base simulation to pass along the input buffer after the user has typed several characters
        ///     into the input buffer. This is used when allowing the user to input custom strings like names for their party
        ///     members.
        /// </summary>
        /// <param name="input">The input.</param>
        void SendCommand(string input);

        /// <summary>
        ///     Called after the Windows has been added to list of modes and made active.
        /// </summary>
        void OnWindowPostCreate();

        /// <summary>
        ///     Called when the Windows manager in simulation makes this Windows the currently active game Windows. Depending on
        ///     order of
        ///     modes this might not get called until the Windows is actually ticked by the simulation.
        /// </summary>
        void OnWindowActivate();

        /// <summary>
        ///     Fired when the simulation adds a game Windows that is not this Windows. Used to execute code in other modes that
        ///     are not
        ///     the active Windows anymore one last time.
        /// </summary>
        void OnWindowAdded();
    }
}
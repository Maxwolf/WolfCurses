// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 12/31/2015@2:38 PM

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WolfCurses.Core
{
    /// <summary>
    ///     Deals with keep track of input to the simulation via whatever form that may end up taking. The default
    ///     implementation is a text user interface (TUI) which allows for the currently accepted commands to be seen and only
    ///     then accepted.
    /// </summary>
    public sealed class InputManager : Module.Module
    {
        /// <summary>
        ///     Holds a constant representation of the string telling the user to press enter key to continue so we don't repeat
        ///     ourselves.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public const string PRESSENTER = "Press ENTER KEY to continue";

        /// <summary>
        ///     Reference to simulation that is controlling the input manager.
        /// </summary>
        private readonly SimulationApp _simUnit;

        /// <summary>
        ///     Holds a series of commands that need to be executed in the order they come out of the collection.
        /// </summary>
        private Queue<string> _commandQueue;

        /// <summary>
        ///     Holds key presses waiting to be handed to the focused window.
        ///     <para>
        ///         Deliberately not the command queue, which drops a command identical to one already waiting — sensible
        ///         for a state-based simulation where asking twice means nothing, and quite wrong here: holding an arrow
        ///         key is a stream of identical presses and every one of them is meant to move something.
        ///     </para>
        /// </summary>
        private Queue<ConsoleKey> _keyQueue;

        /// <summary>
        ///     Initializes a new instance of the <see cref="InputManager" /> class.
        /// </summary>
        /// <param name="simUnit">Core simulation which is controlling the window manager.</param>
        public InputManager(SimulationApp simUnit)
        {
            _simUnit = simUnit;
            _commandQueue = new Queue<string>();
            _keyQueue = new Queue<ConsoleKey>();
            InputBuffer = string.Empty;
        }

        /// <summary>
        ///     Input buffer that we will use to hold characters until need to send them to simulation.
        /// </summary>
        public string InputBuffer { get; private set; }

        /// <summary>
        ///     Fired when the simulation is closing and needs to clear out any data structures that it created so the program can
        ///     exit cleanly.
        /// </summary>
        public override void Destroy()
        {
            // Clear the input buffer.
            InputBuffer = string.Empty;

            // Clear the command queue.
            _commandQueue.Clear();
            _commandQueue = null;

            // And any key presses that arrived but were never handed on.
            _keyQueue.Clear();
            _keyQueue = null;
        }

        /// <summary>
        ///     Called when the simulation is ticked by underlying operating system, game engine, or potato. Each of these system
        ///     ticks is called at unpredictable rates, however if not a system tick that means the simulation has processed enough
        ///     of them to fire off event for fixed interval that is set in the core simulation by constant in milliseconds.
        /// </summary>
        /// <remarks>Default is one second or 1000ms.</remarks>
        /// <param name="systemTick">
        ///     TRUE if ticked unpredictably by underlying operating system, game engine, or potato. FALSE if
        ///     pulsed by game simulation at fixed interval.
        /// </param>
        /// <param name="skipDay">
        ///     Determines if the simulation has force ticked without advancing time or down the trail. Used by
        ///     special events that want to simulate passage of time without actually any actual time moving by.
        /// </param>
        public override void OnTick(bool systemTick, bool skipDay = false)
        {
            // Key presses first, and all of them rather than one a tick: they are moments rather than instructions, so a
            // second one waiting does not replace the first, and anything holding a key down would fall behind forever
            // if only one were spent per tick. Ahead of the early return below on purpose — a tick with no commands in
            // it is still a tick, and was the obvious way to lose every key press in a screen that has no commands.
            while (_keyQueue.Count > 0)
                _simUnit.WindowManager.FocusedWindow?.OnKeyPressed(_keyQueue.Dequeue());

            // Skip if there are no commands to tick.
            if (_commandQueue.Count <= 0)
                return;

            // Dequeue the next command to send and pass along to currently active game Windows if it exists.
            _simUnit.WindowManager.FocusedWindow?.SendCommand(_commandQueue.Dequeue());
        }

        /// <summary>
        ///     Clears the input buffer and submits whatever was in there to the simulation for processing. Implementation is left
        ///     up the game simulation itself entirely.
        /// </summary>
        public void SendInputBufferAsCommand()
        {
            // Destroy the input buffer if we are not accepting commands but return is pressed anyway, so stale
            // typed text never leaks through as a command and only an empty string is passed along.
            if (!_simUnit.WindowManager.AcceptingInput)
                InputBuffer = string.Empty;

            // Trim the result of the input so no extra whitespace at front or end exists.
            var lineBufferTrimmed = InputBuffer.Trim();

            // Send trimmed line buffer to game simulation, if not accepting input we just pass along empty string.
            AddCommandToQueue(lineBufferTrimmed);

            // Always forcefully clear the input buffer after returning it, this makes it ready for more input.
            InputBuffer = string.Empty;
        }

        /// <summary>
        ///     Fired when the simulation receives an individual character from then input system. Depending on what it is we will
        ///     do something, or not!
        /// </summary>
        /// <param name="addedKeyString">String character converted into a string representation of itself.</param>
        private void OnCharacterAddedToInputBuffer(string addedKeyString)
        {
            // Disable passing along input buffer if the simulation is not currently accepting input from the user.
            if (!_simUnit.WindowManager.AcceptingInput)
                return;

            // Add the character to the end of the input buffer.
            InputBuffer += addedKeyString;
        }

        /// <summary>
        ///     Populates an internal input buffer for the simulation that is used to eventually return a possible command string
        ///     to active game Windows.
        /// </summary>
        /// <param name="keyChar">The key Char.</param>
        public void AddCharToInputBuffer(char keyChar)
        {
            // Only printable free-text belongs in the buffer. Enter and Backspace are the caller's responsibility;
            // everything that is not a control code or an invisible formatting mark — letters, digits, spaces, and
            // punctuation alike — is valid input and may enter the buffer.
            if (!IsPrintableInputChar(keyChar))
                return;

            // Convert character to string representation of itself.
            var addedKeyString = char.ToString(keyChar);
            OnCharacterAddedToInputBuffer(addedKeyString);
        }

        /// <summary>
        ///     Appends a string of already-composed text to the input buffer for host applications that need to inject text
        ///     programmatically — pasting, pre-filling a field, or feeding an IME/composed string — without reaching into the
        ///     manager's internals via reflection. Non-printable characters (control codes such as newlines and escapes, and
        ///     zero-width / formatting marks) are stripped, exactly as they are for typed input via
        ///     <see cref="AddCharToInputBuffer" />, so injected text can neither corrupt the single-line prompt nor smuggle
        ///     control characters into a dispatched command. Like typed input, the text is only accepted while the focused
        ///     window/form is currently accepting input; otherwise it is silently dropped.
        /// </summary>
        /// <param name="text">Text to append to the input buffer. A null or empty value is ignored.</param>
        public void AppendToInputBuffer(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // Keep only printable characters so injected/pasted text can never smuggle newlines, escape sequences, or
            // invisible formatting marks into the single-line buffer and, from there, into a dispatched command.
            var printable = new StringBuilder(text.Length);
            foreach (var character in text)
                if (IsPrintableInputChar(character))
                    printable.Append(character);

            if (printable.Length > 0)
                OnCharacterAddedToInputBuffer(printable.ToString());
        }

        /// <summary>
        ///     Determines whether a character is printable free-text that belongs in the input buffer. Rejects control codes
        ///     (Tab, Escape, Delete, and the caller-handled Enter/Backspace) and zero-width / formatting marks (Unicode
        ///     category <see cref="UnicodeCategory.Format" />, e.g. the byte-order mark or a zero-width space) which would
        ///     otherwise sit invisibly in the buffer yet still break exact command matching or corrupt the rendered prompt.
        /// </summary>
        /// <param name="keyChar">Character to test.</param>
        /// <returns>TRUE if the character may be appended to the input buffer.</returns>
        private static bool IsPrintableInputChar(char keyChar)
        {
            return !char.IsControl(keyChar) &&
                   char.GetUnicodeCategory(keyChar) != UnicodeCategory.Format;
        }

        /// <summary>
        ///     Reports a key press to the focused window on the next tick, whatever the key was.
        ///     <para>
        ///         This is how a key that is not text gets heard at all. <see cref="AddCharToInputBuffer" /> takes
        ///         characters, and an arrow key, a function key or Home has none to give it, so the buffer drops them —
        ///         correct for a line being typed and useless for anything being steered. A host that wants both calls
        ///         both: printable keys fill the buffer as they always have <i>and</i> arrive here.
        ///     </para>
        ///     <para>
        ///         Queued rather than delivered at once so it lands inside a tick, like every other input this class
        ///         hands on: a form is free to answer a key by putting a window up, and doing that from the middle of the
        ///         host's read loop would be editing the window stack from outside the simulation's own turn.
        ///     </para>
        /// </summary>
        /// <param name="key">The key the host saw pressed.</param>
        public void SendKeyPress(ConsoleKey key)
        {
            _keyQueue.Enqueue(key);
        }

        /// <summary>
        ///     Removes the last character from input buffer if greater than zero.
        /// </summary>
        public void RemoveLastCharOfInputBuffer()
        {
            if (InputBuffer.Length > 0)
                InputBuffer = InputBuffer.Remove(InputBuffer.Length - 1);
        }

        /// <summary>
        ///     Fired by messaging system or user interface that wants to interact with the simulation by sending string command
        ///     that should be able to be parsed into a valid command that can be run on the current game Windows.
        /// </summary>
        /// <param name="returnedLine">Passed in command from controller, text was trimmed but nothing more.</param>
        private void AddCommandToQueue(string returnedLine)
        {
            // Trim the input.
            var trimmedInput = returnedLine.Trim();

            // Skip if we already entered the same command, simulation is state based... no need for flooding.
            if (_commandQueue.Contains(trimmedInput))
                return;

            // Adds the command to queue to be passed to simulation when input manager is ticked.
            _commandQueue.Enqueue(trimmedInput);
        }

        /// <summary>
        ///     Removes any text data from the input buffer resetting it to an empty string again.
        /// </summary>
        public void ClearBuffer()
        {
            InputBuffer = string.Empty;
        }

        /// <summary>
        ///     Removes any commands and key presses that are waiting to be dispatched so they cannot execute against
        ///     windows created after a session reset.
        /// </summary>
        public void ClearQueue()
        {
            _commandQueue.Clear();

            // Key presses go too, for the same reason: one typed at the old session has nothing to say to the new one.
            _keyQueue.Clear();
        }
    }
}
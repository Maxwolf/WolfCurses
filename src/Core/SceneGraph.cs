// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 12/31/2015@2:38 PM

using System;
using System.Text;
using WolfCurses.Graphics;

namespace WolfCurses.Core
{
    /// <summary>
    ///     Provides base functionality for rendering out the simulation state via text user interface (TUI). This class has no
    ///     idea about how other modules work and only serves to query them for string data which will be compiled into a
    ///     console only view of the simulation which is intended to be the lowest level of visualization but theoretically
    ///     anything could be a renderer for the simulation.
    ///     <para>
    ///         Frames also draw themselves: while nothing is subscribed to <see cref="ScreenBufferDirtyEvent" />, each
    ///         changed frame is written to the attached console through a <see cref="ConsolePresenter" /> this class
    ///         creates on first use — so a console host gets flicker-free presentation without writing any presentation
    ///         code at all. Subscribing to the event takes presentation over completely: the built-in presenter stands
    ///         down while any handler is attached, which is also what keeps a host that wired its own presenter (the
    ///         only way, before this existed) behaving exactly as it always did. With no console attached — output
    ///         redirected, or no terminal at all — nothing is written anywhere and the frames simply wait in the event
    ///         for whoever wants them.
    ///     </para>
    /// </summary>
    public class SceneGraph : Module.Module
    {
        /// <summary>
        ///     Fired when the screen back buffer has changed from what is currently being shown, this forces a redraw.
        /// </summary>
        public delegate void ScreenBufferDirty(string tuiContent);

        /// <summary>
        ///     Default string used when game Windows has nothing better to say.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private const string GAMEMODE_DEFAULT_TUI = "[DEFAULT WINDOW TEXT]";

        /// <summary>
        ///     Default string used when there are no game modes at all.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private const string GAMEMODE_EMPTY_TUI = "[NO WINDOW ATTACHED]";

        /// <summary>
        ///     Default string that is used in menu generations when the user is given a choice. Can be changed per window or form.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public const string PROMPT_TEXT_DEFAULT = "What is your choice?";

        /// <summary>
        ///     Reference to simulation that is controlling the text user interface and actually filling the screen buffer with
        ///     data.
        /// </summary>
        private readonly SimulationApp _simUnit;

        /// <summary>
        ///     Draws the frames nobody else claimed. Created on the first frame that can actually be drawn — never
        ///     earlier, so a simulation that runs headless (tests, a game engine, output piped to a file) never
        ///     constructs one and never touches the console at all.
        /// </summary>
        private ConsolePresenter _autoPresenter;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SceneGraph" /> class.
        /// </summary>
        /// <param name="simUnit">Core simulation which is controlling the window manager.</param>
        public SceneGraph(SimulationApp simUnit)
        {
            _simUnit = simUnit;
            ScreenBuffer = string.Empty;
        }

        /// <summary>
        ///     Holds the last known representation of the game simulation and current Windows text user interface, only pushes
        ///     update
        ///     when a change occurs.
        /// </summary>
        private string ScreenBuffer { get; set; }

        /// <summary>
        ///     Where auto-presented frames go instead of the console. Test seam: the real path needs an attached
        ///     terminal to be observable, which a test host does not have. Null (always, outside tests) means frames
        ///     go to the built-in <see cref="ConsolePresenter" />.
        /// </summary>
        internal Action<string> AutoPresentSink { get; set; }

        /// <summary>
        ///     Fired when the simulation is closing and needs to clear out any data structures that it created so the program can
        ///     exit cleanly.
        /// </summary>
        public override void Destroy()
        {
            ScreenBuffer = string.Empty;
            _autoPresenter = null;
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
            // GetModule the current text user interface data from inheriting class. Comparison must be
            // case-sensitive: a screen differing only by letter case still needs a redraw.
            var tuiContent = OnRender();
            if (ScreenBuffer.Equals(tuiContent, StringComparison.Ordinal))
                return;

            // Update the screen buffer with altered data.
            ScreenBuffer = tuiContent;

            // A subscriber owns presentation outright — the frame is handed over and the built-in presenter stays
            // out of it entirely. Checked per frame rather than once, so a host attaching (or detaching) a handler
            // mid-run hands presentation over (or takes it back) on the very next changed frame.
            var subscribers = ScreenBufferDirtyEvent;
            if (subscribers != null)
            {
                subscribers(ScreenBuffer);
                return;
            }

            AutoPresent(ScreenBuffer);
        }

        /// <summary>
        ///     Draws a frame nobody subscribed for. Silently does nothing without a real console to draw on — that is
        ///     not an error, it is a simulation being run headless (tests, a game engine, output piped to a file),
        ///     where the frames stay available in <see cref="ScreenBuffer" /> and the event for whoever wants them.
        /// </summary>
        /// <param name="frame">The complete frame to draw.</param>
        private void AutoPresent(string frame)
        {
            // The test seam, when installed, stands in for the console entirely.
            var sink = AutoPresentSink;
            if (sink != null)
            {
                sink(frame);
                return;
            }

            if (AnsiConsole.SafeIsOutputRedirected())
                return;

            // First drawable frame: build the presenter that will now serve for the rest of the simulation's life.
            // Its constructor readies the console for ANSI output (VT processing on Windows + UTF-8), so nothing
            // else has to have done that first. If a subscriber later takes over and then detaches again, the
            // presenter's own per-row diff recovers on its next frame — its first draw is always a full redraw.
            _autoPresenter ??= new ConsolePresenter();
            _autoPresenter.Present(frame);
        }

        /// <summary>
        ///     Prints game Windows specific text and options.
        /// </summary>
        /// <returns>
        ///     The text user interface that is the game simulation.<see cref="string" />.
        /// </returns>
        private string OnRender()
        {
            // Spinning ticker that shows activity, lets us know if application hangs or freezes.
            var tui = new StringBuilder();
            tui.Append($"[ {_simUnit.TickPhase} ] - ");

            // Keeps track of active Windows name and active Windows current state name for debugging purposes.
            tui.Append(_simUnit.WindowManager.FocusedWindow?.CurrentForm != null
                ? $"Window({_simUnit.WindowManager.Count}): {_simUnit.WindowManager.FocusedWindow}({_simUnit.WindowManager.FocusedWindow.CurrentForm}) - "
                : $"Window({_simUnit.WindowManager.Count}): {_simUnit.WindowManager.FocusedWindow}() - ");

            // Allows the implementing simulation to control text before window is rendered out.
            tui.Append(_simUnit.OnPreRender());

            // Prints game Windows specific text and options. This typically is menus from commands, or states showing some information.
            tui.Append($"{RenderWindow()}{Environment.NewLine}");

            // Determines if the user is allowed to see their input from buffer as they type it, or is it stored until they press enter.
            if (_simUnit.WindowManager.AcceptingInput)
            {
                var focusedWindow = _simUnit.WindowManager.FocusedWindow;

                // Mask the echoed buffer for password-style prompts so typed characters are not shown on screen.
                var inputBuffer = focusedWindow != null && focusedWindow.MaskInput
                    ? new string('*', _simUnit.InputManager.InputBuffer.Length)
                    : _simUnit.InputManager.InputBuffer;

                tui.Append(focusedWindow != null
                    ? $"{focusedWindow.PromptText} {inputBuffer}"
                    : $"{PROMPT_TEXT_DEFAULT} {inputBuffer}");
            }

            // Outputs the result of the string builder to TUI builder above.
            return tui.ToString();
        }

        /// <summary>Prints game Windows specific text and options.</summary>
        /// <returns>The current window text to be rendered out.<see cref="string" />.</returns>
        private string RenderWindow()
        {
            var focusedWindow = _simUnit.WindowManager.FocusedWindow;

            // A window flagged for removal is torn down on the next tick (its form and menu are already gone). Its
            // render is empty, so suppress the "[DEFAULT WINDOW TEXT]" placeholder that would otherwise flash for a
            // single frame while it is being closed (e.g. when a dialog dismisses itself).
            if (focusedWindow != null && focusedWindow.ShouldRemoveMode)
                return string.Empty;

            // If TUI for active game Windows is not null or empty then use it.
            var activeWindowText = focusedWindow?.OnRenderWindow();
            if (!string.IsNullOrEmpty(activeWindowText) && !string.IsNullOrWhiteSpace(activeWindowText))
                return activeWindowText;

            // Otherwise, display default message if null for Windows.
            return focusedWindow == null ? GAMEMODE_EMPTY_TUI : GAMEMODE_DEFAULT_TUI;
        }

        /// <summary>
        ///     Fired when the screen back buffer has changed from what is currently being shown, this forces a redraw.
        ///     <para>
        ///         Subscribing takes over presentation: while any handler is attached the built-in console presenter
        ///         stands down and the frames are yours to draw — hand them to your own
        ///         <see cref="ConsolePresenter" />, or call <see cref="AnsiGraphics.StripMarkers" /> before writing
        ///         them any other way. With no subscribers, <see cref="SceneGraph" /> presents each changed frame to
        ///         the attached console itself, so most console hosts never touch this event at all.
        ///     </para>
        /// </summary>
        public event ScreenBufferDirty ScreenBufferDirtyEvent;

        /// <summary>
        ///     Removes any and all data from the text user interface renderer.
        /// </summary>
        public void Clear()
        {
            ScreenBuffer = string.Empty;
        }
    }
}
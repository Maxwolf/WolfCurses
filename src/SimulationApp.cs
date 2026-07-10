// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 12/31/2015@4:49 AM

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using WolfCurses.Core;
using WolfCurses.Window.Control;

namespace WolfCurses
{
    /// <summary>
    ///     Base simulation application class object. This class should not be declared directly but inherited by actual
    ///     instance of game controller.
    /// </summary>
    public abstract class SimulationApp : ITick
    {
        /// <summary>
        ///     Determines if the dynamic menu system should show the command names or only numbers. If false then only numbers
        ///     will be shown.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public const bool SHOW_COMMANDS = false;

        /// <summary>
        ///     Constant for the amount of time difference that should occur from last tick and current tick in milliseconds before
        ///     the simulation logic will be ticked.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private const double TICK_INTERVAL = 1000.0d;

        /// <summary>
        ///     Time and date of latest system tick, used to measure total elapsed time and tick simulation after each second.
        /// </summary>
        private DateTime _currentTickTime;

        /// <summary>
        ///     Last known time the simulation was ticked with logic and all sub-systems. This is not the same as a system tick
        ///     which can happen hundreds of thousands of times a second or just a few, we only measure the difference in time on
        ///     them.
        /// </summary>
        private DateTime _lastTickTime;

        /// <summary>
        ///     Spinning character pixel.
        /// </summary>
        private SpinningPixel _spinningPixel;

        /// <summary>
        ///     True only while <see cref="OnFirstTick" /> is executing. Used to detect a <see cref="Restart" /> call
        ///     that originates from inside the first-tick handler (a common and idiomatic pattern) so the tick counter
        ///     reset can be suppressed; otherwise the "first tick" condition would keep re-triggering and
        ///     <see cref="OnFirstTick" /> would recurse on every subsequent tick forever.
        /// </summary>
        private bool _firstTickInProgress;

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:TrailGame.SimulationApp" /> class, seeding the shared
        ///     <see cref="Randomizer" /> from the wall clock.
        /// </summary>
        protected SimulationApp() : this(null)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:TrailGame.SimulationApp" /> class with an explicit random
        ///     seed so a consuming simulation can be made fully reproducible: constructing two apps with the same seed
        ///     yields identical <see cref="Randomizer" /> sequences.
        /// </summary>
        /// <param name="randomSeed">
        ///     Seed handed to the shared <see cref="Randomizer" />, or null to seed from the wall clock as usual.
        /// </param>
        protected SimulationApp(int? randomSeed)
        {
            // We are not closing...
            IsClosing = false;

            // Date and time the simulation was started, which we use as benchmark for all future time passed.
            _lastTickTime = DateTime.UtcNow;
            _currentTickTime = DateTime.UtcNow;

            // Visual tick representations for other sub-systems.
            TotalSecondsTicked = 0;

            // Setup spinning pixel to show game is not thread locked.
            _spinningPixel = new SpinningPixel();
            TickPhase = _spinningPixel.Step();

            // Create modules needed for managing simulation. A caller-supplied seed makes the RNG reproducible.
            Random = randomSeed.HasValue ? new Randomizer(randomSeed.Value) : new Randomizer();
            WindowManager = new WindowManager(this);
            SceneGraph = new SceneGraph(this);

            // Input manager needs event hook for knowing when buffer is sent.
            InputManager = new InputManager(this);
        }

        /// <summary>
        ///     Determines if the simulation is currently closing down.
        /// </summary>
        public bool IsClosing { get; private set; }

        /// <summary>
        ///     Shows the current status of the simulation visually as a spinning glyph, the purpose of which is to show that there
        ///     is no hang in the simulation or logic controllers and everything is moving along and waiting for input or
        ///     displaying something to user.
        /// </summary>
        internal string TickPhase { get; private set; }

        /// <summary>
        ///     Total number of ticks that have gone by from measuring system ticks, this means this measures the total number of
        ///     seconds that have gone by using the pulses and time dilation without the use of dirty times that spawn more
        ///     threads.
        /// </summary>
        private ulong TotalSecondsTicked { get; set; }

        /// <summary>
        ///     Used for rolling the virtual dice in the simulation to determine the outcome of various events.
        /// </summary>
        public Randomizer Random { get; private set; }

        /// <summary>
        ///     Keeps track of the currently attached game Windows, which one is active, and getting text user interface data.
        /// </summary>
        public WindowManager WindowManager { get; private set; }

        /// <summary>
        ///     Handles input from the users keyboard, holds an input buffer and will push it to the simulation when return key is
        ///     pressed.
        /// </summary>
        public InputManager InputManager { get; private set; }

        /// <summary>
        ///     Shows the current state of the simulation as text only interface (TUI). Uses default constants if the attached
        ///     Windows
        ///     or state does not override this functionality and it is ticked.
        /// </summary>
        public SceneGraph SceneGraph { get; private set; }

        /// <summary>
        ///     Determines what windows the simulation will be capable of using and creating using the window managers factory.
        /// </summary>
        public abstract IEnumerable<Type> AllowedWindows { get; }

        /// <summary>
        ///     Extra assemblies to scan for <c>[ParentWindow]</c> forms beyond the SimulationApp subclass's own assembly
        ///     and the process entry assembly. Override this to contribute forms that live in plugin or module
        ///     assemblies. Like <see cref="AllowedWindows" />, it is read while the base constructor builds the window
        ///     manager, so an override must return data that does not depend on subclass fields initialized later.
        /// </summary>
        public virtual IEnumerable<Assembly> AdditionalFormAssemblies => Array.Empty<Assembly>();

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
        public virtual void OnTick(bool systemTick, bool skipDay = false)
        {
            // No ticks allowed if simulation is shutting down.
            if (IsClosing)
                return;

            // Sends commands if queue has any.
            InputManager?.OnTick(systemTick, skipDay);

            // Back buffer for only sending text when changed.
            SceneGraph?.OnTick(systemTick, skipDay);

            // Changes game Windows and state when needed.
            WindowManager?.OnTick(systemTick, skipDay);

            // Rolls virtual dice.
            Random?.OnTick(systemTick, skipDay);

            // A module tick handler may have destroyed the simulation; stop before advancing time or counters.
            if (IsClosing)
                return;

            // System tick is from execution platform, otherwise they are linear simulation ticks.
            if (systemTick)
            {
                _currentTickTime = DateTime.UtcNow;
                var elapsedTicks = _currentTickTime.Ticks - _lastTickTime.Ticks;
                var elapsedSpan = new TimeSpan(elapsedTicks);

                // Check if more than an entire second has gone by.
                if (!(elapsedSpan.TotalMilliseconds > TICK_INTERVAL))
                    return;

                // Advance the accumulator by exactly one interval so the leftover fraction counts toward the next
                // simulation tick; snap to now only when the host stalled more than a full interval behind.
                _lastTickTime = _lastTickTime.AddMilliseconds(TICK_INTERVAL);
                if ((_currentTickTime - _lastTickTime).TotalMilliseconds > TICK_INTERVAL)
                    _lastTickTime = _currentTickTime;

                // Recursive call on ourselves to process non-system ticks.
                OnTick(false, skipDay);
            }
            else
            {
                // Increase the total seconds ticked.
                TotalSecondsTicked++;

                // Fire event for first tick when it occurs, and only then. The in-progress flag lets a Restart
                // triggered from inside OnFirstTick know not to reset the tick counter (which would otherwise make
                // this same "== 1" branch fire again on the very next tick, recursing forever).
                if (TotalSecondsTicked == 1)
                {
                    _firstTickInProgress = true;
                    try
                    {
                        OnFirstTick();
                    }
                    finally
                    {
                        _firstTickInProgress = false;
                    }
                }

                // Visual representation of ticking for debugging purposes. OnFirstTick may have destroyed the
                // simulation, taking the spinning pixel with it.
                TickPhase = _spinningPixel?.Step() ?? string.Empty;
            }
        }

        /// <summary>
        ///     Fired when the ticker receives the first system tick event.
        /// </summary>
        protected abstract void OnFirstTick();

        /// <summary>
        ///     Fired when the simulation is closing and needs to clear out any data structures that it created so the program can
        ///     exit cleanly.
        /// </summary>
        public void Destroy()
        {
            // A second destroy on an already-destroyed simulation must not re-fire teardown hooks.
            if (IsClosing)
                return;

            // Set flag that we are closing now so we can ignore ticks during shutdown.
            IsClosing = true;

            // Allows game simulation above us to cleanup any data structures it cares about.
            OnPreDestroy();

            // Give every module the chance to run its own teardown before the references are dropped.
            InputManager?.Destroy();
            SceneGraph?.Destroy();
            WindowManager?.Destroy();
            Random?.Destroy();

            // Remove simulation presentation variables.
            _lastTickTime = DateTime.MinValue;
            _currentTickTime = DateTime.MinValue;
            TotalSecondsTicked = 0;
            _spinningPixel = null;
            TickPhase = string.Empty;

            // Remove simulation core modules.
            Random = null;
            WindowManager = null;
            SceneGraph = null;
            InputManager = null;
        }

        /// <summary>
        ///     Creates and or clears data sets required for game simulation and attaches the travel menu and the main menu to make
        ///     the program completely restarted as if fresh.
        /// </summary>
        public virtual void Restart()
        {
            // A destroyed simulation has no modules left to restart.
            if (IsClosing || WindowManager == null)
                throw new InvalidOperationException(
                    "Cannot restart a simulation that has been destroyed; create a new instance instead.");

            // Reset tick measurements so the restarted session fires OnFirstTick again just like a fresh one — but
            // only when this Restart is a genuinely new session. A Restart invoked from within OnFirstTick (the
            // idiomatic "OnFirstTick attaches the initial window by calling Restart" pattern the example app uses)
            // is part of the current first tick, so resetting the counter there would re-satisfy the "== 1"
            // condition on the next tick and re-enter OnFirstTick every tick, endlessly wiping windows and forms.
            _lastTickTime = DateTime.UtcNow;
            _currentTickTime = _lastTickTime;
            if (!_firstTickInProgress)
                TotalSecondsTicked = 0;

            // Resets the window manager and clears out all windows and forms from previous session.
            WindowManager.Clear();

            // Clears the input buffer and any queued commands left over from last play session.
            InputManager.ClearBuffer();
            InputManager.ClearQueue();

            // Removes any text from the interface from previous session.
            SceneGraph.Clear();
        }

        /// <summary>
        ///     Called when simulation is about to destroy itself, but right before it actually does it.
        /// </summary>
        protected abstract void OnPreDestroy();

        /// <summary>
        ///     Called by the text user interface scene graph renderer before it asks the active window to render itself out for
        ///     display.
        /// </summary>
        public abstract string OnPreRender();
    }
}
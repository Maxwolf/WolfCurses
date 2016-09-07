// Created by Ron 'Maxwolf' McDowell (ron.mcdowell@gmail.com) 
// Timestamp 01/07/2016@8:28 PM

using System;
using System.Collections.Generic;
using System.Text;
using WolfCurses.Example;

namespace WolfCurses
{
    /// <summary>
    ///     Abstract implementation of a cross-platform console application built using wolf curses library.
    /// </summary>
    public sealed class ConsoleSimulationApp : SimulationApp
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="T:TrailGame.SimulationApp" /> class.
        /// </summary>
        public ConsoleSimulationApp()
        {
        }

        /// <summary>
        ///     Provides stub implementation of a module that ticks along with the simulation, allows logic to be ticked all the
        ///     time and to manipulate windows and forms without being apart of current view.
        /// </summary>
        public ExampleModule ModuleExample { get; private set; }

        /// <summary>
        ///     Singleton instance for the entire game simulation, does not block the calling thread though only listens for
        ///     commands.
        /// </summary>
        public static ConsoleSimulationApp Instance { get; private set; }

        /// <summary>
        ///     Determines what windows the simulation will be capable of using and creating using the window managers factory.
        /// </summary>
        public override IEnumerable<Type> AllowedWindows
        {
            get
            {
                var windowList = new List<Type>
                {
                    typeof (ExampleWindow)
                };

                return windowList;
            }
        }

        /// <summary>
        ///     Creates new instance of simulation. Complains if instance already exists.
        /// </summary>
        public static void Create()
        {
            if (Instance != null)
                throw new InvalidOperationException(
                    "Unable to create new instance of simulation since it already exists!");

            Instance = new ConsoleSimulationApp();
            Instance.OnPostCreate();
        }

        /// <summary>
        ///     Fired after the simulation instance has been created, allowing us to call it using the instance of the simulation
        ///     from static method.
        /// </summary>
        private void OnPostCreate()
        {
            // Example module that ticks all the time with us unlike a form that only ticks when active.
            ModuleExample = new ExampleModule();
        }

        /// <summary>
        ///     Fired when the ticker receives the first system tick event.
        /// </summary>
        protected override void OnFirstTick()
        {
            Restart();
        }

        /// <summary>
        ///     Called when simulation is about to destroy itself, but right before it actually does it.
        /// </summary>
        protected override void OnPreDestroy()
        {
            // Allows module to save any data before core simulation closes.
            ModuleExample.Destroy();
            ModuleExample = null;

            // Destroys simulation instance.
            Instance = null;
        }

        /// <summary>
        ///     Called by the text user interface scene graph renderer before it asks the active window to render itself out for
        ///     display.
        /// </summary>
        public override string OnPreRender()
        {
            // Total number of turns that have passed in the simulation.
            var tui = new StringBuilder();
            tui.AppendLine($"Example Module: {ModuleExample.ExampleModuleData}");
            return tui.ToString();
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
            base.OnTick(systemTick, skipDay);

            // Tick the module.
            ModuleExample?.OnTick(systemTick, skipDay);
        }

        /// <summary>
        ///     Creates and or clears data sets required for game simulation and attaches the travel menu and the main menu to make
        ///     the program completely restarted as if fresh.
        /// </summary>
        public override void Restart()
        {
            // Resets the module to default start.
            ModuleExample.Restart();

            // Resets the window manager in the base simulation.
            base.Restart();

            // Attach example window after the first tick.
            WindowManager.Add(typeof (ExampleWindow));
        }
    }
}
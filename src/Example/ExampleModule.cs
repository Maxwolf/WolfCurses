// Created by Ron 'Maxwolf' McDowell (ron.mcdowell@gmail.com) 
// Timestamp 01/07/2016@8:00 PM

namespace WolfCurses.Example
{
    /// <summary>
    ///     Modules are added to the simulation during startup and have the ability to constantly tick with the underlying
    ///     simulation. They will always get ticks unlike windows and forms whom only tick when they are currently active in
    ///     the window manager.
    /// </summary>
    public sealed class ExampleModule : Module.Module
    {
        /// <summary>
        ///     Provides example of random data being populated by constant tick of the underlying simulation.
        /// </summary>
        private int _randomData;

        /// <summary>
        ///     Example data this module provides for us.
        /// </summary>
        public string ExampleModuleData
        {
            get { return $"Random: {_randomData.ToString("N0")}"; }
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

            // Skip system ticks.
            if (systemTick)
                return;

            // Pick a random number between 1 and 1000 every tick.
            _randomData = ConsoleSimulationApp.Instance.Random.Next(1, 1000);
        }

        /// <summary>
        ///     Example way to make a custom implementation that restarts modules when the simulation is restarted.
        /// </summary>
        public void Restart()
        {
        }
    }
}
// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;

namespace WolfCurses.Apps
{
    /// <summary>
    ///     The simulation the applications run inside. Deliberately about as small as a <see cref="SimulationApp" />
    ///     gets: no extra module, no persisted state and no image set-up, so what is left is the minimum a host has
    ///     to write.
    /// </summary>
    public sealed class OfficeSimulationApp : SimulationApp
    {
        /// <summary>Singleton instance for the whole suite; the host loop ticks this until it goes null.</summary>
        public static OfficeSimulationApp Instance { get; private set; }

        // AllowedWindows is deliberately NOT overridden. The base class discovers OfficeWindow from this assembly and
        // the built-in control windows (file dialog, select list, message box, text input) from the library, so an
        // application can open a file browser or put up a message box with nothing registered anywhere. An override
        // replaces discovery outright rather than adding to it, so it is how you would EXCLUDE a window, not how
        // you would register one.

        /// <summary>Creates a new instance of the simulation. Complains if one already exists.</summary>
        public static void Create()
        {
            if (Instance != null)
                throw new InvalidOperationException(
                    "Unable to create new instance of simulation since it already exists!");

            Instance = new OfficeSimulationApp();
        }

        /// <summary>Fired when the ticker receives the first system tick event.</summary>
        protected override void OnFirstTick()
        {
            Restart();
        }

        /// <summary>Called when the simulation is about to destroy itself, but right before it actually does it.</summary>
        protected override void OnPreDestroy()
        {
            Instance = null;
        }

        /// <summary>
        ///     Appended to the scene graph's own status line, every frame. One short constant on purpose: this text
        ///     lands at the top of <i>every</i> screen, including the middle of whatever application is open, so
        ///     anything that changed here would redraw the top row underneath somebody's work. An application puts
        ///     its own header in its own rendered text, where it belongs.
        /// </summary>
        /// <returns>The text to render above the active window.</returns>
        public override string OnPreRender()
        {
            return "WolfCurses Apps";
        }

        /// <summary>Clears state and re-attaches the menu, as if the program had just started.</summary>
        public override void Restart()
        {
            base.Restart();

            WindowManager.Add(typeof (OfficeWindow));
        }
    }
}

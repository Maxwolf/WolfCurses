// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/16/2026

using System;

namespace WolfCurses.Games
{
    /// <summary>
    ///     The simulation the games run inside. Deliberately about as small as a <see cref="SimulationApp" /> gets:
    ///     there is no extra module, no persisted state and no image set-up, so what is left is the minimum a host
    ///     has to write.
    /// </summary>
    public sealed class GamesSimulationApp : SimulationApp
    {
        /// <summary>Singleton instance for the whole arcade; the host loop ticks this until it goes null.</summary>
        public static GamesSimulationApp Instance { get; private set; }

        // AllowedWindows is deliberately NOT overridden. The base class discovers GamesWindow from this assembly and
        // the built-in control windows (message box, select list, text input, file dialog) from the library, so the
        // games can push a MessageBox without anything being registered anywhere.

        /// <summary>Creates a new instance of the simulation. Complains if one already exists.</summary>
        public static void Create()
        {
            if (Instance != null)
                throw new InvalidOperationException(
                    "Unable to create new instance of simulation since it already exists!");

            Instance = new GamesSimulationApp();
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
        ///     Appended to the scene graph's own status line, every frame. Deliberately one short constant and not a
        ///     scoreboard: this text lands at the top of <i>every</i> screen, including the middle of a game, so
        ///     anything that changed here would redraw the top row underneath whatever is being played. The games put
        ///     their own headers in their own rendered text, where they belong.
        /// </summary>
        /// <returns>The text to render above the active window.</returns>
        public override string OnPreRender()
        {
            return "WolfCurses Games";
        }

        /// <summary>Clears state and re-attaches the arcade menu, as if the program had just started.</summary>
        public override void Restart()
        {
            base.Restart();

            WindowManager.Add(typeof (GamesWindow));
        }
    }
}

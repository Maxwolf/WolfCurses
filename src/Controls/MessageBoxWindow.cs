// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using WolfCurses.Window;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     The window that hosts a <see cref="MessageBox" />. On creation it attaches <see cref="MessageBoxForm" />.
    ///     Push it with <see cref="MessageBox" />'s static methods rather than adding it directly, and list
    ///     <c>typeof(MessageBoxWindow)</c> in your <see cref="SimulationApp.AllowedWindows" />.
    /// </summary>
    public sealed class MessageBoxWindow : Window<MessageBoxCommands, MessageBoxData>
    {
        /// <summary>Initializes a new instance of the <see cref="MessageBoxWindow" /> class.</summary>
        /// <param name="simUnit">Core simulation which is controlling the window.</param>
        // ReSharper disable once UnusedMember.Global
        public MessageBoxWindow(SimulationApp simUnit) : base(simUnit)
        {
        }

        /// <inheritdoc />
        public override void OnWindowPostCreate()
        {
            base.OnWindowPostCreate();
            SetForm(typeof (MessageBoxForm));
        }
    }
}

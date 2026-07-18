// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using WolfCurses.Window;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     The window that hosts a <see cref="TextInputDialog" />. On creation it attaches <see cref="TextInputForm" />
    ///     and, when the dialog is masked, tells the renderer to echo the input buffer as asterisks. Push it with
    ///     <see cref="TextInputDialog.Prompt" /> rather than adding it directly. The default
    ///     <see cref="SimulationApp.AllowedWindows" /> discovers it automatically; an app that overrides
    ///     <c>AllowedWindows</c> must include it in the list.
    /// </summary>
    public sealed class TextInputWindow : Window<TextInputCommandsEnum, TextInputData>
    {
        /// <summary>Initializes a new instance of the <see cref="TextInputWindow" /> class.</summary>
        /// <param name="simUnit">Core simulation which is controlling the window.</param>
        // ReSharper disable once UnusedMember.Global
        public TextInputWindow(SimulationApp simUnit) : base(simUnit)
        {
        }

        /// <summary>Masks the echoed input buffer when the dialog was opened in masked (password) mode.</summary>
        public override bool MaskInput => UserData.Masked;

        /// <inheritdoc />
        public override void OnWindowPostCreate()
        {
            base.OnWindowPostCreate();
            SetForm(typeof (TextInputForm));
        }
    }
}

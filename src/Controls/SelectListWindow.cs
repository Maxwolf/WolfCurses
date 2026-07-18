// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using WolfCurses.Window;

namespace WolfCurses.Controls
{
    /// <summary>
    ///     The window that hosts the <see cref="SelectList" /> picker. It carries no menu of its own; on creation it
    ///     attaches <see cref="SelectListForm" />, which draws the list and handles selection. Push it with
    ///     <see cref="SelectList.Choose(SimulationApp,string,System.Collections.Generic.IEnumerable{string},System.Action{int},System.Action)" />
    ///     rather than adding it directly. The default <see cref="SimulationApp.AllowedWindows" /> discovers it
    ///     automatically; an app that overrides <c>AllowedWindows</c> must include it in the list.
    /// </summary>
    public sealed class SelectListWindow : Window<SelectListCommandsEnum, SelectListData>
    {
        /// <summary>Initializes a new instance of the <see cref="SelectListWindow" /> class.</summary>
        /// <param name="simUnit">Core simulation which is controlling the window.</param>
        // ReSharper disable once UnusedMember.Global
        public SelectListWindow(SimulationApp simUnit) : base(simUnit)
        {
        }

        /// <inheritdoc />
        public override void OnWindowPostCreate()
        {
            base.OnWindowPostCreate();
            SetForm(typeof (SelectListForm));
        }
    }
}

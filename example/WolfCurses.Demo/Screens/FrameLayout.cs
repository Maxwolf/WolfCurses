// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/22/2026

using System.Text;

namespace WolfCurses.Demo.Screens
{
    /// <summary>
    ///     Where this app's frame puts a form, for the screens that have to turn a pointer into a place.
    ///     <para>
    ///         <b>A form's line zero is not the window's row zero, and every screen here that hit-tests has to say
    ///         so.</b> <c>SceneGraph</c> opens each frame with the spinner and the focused window's name, neither of
    ///         which can carry a line break, and then appends <c>SimulationApp.OnPreRender()</c>. So the offset is
    ///         however many lines the pre-render wrote: one for this app, whose version ends its line, and none for
    ///         a host whose version does not and therefore shares that row with the form.
    ///         <c>MouseEvent.Row</c> already has <c>Console.WindowTop</c> subtracted and
    ///         <c>ConsolePresenter</c> writes frame line <i>n</i> at window row <i>n</i>, so the two are one
    ///         coordinate space once this term is in.
    ///     </para>
    ///     <para>
    ///         <b>This existed six times before it existed once.</b> Every screen from the menu bar onward worked it
    ///         out for itself and no two agreed on how: two named constants, a bare <c>+ 1</c>, two counts of the
    ///         pre-render's line breaks, a count of its rows less one, and one screen that searched the rendered
    ///         frame for its own text. All six answered 1 and so all six were right, which is exactly why it could
    ///         sit there - a seventh screen, or a host whose pre-render stopped ending its line, is what turns a
    ///         disagreement nobody can see into a hit test a row out.
    ///     </para>
    ///     <para>
    ///         <b>Counting the pre-render is the technique, and searching the frame is the one that was rejected.</b>
    ///         Measuring the drawn frame assumes nothing about the header and looks like the stronger answer, but it
    ///         needs a frame to measure and needs to find the form's own text inside it, so on the first compose and
    ///         on any miss it has nothing to report and falls back to a written-down 1 - the very constant it was
    ///         replacing, now with the failure hidden. This needs no frame, is a pure function of the app, and is
    ///         correct for a pre-render whether or not it ends its line.
    ///     </para>
    /// </summary>
    internal static class FrameLayout
    {
        /// <summary>How many whole screen rows the scene graph writes above the focused form's first line.</summary>
        /// <param name="simUnit">The running simulation, which may be null while a form is being torn down.</param>
        /// <returns>The offset to add to a form-relative row to get the row a pointer reports.</returns>
        internal static int RowsAboveForm(SimulationApp simUnit)
        {
            return CountLineBreaks(simUnit?.OnPreRender());
        }

        /// <summary>
        ///     How many line breaks are already in the builder, which is the form-relative row the next line will
        ///     occupy. Add <see cref="RowsAboveForm" /> to it to get the row a pointer reports.
        /// </summary>
        /// <param name="builder">The frame being composed.</param>
        /// <returns>The count.</returns>
        internal static int CountLineBreaks(StringBuilder builder)
        {
            if (builder == null)
                return 0;

            var breaks = 0;

            for (var i = 0; i < builder.Length; i++)
            {
                if (builder[i] == '\n')
                    breaks++;
            }

            return breaks;
        }

        /// <summary>How many line breaks a piece of text contains. Null counts as none.</summary>
        /// <param name="text">The text.</param>
        /// <returns>The count.</returns>
        internal static int CountLineBreaks(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var breaks = 0;

            foreach (var glyph in text)
            {
                if (glyph == '\n')
                    breaks++;
            }

            return breaks;
        }
    }
}

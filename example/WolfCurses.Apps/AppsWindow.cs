// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/20/2026

using System;
using System.Text;
using WolfCurses.Window;

namespace WolfCurses.Apps
{
    /// <summary>
    ///     The suite menu, and the only window this application defines. Every application will be a
    ///     <see cref="Window.Form.Form{T}" /> attached to it rather than a window of its own, which is what makes the
    ///     single ESC handler below cover all of them at once.
    ///     <para>
    ///         Adding an application is a folder, a form carrying
    ///         <c>[ParentWindow(typeof(AppsWindow))]</c>, a value on <see cref="AppsCommandsEnum" /> above Quit, and
    ///         one <c>AddCommand</c> line here. There is no registration step: the library discovers the window from
    ///         this assembly and the form from its attribute.
    ///     </para>
    /// </summary>
    public sealed class AppsWindow : Window<AppsCommandsEnum, AppsWindowInfo>
    {
        /// <summary>What is asked under the menu, restored whenever an application hands control back.</summary>
        private const string MenuPrompt = "Which application?";

        /// <summary>Initializes a new instance of the <see cref="AppsWindow" /> class.</summary>
        /// <param name="simUnit">Core simulation which is controlling the form factory.</param>
        // ReSharper disable once UnusedMember.Global
        public AppsWindow(SimulationApp simUnit) : base(simUnit)
        {
        }

        /// <summary>Called after the window has been added to the list of modes and made active.</summary>
        public override void OnWindowPostCreate()
        {
            base.OnWindowPostCreate();

            // Applications go above this line, one AddCommand each, in the order they appear on the menu. Quit stays
            // last, which is what the renumbering note on AppsCommandsEnum is about.
            AddCommand(OpenWordProcessor, AppsCommandsEnum.WordProcessor);
            AddCommand(OpenBasic, AppsCommandsEnum.Basic);
            AddCommand(OpenSpreadsheet, AppsCommandsEnum.Spreadsheet);
            AddCommand(Quit, AppsCommandsEnum.Quit);

            RestoreMenuChrome();
        }

        /// <summary>Puts the menu's own header and prompt back whenever an application hands control over or back.</summary>
        protected override void OnFormChange()
        {
            base.OnFormChange();

            if (CurrentForm != null)
                return;

            RestoreMenuChrome();
        }

        /// <summary>
        ///     ESC backs out of whichever application is showing and returns to the menu. Every application here is a
        ///     form on this one window, so catching the key before the base class forwards it down backs all of them
        ///     out from a single place, and no application has to handle ESC at all. Both sibling examples do exactly
        ///     this, and the library pins the routing it depends on in <c>EscapeReturnsToMenuTests</c>.
        ///     <para>
        ///         Two consequences worth knowing before an application is written against it. The library's own
        ///         modal controls are separate windows this override never sees, so a
        ///         <see cref="Controls.MessageBox" /> an application puts up is dismissed on its own terms rather
        ///         than by this. And <b>ESC is now spent</b>: an application wanting a menu bar of its own cannot
        ///         close it with ESC the way edit.com did, because ESC will already have left the application.
        ///     </para>
        /// </summary>
        /// <param name="key">The key that was pressed.</param>
        public override void OnKeyPressed(ConsoleKey key)
        {
            if (key == ConsoleKey.Escape && CurrentForm != null)
            {
                // The application gets first refusal, because only it knows whether it has a menu open that ESC
                // should shut instead. Everything that has nothing nested says no and is backed out of as before.
                if (CurrentForm is IHandlesEscape handler && handler.TryHandleEscape())
                    return;

                ClearForm();
                return;
            }

            base.OnKeyPressed(key);
        }

        /// <summary>
        ///     Rebuilds the header above the menu and the prompt below it.
        ///     <para>
        ///         Called at creation as well as on the way back from an application, and the first of those matters
        ///         more than it looks. The arcade sets its prompt only on the way back, so at start-up the prompt is
        ///         still the library's default and anything waiting to see the arcade's own wording waits out its
        ///         whole timeout: that cost one test file nine seconds against two minutes. Wait on state rather
        ///         than on text, and where the text is cheap to set early, set it early.
        ///     </para>
        /// </summary>
        private void RestoreMenuChrome()
        {
            var header = new StringBuilder();
            header.AppendLine();
            header.AppendLine("Small office applications, each leaning on a different part of WolfCurses.");
            header.AppendLine();
            header.Append("Choose one (arrow keys + ENTER, or type a number):");

            MenuHeader = header.ToString();
            PromptText = MenuPrompt;
        }

        /// <summary>Opens the word processor on its default document.</summary>
        private void OpenWordProcessor()
        {
            SetForm(typeof (WordProcessor.WordProcessorDialog));
        }

        /// <summary>Shows the BASIC environment.</summary>
        private void OpenBasic()
        {
            SetForm(typeof (Basic.BasicDialog));
        }

        /// <summary>Shows the spreadsheet on its sample sheet.</summary>
        private void OpenSpreadsheet()
        {
            SetForm(typeof (Spreadsheet.SpreadsheetDialog));
        }

        /// <summary>Closes the suite and hands the terminal back.</summary>
        private void Quit()
        {
            Program.Destroy();
        }
    }
}

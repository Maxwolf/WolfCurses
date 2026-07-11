// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 01/07/2016@7:28 PM

using System;
using WolfCurses.Window;

namespace WolfCurses.Example
{
    /// <summary>
    ///     Windows can have a class with any specific data or classes required for the window to function.
    /// </summary>
    public sealed class ExampleWindowInfo : WindowData
    {
        /// <summary>
        ///     Holds the name of the player so we can reference it on another form.
        /// </summary>
        public string PlayerName { get; set; }

        /// <summary>
        ///     Holds the file or folder path the user picked with the file dialog, so a follow-up form can show it.
        /// </summary>
        public string SelectedPath { get; set; }

        /// <summary>
        ///     Holds the outcome of the most recent standard-control demo (selection, message box, text prompt) so the
        ///     shared result dialog can show it back to the user.
        /// </summary>
        public string LastResult { get; set; }

        /// <summary>
        ///     Shows example of using the user data class to generate a piece of data that can be shown on the main interface and
        ///     accessed from form or window.
        /// </summary>
        public string ExampleUserData => $"Time: {DateTime.Now}";
    }
}
// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 01/07/2016@7:28 PM

using System;
using System.Collections.Generic;
using WolfCurses.Graphics;
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
        ///     The toppings the user last confirmed in the multi-select demo. Seeded with a couple of defaults so the
        ///     picker opens with something already checked, then re-checked on every reopen to show the round-trip.
        /// </summary>
        public List<string> SelectedToppings { get; } = new() {"Cheese", "Pepperoni"};

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
        ///     The renderer the user forced for the slideshow. Held here so the choice made in the menu's picker
        ///     survives into the form that draws the slides.
        /// </summary>
        public IImageRenderer SelectedImageRenderer { get; set; }

        /// <summary>
        ///     How much color the forced renderer may use. Only <see cref="HalfBlockImageRenderer" /> reads this — it is
        ///     what separates true color from 256-color, grayscale, and the colorless ASCII fallback. The true-pixel
        ///     renderers carry their own color story (sixel quantizes to its own palette, kitty is always 24-bit), so
        ///     they leave it at <see cref="AnsiColorModeEnum.Auto" /> and ignore it.
        /// </summary>
        public AnsiColorModeEnum SelectedImageColorMode { get; set; } = AnsiColorModeEnum.Auto;

        /// <summary>Human-readable name of the forced render type, shown as the slideshow's title.</summary>
        public string SelectedImageRendererName { get; set; }

        /// <summary>
        ///     Shows example of using the user data class to generate a piece of data that can be shown on the main interface and
        ///     accessed from form or window.
        /// </summary>
        public string ExampleUserData => $"Time: {DateTime.Now}";
    }
}
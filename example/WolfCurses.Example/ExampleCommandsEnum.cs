// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 01/07/2016@7:28 PM

using WolfCurses.Utility;

namespace WolfCurses.Example
{
    /// <summary>
    ///     Commands that will be loaded into the example window.
    /// </summary>
    public enum ExampleCommandsEnum
    {
        /// <summary>
        ///     Basic
        /// </summary>
        [Description("Prompts with text and no input.")] TextPrompt = 1,

        /// <summary>
        ///     Dialog prompt that asks a YES or NO question, user can enter single letters, spell it out, or even use alternative
        ///     words like NOPE.
        /// </summary>
        [Description("Prompts with yes/no question.")] YesNoPrompt = 2,

        /// <summary>
        ///     Dialog prompt that is not a question but waiting for specific information such as the users name.
        /// </summary>
        [Description("Prompts with custom input required.")] CustomPrompt = 3,

        /// <summary>
        ///     Slideshow that cycles through every image in the media folder using ANSI graphics.
        /// </summary>
        [Description("Slideshow of all media images.")] Slideshow = 4,

        /// <summary>
        ///     Slideshow with the transparent penguin composited on top of each media image.
        /// </summary>
        [Description("Compositing: penguin over the slideshow images.")] CompositeSlideshow = 5,

        /// <summary>
        ///     Opens the file browser to pick an image, then shows it with ANSI graphics.
        /// </summary>
        [Description("Open an image file (file browser).")] OpenImageFile = 6,

        /// <summary>
        ///     Opens the folder browser to pick a folder.
        /// </summary>
        [Description("Pick a folder (folder browser).")] SelectFolder = 7,

        /// <summary>
        ///     Live dashboard of the progress bar and graph controls (progress bar, marquee, sparkline, bar chart, line graph).
        /// </summary>
        [Description("Progress bars & graphs (live dashboard).")] ProgressAndGraphs = 8,

        /// <summary>
        ///     Opens the list picker to choose one option.
        /// </summary>
        [Description("Pick one from a list (SelectList).")] SelectFromList = 9,

        /// <summary>
        ///     Opens the list picker to check several options.
        /// </summary>
        [Description("Pick several from a list (multi-select).")] MultiSelectList = 10,

        /// <summary>
        ///     Opens a yes/no/cancel message box.
        /// </summary>
        [Description("Message box (yes/no/cancel).")] MessageBoxDemo = 11,

        /// <summary>
        ///     Opens a text prompt with a default value and validation.
        /// </summary>
        [Description("Text input (default + validation).")] TextInputDemo = 12,

        /// <summary>
        ///     Opens a masked text prompt for a passphrase.
        /// </summary>
        [Description("Password input (masked).")] PasswordDemo = 13,

        /// <summary>
        ///     Slideshow drawn with a render type the user forces, anywhere from a terminal's real pixels down to the
        ///     colorless ASCII fallback, so every rung of the ladder can be seen on the same pictures.
        /// </summary>
        [Description("Force slideshow render type (pixels down to ASCII).")] ForceRenderType = 14,

        /// <summary>
        ///     Plays the animated GIF from the media folder on a loop, at the speed the file asks for.
        /// </summary>
        [Description("Show animated GIF (plays on loop).")] ShowAnimatedGif = 15,

        /// <summary>
        ///     Bounces the DVD logo around a photograph like the screensaver did: one sprite, moving, not animated.
        /// </summary>
        [Description("Sprite Test (Basic) - DVD logo bounce.")] SpriteTestBasic = 16,

        /// <summary>
        ///     Five animated GIFs at random sizes bouncing through one another, added and removed from the scene on a
        ///     loop: sprite-over-sprite blending, scene editing, scaling, and animation all at once.
        /// </summary>
        [Description("Sprite Test (Advanced) - 5 animated, add/remove.")] SpriteTestAdvanced = 17,

        /// <summary>
        ///     Two penguins and the arrow keys: walk one into the other and the scene reports which sprite was hit.
        /// </summary>
        [Description("Sprite Test (Collision) - arrow keys, two penguins.")] SpriteTestCollision = 18,

        /// <summary>
        ///     Closes the console application.
        /// </summary>
        [Description("End")] CloseSimulation = 19
    }
}
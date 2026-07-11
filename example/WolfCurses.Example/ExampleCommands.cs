// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 01/07/2016@7:28 PM

using WolfCurses.Utility;

namespace WolfCurses.Example
{
    /// <summary>
    ///     Commands that will be loaded into the example window.
    /// </summary>
    public enum ExampleCommands
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
        [Description("Slideshow of all media images (ANSI graphics).")] Slideshow = 4,

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
        ///     Closes the console application.
        /// </summary>
        [Description("End")] CloseSimulation = 9
    }
}
// Created by Ron 'Maxwolf' McDowell (ron.mcdowell@gmail.com) 
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
        ///     Closes the console application.
        /// </summary>
        [Description("End")] CloseSimulation = 4
    }
}
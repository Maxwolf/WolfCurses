// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.IO;

namespace WolfCurses.Apps.Basic
{
    /// <summary>
    ///     Where the sample programs live and how one is read. Filesystem only, no console, so the interesting half
    ///     of opening a file can be tested without a screen.
    ///     <para>
    ///         <b>Nothing here came from anywhere else.</b> The programs shipped beside the executable were written
    ///         for this repository. The QBasic samples everybody remembers are Microsoft's and are not redistributable,
    ///         so they are not included; open one from your own disk instead, which is what the Open dialog is for.
    ///     </para>
    /// </summary>
    internal static class BasicLibrary
    {
        /// <summary>The program the environment opens on.</summary>
        private const string DefaultProgramName = "welcome.bas";

        /// <summary>Where the shipped programs are once the build has copied them.</summary>
        public static string Folder => Path.Combine(AppContext.BaseDirectory, "programs");

        /// <summary>The program to open on.</summary>
        public static string DefaultProgramPath => Path.Combine(Folder, DefaultProgramName);

        /// <summary>Where the Open dialog starts, falling back to the executable's own folder.</summary>
        public static string BrowseFolder => Directory.Exists(Folder) ? Folder : AppContext.BaseDirectory;

        /// <summary>Reads a program, or says why it could not.</summary>
        /// <param name="path">The file to read.</param>
        /// <param name="error">Why it failed, when it did.</param>
        /// <returns>The text, or null.</returns>
        public static string TryLoad(string path, out string error)
        {
            error = null;

            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException problem)
            {
                error = problem.Message;
                return null;
            }
            catch (UnauthorizedAccessException problem)
            {
                error = problem.Message;
                return null;
            }
        }
    }
}

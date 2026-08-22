// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.IO;

namespace WolfCurses.Apps.MediaPlayer
{
    /// <summary>
    ///     Where to look for something to play, and what counts as playable. No console anywhere in here.
    ///     <para>
    ///         <b>Nothing is shipped to play, and that is deliberate.</b> Every other application in this suite
    ///         carries its own sample, because a spreadsheet is a kilobyte of text and a calendar is less. A video
    ///         is megabytes and every one worth watching belongs to somebody, so this is the one screen that opens
    ///         empty and starts its browser in the folder the machine already keeps films and music in. The
    ///         generated test pattern is what stands in for a sample: it needs no file, no download and no licence,
    ///         and it exercises the whole pipeline.
    ///     </para>
    /// </summary>
    internal static class MediaLibrary
    {
        /// <summary>The file extensions the Open dialog offers, pictures and sound together.</summary>
        public static string[] Extensions { get; } =
        {
            ".mp4", ".mkv", ".webm", ".avi", ".mov", ".m4v", ".mpg", ".mpeg", ".wmv", ".flv", ".gif",
            ".mp3", ".flac", ".wav", ".ogg", ".opus", ".m4a", ".aac", ".wma"
        };

        /// <summary>The extensions that usually carry pictures, which is the guess made when nothing else can tell.</summary>
        private static readonly string[] _videoExtensions =
        {
            ".mp4", ".mkv", ".webm", ".avi", ".mov", ".m4v", ".mpg", ".mpeg", ".wmv", ".flv", ".gif"
        };

        /// <summary>
        ///     Where the Open dialog starts: the machine's own videos, then its music, then wherever this program
        ///     is. Somewhere that certainly exists, since a browser opening on a folder that does not is a browser
        ///     showing an error before the user has done anything.
        /// </summary>
        public static string BrowseFolder
        {
            get
            {
                foreach (var folder in new[] {Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.MyMusic})
                {
                    var path = SafeFolder(folder);

                    if (path != null)
                        return path;
                }

                return AppContext.BaseDirectory;
            }
        }

        /// <summary>Whether a name looks like it holds pictures rather than only sound.</summary>
        /// <param name="path">The file name.</param>
        /// <returns>TRUE when it probably has pictures in it.</returns>
        public static bool LooksLikeVideo(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            var extension = Path.GetExtension(path);

            foreach (var known in _videoExtensions)
            {
                if (string.Equals(extension, known, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>A special folder's path, or null when it is not there or cannot be asked about.</summary>
        /// <param name="folder">Which folder.</param>
        /// <returns>The path, or null.</returns>
        private static string SafeFolder(Environment.SpecialFolder folder)
        {
            try
            {
                var path = Environment.GetFolderPath(folder);

                return !string.IsNullOrEmpty(path) && Directory.Exists(path) ? path : null;
            }
            catch (Exception exception) when (exception is ArgumentException or PlatformNotSupportedException)
            {
                return null;
            }
        }
    }
}

// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 07/11/2026

using System;
using System.IO;
using System.Linq;
using WolfCurses.Graphics;

namespace WolfCurses.Example.Demos
{
    /// <summary>
    ///     Locates and sizes the shared media images that the ANSI graphics demos display. The project copies the
    ///     repository's <c>media/</c> images next to the executable into an <c>images/</c> folder, so everything is
    ///     resolved relative to the application directory and works no matter where the app is launched from.
    /// </summary>
    internal static class DemoImages
    {
        /// <summary>File name of the transparent penguin used as the compositing overlay.</summary>
        public const string PenguinFileName = "transparent_test.png";

        /// <summary>Folder next to the executable that holds the copied media images.</summary>
        public static string Folder => Path.Combine(AppContext.BaseDirectory, "images");

        /// <summary>Path to the WolfCurses logo used for the startup splash.</summary>
        public static string LogoPath => Path.Combine(Folder, "logo.jpg");

        /// <summary>Every supported image in the media folder, ordered by name.</summary>
        public static string[] ImageFiles()
        {
            if (!Directory.Exists(Folder))
                return Array.Empty<string>();

            return Directory.EnumerateFiles(Folder)
                .Where(IsSupportedImage)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>Options that fit an image inside the console, leaving room for the surrounding demo chrome.</summary>
        public static AnsiImageOptions FitOptions()
        {
            return new AnsiImageOptions
            {
                MaxColumns = Math.Max(1, SafeWindowWidth() - 2),
                MaxRows = Math.Max(1, SafeWindowHeight() - 8)
            };
        }

        private static bool IsSupportedImage(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".gif":
                    return true;
                default:
                    return false;
            }
        }

        private static int SafeWindowWidth()
        {
            try
            {
                return Console.WindowWidth;
            }
            catch
            {
                return 80;
            }
        }

        private static int SafeWindowHeight()
        {
            try
            {
                return Console.WindowHeight;
            }
            catch
            {
                return 24;
            }
        }
    }
}

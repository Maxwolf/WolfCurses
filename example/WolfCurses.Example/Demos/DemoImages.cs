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

        /// <summary>
        ///     What the photographs the slideshows cycle through are named: <c>image_001.jpg</c> through
        ///     <c>image_004.png</c>, and anything else named to match.
        /// </summary>
        private const string SlideshowPrefix = "image_";

        /// <summary>Folder next to the executable that holds the copied media images.</summary>
        public static string Folder => Path.Combine(AppContext.BaseDirectory, "images");

        /// <summary>Path to the WolfCurses logo used for the startup splash.</summary>
        public static string LogoPath => Path.Combine(Folder, "logo.jpg");

        /// <summary>Path to the animated GIF played by the animation demo.</summary>
        public static string AnimatedGifPath => Path.Combine(Folder, "animated.gif");

        /// <summary>Path to the transparent penguin composited over the slideshow images.</summary>
        public static string PenguinPath => Path.Combine(Folder, PenguinFileName);

        /// <summary>
        ///     The photographs the slideshows cycle through — <c>image_001.jpg</c> through <c>image_004.png</c> —
        ///     ordered by name.
        ///     <para>
        ///         Deliberately not everything in the folder, which is why this is a filter and not a directory listing.
        ///         <c>media/</c> is a fixture drawer rather than a gallery: the logo is the startup splash, the penguin
        ///         is what the compositing demo puts on top, and the GIFs belong to the animation demo — a slideshow
        ///         would show each of those as a motionless first frame and teach nothing about either. The slideshows
        ///         are about photographs, so they get the photographs, and every other demo names the one file it wants.
        ///     </para>
        /// </summary>
        public static string[] SlideshowImages()
        {
            if (!Directory.Exists(Folder))
                return Array.Empty<string>();

            return Directory.EnumerateFiles(Folder)
                .Where(IsSupportedImage)
                .Where(path => Path.GetFileName(path).StartsWith(SlideshowPrefix, StringComparison.OrdinalIgnoreCase))
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

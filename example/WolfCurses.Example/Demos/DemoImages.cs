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

        /// <summary>
        ///     The half blocks that <see cref="RendererSwitch" /> offers as the alternative to whatever the terminal
        ///     answered the startup probe with. <b>Not a default</b>: every moving demo opens on
        ///     <see cref="ImageRenderers.Default" />, because a demo that quietly substituted a cheaper renderer would
        ///     be showing you something other than what this terminal does. TAB is what reaches this.
        ///     <para>
        ///         Kept here, and shared, because it is one object rather than four and because the reason to reach for
        ///         it belongs in one place. Measured on the basic sprite test's scene into a 200x50 terminal, one
        ///         session, same machine: <b>half blocks ~5ms a frame, kitty ~1.7ms, sixel ~21ms</b>. Every renderer
        ///         clears the 33ms that 30fps allows, so TAB no longer rescues a slideshow-speed sixel — before the
        ///         2026-07-17 rework the same scene cost sixel ~205ms and kitty ~64ms in that session, because both
        ///         upscaled the canvas to 1.6 million terminal pixels on the CPU and then worked per pixel on the
        ///         result. Now sixel quantizes the ~100K source pixels and stretches runs arithmetically while
        ///         encoding, and kitty transmits the source and lets the terminal scale it.
        ///     </para>
        ///     <para>
        ///         What TAB still shows is <i>where the bill arrives</i>. The sprite tests compose a new picture every
        ///         frame, so TAB swaps the cost of every future frame and the ms/frame readout moves at once — watch
        ///         that figure, because fps sits pinned at the ~30 the frame budget allows on both sides now.
        ///         <see cref="AnimatedGifDialog" /> knows all its frames before it starts, renders them up front and
        ///         plays back an array lookup — so TAB there changes nothing about playback and everything about the
        ///         wait at the door: about half a second of half blocks against about two seconds of sixel, down from
        ///         seven before the rework. Same switch, entirely different bill.
        ///     </para>
        /// </summary>
        public static IImageRenderer AnimationRenderer { get; } = new HalfBlockImageRenderer();

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

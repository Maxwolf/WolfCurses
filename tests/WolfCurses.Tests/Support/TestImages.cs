using System;
using System.IO;

namespace WolfCurses.Tests.Support
{
    /// <summary>
    ///     Locates the real image fixtures that live at the repository root ("images for ANSI support" and
    ///     "media/logo.jpg") so the ANSI graphics integration tests can decode genuine PNG/JPEG files. The test binaries
    ///     run from deep inside bin/, so we walk up until we find the solution file and resolve paths from there. When
    ///     the repository layout is not present (for example a packaged-only checkout) the accessors report that so the
    ///     integration tests can skip rather than fail.
    /// </summary>
    internal static class TestImages
    {
        private static readonly Lazy<string> RepoRootLazy = new(FindRepoRoot);

        /// <summary>The repository root directory, or null if it could not be located.</summary>
        public static string RepoRoot => RepoRootLazy.Value;

        /// <summary>True when the image fixtures are present and the integration tests can run.</summary>
        public static bool Available => RepoRoot != null && Directory.Exists(AnsiFolder);

        /// <summary>Absolute path to the "images for ANSI support" folder (may not exist).</summary>
        public static string AnsiFolder =>
            RepoRoot == null ? null : Path.Combine(RepoRoot, "images for ANSI support");

        /// <summary>Absolute path to the project logo (may not exist).</summary>
        public static string Logo =>
            RepoRoot == null ? null : Path.Combine(RepoRoot, "media", "logo.jpg");

        /// <summary>Resolves a file inside the "images for ANSI support" folder by name.</summary>
        public static string Ansi(string fileName) =>
            AnsiFolder == null ? null : Path.Combine(AnsiFolder, fileName);

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "WolfCurses.sln")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            return null;
        }
    }
}

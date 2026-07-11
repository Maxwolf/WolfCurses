using System;
using System.IO;

namespace WolfCurses.Tests.Support
{
    /// <summary>
    ///     Locates the image fixtures in the repository's <c>media/</c> folder so the ANSI graphics integration tests
    ///     can decode genuine PNG/JPEG files. The test binaries run from deep inside bin/, so we walk up until we find
    ///     the solution file and resolve paths from there. When the image fixtures are not present (for example a
    ///     checkout that only kept the logo) the accessors report that so the integration tests skip rather than fail.
    /// </summary>
    internal static class TestImages
    {
        private static readonly Lazy<string> RepoRootLazy = new(FindRepoRoot);

        /// <summary>The repository root directory, or null if it could not be located.</summary>
        public static string RepoRoot => RepoRootLazy.Value;

        /// <summary>Absolute path to the media folder (may not exist).</summary>
        public static string MediaFolder =>
            RepoRoot == null ? null : Path.Combine(RepoRoot, "media");

        /// <summary>Absolute path to the project logo (may not exist).</summary>
        public static string Logo =>
            MediaFolder == null ? null : Path.Combine(MediaFolder, "logo.jpg");

        /// <summary>Resolves a file inside the media folder by name.</summary>
        public static string Media(string fileName) =>
            MediaFolder == null ? null : Path.Combine(MediaFolder, fileName);

        /// <summary>True when the ANSI image fixtures are present and the integration tests can run.</summary>
        public static bool Available => Media("image_001.jpg") is { } path && File.Exists(path);

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

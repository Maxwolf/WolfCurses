using System;
using System.IO;
using System.Linq;
using WolfCurses.Controls;
using Xunit;

namespace WolfCurses.Tests.Controls
{
    /// <summary>
    ///     Tests the filesystem logic of the file dialog against a throwaway temp folder tree: extension normalization
    ///     and matching, folder/file listing (with the open-file vs folder-mode distinction), and the navigation
    ///     helpers on <see cref="FileDialogData" />.
    /// </summary>
    public class FileDialogListingTests : IDisposable
    {
        private readonly string _root;

        public FileDialogListingTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "wolfcurses_fd_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            Directory.CreateDirectory(Path.Combine(_root, "Alpha"));
            Directory.CreateDirectory(Path.Combine(_root, "Beta"));
            File.WriteAllText(Path.Combine(_root, "photo.JPG"), "x");
            File.WriteAllText(Path.Combine(_root, "notes.txt"), "x");
            File.WriteAllText(Path.Combine(_root, "image.png"), "x");
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        [Fact]
        public void NormalizeExtensions_AddsDotsLowercasesTrimsAndDedupes()
        {
            var result = FileDialogListing.NormalizeExtensions(new[] { "jpg", ".JPG", " Png ", "", null, "png" });
            Assert.Equal(new[] { ".jpg", ".png" }, result);
        }

        [Fact]
        public void NormalizeExtensions_Null_ReturnsEmpty()
        {
            Assert.Empty(FileDialogListing.NormalizeExtensions(null));
        }

        [Theory]
        [InlineData("photo.JPG", true)]
        [InlineData("image.png", true)]
        [InlineData("notes.txt", false)]
        [InlineData("noext", false)]
        public void MatchesExtension_HonorsFilterCaseInsensitively(string fileName, bool expected)
        {
            var filter = new[] { ".jpg", ".png" };
            Assert.Equal(expected, FileDialogListing.MatchesExtension(fileName, filter));
        }

        [Fact]
        public void MatchesExtension_EmptyFilter_MatchesEverything()
        {
            Assert.True(FileDialogListing.MatchesExtension("anything.xyz", Array.Empty<string>()));
        }

        [Fact]
        public void BuildEntries_OpenFile_ListsParentThenFoldersThenFilteredFiles()
        {
            var filter = FileDialogListing.NormalizeExtensions(new[] { "jpg", "png" });
            var entries = FileDialogListing.BuildEntries(_root, filter, includeFiles: true);

            // Parent is always first.
            Assert.Equal(FileDialogEntryKind.ParentDirectory, entries[0].Kind);

            // Folders (sorted) come before files.
            Assert.Equal("Alpha", entries[1].Name);
            Assert.Equal(FileDialogEntryKind.Directory, entries[1].Kind);
            Assert.Equal("Beta", entries[2].Name);

            var names = entries.Select(e => e.Name).ToList();
            Assert.Contains("photo.JPG", names); // .JPG matches .jpg
            Assert.Contains("image.png", names);
            Assert.DoesNotContain("notes.txt", names); // filtered out

            // Files are ordered after all folders.
            var firstFileIndex = entries.ToList().FindIndex(e => e.Kind == FileDialogEntryKind.File);
            var lastDirIndex = entries.ToList().FindLastIndex(e => e.Kind == FileDialogEntryKind.Directory);
            Assert.True(firstFileIndex > lastDirIndex);
        }

        [Fact]
        public void BuildEntries_FolderMode_ExcludesFiles()
        {
            var entries = FileDialogListing.BuildEntries(_root, Array.Empty<string>(), includeFiles: false);

            Assert.DoesNotContain(entries, e => e.Kind == FileDialogEntryKind.File);
            Assert.Contains(entries, e => e.Name == "Alpha");
        }

        [Fact]
        public void BuildEntries_NoFilter_IncludesEveryFile()
        {
            var entries = FileDialogListing.BuildEntries(_root, Array.Empty<string>(), includeFiles: true);

            var names = entries.Select(e => e.Name).ToList();
            Assert.Contains("photo.JPG", names);
            Assert.Contains("notes.txt", names);
            Assert.Contains("image.png", names);
        }

        [Fact]
        public void BuildDriveEntries_ReturnsReadyDrives()
        {
            var drives = FileDialogListing.BuildDriveEntries();

            Assert.NotEmpty(drives); // every machine has at least one drive/root
            Assert.All(drives, d => Assert.Equal(FileDialogEntryKind.Drive, d.Kind));
        }

        [Fact]
        public void Data_Initialize_OpenFile_LoadsStartFolderAndFilters()
        {
            var data = new FileDialogData();
            data.Initialize(FileDialogMode.OpenFile, _root, new[] { ".png" }, _ => { }, null);

            Assert.True(data.Initialized);
            Assert.Equal(Path.GetFullPath(_root), data.CurrentDirectory);
            Assert.Contains(data.Entries, e => e.Name == "image.png");
            Assert.DoesNotContain(data.Entries, e => e.Name == "photo.JPG"); // filtered to .png
        }

        [Fact]
        public void Data_Initialize_MissingFolder_FallsBackToDrives()
        {
            var data = new FileDialogData();
            data.Initialize(FileDialogMode.OpenFile, Path.Combine(_root, "does-not-exist"), null, _ => { }, null);

            Assert.Null(data.CurrentDirectory); // drive-selection view
            Assert.All(data.Entries, e => Assert.Equal(FileDialogEntryKind.Drive, e.Kind));
        }

        [Fact]
        public void Data_GoUp_MovesToParentFolder()
        {
            var data = new FileDialogData();
            data.Initialize(FileDialogMode.OpenFile, Path.Combine(_root, "Alpha"), null, _ => { }, null);
            Assert.Equal(Path.GetFullPath(Path.Combine(_root, "Alpha")), data.CurrentDirectory);

            data.GoUp();

            Assert.Equal(Path.GetFullPath(_root), data.CurrentDirectory);
        }

        [Fact]
        public void Data_LoadDirectory_Invalid_SetsErrorAndStaysPut()
        {
            var data = new FileDialogData();
            data.Initialize(FileDialogMode.OpenFile, _root, null, _ => { }, null);
            var before = data.CurrentDirectory;

            data.LoadDirectory(Path.Combine(_root, "does-not-exist"));

            Assert.NotNull(data.ErrorMessage);
            Assert.Equal(before, data.CurrentDirectory);
        }

        [Fact]
        public void MatchesExtension_CompoundExtension_MatchesBySuffix()
        {
            var filter = FileDialogListing.NormalizeExtensions(new[] { "tar.gz" });

            Assert.True(FileDialogListing.MatchesExtension("backup.tar.gz", filter));
            Assert.False(FileDialogListing.MatchesExtension("photo.jpg", filter));
            // A bare name ending in the same letters (no separating dot) must not match.
            Assert.False(FileDialogListing.MatchesExtension("mytar.gz", filter));
        }

        [Fact]
        public void Data_TrailingSeparatorStart_GoUpReachesRealParentOnFirstPress()
        {
            var data = new FileDialogData();
            data.Initialize(FileDialogMode.OpenFile, _root + Path.DirectorySeparatorChar, null, _ => { }, null);

            // The stored path has no trailing separator...
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(_root)), data.CurrentDirectory);

            // ...so the very first "up" reaches the real parent instead of reloading the same folder.
            data.GoUp();
            Assert.Equal(Directory.GetParent(Path.GetFullPath(_root))!.FullName, data.CurrentDirectory);
        }
    }
}

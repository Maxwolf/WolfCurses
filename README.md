# Wolf Curses

Provides an abstraction of one or more windows that maps onto the console. Each window is represented by a character matrix. The programmer sets up each window to look as they want the display to look, and then tells wolf curses to update the screen. The library determines a minimal set of changes needed to update the display and then executes these using the terminal's specific capabilities and control sequences.

In short, this means that the programmer simply creates a character matrix of how the screen should look and lets wolf curses handle the work.

Contains example implementation of a console application using the Wolf curses library. A menu is displayed with a list of choices the user can make.

Fork this repository and use it as the base for your next application or just look at the code and cherry pick from it as you please.

![A cursing wolf.](https://raw.githubusercontent.com/Maxwolf/WolfCurses/master/media/logo.jpg)

## NuGet Package ##

To install Wolf Curses, run the following command in the [Package Manager Console](https://docs.nuget.org/docs/start-here/using-the-package-manager-console). If you would like to see the NuGet gallery page you can [find it here](https://www.nuget.org/packages/WolfCurses/).

```cmd
PM> Install-Package WolfCurses
```

## Cloning Instructions ##

```cmd
git clone https://github.com/Maxwolf/WolfCurses.git
```

## Compilation Instructions ##

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). From the repository root:

```cmd
dotnet build WolfCurses.sln
```

## Example Implementation ##

A runnable example console application lives in this repository at [`example/WolfCurses.Example`](example/WolfCurses.Example). It is its own project (referencing the library directly) and shows a few different menus, windows, and forms — plus a WolfCurses logo splash on startup, **Slideshow** / **Compositing** menu items that display the `media/` images (and a transparent penguin composited over them), and **file/folder browser** menu items that pick an image to display or a folder to report. Run it with:

```cmd
dotnet run --project example/WolfCurses.Example
```

## ANSI Graphics ##

Wolf Curses can display images (PNG, JPEG — baseline *and* progressive — BMP, GIF, TGA, …) directly in the terminal. Images are converted to a block of text and ANSI color escape sequences that you drop into your window's rendered text like any other string, so the scene graph draws them along with everything else.

```csharp
using WolfCurses.Graphics;

// Once, at start-up, so the terminal interprets the escapes and shows the block glyphs
// (enables virtual-terminal processing + a UTF-8 output encoding on Windows):
AnsiConsole.Enable();

// Decode + render ONCE and cache the string — the escapes never change, and a window's
// OnRenderWindow runs every tick, so rendering there would re-decode the image constantly:
private readonly string _logo = AnsiImage.RenderFile("media/logo.jpg");

// ...then just return the cached text from your window/form:
public override string OnRenderWindow() => _logo;
```

- **Sized to fit.** By default the image is scaled to fit the current console window while preserving its aspect ratio, so it is fully visible without resizing the terminal. Pass `new AnsiImageOptions { MaxColumns = …, MaxRows = … }` to bound it yourself.
- **Fit modes.** `AnsiImageOptions.Fit` controls how the image fills the area, mirroring CSS `object-fit`: `Contain` (default — show all of the image, letterboxed), `Cover` (fill the whole scene, cropping the overflow), `Stretch` (fill exactly, ignoring aspect ratio), or `ScaleDown` (like Contain but never enlarge past native size). `HorizontalAlignment`/`VerticalAlignment` choose which part `Cover` keeps.
- **Compositing.** Overlay a transparent image on top of another and see both — `background.Overlay(foreground)` (centered) or `background.Overlay(foreground, x, y)` alpha-composites them into one image, with the overlay's own transparency preserved. Use `.Resize(w, h)` to size an overlay first.
- **Two pixels per character.** It uses the Unicode half-block (`▀`) trick — foreground color for the top pixel, background color for the bottom — to double the vertical resolution and keep pixels square.
- **Transparency.** Transparent PNG pixels let the terminal background show through; set `AnsiImageOptions.BackgroundColor` to composite the image onto a solid color instead.
- **Graceful color downgrade.** True color by default, with automatic fallback to the 256-color palette, grayscale, or shaded ASCII on terminals that cannot do better (honoring `NO_COLOR`). Force a mode with `AnsiImageOptions.ColorMode`.
- **Pluggable decoding.** Decoding is done through [StbImageSharp](https://github.com/StbSharp/StbImageSharp) (a managed, public-domain decoder, no native binaries). To use a different image library, implement `IImageDecoder` and assign `ImageDecoders.Default` once at start-up.

## File & folder browser ##

WolfCurses ships a ready-made file/folder picker so an application doesn't have to build directory navigation itself. From inside a window the running simulation is available as `SimUnit`:

```csharp
using WolfCurses.Controls;

// Let the user pick an image file (only these extensions are shown):
FileDialog.OpenFile(SimUnit, startDirectory: "C:\\", extensions: new[] { ".jpg", ".png" },
    onFileSelected: path => { /* do something with the chosen file */ });

// ...or pick a folder:
FileDialog.SelectFolder(SimUnit, startDirectory: "C:\\",
    onFolderSelected: path => { /* do something with the chosen folder */ });
```

The dialog pushes itself on top of the current screen and lets the user navigate drives and folders — type a number to open an entry, `U` to go up, `D` to list drives, `N`/`P` to page through a long folder, and `C` to cancel (plus `S` to confirm the current folder when picking a folder). When the user chooses, your callback runs with the full path and the dialog closes itself. An empty extension filter shows every file.

Because the dialog is a window, list `typeof(FileDialogWindow)` in your app's `AllowedWindows`; its form ships inside the library and is discovered automatically.

## Purpose ##

The purpose of this project was to replicate the concept of the curses library created by Ken Arnold and originally released with BSD UNIX, where it was used for several games, most notably [Rogue](https://en.wikipedia.org/wiki/Rogue_(video_game) "Rogue (video game)").

## Curses-based software ##

The original curses project was designed to facilitate GUI-like functionality on a text-only device, such as a PC running in console mode, a hardware ANSI terminal, a [Telnet](https://en.wikipedia.org/wiki/Telnet "Telnet") or [SSH](https://en.wikipedia.org/wiki/Secure_Shell "Secure Shell") client, or similar.

Curses-based programs often have a [user interface](https://en.wikipedia.org/wiki/User_interface "User interface") that resembles a traditional graphical user interface, including '[widgets](https://en.wikipedia.org/wiki/Widget_(computing) "Widget (GUI)")' such as text boxes and scrollable lists, rather than the [command line interface](https://en.wikipedia.org/wiki/Command-line_interface "Command-line interface") (CLI) most commonly found on text-only devices. This can make them more user-friendly than a CLI-based program, while still being able to run on text-only devices. Curses-based software can also have a lighter resource footprint and operate on a wider range of systems (both in terms of hardware and software) than their GUI-based counterparts. This includes old pre-1990 machines along with modern embedded systems using text-only displays.

However, not all Curses-based software employs a [text user interface](https://en.wikipedia.org/wiki/Text-based_user_interface "Text-based user interface") which resembles a graphical user interface. One counterexample would be the popular vi text editor, which while not being CLI-based, uses memorized keyboard commands almost exclusively, rather than the prompting TUI/GUI style, which relies more on recognition than recall.

Curses is most commonly associated with Unix-like operating systems, although implementations for Microsoft Windows also exist.

## History ##

There are several other projects that have come after curses such as pcurses, PDCurses, and more recently ncurses which is used by most Linux text-mode installers to this day (2016 time of writing).

I am not affiliated with these other projects at all. I wanted to re-imagine these libraries for modern object-oriented languages without using a wrapper.

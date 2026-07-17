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
- **Pluggable drawing.** How pixels become screen output is a seam too — see below.

### Real pixels: sixel and kitty ###

Half blocks work everywhere, but they only get two pixels per character cell. Terminals that speak a true-pixel protocol can do far better — on a typical 10x20 cell that is about two hundred pixels per cell instead of two — and WolfCurses can drive them. Drawing is a seam mirroring the decoder one, so switching is a single line at start-up:

```csharp
// Everything drawn from now on uses sixel instead of half blocks:
ImageRenderers.Default = new SixelImageRenderer();

// ...or draw one picture differently without disturbing the global default:
var photo = image.ToAnsi(options, new KittyImageRenderer());
```

- **`HalfBlockImageRenderer`** — the default. Colored `▀` characters; works in any terminal that can do color at all.
- **`SixelImageRenderer`** — real pixels via the DEC sixel protocol, supported by xterm (built with sixel), foot, WezTerm, mlterm, contour, and Windows Terminal 1.22+. Sixel is indexed, so the picture is reduced to a palette (256 colors by default) chosen per-image by median cut — entries are spent where the picture actually has detail rather than on a fixed grid.
- **`KittyImageRenderer`** — real pixels via the kitty graphics protocol, supported by kitty, WezTerm, Ghostty, and Konsole. It transmits the pixels as they are — full 24-bit color and a real alpha channel, no palette — so it is the better choice where it is available.

Both are **opt-in on purpose**: there is no reliable way to ask a terminal what it supports without reading back an escape-sequence reply, which would race the library's own input handling — so a terminal that does not understand the protocol would print it as garbage rather than degrade. Choose based on what your application knows about where it runs, and keep `HalfBlockImageRenderer` as the safe default.

Both take the terminal's cell size in pixels (`new SixelImageRenderer(cellPixelWidth: 10, cellPixelHeight: 20)`), which is what converts between the pixels they draw in and the character cells the rest of the library speaks in. The defaults suit most terminals; raise them if pictures come out smaller than expected.

If you subscribe your own handler to `ScreenBufferDirtyEvent` instead of using `ConsolePresenter`, call `AnsiGraphics.StripMarkers(frame)` before writing it — true-pixel renderers mark the rows a picture covers so the presenter knows not to erase through them, and those markers must not reach the terminal. Frames without images pass through untouched.

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

## Progress bars & graphs ##

WolfCurses ships a set of drop-in **display widgets** (in `WolfCurses.Window.Control`) that turn data into a block of text you return from your window or form's render — no extra windows to register. They are pure string producers, so they compose with everything else (including ANSI images) and update in place as your data changes.

```csharp
using WolfCurses.Window.Control;

// Determinate progress bar: value against a maximum, or a 0..1 fraction.
var bar = new ProgressBar { Width = 24, Label = "Download" };
string line = bar.Render(bytesDone, bytesTotal);      // Download [██████████░░░░░░░░░░░░░░]  42%

// Inline sparkline of a whole series.
string trend = new Sparkline().Render(samples);        // ▁▂▄▅▇█▆▄▂  (one glyph per point)

// Horizontal bar chart of labelled values.
string chart = new BarChart { Width = 20 }.Render(new[]
{
    new BarChartValue("Wood", 12),
    new BarChartValue("Iron", 5),
});

// 2-D line graph of a series over time (optional axis, scale labels, and area fill).
string graph = new LineGraph { Width = 40, Height = 10 }.Render(samples);
```

- **`ProgressBar`** — determinate bar with configurable width, filled/empty glyphs, optional brackets, percentage, and a leading label. Clamps out-of-range and non-finite input (a non-positive maximum renders empty rather than throwing). For a quick one-off, the older static `TextProgress.DrawProgressBar(value, max, size)` is still there; `MarqueeBar` gives you an indeterminate ping-pong bar and `SpinningPixel` a spinner.
- **`Sparkline`** — a series drawn as one line of block glyphs (`▁▂▃▄▅▆▇█`), auto-scaled between the series min/max or a range you pin with `Minimum`/`Maximum`. Handy next to a label.
- **`BarChart`** — one row per `BarChartValue`, labels aligned to a common width, bars scaled to the largest value, with an optional aligned "track" and printed values.
- **`LineGraph`** — plots a series across a `Width`×`Height` grid (top = max, bottom = min) with optional connecting segments, area fill, a left Y-axis with min/max scale labels, and a bottom X-axis — good for a rolling metric over time.

The multi-line widgets join their rows with the platform newline and emit no trailing newline, so they slot cleanly into surrounding text. The example app's **Progress bars & graphs** menu item shows all of them animating together off the simulation tick.

## Dialogs & panels ##

Beyond the file browser, WolfCurses ships ready-made **modal dialogs** and a **panel** widget so common interactions don't have to be built from scratch. The dialogs push themselves on top of the current screen, take over input, and call you back with the result before closing themselves — the same pattern as `FileDialog`. From inside a window the simulation is available as `SimUnit`.

```csharp
using WolfCurses.Controls;
using WolfCurses.Window.Control;

// A bordered panel (a pure string widget — no window needed):
string panel = new Box { Title = "Status", Border = BoxBorderEnum.Double, Padding = 1 }.Render("All systems nominal.");

// Pick one option from a list (or ChooseMany for multi-select, returning several):
SelectList.Choose(SimUnit, "Pick a color", new[] { "Crimson", "Emerald", "Sapphire" },
    onChosen: index => { /* the chosen index */ });

// A yes/no/cancel message box (or MessageBox.Show(...) for a simple OK, MessageBox.Confirm(...) for yes/no):
MessageBox.Show(SimUnit, "Enable hard mode?", MessageBoxButtonsEnum.YesNoCancel,
    result => { /* result is Yes / No / Cancel */ });

// A text prompt with a default value, validation, and optional password masking:
TextInputDialog.Prompt(SimUnit, "What is your name?",
    onSubmit: name => { /* the entered value */ },
    defaultValue: "Traveler",
    validator: v => v.Length < 2 ? "Name must be at least 2 characters." : null);
```

- **`Box`** — draws a border (single, double, rounded, ASCII, or none) around any text, with an optional aligned title and interior padding. Widths are measured ignoring ANSI color escapes, so it frames colored text and even ANSI images correctly. The dialogs use it for their own framing.
- **`SelectList`** — a paginated picker. `Choose` returns the chosen option (by index, or by item with the generic overload); `ChooseMany` lets the user check several and returns them all. Numbers pick/toggle, `S` confirms a multi-select, `A`/`X` select all/none, `N`/`P` page, `C` cancels.
- **`MessageBox`** — `Show` for a simple acknowledgement, `Confirm` for a yes/no question, or the buttons overload for yes/no/cancel; the callback receives the `MessageBoxResultEnum`.
- **`TextInputDialog`** — `Prompt` for a line of text, optionally pre-filled with a default, validated (a returned message rejects and keeps the dialog open), and/or masked so typed characters echo as asterisks. Submitting a blank line cancels.

Each dialog is a window, so list its window type (`typeof(SelectListWindow)`, `typeof(MessageBoxWindow)`, `typeof(TextInputWindow)`) in your app's `AllowedWindows`; the forms ship in the library and are discovered automatically. The example app demonstrates all four.

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

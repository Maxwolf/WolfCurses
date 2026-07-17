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

A runnable example console application lives in this repository at [`example/WolfCurses.Example`](example/WolfCurses.Example). It is its own project (referencing the library directly) and shows a few different menus, windows, and forms — plus a WolfCurses logo splash on startup, **Slideshow** / **Compositing** menu items that display the `media/` photographs (and a transparent penguin composited over them), a **Show animated GIF** item that plays an animated GIF on loop at the speed the file asks for, **Sprite Test** items — **(Basic)** bounces the DVD logo around a photograph like the screensaver did, **(Advanced)** flies five animated GIFs at random sizes through one another while adding and removing them from the scene on a loop, **(Collision)** lets you walk one penguin into another with the arrow keys, all with a live fps readout — a **Force slideshow render type** item that redraws those same photos with every render type from a terminal's real pixels down to colorless ASCII, and **file/folder browser** menu items that pick an image to display or a folder to report. Run it with:

```cmd
dotnet run --project example/WolfCurses.Example
```

## ANSI Graphics ##

Wolf Curses can display images directly in the terminal — PNG, JPEG (baseline *and* progressive) and GIF work out of the box, with no set-up and no dependencies. Images are converted to a block of text and ANSI color escape sequences that you drop into your window's rendered text like any other string, so the scene graph draws them along with everything else.

```csharp
using WolfCurses.Graphics;

// Once, so the terminal interprets the escapes and shows the block glyphs
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
- **Sprites.** When the thing on top *moves*, a `SpriteScene` keeps the background as pixels and recomposes it as often as you like: `scene.Sprites.Add(new Sprite(pixels, x, y))`, then `scene.ToAnsi(options)` each frame. Sprites draw in order (last is nearest), are clipped rather than refused so they can walk in from off-screen, and honour their own transparency. Set `sprite.Image` to animate one — an animated sprite needs nothing else. The scene is the size of its background, which is the knob worth knowing: resize the background once to something near what the terminal can show and a frame costs a fraction of what it costs at a photograph's native resolution.
- **Collision.** `scene.SpritesTouching(sprite)` reports *which* sprites a given one has run into, and `a.Intersects(b)` is the bare bounding-box test behind it. The three **Sprite Test** demos cover the lot: the DVD logo bouncing, five animated GIFs flying through one another while being added and removed, and two penguins you steer into each other with the arrow keys.
- **Two pixels per character.** It uses the Unicode half-block (`▀`) trick — foreground color for the top pixel, background color for the bottom — to double the vertical resolution and keep pixels square.
- **Transparency.** Transparent PNG pixels let the terminal background show through; set `AnsiImageOptions.BackgroundColor` to composite the image onto a solid color instead.
- **Graceful color downgrade.** True color by default, with automatic fallback to the 256-color palette, grayscale, or shaded ASCII on terminals that cannot do better (honoring `NO_COLOR`). Force a mode with `AnsiImageOptions.ColorMode`.
- **Decoders included, and replaceable.** PNG, JPEG and GIF are decoded by the package itself, written from their specifications in pure managed code — so images work with no set-up and the package still has **zero dependencies**. A decoder is the one part of an image pipeline everybody needs and nobody wants to choose; owning the formats outright is what avoids making every consumer of a terminal UI library take a transitive dependency on an imaging library just to show a logo. What they are *not* is the fastest available, and they don't try to be — a picture bound for a terminal is about to be scaled down to a few thousand pixels anyway. Need a format outside those three, or speed, or just to not decode images two different ways in one process? Implement `IImageDecoder` — one method — and assign `ImageDecoders.Default` once at start-up. The example has a ready-made [StbImageSharp](https://github.com/StbSharp/StbImageSharp) adapter at [`Graphics/StbImageDecoder.cs`](example/WolfCurses.Example/Graphics/StbImageDecoder.cs) to copy. Pixels you decoded yourself (`AnsiImage.FromPixels`) never touch a decoder at all.
- **Missing textures look missing.** An image that can't be loaded — wrong path, corrupt file, a format nothing installed can decode — becomes the magenta-and-black checkerboard you already know from a game engine, instead of throwing. Nothing real is that colour, so you spot it across the room without reading anything. This matters more than it sounds: the recommended usage above is a field initializer, where an exception surfaces as a `TypeInitializationException` from a stack that no longer mentions the image; and in a text UI the console *is* the screen, so a stack trace lands on top of your interface. The reason is still in `AnsiImage.Error` (and goes to `Trace`), and `ImageDecoders.Default.Decode(stream)` still throws if you'd rather handle it yourself — the seam's contract is unchanged, only the convenience layer is forgiving.
- **Pluggable drawing.** How pixels become screen output is a seam too — see below.

<details>
<summary>What the built-in decoders cover</summary>

| | Covered | Not covered |
|---|---|---|
| **PNG** | Every colour type (greyscale, truecolour, palette, both alpha variants), every bit depth 1–16, transparency in all three forms, Adam7 interlacing | — |
| **JPEG** | Baseline, extended sequential, and **progressive**; 4:4:4 / 4:2:2 / 4:2:0 and any other sampling factors; restart markers; greyscale | Arithmetic coding, lossless and hierarchical modes, CMYK/YCCK |
| **GIF** | 87a and 89a, interlacing, transparency, local colour tables, animation (`GifDecoder.DecodeFrames`) | — |

Anything unsupported fails with a message naming the format and the seam, not with garbage pixels. The decoders are checked against [StbImageSharp](https://github.com/StbSharp/StbImageSharp) on real files as part of the test suite — an independent implementation reading the same bytes, which is the only cheap way to catch a misread spec.

</details>

### Real pixels: sixel and kitty ###

Half blocks work everywhere, but they only get two pixels per character cell. Terminals that speak a true-pixel protocol can do far better — on a typical 10x20 cell that is about two hundred pixels per cell instead of two — and WolfCurses can drive them. Drawing is a seam mirroring the decoder one, so picking the best renderer for wherever you happen to be running is one line at start-up:

```csharp
// Use whatever this terminal can actually do; falls back to half blocks when that is nothing special.
ImageRenderers.Default = ImageRenderers.ForCurrentTerminal();

// ...or draw one picture differently without disturbing the global default:
var photo = image.ToAnsi(options, new KittyImageRenderer());
```

- **`HalfBlockImageRenderer`** — the fallback, and the default until you change it. Colored `▀` characters; works in any terminal that can do color at all, and degrades further on its own to 256-color, grayscale, or plain ASCII.
- **`SixelImageRenderer`** — real pixels via the DEC sixel protocol, supported by xterm (built with sixel), foot, WezTerm, mlterm, contour, recent Konsole and VTE, iTerm2, and Windows Terminal 1.22+. Sixel is indexed, so the picture is reduced to a palette (256 colors by default) chosen per-image by median cut — entries are spent where the picture actually has detail rather than on a fixed grid.
- **`KittyImageRenderer`** — real pixels via the kitty graphics protocol, supported by kitty, WezTerm, and Ghostty. It transmits the pixels as they are — full 24-bit color and a real alpha channel, no palette — so it is preferred wherever both are available.

Both take the terminal's cell size in pixels (`new SixelImageRenderer(cellPixelWidth: 10, cellPixelHeight: 20)`), which is what converts between the pixels they draw in and the character cells the rest of the library speaks in. The defaults suit most terminals; raise them if pictures come out smaller than expected.

#### Detecting what the terminal can do ####

`ImageRenderers.ForCurrentTerminal()` reads the environment the terminal advertises itself through (`TERM`, `KITTY_WINDOW_ID`, `TERM_PROGRAM`, `VTE_VERSION`, and so on). It asks the terminal nothing, so it cannot hang or disturb input, and it is safe to call anywhere — including with output redirected. It is deliberately biased towards half blocks: **guessing wrong the safe way costs picture quality, guessing wrong the other way fills the screen with raw escape sequences.** Multiplexers (tmux, screen) report as half blocks too, since they rewrite escape sequences and need per-user passthrough configuration to let graphics past.

Two common terminals cannot be settled from the environment at all — xterm only has sixel when built and started for it, and Windows Terminal publishes no version to say whether it is 1.22 or later — so both come back as half blocks. To settle them, ask the terminal itself:

```csharp
// In your Main, BEFORE the loop that reads keys:
ImageRenderers.Default = ImageRenderers.For(AnsiConsole.ProbeGraphicsProtocol());
```

`ProbeGraphicsProtocol` writes a query and reads the terminal's reply off standard input, so it must run **before your input loop starts** — otherwise the two steal each other's characters. Nothing inside the library reads input on its own (`InputManager` is fed by your host), so that is the only requirement. It never throws, is bounded by a timeout, and falls back to the environment guess if the terminal says nothing useful. The example app does exactly this in `Program.cs`.

To see what any of this looks like on your own terminal, the example's **Force slideshow render type** menu item redraws the same photos with each render type in turn — kitty, sixel, half blocks in true color / 256 colors / grayscale, and the colorless ASCII fallback — alongside an *Auto* choice that reports whichever one the probe settled on. Forcing a protocol the terminal does not speak is instructive rather than harmful: you get the screenful of escape-sequence garbage that detection exists to avoid.

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

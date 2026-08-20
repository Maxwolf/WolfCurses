# Wolf Curses

**Build text user interfaces in C#.** You describe what the screen should look like; the library works out the smallest set of changes and draws it, flicker-free.

![A cursing wolf.](https://raw.githubusercontent.com/Maxwolf/WolfCurses/master/media/logo.gif)

Windows and forms, menus, dialogs, file pickers, progress bars and graphs, plus images, sprites and animation, drawn in the terminal with real pixels where the terminal supports them.

**Zero dependencies.** PNG, JPEG and GIF decoding included.

![The demo's basic sprite test: the DVD logo bouncing over a photograph drawn as real sixel pixels, with a live fps readout](docs/demo-sprite-basic.gif)

*Yes, that is a photograph in a terminal, at ~30 fps. Sixel where the terminal speaks it, half blocks everywhere else, detected automatically.*

## Install

```cmd
dotnet add package WolfCurses
```

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). [NuGet gallery page.](https://www.nuget.org/packages/WolfCurses/)

## Quick start

A complete program with a menu, a dialog, and the loop that drives it:

```csharp
using System.Threading;
using WolfCurses;
using WolfCurses.Controls;
using WolfCurses.Utility;
using WolfCurses.Window;

// Each enum value becomes a numbered menu choice; the description is the line the user reads.
public enum MainMenu
{
    [Description("Say hello")] Hello = 1,
    [Description("Quit")] Quit = 2
}

// Per-window state, yours to fill in.
public sealed class MainData : WindowData { }

// A window is a screen. Commands are wired to methods.
public sealed class MainWindow : Window<MainMenu, MainData>
{
    public MainWindow(SimulationApp simUnit) : base(simUnit) { }

    public override void OnWindowPostCreate()
    {
        AddCommand(() => MessageBox.Show(SimUnit, "Hello from WolfCurses!"), MainMenu.Hello);
        AddCommand(() => SimUnit.Destroy(), MainMenu.Quit);
    }
}

public sealed class MyApp : SimulationApp
{
    public static MyApp Instance { get; private set; }
    public static void Create() => Instance = new MyApp();

    protected override void OnFirstTick() => Restart();
    protected override void OnPreDestroy() => Instance = null;
    public override string OnPreRender() => "My App";

    public override void Restart()
    {
        base.Restart();
        WindowManager.Add(typeof(MainWindow));
    }
}

internal static class Program
{
    private static void Main()
    {
        MyApp.Create();
        while (MyApp.Instance != null)
        {
            MyApp.Instance.OnTick(true);   // reads input, ticks logic, redraws what changed
            Thread.Sleep(1);
        }
    }
}
```

That is the whole setup. You don't register windows, read keys, or draw frames. The library discovers `MainWindow`, drains the keyboard each tick, and presents changed rows itself.

Menus are steerable with the arrow keys as well as by typing a number, and typed input always wins.

## See it running

Two example apps live in this repo, each with its own README:

- **[The library tour](example/WolfCurses.Demo/README.md)**: images, sprites, widgets, colour, and every dialog.
- **[The arcade](example/WolfCurses.Games/README.md)**: ten games, each built on a different part of the library. Snake, Minesweeper, Tetris, WolfChess 5000, Missile Command, Labyrinth, Pac-Man, Blackjack, Poker and Battlezone.

```cmd
dotnet run --project example/WolfCurses.Demo
dotnet run --project example/WolfCurses.Games
```

Both are terminal UIs, so run them from a real terminal window and give it some room. Between them they cover both input styles, every pacing model, styled output, generated and decoded graphics, scrolling, and falling back to characters where a terminal can't show a picture.

Prebuilt, self-contained downloads for Windows, macOS and Linux are on the [releases page](https://github.com/Maxwolf/WolfCurses/releases), both apps in a single archive with no .NET install needed.

## Images in the terminal

Images become a block of text and color escapes that you drop into your window's output like any other string.

![The demo's animated GIF demo: a progress bar fills while every frame is pre-rendered, then the animation plays on loop with fps and ms/frame readouts](docs/demo-animated-gif.gif)

*An animated GIF playing on loop: decoded, pre-rendered, then played back at the speed the file asks for.*

```csharp
using WolfCurses.Graphics;

// Once at startup, so the terminal interprets escapes and shows block glyphs:
AnsiConsole.Enable();

// Decode and render ONCE, then cache. A window renders every tick, and you
// don't want to re-decode a picture a thousand times a second:
private readonly string _logo = AnsiImage.RenderFile("media/logo.png");

public override string OnRenderWindow() => _logo;
```

- **Fits automatically.** Scaled to the console window, aspect preserved. `AnsiImageOptions.Fit` gives you the CSS `object-fit` modes (`Contain`, `Cover`, `Stretch`, `ScaleDown`).
- **Real pixels where possible.** Sixel and kitty are detected and used automatically; half blocks (`▀`, two pixels per cell) everywhere else.
- **Color degrades gracefully.** True color → 256 colors → grayscale → shaded ASCII, honoring `NO_COLOR`.
- **Transparency works**, and `background.Overlay(foreground)` alpha-composites two images into one.
- **Missing textures look missing.** A broken path or corrupt file becomes the magenta-and-black checkerboard you know from game engines instead of throwing, because in a text UI a stack trace lands on top of your interface. The reason is in `AnsiImage.Error`.
- **Draw your own.** `Fill`, `DrawLine` and `DrawDisc` on a `PixelBuffer`. The rule: **`Fill` paints, every `Draw` composites.**
- **Decoders are swappable.** Implement `IImageDecoder` (one method) and assign `ImageDecoders.Default`.

<details>
<summary>What the built-in decoders cover</summary>

| | Covered | Not covered |
|---|---|---|
| **PNG** | Every colour type, every bit depth 1 to 16, transparency in all three forms, Adam7 interlacing | None |
| **JPEG** | Baseline, extended sequential, and **progressive**; any sampling factors; restart markers; greyscale | Arithmetic coding, lossless and hierarchical modes, CMYK/YCCK |
| **GIF** | 87a and 89a, interlacing, transparency, local colour tables, animation (`GifDecoder.DecodeFrames`) | None |

Unsupported input fails with a message naming the format and the seam, not with garbage pixels. The decoders are checked against [StbImageSharp](https://github.com/StbSharp/StbImageSharp) on real files in the test suite. That is an independent implementation reading the same bytes, and the cheap way to catch a misread spec.

</details>

<details>
<summary>Choosing the renderer yourself</summary>

Detection runs once, when your `SimulationApp` is constructed. It asks the terminal directly and falls back to environment variables when there's nothing to ask. It is deliberately biased toward half blocks: **guessing wrong the safe way costs picture quality; guessing wrong the other way fills the screen with escape sequences.** tmux and screen report as half blocks, since they rewrite escapes.

```csharp
// Overrule detection. A renderer you assign always wins:
ImageRenderers.Default = new SixelImageRenderer();

// ...or draw one picture differently, without touching the global default:
var photo = image.ToAnsi(options, new KittyImageRenderer());
```

- **`HalfBlockImageRenderer`**: the fallback; works in any terminal that can do color.
- **`SixelImageRenderer`**: xterm (built with sixel), foot, WezTerm, mlterm, contour, recent Konsole and VTE, iTerm2, Windows Terminal 1.22+.
- **`KittyImageRenderer`**: kitty, WezTerm, Ghostty. Full 24-bit color and real alpha, so it wins where both are available.

The demo app's **Force render type** menu item redraws everything with each one in turn, so you can see what your terminal actually does.

</details>

## Sprites, animation and collision

When the thing on top *moves*, a `SpriteScene` holds the background as pixels and recomposes as often as you like.

```csharp
var scene = new SpriteScene(background);
scene.Sprites.Add(new Sprite(pixels, x, y));

string frame = scene.ToAnsi(options);        // each frame
sprite.Image = nextGifFrame;                 // that's all animation takes
var hits = scene.SpritesTouching(sprite);    // which sprites it ran into
```

Sprites draw in order (last is nearest), are clipped rather than refused so they can walk in from off-screen, and honor their own transparency.

> **The one knob worth knowing:** the scene is the size of its background. Resize the background once to roughly what the terminal can show and a frame costs a fraction of what it costs at a photograph's native resolution.

The [three sprite demos](example/WolfCurses.Demo/README.md#sprites) cover the lot: a bouncing logo, five animated GIFs flying through one another while being added and removed, and two penguins you steer together to watch collision fire.

## Widgets

Drop-in display widgets that turn data into text you return from your render. No windows to register: they're pure string producers, so they compose with everything else.

![The demo's progress bars and graphs demo: a progress bar, marquee, sparkline, bar chart and scrolling line graph all animating together](docs/demo-progress-graphs.gif)

*`ProgressBar`, `MarqueeBar`, `Sparkline`, `BarChart` and `LineGraph` animating together. Every one of them is just a string returned from the form's render.*

```csharp
using WolfCurses.Window.Control;

var bar = new ProgressBar { Width = 24, Label = "Download" };
string line = bar.Render(bytesDone, bytesTotal);   // Download [██████████░░░░░░]  42%

string trend = new Sparkline().Render(samples);    // ▁▂▄▅▇█▆▄▂
string graph = new LineGraph { Width = 40, Height = 10 }.Render(samples);
string chart = new BarChart { Width = 20 }.Render(new[]
{
    new BarChartValue("Wood", 12),
    new BarChartValue("Iron", 5),
});
```

- **`ProgressBar`**: determinate, with configurable glyphs, brackets, percentage and label. `MarqueeBar` and `SpinningPixel` cover the indeterminate case.
- **`Sparkline`**: a series as one line of block glyphs, auto-scaled.
- **`BarChart`**: labelled horizontal bars, aligned to a common width.
- **`LineGraph`**: a 2-D plot with optional axes, scale labels and area fill.
- **`TextGrid`**: a rectangle of characters, each with its own style: boards, maps, playfields. `Render(x, y, columns, rows)` is a **window onto the grid**, so a world larger than the screen just scrolls, and `CenterOrigin` is the camera.
- **`BoxDrawing`**: picks the box-drawing character joining lines in a given set of directions. A rectangle knows its six glyphs up front; a *network* of lines (a maze, a table with interior rules, a wiring diagram) has sixteen answers and has to decide each cell from its neighbours.

Every widget takes colors and ramps, and anything you leave alone emits no escapes at all, so a plain build stays byte-for-byte plain.

## Dialogs, panels and pickers

Ready-made modals that push themselves on top of the current screen, take over input, and call you back before closing themselves.

```csharp
using WolfCurses.Controls;
using WolfCurses.Window.Control;

// A bordered panel (a pure string widget, no window needed):
string panel = new Box { Title = "Status", Border = BoxBorderEnum.Double, Padding = 1 }
    .Render("All systems nominal.");

SelectList.Choose(SimUnit, "Pick a color", new[] { "Crimson", "Emerald", "Sapphire" },
    onChosen: index => { /* ... */ });

MessageBox.Confirm(SimUnit, "Enable hard mode?", onYes: () => { /* ... */ });

TextInputDialog.Prompt(SimUnit, "What is your name?",
    onSubmit: name => { /* ... */ },
    defaultValue: "Traveler",
    validator: v => v.Length < 2 ? "Name must be at least 2 characters." : null);

FileDialog.OpenFile(SimUnit, startDirectory: "C:\\", extensions: new[] { ".jpg", ".png" },
    onFileSelected: path => { /* ... */ });
```

- **`Box`**: a border (single, double, rounded, ASCII or none) around any text, with optional title and padding. Widths ignore ANSI escapes, so it frames colored text and even images correctly.
- **`SelectList`**: a paginated picker; `Choose` for one, `ChooseMany` for several.
- **`MessageBox`**: `Show`, `Confirm`, or yes/no/cancel.
- **`TextInputDialog`**: a line of text, with a default, validation and optional password masking.
- **`FileDialog`**: browse drives and folders to pick a file or a directory.

All of them ship inside the library and are discovered automatically. (If you override `AllowedWindows` to curate your window list, include the ones you use.)

## Input

- **Keys arrive as you'd expect.** ENTER submits the typed command, BACKSPACE edits it, everything else both fills the prompt and reaches the focused form.
- **Mouse is opt-in.** `AnsiConsole.EnableMouse()` and presses start arriving as `OnMousePressed(MouseEvent)` with the cell that was clicked. Presses only: no motion, drag or wheel. **Windows only for now**; elsewhere it returns false having written nothing, so nothing changes.
- **`HeldAxis`** solves a problem every real-time terminal app hits: a terminal never reports a key being *let go*, so a held key arrives as a burst of presses. Feed it `Press(-1)` and read `Direction`; it infers the key-up from silence.

## Building from source

```cmd
git clone https://github.com/Maxwolf/WolfCurses.git
dotnet build WolfCurses.sln
dotnet test
```

Fork it and use it as the base for your next application, or just cherry-pick from it.

## Where it came from

This project re-creates the idea behind [curses](https://en.wikipedia.org/wiki/Curses_(programming_library)), the library Ken Arnold originally released with BSD UNIX and which [Rogue](https://en.wikipedia.org/wiki/Rogue_(video_game)) made famous, for a modern object-oriented language and without wrapping a native library.

<details>
<summary>A little more about curses</summary>

Curses was designed to give GUI-like functionality on a text-only device: a PC in console mode, a hardware ANSI terminal, a Telnet or SSH session. Curses-based programs often have an interface resembling a graphical one, with widgets like text boxes and scrollable lists, rather than the command-line style usually found on text-only devices. That can make them friendlier than a CLI while still running anywhere, including pre-1990 machines and modern embedded systems with text-only displays.

Not all of it looks like a GUI, though. vi is the counterexample: not command-line based, but driven almost entirely by memorized keystrokes rather than the prompting style that relies on recognition over recall.

Several projects followed the original: pcurses, PDCurses, and more recently ncurses, still used by most Linux text-mode installers. This project isn't affiliated with any of them.

</details>

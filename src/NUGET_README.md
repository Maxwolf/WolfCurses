# Wolf Curses

**Build text user interfaces in C#.** You describe what the screen should look like; the library works out the smallest set of changes and draws it, flicker-free.

Windows and forms, menus, dialogs, file pickers, progress bars and graphs, plus images, sprites and animation, drawn in the terminal with real pixels where the terminal supports them.

**Zero dependencies.** PNG, JPEG and GIF decoding included.

![The DVD logo bouncing over a photograph drawn as real sixel pixels, with a live fps readout](https://raw.githubusercontent.com/Maxwolf/WolfCurses/master/docs/demo-sprite-basic.gif)

*Yes, that is a photograph in a terminal, at ~30 fps. Sixel where the terminal speaks it, half blocks everywhere else, detected automatically.*

## Install

```
dotnet add package WolfCurses
```

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

That is the whole setup. Three things you might expect to write are already done for you:

- **Windows are discovered.** Every concrete `IWindow` in your assembly, plus the built-in control windows, with no registration.
- **Keys are read and routed**, at the start of every tick. ENTER submits the typed command, BACKSPACE edits it, every other key both fills the prompt and reaches the focused form.
- **Frames present themselves**, without flicker: rows overwritten in place rather than cleared first, only changed rows rewritten, the whole update as one write.

Forms (dialogs, prompts) derive from `Form<TData>` and attach to their parent window with a `[ParentWindow(typeof(MainWindow))]` attribute, again with no manual registration. Menus are steerable with the arrow keys as well as by typing a number, and typed input always wins. Up and Down walk the items in numbered order and roll over at the ends; when a menu is too tall for the terminal it reflows into columns, and Left and Right cross between them.

## What else is in the box

Each of these has a fuller write-up with code in the [main README](https://github.com/Maxwolf/WolfCurses).

**[Images in the terminal](https://github.com/Maxwolf/WolfCurses#images-in-the-terminal).** Decode a PNG, JPEG or GIF and drop it into your window's output like any other string:

```csharp
// Decode and render ONCE, then cache. A window renders every tick.
private readonly string _logo = AnsiImage.RenderFile("media/logo.png");
public override string OnRenderWindow() => _logo;
```

Scaled to fit with the CSS `object-fit` modes, transparency preserved, colour degrading from true colour through 256 and grayscale to plain ASCII, honouring `NO_COLOR`. Sixel and kitty are detected and used automatically, half blocks everywhere else. A file that cannot be loaded becomes the magenta-and-black checkerboard familiar from game engines instead of throwing, because in a text UI a stack trace lands on top of your interface. The decoders are written from spec in pure managed code, which is how this has zero dependencies, and `IImageDecoder` swaps in your own.

**[Sprites, animation and collision](https://github.com/Maxwolf/WolfCurses#sprites-animation-and-collision).** A `SpriteScene` holds the background as pixels and recomposes it as often as you like. Set `sprite.Image` to animate one, and `scene.SpritesTouching(sprite)` reports what it ran into.

**[Widgets](https://github.com/Maxwolf/WolfCurses#widgets).** `ProgressBar`, `Sparkline`, `BarChart` and `LineGraph` turn data into text you return from a render, with no windows to register. `TextGrid` is a rectangle of styled cells for boards and maps, with a viewport so a world larger than the screen just scrolls, and `BoxDrawing` picks the character that joins lines running in any set of directions.

![A progress bar, marquee, sparkline, bar chart and scrolling line graph all animating together](https://raw.githubusercontent.com/Maxwolf/WolfCurses/master/docs/demo-progress-graphs.gif)

Every widget takes colours and ramps, and colour is entirely opt-in: with styles left at their defaults a widget emits byte-for-byte what it did before colour existed, not even a reset.

**[Dialogs, panels and pickers](https://github.com/Maxwolf/WolfCurses#dialogs-panels-and-pickers).** `MessageBox`, `SelectList`, `TextInputDialog` and `FileDialog` push themselves on top of the current screen, take over input, and call you back with the result before closing themselves. `Box` borders any text, measuring width past ANSI escapes.

**[Input](https://github.com/Maxwolf/WolfCurses#input).** Mouse support is opt-in through `AnsiConsole.EnableMouse()` and Windows-only for now. `HeldAxis` recovers a held direction from the burst of presses a terminal actually delivers, since a terminal never reports a key being let go.

**Real-time screens.** `IntervalTimer` paces anything that moves on its own, off the system tick rather than the once-a-second simulation tick. A late period is dropped rather than banked, so a slow frame is never repaid as a burst of instant ones.

**Measuring styled text.** An escape sequence has length but no width, so `string.Length` is the wrong number for anything you want to pad or place beside something else. `AnsiText.VisibleLength` and `AnsiText.StripEscapes` share one parser, and `TextColumns.Join` puts blocks of text side by side using that measurement.

## See it running

Two example apps ship in the repository, each with its own guide:

- **[The library tour](https://github.com/Maxwolf/WolfCurses/blob/master/example/WolfCurses.Demo/README.md)**: images, sprites, widgets, colour, and every dialog.
- **[The arcade](https://github.com/Maxwolf/WolfCurses/blob/master/example/WolfCurses.Games/README.md)**: ten games, each built on a different part of the library.

Prebuilt, self-contained downloads for Windows, macOS and Linux are on the [releases page](https://github.com/Maxwolf/WolfCurses/releases), both apps in a single archive with no .NET install needed.

## Links

- [Source code and full documentation](https://github.com/Maxwolf/WolfCurses)
- [Example applications](https://github.com/Maxwolf/WolfCurses/tree/master/example)
- [MIT license](https://github.com/Maxwolf/WolfCurses/blob/master/LICENSE)

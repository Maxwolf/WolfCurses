# Wolf Curses

A text user interface (TUI) library for .NET console applications. You describe how the screen should look as plain text, and Wolf Curses tracks a back buffer and only pushes updates to the terminal when something actually changed. It also handles keyboard input buffering, stackable windows, menus built from enums, and modal forms/dialogs.

## Installation

```
dotnet add package WolfCurses
```

## Usage

Subclass `SimulationApp` and pump ticks from your main loop. Window types are discovered automatically — every concrete `IWindow` in your app's assembly (and the built-in controls in the library) is available with no registration; override `AllowedWindows` only to curate the list explicitly:

```csharp
public class ExampleApp : SimulationApp
{
    protected override void OnFirstTick()
    {
        Restart();
        WindowManager.Add(typeof(MainMenuWindow));
    }

    public override string OnPreRender() => string.Empty;
    protected override void OnPreDestroy() { }
}
```

```csharp
var app = new ExampleApp();

// That's the whole set-up. Frames draw themselves: whenever one changes, the scene graph presents it to
// the console without flicker — rows overwritten in place (never cleared first), only changed rows
// rewritten, the whole update as one write. To draw frames your own way instead, subscribe
// app.SceneGraph.ScreenBufferDirtyEvent — while any handler is attached, the built-in presenter stands down.

while (!app.IsClosing)
{
    // Keys are read and routed automatically too, at the start of every tick: ENTER submits the typed
    // command, BACKSPACE edits it, and every other key both fills the prompt and reaches the focused
    // form. To own key reading yourself, set app.InputManager.ReadsConsoleInput = false and hand keys
    // to app.InputManager.SendConsoleKey(key) — the identical routing — from wherever you get them.
    app.OnTick(true);
    Thread.Sleep(1);
}
```

Windows derive from `Window<TCommands, TData>`, where each value of the `TCommands` enum becomes a menu choice. Forms (dialogs, prompts) derive from `Form<TData>` and attach to their parent window with a `[ParentWindow(typeof(MainMenuWindow))]` attribute — no manual registration needed.

## ANSI graphics

Display images right in the terminal — PNG (with transparency), baseline and progressive JPEG, and GIF, all decoded by this package with no set-up and no dependencies. An image becomes a string of block characters and ANSI color escapes that you embed in your window's rendered text.

```csharp
using WolfCurses.Graphics;

// Enables VT processing + UTF-8 output so the escapes/glyphs render (Windows). Already done for you
// when a real terminal is attached — at start-up and again by the built-in frame presenter — so only
// hosts writing to the console entirely on their own ever call it.
AnsiConsole.Enable();

// Decode + render ONCE and cache it — OnRenderWindow runs every tick, so never render there.
private readonly string _logo = AnsiImage.RenderFile("media/logo.jpg");
public override string OnRenderWindow() => _logo;
```

By default the image is scaled to fit the console window while keeping its aspect ratio (no terminal resizing needed), transparent pixels let the background show through, and true color degrades gracefully to 256-color/grayscale/ASCII. Set `AnsiImageOptions.Fit` to `Cover`, `Stretch`, or `ScaleDown` to fill a scene instead of letterboxing, and composite a transparent image onto another with `background.Overlay(foreground)`.

**Decoders included, and replaceable.** PNG, JPEG and GIF are decoded by the package itself — written from their specifications in pure managed code, so images work out of the box and this still has **zero dependencies**. PNG covers every colour type, bit depth and Adam7; JPEG covers baseline *and* progressive at any chroma subsampling; GIF covers 87a/89a, interlacing and transparency, and `GifDecoder.DecodeFrames` walks an animation frame by frame with its delays. They aim at correctness rather than speed, which is the right trade when the picture is about to be scaled down to fit a terminal anyway. Need another format, or more speed, or just to reuse the imaging library you already have? Implement `IImageDecoder` — a single method — and assign `ImageDecoders.Default` once at start-up; the example app has a [StbImageSharp](https://github.com/StbSharp/StbImageSharp) adapter to copy. `AnsiImage.FromPixels` needs no decoder at all.

**Missing textures look missing.** An image that can't be loaded — wrong path, corrupt file, unsupported format — becomes the magenta-and-black checkerboard familiar from game engines rather than throwing, because the documented usage is a field initializer (where an exception becomes a confusing `TypeInitializationException`) and because in a text UI a stack trace lands on top of your interface. The reason stays in `AnsiImage.Error`; `ImageDecoders.Default.Decode(stream)` still throws if you want to handle it yourself.

### Real pixels: sixel and kitty

Half blocks get two pixels per character cell and work anywhere. Terminals speaking a true-pixel protocol can do roughly two hundred times better, and WolfCurses uses the best one available **automatically**: creating your simulation asks the terminal, once, which protocol it understands and routes every image through the answer. How pixels become output is still a seam, so overruling the terminal is one line — a renderer you assign always wins, before or after the simulation exists:

```csharp
ImageRenderers.Default = new SixelImageRenderer();
```

`SixelImageRenderer` (xterm with sixel, foot, WezTerm, mlterm, contour, recent Konsole/VTE, iTerm2, Windows Terminal 1.22+) reduces the picture to a per-image palette chosen by median cut. `KittyImageRenderer` (kitty, WezTerm, Ghostty) sends full 24-bit color with a real alpha channel. Anything unproven — including tmux/screen and anything with output redirected — falls back to `HalfBlockImageRenderer`, because guessing wrong the safe way costs quality while guessing wrong the other way prints escape sequences as garbage.

Detection (`ImageRenderers.AutoDetect()`) probes the terminal over standard input — the only way to settle xterm and Windows Terminal, which advertise nothing — so it must run **before your input loop starts**, which constructing the simulation naturally is; it never throws, is bounded by a timeout, and falls back to the environment's guess (`TERM`, `KITTY_WINDOW_ID`, `VTE_VERSION`, ...). It runs once per process, and drawing images *before* the simulation exists just means calling it yourself first. Everything is pure managed code: no native binaries, and no package dependencies at all.

## Progress bars & graphs

Drop-in display widgets in `WolfCurses.Window.Control` turn data into text you return from a window/form's render — no windows to register.

```csharp
using WolfCurses.Window.Control;

new ProgressBar { Label = "Download", Width = 24 }.Render(done, total); // determinate bar + percentage
new Sparkline().Render(samples);                                       // inline ▁▂▄▅▇█ trend of a series
new BarChart { Width = 20 }.Render(new[] { new BarChartValue("Wood", 12), new BarChartValue("Iron", 5) });
new LineGraph { Width = 40, Height = 10 }.Render(samples);             // 2-D plot over time
```

`ProgressBar` (determinate), `Sparkline`, `BarChart`, and `LineGraph` are pure string producers with robust clamping and edge-case handling (empty/flat/negative/non-finite input); `MarqueeBar`/`SpinningPixel` cover the indeterminate cases. The example app's **Progress bars & graphs** screen animates them all off the simulation tick.

## File & folder browser

A ready-made picker so you don't build directory navigation yourself. From inside a window the simulation is available as `SimUnit`:

```csharp
using WolfCurses.Controls;

FileDialog.OpenFile(SimUnit, "C:\\", new[] { ".jpg", ".png" }, onFileSelected: path => { /* ... */ });
FileDialog.SelectFolder(SimUnit, "C:\\", onFolderSelected: path => { /* ... */ });
```

The dialog's window and form ship in the library and are discovered automatically; if your app overrides `AllowedWindows`, include `typeof(FileDialogWindow)` in the list.

## Dialogs & panels

Ready-made modal dialogs and a panel widget for common interactions. The dialogs call you back with the result and close themselves.

```csharp
using WolfCurses.Controls;
using WolfCurses.Window.Control;

new Box { Title = "Status", Border = BoxBorderEnum.Double, Padding = 1 }.Render("All good."); // bordered panel

SelectList.Choose(SimUnit, "Pick", new[] { "A", "B", "C" }, index => { /* ... */ }); // or ChooseMany for multi-select
MessageBox.Show(SimUnit, "Proceed?", MessageBoxButtonsEnum.YesNoCancel, result => { /* Yes/No/Cancel */ });
TextInputDialog.Prompt(SimUnit, "Name?", name => { /* ... */ }, defaultValue: "Traveler",
    validator: v => v.Length < 2 ? "Too short" : null); // add masked: true for passwords
```

`Box` (a pure widget) borders any text with an optional title, measuring width past ANSI color escapes. `SelectList`, `MessageBox`, and `TextInputDialog` are windows shipped in the library, so they are discovered automatically — an app that overrides `AllowedWindows` must include `typeof(SelectListWindow)` / `typeof(MessageBoxWindow)` / `typeof(TextInputWindow)` in its list.

## Links

- [Source code](https://github.com/Maxwolf/WolfCurses)
- [Complete example application](https://github.com/Maxwolf/WolfCurses/tree/master/example/WolfCurses.Example)
- [MIT license](https://github.com/Maxwolf/WolfCurses/blob/master/LICENSE)

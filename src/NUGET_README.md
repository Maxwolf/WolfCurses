# Wolf Curses

A text user interface (TUI) library for .NET console applications. You describe how the screen should look as plain text, and Wolf Curses tracks a back buffer and only pushes updates to the terminal when something actually changed. It also handles keyboard input buffering, stackable windows, menus built from enums, and modal forms/dialogs.

## Installation

```
dotnet add package WolfCurses
```

## Usage

Subclass `SimulationApp`, list the window types your app can show, and pump ticks from your main loop:

```csharp
public class ExampleApp : SimulationApp
{
    public override IEnumerable<Type> AllowedWindows =>
        new[] { typeof(MainMenuWindow) };

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

// Presents each frame without flicker: rows are overwritten in place (never cleared first), only rows
// that changed are rewritten, and the whole update goes out as one write. The constructor also readies
// the console for ANSI graphics (VT processing on Windows + UTF-8 output).
var presenter = new ConsolePresenter();
app.SceneGraph.ScreenBufferDirtyEvent += presenter.Present;

while (!app.IsClosing)
{
    app.OnTick(true);

    // Forward keystrokes into the input buffer.
    if (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true);
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                app.InputManager.SendInputBufferAsCommand();
                break;
            case ConsoleKey.Backspace:
                app.InputManager.RemoveLastCharOfInputBuffer();
                break;
            default:
                app.InputManager.AddCharToInputBuffer(key.KeyChar);
                break;
        }
    }
}
```

Windows derive from `Window<TCommands, TData>`, where each value of the `TCommands` enum becomes a menu choice. Forms (dialogs, prompts) derive from `Form<TData>` and attach to their parent window with a `[ParentWindow(typeof(MainMenuWindow))]` attribute — no manual registration needed.

## ANSI graphics

Display images (PNG with transparency, baseline and progressive JPEG, and more) right in the terminal. An image becomes a string of block characters and ANSI color escapes that you embed in your window's rendered text.

```csharp
using WolfCurses.Graphics;

// Once at start-up: enables VT processing + UTF-8 output so the escapes/glyphs render (Windows).
// Already done for you if you create a ConsolePresenter, as in the usage snippet above.
AnsiConsole.Enable();

// Decode + render ONCE and cache it — OnRenderWindow runs every tick, so never render there.
private readonly string _logo = AnsiImage.RenderFile("media/logo.jpg");
public override string OnRenderWindow() => _logo;
```

By default the image is scaled to fit the console window while keeping its aspect ratio (no terminal resizing needed), transparent pixels let the background show through, and true color degrades gracefully to 256-color/grayscale/ASCII. Set `AnsiImageOptions.Fit` to `Cover`, `Stretch`, or `ScaleDown` to fill a scene instead of letterboxing, and composite a transparent image onto another with `background.Overlay(foreground)`. Options live on `AnsiImageOptions`; decoding is pluggable via `IImageDecoder` / `ImageDecoders.Default` (the built-in decoder is the managed, public-domain StbImageSharp).

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

List `typeof(FileDialogWindow)` in your app's `AllowedWindows`; the dialog's form ships in the library and is discovered automatically.

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

`Box` (a pure widget) borders any text with an optional title, measuring width past ANSI color escapes. `SelectList`, `MessageBox`, and `TextInputDialog` are windows — list `typeof(SelectListWindow)` / `typeof(MessageBoxWindow)` / `typeof(TextInputWindow)` in your `AllowedWindows`.

## Links

- [Source code](https://github.com/Maxwolf/WolfCurses)
- [Complete example application](https://github.com/Maxwolf/WolfCurses/tree/master/example/WolfCurses.Example)
- [MIT license](https://github.com/Maxwolf/WolfCurses/blob/master/LICENSE)

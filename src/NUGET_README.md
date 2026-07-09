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

// Redraw the console only when the screen text changes.
app.SceneGraph.ScreenBufferDirtyEvent += tuiContent =>
{
    Console.Clear();
    Console.WriteLine(tuiContent);
};

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

## Links

- [Source code](https://github.com/Maxwolf/WolfCurses)
- [Complete example application](https://github.com/Maxwolf/WolfCurses.Example)
- [MIT license](https://github.com/Maxwolf/WolfCurses/blob/master/LICENSE)

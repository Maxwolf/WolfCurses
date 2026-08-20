# WolfCurses.Demo

The library tour: a guided walk through everything [WolfCurses](../../README.md) can draw. Menus, forms and dialogs, images and sprites, widgets and colour.

```cmd
dotnet run --project example/WolfCurses.Demo
```

This is an interactive terminal UI, so run it from a real terminal window rather than by double-clicking, and give the window some room. It cannot be smoke-run with output redirected.

**ESC backs out of any demo** to the main menu.

## On startup

The WolfCurses wordmark in ASCII art, with a rainbow sliding through it. There is no image file behind it: each cell takes a sample from `ColorRamp.Rainbow` where the position depends on column, row and elapsed time, so a gradient that slides is just arithmetic. Nothing was added to the library for it.

It is also the smallest demonstration of how colour degrades. The styles are left on `Auto`, so `NO_COLOR` or the **Force render type** screen reach it, and at no colour the escapes vanish and the output is byte-identical to the plain art.

## Images

| Menu item | What it shows |
| --- | --- |
| **Slideshow** | The `media/` photographs, one per tick |
| **Compositing** | A transparent penguin alpha-composited over those same photographs |
| **Show animated GIF** | An animated GIF playing on loop |
| **Image error handling** | Broken paths and truncated files becoming the magenta-and-black checkerboard |
| **Force render type** | Overrides the whole graphics stack, then snaps back to the menu |

![An animated GIF playing on loop, with a progress bar filling while frames are pre-rendered](../../docs/demo-animated-gif.gif)

**Show animated GIF** is the test rig for animated decoding. The library's own decoder walks all 91 frames, a `ProgressBar` fills while they are pre-rendered, and then playback is an array lookup at 0.00 ms/frame. The pre-render is spread across the tick loop a slice at a time rather than blocking, which is why the bar moves at all.

**Force render type** is worth finding. It offers kitty, sixel, half blocks at true colour, 256 colours or grayscale, colourless ASCII, and *Auto* to hand it back to the startup probe. The choice is global, so whichever demo you open next is drawn that way. The colour modes reach further than the pictures do: widgets and styled prose resolve through the same setting, so forcing grayscale greys the graphs and the pride flags too. Forcing a protocol your terminal does not speak is instructive rather than harmful, since you get exactly the screenful of escape-sequence garbage that automatic detection exists to avoid.

## Sprites

![The DVD logo bouncing over a photograph, with a live fps readout](../../docs/demo-sprite-basic.gif)

**Sprite Test (Basic)** is the DVD screensaver bounce over a photograph, with a live fps and ms/frame readout.

![Five animated GIF sprites at random sizes flying over a photograph, blending and bouncing](../../docs/demo-sprite-advanced.gif)

**Sprite Test (Advanced)** tests four things at once: sprite-over-sprite alpha blending, live scene mutation, random scaling, and animation. Five animated GIFs spawn at random sizes and bounce, one is removed every two seconds, the scene empties completely, then it refills with new ones. Animation needs nothing from the library, since `Sprite.Image` is settable and each sprite runs its own clock.

**Sprite Test (Collision)** lets you walk one penguin into another with the arrow keys. It is the worked example for the trap in collision detection: touching stays true for hundreds of frames, so you act on the *transition*, not the state. It also shows modal pause for free, since the message box takes focus and only the focused window ticks.

All three read fps and ms/frame, which measure different things on purpose. ms/frame is what the work costs, and it moves when you change the canvas size. fps is how often it happened, gated by the frame budget and the host loop.

**TAB flips the renderer live** in every moving demo, and the same switch bills two completely different ways. In a sprite test it changes the cost of every future frame. In the animated GIF it has to re-decode and re-render all 91 frames before anything moves again.

## Widgets and colour

![A progress bar, marquee, sparkline, bar chart and scrolling line graph animating together](../../docs/demo-progress-graphs.gif)

**Progress bars & graphs** animates `ProgressBar`, `MarqueeBar`, `Sparkline`, `BarChart` and `LineGraph` together off the simulation tick. Every one of them is just a string returned from the form's render.

**Pride flags** is the proof that widget colour is general rather than a special case. LEFT and RIGHT walk seventeen flags, and each one is an ordinary `BarChart` with empty labels, no separator, values hidden and all values equal, which leaves nothing on a row but a full-width bar. Give it the flag's stepped ramp in `Spread` mode and you have stripes. Nothing flag-specific was added to the library.

## Dialogs and prompts

The rest of the menu covers the interaction pieces: a text prompt, a yes/no question, a custom input prompt, single and multi-select pickers, a message box, a validated text input, a masked password field, and the file and folder browsers.

## Notes

The project copies the repository's shared `media/` images next to the executable into an `images/` folder. The glob is per extension, so a new image format needs a line in the csproj or the demos will never see it.

`media/` is a fixture drawer rather than a gallery. The slideshows show only the numbered photographs, and every other demo names the one file it wants, so dropping a new asset in does not add it to the slideshows. That is deliberate: the GIFs would show up as motionless first frames.

`Graphics/StbImageDecoder.cs` is a complete worked example of the thirty-line adapter the `IImageDecoder` seam takes, but it is **not installed**. `Program.Main` has the one line that would install it, commented out, so this app runs on the library's built-in decoders and proves they handle real photographs.

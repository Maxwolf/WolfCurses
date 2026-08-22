# WolfCurses.Games

A small arcade built on [WolfCurses](../../README.md). Ten games, each deliberately built on a different part of the library, so between them they cover both input styles, every pacing model, styled output, generated and decoded graphics, scrolling, and falling back to characters where a terminal cannot show a picture.

```cmd
dotnet run --project example/WolfCurses.Games
```

These are terminal UIs, so run them from a real terminal window rather than by double-clicking, and give the window some room. Games that can draw pictures check the terminal first and fall back to a character view, which is never a consolation prize: every entity has its own glyph as well as its own colour, so an 80x24 console plays exactly the same game.

## The games

| Game | Controls | What it demonstrates |
| --- | --- | --- |
| **Snake** | Arrows or WASD, ENTER or ESC to quit | Steering, paced off the system tick |
| **Minesweeper** | Type `B4`, `F B4` to flag, `R` new board. With a mouse: the square under the pointer lights up, left opens, right flags, and clicking the face deals a new board | Typed input and the pointer, on a panel drawn the way Windows 95 drew it |
| **Tetris** | Arrows or WASD, UP to turn, SPACE to drop, `R` for a new well | Layout: the only screen putting two things side by side |
| **WolfChess 5000** | Type a move (`e4`, `Nf3`, `e2e4`) or arrows plus ENTER, `help` for commands | Real piece artwork composited into one image, and a bot that thinks in slices so the screen keeps moving |
| **Missile Command** | Arrows or WASD aim, SPACE fires, `Z`/`X`/`C` picks a battery, TAB switches view. With a mouse: move to aim, click to fire, hold the button and sweep for a barrage | 30 fps ballistics on a picture the program *draws* rather than decodes |
| **Labyrinth** | Arrows or WASD, `R` for a new maze | A world bigger than the terminal, so it needs a camera |
| **Pac-Man** | Arrows or WASD | Walls as a connected network of lines, and four ghosts with one line of targeting logic each |
| **Blackjack** | `H` hit, `S` stand, ENTER deals again, TAB toggles pictures, ESC leaves | Card artwork, fanned so only each card's corner shows |
| **Poker** | `1`-`5` hold, SPACE draws, ENTER deals again, TAB toggles pictures, ESC leaves | Five-card draw, jacks or better, sharing every line of card code with blackjack |
| **Battlezone** | `W`/`S` gear, `A`/`D` steer, SPACE fires, TAB switches view, ENTER replays, ESC leaves | A first-person wireframe view rather than a map: a camera, a horizon and a radar |

## A few things worth knowing

**Minesweeper sizes its board to your terminal.** It picks the largest of four presets that fits, so a modern window gets a real game rather than a nine-by-nine postage stamp. Expert is offered only when a mouse is available, because thirty columns runs off the end of the alphabet and nobody wants to type `AD7`. Every unopened square is a real four-sided box, which is why a tile takes two rows: a box needs a line above its contents and one below, and a character cell holds one.

**Pac-Man has no path-finding at all.** All four ghosts run the same seven lines and differ only in the one-line answer to what they are aiming at. Being flanked in a corridor is a consequence of four different targets, nothing more.

**Battlezone is the only screen here that is a view rather than a map.** Everything else is shown whole from outside, so your information is complete and the difficulty is in what to do with it. Here the screen shows what is in front of the tank and the radar shows only bearings, so combining them while something you cannot see manoeuvres behind you *is* the game. Its character view wants about 30 rows before it will switch to pictures, since a wireframe is the one subject a character grid is genuinely good at.

**The throttle is a gear that stays where you put it.** Holding a direction and steering at the same time does not work in a terminal: the operating system repeats only the *last* key pressed, so a tank driven by two held keys stops dead every time you turn. The original cabinet used levers, and so does this.

## Chess

The move generator is verified against published perft node counts rather than hand-written examples, which is the only test of a move generator worth having. The test suite caps this at depth 4; these verbs go further by hand:

```cmd
dotnet run --project example/WolfCurses.Games -- perft 4   # move generation vs. published counts
dotnet run --project example/WolfCurses.Games -- rules     # notation, results, the draw rules
dotnet run --project example/WolfCurses.Games -- bot       # tactics and thinking time
dotnet run --project example/WolfCurses.Games -- board     # the render pipeline, as ASCII
```

The bot searches one root move per tick rather than blocking, so the board keeps redrawing and ESC keeps working while it thinks. It also carries an endgame term, without which it could not win a won game: material and piece-square tables score every king-and-queen-against-king position identically, so those all ended in threefold repetition.

## How it is put together

Every game keeps its rules in a plain class with no console attached, next to the form that draws it. Each game folder splits into `Rules/` for what the game *is* and `Rendering/` for how it *looks*, plus `Bot/` and `Diagnostics/` for chess, the only one big enough to need them. Blackjack and Poker share a `Cards/` folder split the same way, so a third card game would need no new card code at all.

There are **no package references here and there should not be any**: everything is the .NET base class library plus WolfCurses, which is the whole claim these examples exist to back up.

## Artwork

Both sets are public domain, and credited anyway because where art came from is worth being able to look up.

- **Chess pieces** (`Assets/chess/`): [samboy/ChessGraphics](https://github.com/samboy/ChessGraphics), Unlicense / BSD-0.
- **Playing cards** (`Assets/cards/`): [saulspatz/SVGCards](https://github.com/saulspatz/SVGCards), the Vertical2 deck. Picture cards originate in Byron Knoll's public-domain set and the backs in openclipart.org.

Both are decoded by the library's own PNG decoder like every other image here.

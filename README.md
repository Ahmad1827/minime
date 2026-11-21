# ForYusef 🏰

A small collection of games for a baby (Yusef, 1.5 years old) to play on the PC or a tablet with a parent, a few minutes at a time.
The keyboard and mouse are safe to bang on: inside a game, nothing the baby presses can leave the game or break anything.

## How it works
1. Open **ForYusef**. A menu shows one big card per game.
2. Click a card. The game opens full-screen and **locks** the keyboard.
3. When playtime is over, the parent types the exit code (below) and you're back on the menu.
4. Close the app with the **×** in the menu's top-right corner.

## Download
Get `ForYusef.exe` from the [latest release](https://github.com/Ahmad1827/minime/releases/latest) and double-click it. It runs on Windows 10/11 and needs nothing extra installed.
Windows SmartScreen may warn you because the app isn't signed. Click **More info → Run anyway**.

## Parent keys (inside a game)
- **Back to the menu:** hold `Ctrl` + `Alt` and type `Q` `U` `I` `T` (one letter after another, without letting go of Ctrl + Alt).
- **Next scene:** `Ctrl` + `Alt` + `N`.

## Games
### 1. Little Knight ♞
A medieval / chess "baby smash" game.

#### Scenes
Four hand-painted scenes, each with its own board and little moving things:
- **Castle at Dusk**: twilight sky, setting sun, lake, glowing castle windows, a walnut and gold board, and fireflies.
- **Sunny Meadow**: rainbow, hot-air balloons, a windmill village, flowers, a honey-oak and leaf-green board, and butterflies.
- **Snowy Night**: northern lights, full moon, snowy mountains, a frozen lake, a snowman, a frosted ice board, and falling snow.
- **Seaside Castle**: a castle on a cliff, sailing ships, a lighthouse, a beach with a sandcastle, a driftwood and sea-glass board, seagulls, and sparkles on the water.

Little Knight starts on a random scene and moves to the next one every 4 minutes. The new scene opens from the middle in a ring of sparkles.
**Parents can switch right away:** `Ctrl` + `Alt` + `N` on Windows, or on Android hold one finger on each **bottom** corner for 1.5 seconds.

#### What the baby sees
A painted medieval scene with a castle and a chessboard in the middle (see **Scenes** above). The toys are drawn in a flat, toy-like style.
- **Letters A-Z**: a wooden-style alphabet block next to a picture: A Apple, B Bishop, C Castle, D Drum, K King, O Owl, Q Queen...
- **Numbers 0-9**: a number block, then pawns march in one by one with a note for each pawn, so the music counts along.
- **Other keys**: chess pieces and treasures (crown, shield, gem, star, sword...).
- **Mouse click on the board**: puts a chess piece on that square and lights up the squares it can move to.
- **Mouse move**: a trail of colorful dots.
- **Sound**: soft plucked harp notes from a scale that always sounds nice, with a little tune for each chess piece. No voice.

### 2. Little Pilot ✈️
A cut-paper sky: everything looks like layered paper cut-outs with soft shadows and a paper grain (a different look from Little Knight).
A little propeller plane with a pilot in goggles flies by itself over hills, trees and little houses. Everything the baby does puts something in the sky for it to fly into. Nothing can be missed or lost.
- **Letters A-Z**: a balloon with that letter. The plane pops it and the letter jumps out big.
- **Numbers 0-9**: that many balloons, numbered 1, 2, 3..., popping in rising notes so the music counts along (0 makes ten).
- **Space, Enter, mouse wheel**: the plane does a loop-the-loop.
- **Other keys**: gold stars (a little chime), rainbow hoops (fly through for a rainbow trail), bunches of balloons, loops.
- **Mouse**: the plane follows it. A click puts a balloon right there, a right click puts a star.
- **Android**: touch anywhere for a balloon. The plane follows your finger.
- Three skies, **Sunny Morning**, **Pink Sunset** and **Starry Night**. They change every 4 minutes with a page-turn, or right away with `Ctrl` + `Alt` + `N` (Android: both bottom corners).

## What is blocked (inside a game)
Every key, including the Windows key, Alt+Tab, Alt+F4, Ctrl+Esc, Ctrl+Shift+Esc and the Sticky Keys popup.
Every monitor gets its own full copy of the game, showing the same thing on each, as if it were the only screen. The windows stay on top and keep the screen from sleeping.

**Windows does not let any app block these:** `Ctrl+Alt+Del`, `Win+L` (lock screen) and the power button.
They can't break anything. They only take you to the Windows lock/security screen.

The menu itself is not locked. It's the grown-up's screen.

## Adding a game
Windows: make a folder `windows/Games/<Name>/` with a class that implements `IGame` (see `windows/Game.cs`) and add one line to `Catalog.All`.
Android: make a package `com.foryusef.<name>` with a class that implements `Game` and add it to `Catalog.ALL`.
The menu card (a live picture of the game), the input lock, every monitor, the sound and the parent exit all come from the shell.

## Project layout
- `windows/`: the Windows shell. `Program.cs` (app, menu ↔ game), `Menu.cs`, `Session.cs` (input lock + one window per monitor), `Harp.cs` (sound), `Game.cs` (game interface + list of games).
- `windows/Games/LittleKnight/`, `windows/Games/LittlePilot/`: the games.
- `android/src/com/foryusef/`: the Android shell (menu, pinned game screen, sound). `android/src/com/foryusef/littleknight/`, `.../littlepilot/`: the games.

## Build
Double-click `build.bat` on Windows, or run `./build.sh` from WSL. It uses the C# compiler that comes with Windows, so nothing extra needs to be installed.
Previews without locking anything: `ForYusef.exe --menu menu.png`, `--pilot prefix` for Little Pilot, and for Little Knight `--snapshot`, `--gallery`, `--mirror`, `--themes`.

## Android
Install `ForYusef.apk` on the phone or tablet (allow "install unknown apps" when asked). Android 7.0 or newer.

The app opens on the same menu. Tap a card to start a game. In Little Knight:
- **Touch the board** to put a chess piece there and light up its moves. **Touch anywhere else** for a treasure. **Drag** for a trail of colorful dots. Lots of fingers at once works too.
- A hardware keyboard (tablet keyboard, Chromebook) gets letters, numbers and pieces just like on Windows.
- **Back to the menu: hold one finger on the top-left corner and one on the top-right corner for 3 seconds.** Two golden rings fill up while you hold.
- **Next scene:** hold one finger on each bottom corner for 1.5 seconds.

**Locking:** the app uses Android's *screen pinning*, so Home, Recents and the notification shade do nothing while the baby plays.
Turn it on once in **Settings → Security → App pinning** (the exact place varies by phone; search Settings for "pin"). Android asks "Pin this app?" each time a game starts. Tap **Got it** / **Pin**.
The menu is not pinned. Without pinning a game still works, but Home can leave it.
If you ever need the system way out, Android's own unpin gesture still works: hold Back + Recents, or swipe up and hold with gesture navigation.

Build it with `./build-android.sh` (JDK 17 + Android SDK with build-tools 35 and platform 35, no Gradle). It signs with `~/.android/debug.keystore`. Keep that file, because Android only installs an update when it's signed with the same key.

ForYusef is a new Android app (`com.foryusef`). The old **Little Knight** app is separate: uninstall it once ForYusef is installed.

# Little Knight 🏰♞

A medieval / chess "baby smash" app for Windows and Android. The baby can bang on the keyboard and mouse without breaking anything.

## Download
Get `LittleKnight.exe` from the [latest release](https://github.com/Ahmad1827/minime/releases/latest) and double-click it. It runs on Windows 10/11 and needs nothing extra installed.
Windows SmartScreen may warn you because the app isn't signed. Click **More info → Run anyway**.

## Parent exit
**Hold `Ctrl` + `Alt` and type `Q` `U` `I` `T`** (one letter after another, without letting go of Ctrl + Alt).

## Scenes
Four hand-painted scenes, each with its own board and little moving things:
- **Castle at Dusk**: twilight sky, setting sun, lake, glowing castle windows, a walnut and gold board, and fireflies.
- **Sunny Meadow**: rainbow, hot-air balloons, a windmill village, flowers, a honey-oak and leaf-green board, and butterflies.
- **Snowy Night**: northern lights, full moon, snowy mountains, a frozen lake, a snowman, a frosted ice board, and falling snow.
- **Seaside Castle**: a castle on a cliff, sailing ships, a lighthouse, a beach with a sandcastle, a driftwood and sea-glass board, seagulls, and sparkles on the water.

The game starts on a random scene and moves to the next one every 4 minutes. The new scene opens from the middle in a ring of sparkles.
**Parents can switch right away:** `Ctrl` + `Alt` + `N` on Windows, or on Android hold one finger on each **bottom** corner for 1.5 seconds.

## What the baby sees
A painted medieval scene with a castle and a chessboard in the middle (see **Scenes** above). The toys are drawn in a flat, toy-like style.
- **Letters A-Z**: a wooden-style alphabet block next to a picture: A Apple, B Bishop, C Castle, D Drum, K King, O Owl, Q Queen...
- **Numbers 0-9**: a number block, then pawns march in one by one with a note for each pawn, so the music counts along.
- **Other keys**: chess pieces and treasures (crown, shield, gem, star, sword...).
- **Mouse click on the board**: puts a chess piece on that square and lights up the squares it can move to.
- **Mouse move**: a trail of colorful dots.
- **Sound**: soft plucked harp notes from a scale that always sounds nice, with a little tune for each chess piece. No voice.

## What is blocked
Every key, including the Windows key, Alt+Tab, Alt+F4, Ctrl+Esc, Ctrl+Shift+Esc and the Sticky Keys popup.
Every monitor gets its own full copy of the game, showing the same thing on each, as if it were the only screen. The windows stay on top and keep the screen from sleeping.

**Windows does not let any app block these:** `Ctrl+Alt+Del`, `Win+L` (lock screen) and the power button.
They can't break anything. They only take you to the Windows lock/security screen.

## Build
Double-click `build.bat` on Windows, or run `./build.sh` from WSL. It uses the C# compiler that comes with Windows, so nothing extra needs to be installed.


## Android
Install `LittleKnight.apk` on the phone or tablet (allow "install unknown apps" when asked). Android 7.0 or newer.

- **Touch the board** to put a chess piece there and light up its moves. **Touch anywhere else** for a treasure. **Drag** for a trail of colorful dots. Lots of fingers at once works too.
- A hardware keyboard (tablet keyboard, Chromebook) gets letters, numbers and pieces just like on Windows.
- **Parent exit: hold one finger on the top-left corner and one on the top-right corner for 3 seconds.** Two golden rings fill up while you hold.

**Locking:** the app uses Android's *screen pinning*, so Home, Recents and the notification shade do nothing while the baby plays.
Turn it on once in **Settings → Security → App pinning** (the exact place varies by phone; search Settings for "pin"). Android asks "Pin this app?" each time the game starts. Tap **Got it** / **Pin**.
Without pinning the game still works, but Home can leave it.
If you ever need the system way out, Android's own unpin gesture still works: hold Back + Recents, or swipe up and hold with gesture navigation.

Build it with `./build-android.sh` (JDK 17 + Android SDK with build-tools 35 and platform 35, no Gradle). It signs with `~/.android/debug.keystore`. Keep that file, because Android only installs an update when it's signed with the same key.

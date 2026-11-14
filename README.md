# Little Knight 🏰♞

A medieval / chess "baby smash" app for Windows. The baby can bang on the keyboard and mouse without breaking anything.

## Download
Get `LittleKnight.exe` from the [latest release](https://github.com/Ahmad1827/minime/releases/latest) and double-click it. It runs on Windows 10/11 and needs nothing extra installed.
Windows SmartScreen may warn you because the app isn't signed. Click **More info → Run anyway**.

## Parent exit
**Hold `Ctrl` + `Alt` and type `Q` `U` `I` `T`** (one letter after another, without letting go of Ctrl + Alt).

## What the baby sees
A sunny meadow with a little castle and a friendly green chessboard. Everything is drawn in a flat, toy-like style.
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

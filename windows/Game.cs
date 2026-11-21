// What a ForYusef game looks like to the shell, and the list of games shown on the menu.
//
// ADDING A GAME
//   1. Make a folder windows/Games/<Name>/ with a class that implements IGame.
//   2. Add one GameInfo line to Catalog.All below.
//   That's it: the menu card, the input lock, every monitor, the sound and the parent exit come for free.

using System;
using System.Collections.Generic;
using System.Drawing;

namespace ForYusef
{
    // One running copy of a game on one screen. With several monitors the shell makes one copy per
    // screen, all built with the same seed and fed the same events, so they all show the same thing.
    interface IGame
    {
        int W { get; }
        int H { get; }

        // Sound: (note 0..11 of a pentatonic scale, delay in seconds, volume 0..1). Only the
        // primary screen's copy gets a real harp, the others get a silent one.
        Action<int, float, float> Play { set; }

        // Parent instructions to show for the first few seconds (exit, next scene...).
        string StartHint { set; }

        void Start();                  // the game just opened: a little welcome tune, for example
        void Update(float dt);         // seconds since the last frame (at most 0.05)
        void Draw(Graphics g);         // whole screen, every frame

        void OnKey(int vk);            // any key the baby presses (auto-repeat already filtered out)
        void OnMouseMove(int x, int y);
        void OnWheel(int x, int y);

        // A click is decided once, on the screen that was clicked (ResolveClick), and then given to
        // every copy (Click), with x/y converted to that copy's screen size.
        object ResolveClick(int x, int y, bool right);
        void Click(object resolved, int x, int y);

        void ParentNext();             // Ctrl + Alt + N: next scene / level / whatever fits the game
    }

    class GameInfo
    {
        public string Title, Tagline;
        public Color Accent;                         // card ribbon color on the menu
        public Func<int, int, int, IGame> Create;    // (width, height, seed)
        public Action<IGame> Demo = delegate { };    // puts a fresh game into a nice-looking state for its menu card
        public string ParentNextHint = "";           // what Ctrl + Alt + N does in this game ("" = nothing)
    }

    static class Catalog
    {
        public static readonly List<GameInfo> All = new List<GameInfo>
        {
            LittleKnight.LittleKnightGame.Info,
            LittlePilot.PilotGame.Info,
            // next game goes here
        };
    }
}

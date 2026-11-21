// Connects Little Knight's Scene to the ForYusef shell (see windows/Game.cs).

using System;
using System.Drawing;

namespace ForYusef.LittleKnight
{
    class LittleKnightGame : IGame
    {
        public static readonly GameInfo Info = new GameInfo
        {
            Title = "Little Knight",
            Tagline = "Letters, numbers and chess pieces",
            Accent = Color.FromArgb(232, 170, 60),
            Create = (w, h, seed) => new LittleKnightGame(w, h, seed),
            Demo = g => ((LittleKnightGame)g).DemoScene(),
            ParentNextHint = "Ctrl + Alt + N shows the next scene",
        };

        // every copy on every screen starts on the same scene: picked from the shared seed
        readonly Scene scene;

        LittleKnightGame(int w, int h, int seed)
        {
            scene = new Scene(w, h, seed, Math.Abs(seed) % Scene.ThemeNames.Length);
        }

        public int W { get { return scene.W; } }
        public int H { get { return scene.H; } }
        public Action<int, float, float> Play { set { scene.Play = value; } }
        public string StartHint { set { scene.StartHint = value; } }

        public void Start() { scene.Welcome(); }
        public void Update(float dt) { scene.Update(dt); }
        public void Draw(Graphics g) { scene.Draw(g); }
        public void OnKey(int vk) { scene.OnKey(vk); }
        public void OnMouseMove(int x, int y) { scene.OnMouseMove(x, y); }
        public void OnWheel(int x, int y) { scene.OnWheel(x, y); }
        public void ParentNext() { scene.NextTheme(); }

        class BoardClick { public int Col, Row; public bool White; }

        public object ResolveClick(int x, int y, bool right)
        {
            int col, row;
            if (scene.BoardCell(x, y, out col, out row)) return new BoardClick { Col = col, Row = row, White = !right };
            return null; // off the board: a treasure where the mouse is
        }

        public void Click(object resolved, int x, int y)
        {
            var b = resolved as BoardClick;
            if (b != null) scene.ClickSquare(b.Col, b.Row, b.White);
            else scene.ClickOutside(x, y);
        }

        // menu card: a few things already on the board
        void DemoScene()
        {
            scene.OnKey('K'); scene.Update(0.4f);
            scene.OnKey('3'); scene.Update(0.3f);
            int col = 3, row = 4;
            scene.ClickSquare(col, row, true);
            for (int i = 0; i < 12; i++) scene.Update(0.12f);
        }
    }
}

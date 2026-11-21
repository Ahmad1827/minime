// The ForYusef menu: a big friendly page with one card per game. A parent clicks a card and the game
// starts, locked. Typing the exit code in a game comes back here. The x in the corner closes the app.
// The menu itself does not lock anything: it is the grown-up's screen.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ForYusef
{
    class MenuForm : Form
    {
        readonly MenuView view;
        readonly Timer timer = new Timer();
        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        double last;

        public MenuForm(Action<GameInfo> launch, Action quit, Action<int, float, float> play)
        {
            Text = "ForYusef";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.Bounds;
            BackColor = Color.FromArgb(150, 210, 240);
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            view = new MenuView(Math.Max(1, Bounds.Width), Math.Max(1, Bounds.Height), launch, quit, play);

            timer.Interval = 16;
            timer.Tick += delegate
            {
                double now = clock.Elapsed.TotalSeconds;
                view.Update((float)Math.Min(0.05, now - last)); last = now;
                Invalidate();
            };
            last = clock.Elapsed.TotalSeconds;
            timer.Start();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) { last = clock.Elapsed.TotalSeconds; timer.Start(); view.Shown(); } else timer.Stop();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            try { view.Draw(e.Graphics); }
            catch { try { e.Graphics.ResetTransform(); e.Graphics.Clear(BackColor); } catch { } }
        }

        protected override void OnMouseMove(MouseEventArgs e) { view.MouseMove(e.X, e.Y); }
        protected override void OnMouseLeave(EventArgs e) { view.MouseMove(-1, -1); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) view.MouseDown(e.X, e.Y); }
        protected override void OnMouseUp(MouseEventArgs e) { if (e.Button == MouseButtons.Left) view.MouseUp(e.X, e.Y); }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Up: view.Move(-1); return true;
                case Keys.Right: case Keys.Down: case Keys.Tab: view.Move(1); return true;
                case Keys.Enter: case Keys.Space: view.Activate(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); view.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // Everything the menu draws and how it reacts, without the window (so it can also be saved as a picture).
    class MenuView : IDisposable
    {
        public readonly int W, H;
        readonly Action<GameInfo> launch;
        readonly Action quit;
        readonly Action<int, float, float> play;

        class Card { public GameInfo Info; public RectangleF Rect; public Bitmap Thumb; public float Hover; }
        readonly List<Card> cards = new List<Card>();
        RectangleF closeRect; float closeHover;
        int selected = -1, pressed = -1;
        bool pressedClose;
        float time;
        Bitmap background;

        static readonly FontFamily TitleFont = Find("Segoe UI Black", "Arial Black", "Segoe UI", "Arial");
        static readonly FontFamily UiFont = Find("Segoe UI", "Arial");
        static readonly Color Ink = Color.FromArgb(52, 54, 92);
        static readonly Color[] TitleColors =
        {
            Color.FromArgb(240, 98, 98), Color.FromArgb(247, 160, 60), Color.FromArgb(242, 200, 40),
            Color.FromArgb(96, 190, 100), Color.FromArgb(70, 160, 230), Color.FromArgb(150, 110, 220),
        };
        const int MinCards = 3; // empty "more games soon" cards fill the row until there are this many games

        public MenuView(int w, int h, Action<GameInfo> launch, Action quit, Action<int, float, float> play)
        {
            W = w; H = h; this.launch = launch; this.quit = quit; this.play = play ?? delegate { };
            Layout();
            background = BuildBackground();
        }

        static FontFamily Find(params string[] names)
        {
            foreach (var n in names)
                foreach (var f in FontFamily.Families)
                    if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) return f;
            return FontFamily.GenericSansSerif;
        }

        // ---------------------------------------------------------- layout
        void Layout()
        {
            int n = Math.Max(MinCards, Catalog.All.Count);
            int cols = Math.Min(n, 4), rows = (n + cols - 1) / cols;
            float gap = Math.Min(W, H) * 0.04f;
            float top = H * 0.30f, bottom = H * 0.88f;
            // card = 16:10 picture + a caption strip
            float cw = (W * 0.86f - gap * (cols - 1)) / cols;
            float ch = cw * (10f / 16f) + cw * 0.24f;
            float maxH = (bottom - top - gap * (rows - 1)) / rows;
            if (ch > maxH) { cw *= maxH / ch; ch = maxH; }
            float x0 = (W - (cols * cw + (cols - 1) * gap)) / 2f;
            float y0 = top + (bottom - top - (rows * ch + (rows - 1) * gap)) / 2f;
            for (int i = 0; i < n; i++)
            {
                int c = i % cols, r = i / cols;
                int inRow = Math.Min(cols, n - r * cols); // center a last row that isn't full
                float rowX = x0 + (cols - inRow) * (cw + gap) / 2f;
                var card = new Card { Info = i < Catalog.All.Count ? Catalog.All[i] : null, Rect = new RectangleF(rowX + c * (cw + gap), y0 + r * (ch + gap), cw, ch) };
                if (card.Info != null) card.Thumb = Thumbnail(card.Info, (int)cw, (int)(cw * 10f / 16f));
                cards.Add(card);
            }
            float cs = Math.Max(36, H * 0.055f);
            closeRect = new RectangleF(W - cs - H * 0.03f, H * 0.03f, cs, cs);
        }

        // a real, live-rendered frame of the game, so the card shows exactly what the baby will see
        static Bitmap Thumbnail(GameInfo info, int w, int h)
        {
            try
            {
                var game = info.Create(1440, 900, 7);
                game.Play = delegate { };
                game.StartHint = "";
                info.Demo(game);
                using (var big = new Bitmap(game.W, game.H))
                {
                    using (var g = Graphics.FromImage(big)) game.Draw(g);
                    var small = new Bitmap(Math.Max(1, w), Math.Max(1, h));
                    using (var g = Graphics.FromImage(small))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(big, new Rectangle(0, 0, small.Width, small.Height));
                    }
                    return small;
                }
            }
            catch { return null; }
        }

        Bitmap BuildBackground()
        {
            var bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var sky = new LinearGradientBrush(new Rectangle(0, 0, W, H), Color.FromArgb(132, 204, 244), Color.FromArgb(255, 226, 196), 90f))
                {
                    var blend = new ColorBlend(3);
                    blend.Colors = new[] { Color.FromArgb(120, 196, 242), Color.FromArgb(196, 228, 246), Color.FromArgb(255, 228, 196) };
                    blend.Positions = new[] { 0f, 0.55f, 1f };
                    sky.InterpolationColors = blend;
                    g.FillRectangle(sky, 0, 0, W, H);
                }
                // soft sun, top left
                float sr = H * 0.32f, sx = W * 0.12f, sy = H * 0.12f;
                using (var p = new GraphicsPath())
                {
                    p.AddEllipse(sx - sr, sy - sr, sr * 2, sr * 2);
                    using (var b = new PathGradientBrush(p) { CenterColor = Color.FromArgb(150, 255, 245, 200), SurroundColors = new[] { Color.FromArgb(0, 255, 245, 200) } })
                        g.FillPath(b, p);
                }
                using (var b = new SolidBrush(Color.FromArgb(255, 250, 222))) g.FillEllipse(b, sx - H * 0.06f, sy - H * 0.06f, H * 0.12f, H * 0.12f);
                // rolling hills along the bottom
                Hill(g, H * 0.86f, H * 0.07f, 0.9f, 0.4f, Color.FromArgb(150, 210, 130));
                Hill(g, H * 0.91f, H * 0.06f, 1.4f, 2.1f, Color.FromArgb(112, 186, 104));
            }
            return bmp;
        }

        void Hill(Graphics g, float baseY, float amp, float freq, float phase, Color c)
        {
            var pts = new List<PointF>();
            for (int i = 0; i <= 40; i++)
            {
                float x = W * i / 40f;
                pts.Add(new PointF(x, baseY - amp * (float)(0.5 + 0.5 * Math.Sin(i / 40.0 * Math.PI * 2 * freq + phase))));
            }
            pts.Add(new PointF(W, H)); pts.Add(new PointF(0, H));
            using (var b = new SolidBrush(c)) g.FillPolygon(b, pts.ToArray());
        }

        // ---------------------------------------------------------- input
        int CardAt(float x, float y)
        {
            for (int i = 0; i < cards.Count; i++) if (cards[i].Info != null && cards[i].Rect.Contains(x, y)) return i;
            return -1;
        }

        public void MouseMove(int x, int y)
        {
            int i = CardAt(x, y);
            if (i >= 0 && i != selected) play(i % 12, 0, 0.18f);
            if (i >= 0 || x < 0) selected = i;
        }

        public void MouseDown(int x, int y)
        {
            pressed = CardAt(x, y);
            pressedClose = closeRect.Contains(x, y);
        }

        public void MouseUp(int x, int y)
        {
            if (pressedClose && closeRect.Contains(x, y)) { pressedClose = false; quit(); return; }
            int i = CardAt(x, y);
            if (i >= 0 && i == pressed) { selected = i; Activate(); }
            pressed = -1; pressedClose = false;
        }

        public void Move(int step)
        {
            int n = Catalog.All.Count;
            if (n == 0) return;
            selected = selected < 0 ? 0 : ((selected + step) % n + n) % n;
            play(selected % 12, 0, 0.18f);
        }

        public void Activate()
        {
            if (selected < 0 || selected >= cards.Count || cards[selected].Info == null) return;
            launch(cards[selected].Info);
        }

        public void Shown() { pressed = -1; pressedClose = false; }

        public void Update(float dt)
        {
            time += dt;
            for (int i = 0; i < cards.Count; i++)
                cards[i].Hover = Approach(cards[i].Hover, i == selected ? 1f : 0f, dt * 7f);
            closeHover = Approach(closeHover, closeRect.Contains(Cursor.Position.X - Screen.PrimaryScreen.Bounds.X, Cursor.Position.Y - Screen.PrimaryScreen.Bounds.Y) ? 1f : 0f, dt * 8f);
        }

        static float Approach(float v, float target, float step)
        {
            return v < target ? Math.Min(target, v + step) : Math.Max(target, v - step);
        }

        // ---------------------------------------------------------- drawing
        public void Draw(Graphics g)
        {
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.DrawImageUnscaled(background, 0, 0);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            DrawClouds(g);
            DrawTitle(g);
            for (int i = 0; i < cards.Count; i++) DrawCard(g, cards[i], i);
            DrawClose(g);
            DrawFooter(g);
        }

        void DrawClouds(Graphics g)
        {
            using (var b = new SolidBrush(Color.FromArgb(215, 255, 255, 255)))
                for (int i = 0; i < 5; i++)
                {
                    float s = H * (0.05f + 0.015f * (i % 3));
                    float span = W + s * 8;
                    float x = (float)((i * 0.23 * span + time * H * (0.008 + 0.003 * i)) % span) - s * 4;
                    float y = H * (0.06f + 0.05f * ((i * 7) % 4));
                    g.FillEllipse(b, x, y, s * 2.4f, s);
                    g.FillEllipse(b, x + s * 0.5f, y - s * 0.45f, s * 1.3f, s * 1.1f);
                    g.FillEllipse(b, x + s * 1.2f, y - s * 0.25f, s * 1.0f, s * 0.9f);
                }
        }

        // "For Yusef": every letter its own color, gently bobbing
        void DrawTitle(Graphics g)
        {
            const string title = "For Yusef";
            float em = H * 0.12f;
            var sf = StringFormat.GenericTypographic;
            float[] widths = new float[title.Length];
            float total = 0;
            using (var f = new Font(TitleFont, em, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                for (int i = 0; i < title.Length; i++)
                {
                    widths[i] = title[i] == ' ' ? em * 0.32f : g.MeasureString(title[i].ToString(), f, 1000, sf).Width;
                    total += widths[i];
                }
            }
            float maxW = W * 0.8f;
            if (total > maxW) { em *= maxW / total; for (int i = 0; i < widths.Length; i++) widths[i] *= maxW / total; total = maxW; }
            float x = (W - total) / 2f, baseY = H * 0.07f;
            int colorIndex = 0;
            for (int i = 0; i < title.Length; i++)
            {
                if (title[i] != ' ')
                {
                    float bob = (float)Math.Sin(time * 2.2 + i * 0.7) * H * 0.008f;
                    using (var p = new GraphicsPath())
                    {
                        p.AddString(title[i].ToString(), TitleFont, (int)FontStyle.Regular, em, new PointF(x, baseY + bob), sf);
                        using (var pen = new Pen(Color.White, em * 0.14f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, p);
                        using (var b = new SolidBrush(TitleColors[colorIndex % TitleColors.Length])) g.FillPath(b, p);
                    }
                    colorIndex++;
                }
                x += widths[i];
            }
            using (var f = new Font(UiFont, H * 0.03f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(Color.FromArgb(200, Ink)))
            using (var c = new StringFormat { Alignment = StringAlignment.Center })
                g.DrawString("Pick a game to play together", f, b, new RectangleF(0, baseY + em * 1.25f, W, H * 0.06f), c);
        }

        void DrawCard(Graphics g, Card card, int index)
        {
            var r = card.Rect;
            float lift = card.Hover * r.Height * 0.035f;
            float grow = 1 + card.Hover * 0.025f;
            var st = g.Save();
            g.TranslateTransform(r.X + r.Width / 2, r.Y + r.Height / 2 - lift);
            g.ScaleTransform(grow, grow);
            var rect = new RectangleF(-r.Width / 2, -r.Height / 2, r.Width, r.Height);
            float rad = r.Width * 0.06f;

            if (card.Info == null) { DrawSoonCard(g, rect, rad); g.Restore(st); return; }

            using (var sp = RRect(rect.X + r.Width * 0.01f, rect.Y + r.Width * 0.02f + lift * 0.6f, rect.Width, rect.Height, rad))
            using (var sb = new SolidBrush(Color.FromArgb(40 + (int)(30 * card.Hover), 30, 40, 80)))
                g.FillPath(sb, sp);
            using (var path = RRect(rect.X, rect.Y, rect.Width, rect.Height, rad))
            {
                using (var b = new SolidBrush(Color.White)) g.FillPath(b, path);
                float pad = r.Width * 0.03f;
                var pic = new RectangleF(rect.X + pad, rect.Y + pad, rect.Width - pad * 2, (rect.Width - pad * 2) * 10f / 16f);
                using (var clip = RRect(pic.X, pic.Y, pic.Width, pic.Height, rad * 0.7f))
                {
                    if (card.Thumb != null)
                    {
                        var s2 = g.Save();
                        g.SetClip(clip, CombineMode.Intersect);
                        g.DrawImage(card.Thumb, pic);
                        g.Restore(s2);
                    }
                    else using (var b = new SolidBrush(card.Info.Accent)) g.FillPath(b, clip);
                }

                // caption: title + tagline, with a colored dot that matches the game
                float capY = pic.Bottom, capH = rect.Bottom - capY;
                float dot = capH * 0.32f;
                using (var b = new SolidBrush(card.Info.Accent)) g.FillEllipse(b, rect.X + pad * 1.6f, capY + capH / 2 - dot / 2, dot, dot);
                float tx = rect.X + pad * 1.6f + dot * 1.45f, tw = rect.Right - pad - tx;
                using (var f = new Font(UiFont, capH * 0.30f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var b = new SolidBrush(Ink))
                    g.DrawString(card.Info.Title, f, b, new RectangleF(tx, capY + capH * 0.12f, tw, capH * 0.45f));
                using (var f = new Font(UiFont, capH * 0.19f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var b = new SolidBrush(Color.FromArgb(170, Ink)))
                    g.DrawString(card.Info.Tagline, f, b, new RectangleF(tx, capY + capH * 0.53f, tw, capH * 0.4f));

                if (card.Hover > 0.01f)
                    using (var pen = new Pen(Color.FromArgb((int)(255 * card.Hover), card.Info.Accent), r.Width * 0.012f))
                        g.DrawPath(pen, path);
            }

            // a round "play" button on the picture when hovered
            if (card.Hover > 0.01f)
            {
                float pr = r.Width * 0.09f * (0.6f + 0.4f * card.Hover);
                float cx = 0, cy = rect.Y + r.Width * 0.03f + (r.Width * 0.94f) * 10f / 32f;
                using (var b = new SolidBrush(Color.FromArgb((int)(235 * card.Hover), 255, 255, 255))) g.FillEllipse(b, cx - pr, cy - pr, pr * 2, pr * 2);
                using (var b = new SolidBrush(Color.FromArgb((int)(255 * card.Hover), card.Info.Accent)))
                    g.FillPolygon(b, new[] { new PointF(cx - pr * 0.32f, cy - pr * 0.45f), new PointF(cx - pr * 0.32f, cy + pr * 0.45f), new PointF(cx + pr * 0.48f, cy) });
            }
            g.Restore(st);
        }

        void DrawSoonCard(Graphics g, RectangleF rect, float rad)
        {
            using (var path = RRect(rect.X, rect.Y, rect.Width, rect.Height, rad))
            {
                using (var b = new SolidBrush(Color.FromArgb(110, 255, 255, 255))) g.FillPath(b, path);
                using (var pen = new Pen(Color.FromArgb(90, Ink), rect.Width * 0.008f) { DashStyle = DashStyle.Dash }) g.DrawPath(pen, path);
            }
            float pr = rect.Width * 0.1f, cy = rect.Y + rect.Height * 0.42f;
            using (var pen = new Pen(Color.FromArgb(110, Ink), pr * 0.14f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawEllipse(pen, -pr, cy - pr, pr * 2, pr * 2);
                g.DrawLine(pen, -pr * 0.45f, cy, pr * 0.45f, cy);
                g.DrawLine(pen, 0, cy - pr * 0.45f, 0, cy + pr * 0.45f);
            }
            using (var f = new Font(UiFont, rect.Height * 0.075f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(Color.FromArgb(150, Ink)))
            using (var c = new StringFormat { Alignment = StringAlignment.Center })
                g.DrawString("More games soon", f, b, new RectangleF(rect.X, cy + pr * 1.4f, rect.Width, rect.Height * 0.15f), c);
        }

        void DrawClose(Graphics g)
        {
            var r = closeRect;
            using (var b = new SolidBrush(Color.FromArgb(150 + (int)(105 * closeHover), 255, 255, 255))) g.FillEllipse(b, r);
            float k = r.Width * 0.22f, cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            using (var pen = new Pen(Color.FromArgb(200, Ink), r.Width * 0.09f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pen, cx - k, cy - k, cx + k, cy + k);
                g.DrawLine(pen, cx - k, cy + k, cx + k, cy - k);
            }
        }

        void DrawFooter(Graphics g)
        {
            string text = "Click a game to start   ·   In a game, hold Ctrl + Alt and type Q U I T to come back here";
            using (var f = new Font(UiFont, Math.Max(12, H * 0.022f), FontStyle.Bold, GraphicsUnit.Pixel))
            {
                SizeF sz = g.MeasureString(text, f);
                float h = sz.Height + H * 0.012f;
                var rect = new RectangleF((W - sz.Width) / 2 - h * 0.6f, H * 0.95f - h / 2, sz.Width + h * 1.2f, h);
                using (var path = RRect(rect.X, rect.Y, rect.Width, rect.Height, h / 2))
                using (var b = new SolidBrush(Color.FromArgb(215, 255, 255, 255)))
                    g.FillPath(b, path);
                using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var b = new SolidBrush(Ink))
                    g.DrawString(text, f, b, rect, c);
            }
        }

        static GraphicsPath RRect(float x, float y, float w, float h, float r)
        {
            r = Math.Max(0.5f, Math.Min(r, Math.Min(w, h) / 2));
            var p = new GraphicsPath();
            p.AddArc(x, y, 2 * r, 2 * r, 180, 90);
            p.AddArc(x + w - 2 * r, y, 2 * r, 2 * r, 270, 90);
            p.AddArc(x + w - 2 * r, y + h - 2 * r, 2 * r, 2 * r, 0, 90);
            p.AddArc(x, y + h - 2 * r, 2 * r, 2 * r, 90, 90);
            p.CloseFigure();
            return p;
        }

        public void Dispose()
        {
            if (background != null) { background.Dispose(); background = null; }
            foreach (var c in cards) if (c.Thumb != null) { c.Thumb.Dispose(); c.Thumb = null; }
        }

        // offline preview: the menu with the first card hovered
        public static void Snapshot(string file, int w, int h)
        {
            using (var v = new MenuView(w, h, delegate { }, delegate { }, null))
            {
                v.selected = 0;
                for (int i = 0; i < 20; i++) v.Update(0.05f);
                using (var bmp = new Bitmap(w, h))
                using (var g = Graphics.FromImage(bmp)) { v.Draw(g); bmp.Save(file, ImageFormat.Png); }
            }
        }
    }
}

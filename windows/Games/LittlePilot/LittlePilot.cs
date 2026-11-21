// Little Pilot: the second ForYusef game. A cut-paper sky where a little propeller plane flies by itself,
// and everything the baby does puts something in the sky for it to fly into:
//   Letters A-Z   a balloon with that letter (it pops into a big letter)
//   Numbers 0-9   that many balloons, numbered 1, 2, 3... that pop in rising notes, so the music counts along
//   Space / Enter / mouse wheel   the plane does a loop-the-loop
//   Other keys    stars, rainbow hoops (fly through for a rainbow trail), balloons, loops
//   Mouse         the plane follows it, and a click puts a balloon right there (right click: a star)
// Art style: layered paper cut-outs with soft shadows and a paper grain, very different from Little Knight.
// Written for C# 5 (the csc.exe bundled with .NET Framework 4.x).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace ForYusef.LittlePilot
{
    class Palette
    {
        public string Name; public int Sky; // 0 sun high, 1 sun low and big, 2 moon and stars
        public Color SkyTop, SkyBottom, Sun, SunRim, Far, Mid, Near, Front, Cloud, Trunk, TreeA, TreeB, House, Roof, Window, Flower;
    }

    class Thing
    {
        public const int Balloon = 0, Star = 1, Ring = 2;
        public int Kind, Note; public float X, Y, Born, Phase; public Color C; public string Label = "";
    }

    class Bit { public int Shape; public float X, Y, VX, VY, Rot, VR, Age, Life, Size, Grow; public Color C; } // 0 puff, 1 confetti, 2 sparkle, 3 ring wave
    class Popup { public string Text; public float X, Y, Born; public Color C; }
    class CloudSpot { public float X, Y, Depth; public int Kind; }

    class Scenery : IDisposable
    {
        public Palette P; public Bitmap Sky, Far, Mid, Near; public Bitmap[] Clouds = new Bitmap[3];
        public int TileW, FarY, MidY, NearY;
        public void Dispose()
        {
            foreach (var b in new[] { Sky, Far, Mid, Near }) if (b != null) b.Dispose();
            foreach (var b in Clouds) if (b != null) b.Dispose();
        }
    }

    class PilotGame : IGame
    {
        public static readonly GameInfo Info = new GameInfo
        {
            Title = "Little Pilot",
            Tagline = "Fly, pop balloons, catch stars",
            Accent = Color.FromArgb(84, 166, 228),
            Create = (w, h, seed) => new PilotGame(w, h, seed),
            Demo = g => ((PilotGame)g).DemoScene(),
            ParentNextHint = "Ctrl + Alt + N changes the sky",
        };

        static Color C(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
        static Color A(int a, Color c) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c); }

        public static readonly Palette[] Palettes =
        {
            new Palette { Name = "Sunny Morning", Sky = 0, SkyTop = C(0x9FD7F2), SkyBottom = C(0xFFF4DE), Sun = C(0xFFD25E), SunRim = C(0xFFE9A6),
                Far = C(0xA9C2E6), Mid = C(0x9BD6A4), Near = C(0x6CBF7C), Front = C(0x4FA86A), Cloud = C(0xFFFFFF),
                Trunk = C(0x9C6B4E), TreeA = C(0x4E9E62), TreeB = C(0x83CC78), House = C(0xF8E6C8), Roof = C(0xE8775F), Window = C(0x86BEE3), Flower = C(0xFFF1A8) },
            new Palette { Name = "Pink Sunset", Sky = 1, SkyTop = C(0x8C7CC6), SkyBottom = C(0xFFC28C), Sun = C(0xFF8D5C), SunRim = C(0xFFB98F),
                Far = C(0xB27DAE), Mid = C(0xE39A7C), Near = C(0xC57067), Front = C(0xA65A5A), Cloud = C(0xFFE0D8),
                Trunk = C(0x6E4A55), TreeA = C(0x8E4F63), TreeB = C(0xB8687A), House = C(0xFCE1C6), Roof = C(0x7B4B7A), Window = C(0xFFE08A), Flower = C(0xFFD3B0) },
            new Palette { Name = "Starry Night", Sky = 2, SkyTop = C(0x1B2356), SkyBottom = C(0x44528F), Sun = C(0xFFF2C4), SunRim = C(0xFFF8E0),
                Far = C(0x34407B), Mid = C(0x2D5C6E), Near = C(0x224A58), Front = C(0x1A3A46), Cloud = C(0x5F6DAA),
                Trunk = C(0x2A3440), TreeA = C(0x1E4A4A), TreeB = C(0x2E6661), House = C(0x4B5B8C), Roof = C(0x2C3466), Window = C(0xFFD86B), Flower = C(0x8FA0D8) },
        };

        static readonly Color[] BalloonColors = { C(0xEE6B5D), C(0xF5B53F), C(0x5CC39B), C(0x59A7E8), C(0xA588DD), C(0xF489B4) };
        static readonly Color Gold = C(0xF7C948), Ink = C(0x3A3A5E);
        const float LoopTime = 1.2f, WipeTime = 1.5f, ThemeSeconds = 240f;

        int W, H;
        public float Time;
        Action<int, float, float> play = delegate { };
        string startHint = "";
        readonly Random rnd;               // same seed on every screen: things appear in the same places
        readonly Random fx = new Random(); // cosmetic only

        readonly List<Thing> things = new List<Thing>();
        readonly List<Bit> bits = new List<Bit>();
        readonly List<Popup> popups = new List<Popup>();
        readonly List<CloudSpot> clouds = new List<CloudSpot>();
        Scenery scenery, oldScenery;
        int theme; float themeSince, wipeBorn = -100;
        string caption; float captionBorn = -100;

        float scroll, planeX, planeY, planeVY, prop, puffTimer, loopBorn = -100, rainbowUntil = -100;
        int rainbowHue;
        float mouseX = -1, mouseY = -1, mouseUntil = -100;
        Font balloonFont, popupFont, uiFont;
        readonly Dictionary<int, SolidBrush> brushes = new Dictionary<int, SolidBrush>();

        static readonly FontFamily Round = Find("Arial Rounded MT Bold", "Segoe UI Black", "Arial Black", "Arial");
        static readonly FontFamily Ui = Find("Segoe UI", "Arial");
        static Bitmap grain;

        static FontFamily Find(params string[] names)
        {
            foreach (var n in names)
                foreach (var f in FontFamily.Families)
                    if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) return f;
            return FontFamily.GenericSansSerif;
        }

        public PilotGame(int w, int h, int seed)
        {
            rnd = new Random(seed);
            theme = Math.Abs(seed) % Palettes.Length;
            for (int i = 0; i < 8; i++)
                clouds.Add(new CloudSpot { X = (float)rnd.NextDouble(), Y = 0.05f + 0.32f * (float)rnd.NextDouble(), Depth = i % 2 == 0 ? 0.18f : 0.5f, Kind = i % 3 });
            clouds.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            Resize(w, h);
            // something to fly into right away
            Spawn(Thing.Balloon, W * 0.62f, H * 0.36f, "", 0, 0, -1);
            Spawn(Thing.Star, W * 0.8f, H * 0.5f, "", 4, 0, -1);
            Spawn(Thing.Balloon, W * 0.97f, H * 0.3f, "", 2, 2, -1);
        }

        void Resize(int w, int h)
        {
            W = Math.Max(1, w); H = Math.Max(1, h);
            planeX = W * 0.3f; planeY = H * 0.4f;
            if (balloonFont != null) { balloonFont.Dispose(); popupFont.Dispose(); uiFont.Dispose(); }
            balloonFont = new Font(Round, 40f, FontStyle.Bold, GraphicsUnit.Pixel); // drawn in a unit space scaled to the balloon
            popupFont = new Font(Round, 100f, FontStyle.Bold, GraphicsUnit.Pixel);
            uiFont = new Font(Ui, Math.Max(12, H * 0.024f), FontStyle.Bold, GraphicsUnit.Pixel);
            if (scenery != null) scenery.Dispose();
            scenery = Build(Palettes[theme]);
        }

        // ================================================================== IGame
        int IGame.W { get { return W; } }
        int IGame.H { get { return H; } }
        public Action<int, float, float> Play { set { play = value ?? delegate { }; } }
        public string StartHint { set { startHint = value ?? ""; } }

        public void Start() { Tune(new[] { 0, 2, 4, 7, 9, 11 }, 0.11f, 0.45f); }

        public void OnKey(int vk)
        {
            if (vk >= 'A' && vk <= 'Z')
            {
                int i = vk - 'A';
                Spawn(Thing.Balloon, NextX(), RandomY(), ((char)vk).ToString(), i % BalloonColors.Length, i % 12, 0);
            }
            else if ((vk >= '0' && vk <= '9') || (vk >= 0x60 && vk <= 0x69)) Count(vk >= 0x60 ? vk - 0x60 : vk - '0');
            else if (vk == 0x20 || vk == 0x0D) Loop();
            else if (vk >= 0x25 && vk <= 0x28) Spawn(Thing.Star, NextX(), RandomY(), "", 0, 4 + rnd.Next(6), 0);
            else
            {
                int r = rnd.Next(10);
                if (r < 4) Spawn(Thing.Star, NextX(), RandomY(), "", 0, 4 + rnd.Next(6), 0);
                else if (r < 6) Spawn(Thing.Ring, NextX(), H * (0.25f + 0.3f * (float)rnd.NextDouble()), "", rnd.Next(BalloonColors.Length), 0, 0);
                else if (r < 8) { float x = NextX(), y = RandomY(); for (int k = 0; k < 3; k++) Spawn(Thing.Balloon, x + k * H * 0.09f, y + (k == 1 ? -H * 0.07f : 0), "", rnd.Next(BalloonColors.Length), 3 + k * 2, k * 0.12f); }
                else Loop();
            }
        }

        public void OnMouseMove(int x, int y)
        {
            if (Math.Abs(x - mouseX) + Math.Abs(y - mouseY) > 2) mouseUntil = Time + 2.5f;
            mouseX = x; mouseY = y;
        }

        public void OnWheel(int x, int y) { Loop(); }
        public object ResolveClick(int x, int y, bool right) { return right; }

        public void Click(object resolved, int x, int y)
        {
            float cy = Math.Max(H * 0.1f, Math.Min(H * 0.7f, y));
            if (resolved is bool && (bool)resolved) Spawn(Thing.Star, x, cy, "", 0, 4 + rnd.Next(6), 0);
            else Spawn(Thing.Balloon, x, cy, "", rnd.Next(BalloonColors.Length), rnd.Next(12), 0);
        }

        public void ParentNext() { SetTheme(theme + 1, true, false); }

        // ================================================================== spawning
        float RandomY() { return H * (0.16f + 0.46f * (float)rnd.NextDouble()); }

        // new things pop in on screen (so the baby sees them right away), after whatever is already waiting there
        float NextX()
        {
            float x = W * 0.78f;
            foreach (var t in things) if (t.X > x - H * 0.14f && t.X < W) x = t.X + H * 0.15f;
            return x < W - H * 0.08f ? x : W * 0.78f;
        }

        void Spawn(int kind, float x, float y, string label, int color, int note, float delay)
        {
            if (things.Count >= 20) things.RemoveAt(0);
            things.Add(new Thing
            {
                Kind = kind, X = x, Y = y, Label = label, C = BalloonColors[((color % 6) + 6) % 6], Note = Math.Max(0, Math.Min(11, note)),
                Born = Time + Math.Max(0, delay), Phase = (float)(rnd.NextDouble() * 6.28),
            });
            if (delay >= 0) play(Math.Min(11, note + 2), Math.Max(0, delay), 0.18f); // a soft "here I am" note
        }

        void Count(int n)
        {
            if (n == 0) n = 10;
            float x0 = Math.Max(W * 0.5f, planeX + H * 0.3f);
            for (int i = 0; i < n; i++)
                Spawn(Thing.Balloon, x0 + i * H * 0.15f, H * (0.38f + 0.1f * (float)Math.Sin(i * 0.9)), (i + 1).ToString(), i, i, i * 0.1f);
        }

        void Loop()
        {
            if (Time - loopBorn < LoopTime) return;
            loopBorn = Time;
            Tune(new[] { 4, 7, 9, 11, 9 }, 0.12f, 0.3f);
        }

        void Tune(int[] steps, float spacing, float gain) { for (int i = 0; i < steps.Length; i++) play(steps[i], i * spacing, gain); }

        // ================================================================== update
        float Speed { get { return H * 0.34f; } }

        void PlanePose(out float x, out float y, out float angle)
        {
            float tilt = Math.Max(-22, Math.Min(22, planeVY / H * 45f));
            float k = (Time - loopBorn) / LoopTime;
            if (k >= 0 && k < 1)
            {
                float e = k * k * (3 - 2 * k), th = e * 6.2832f, R = H * 0.13f;
                x = planeX + R * (float)Math.Sin(th); y = planeY - R * (1 - (float)Math.Cos(th)); angle = tilt - 360f * e;
            }
            else { x = planeX; y = planeY; angle = tilt; }
        }

        public void Update(float dt)
        {
            Time += dt;
            scroll += Speed * dt;
            prop += dt * 40f;
            if (Time - themeSince > ThemeSeconds) SetTheme(theme + 1, true, false);

            foreach (var t in things) t.X -= Speed * dt;
            things.RemoveAll(t => t.X < -H * 0.25f);

            // steer: follow the mouse while it moves, otherwise fly to the nearest thing ahead
            float ty, tx = W * 0.3f;
            if (Time < mouseUntil && mouseY >= 0)
            {
                ty = mouseY;
                tx = Math.Max(W * 0.12f, Math.Min(W * 0.62f, mouseX));
            }
            else
            {
                Thing best = null;
                foreach (var t in things) if (Time >= t.Born && t.X > planeX - H * 0.02f && (best == null || t.X < best.X)) best = t;
                ty = best != null ? best.Y : H * (0.38f + 0.1f * (float)Math.Sin(Time * 0.6));
            }
            ty = Math.Max(H * 0.1f, Math.Min(H * 0.7f, ty));
            float want = Math.Max(-H * 1.1f, Math.Min(H * 1.1f, (ty - planeY) * 3.4f));
            planeVY += (want - planeVY) * Math.Min(1, dt * 6f);
            planeY += planeVY * dt;
            planeX += (tx - planeX) * Math.Min(1, dt * 2.2f);

            float px, py, ang;
            PlanePose(out px, out py, out ang);
            float S = H * 0.2f;
            double rad = ang * Math.PI / 180;
            float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
            float hx = px + cos * S * 0.2f, hy = py + sin * S * 0.2f; // the plane's middle-front: what "touches" things
            for (int i = things.Count - 1; i >= 0; i--)
            {
                var t = things[i];
                if (Time < t.Born) continue;
                float dx = t.X - hx, dy = t.Y - hy;
                bool hit = t.Kind == Thing.Ring ? Math.Abs(dx) < H * 0.04f && Math.Abs(dy) < H * 0.1f : dx * dx + dy * dy < H * H * 0.085f * 0.085f;
                if (hit) { things.RemoveAt(i); Collect(t); }
            }

            // puffy paper trail from the tail
            puffTimer -= dt;
            if (puffTimer <= 0)
            {
                puffTimer = 0.05f;
                bool rainbow = Time < rainbowUntil;
                Color c = rainbow ? BalloonColors[rainbowHue++ % BalloonColors.Length] : A(Palettes[theme].Sky == 2 ? 150 : 225, Color.White);
                bits.Add(new Bit { Shape = 0, X = px - cos * S * 0.52f, Y = py - sin * S * 0.52f, VX = -Speed * 0.55f, VY = (float)(fx.NextDouble() - 0.5) * H * 0.03f, Size = H * (rainbow ? 0.016f : 0.012f), Grow = 1.6f, Life = rainbow ? 1.3f : 0.9f, C = c });
            }

            for (int i = bits.Count - 1; i >= 0; i--)
            {
                var b = bits[i];
                b.Age += dt;
                if (b.Age >= b.Life) { bits.RemoveAt(i); continue; }
                b.X += b.VX * dt; b.Y += b.VY * dt; b.Rot += b.VR * dt;
                if (b.Shape == 1) { b.VY += H * 1.1f * dt; b.VX *= 1 - dt * 0.8f; }
                if (b.Shape == 2) { b.VX *= 1 - dt * 2f; b.VY *= 1 - dt * 2f; }
            }
            popups.RemoveAll(p => Time - p.Born > 1.4f);
            if (oldScenery != null && Time - wipeBorn > WipeTime) { oldScenery.Dispose(); oldScenery = null; }
        }

        void Collect(Thing t)
        {
            if (t.Kind == Thing.Balloon)
            {
                play(t.Note, 0, 0.55f);
                for (int i = 0; i < 16; i++)
                {
                    double a = fx.NextDouble() * Math.PI * 2; float sp = H * (0.25f + 0.45f * (float)fx.NextDouble());
                    bits.Add(new Bit { Shape = 1, X = t.X, Y = t.Y, VX = (float)Math.Cos(a) * sp - Speed * 0.3f, VY = (float)Math.Sin(a) * sp - H * 0.2f, Rot = (float)(fx.NextDouble() * 6), VR = (float)(fx.NextDouble() - 0.5) * 16, Size = H * 0.016f, Life = 1.1f, C = i % 3 == 0 ? Color.White : (i % 3 == 1 ? t.C : BalloonColors[fx.Next(6)]) });
                }
                bits.Add(new Bit { Shape = 3, X = t.X, Y = t.Y, VX = -Speed * 0.3f, Size = H * 0.05f, Life = 0.4f, C = t.C });
                if (t.Label != "") popups.Add(new Popup { Text = t.Label, X = t.X, Y = t.Y, Born = Time, C = t.C });
            }
            else if (t.Kind == Thing.Star)
            {
                play(t.Note, 0, 0.45f); play(Math.Min(11, t.Note + 2), 0.09f, 0.4f); play(Math.Min(11, t.Note + 4), 0.18f, 0.35f);
                for (int i = 0; i < 12; i++)
                {
                    double a = i * Math.PI * 2 / 12; float sp = H * 0.5f;
                    bits.Add(new Bit { Shape = 2, X = t.X, Y = t.Y, VX = (float)Math.Cos(a) * sp - Speed * 0.3f, VY = (float)Math.Sin(a) * sp, Size = H * 0.018f, Life = 0.8f, C = i % 2 == 0 ? Gold : Color.White });
                }
            }
            else
            {
                rainbowUntil = Time + 3.5f;
                Tune(new[] { 0, 2, 4, 5, 7, 9, 11 }, 0.07f, 0.35f);
                bits.Add(new Bit { Shape = 3, X = t.X, Y = t.Y, VX = -Speed * 0.5f, Size = H * 0.09f, Life = 0.5f, C = t.C });
            }
        }

        // ================================================================== scenes
        void SetTheme(int t, bool announce, bool instant)
        {
            t = ((t % Palettes.Length) + Palettes.Length) % Palettes.Length;
            themeSince = Time;
            if (t == theme) return;
            theme = t;
            if (oldScenery != null) oldScenery.Dispose();
            oldScenery = null;
            if (instant) { scenery.Dispose(); scenery = Build(Palettes[t]); return; }
            oldScenery = scenery;
            scenery = Build(Palettes[t]);
            wipeBorn = Time;
            if (announce) { caption = Palettes[t].Name; captionBorn = Time; Tune(new[] { 9, 7, 4, 7, 9, 11 }, 0.1f, 0.4f); }
        }

        static Bitmap Grain()
        {
            if (grain != null) return grain;
            var r = new Random(99);
            var b = new Bitmap(128, 128, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    int v = r.Next(100);
                    b.SetPixel(x, y, v < 30 ? Color.FromArgb(r.Next(6, 16), 0, 0, 0) : v < 55 ? Color.FromArgb(r.Next(8, 20), 255, 255, 255) : Color.Transparent);
                }
            return grain = b;
        }

        // a paper cut-out: soft shadow, flat color, paper grain
        static void Paper(Graphics g, GraphicsPath path, Color c, float sdx, float sdy, int shadow)
        {
            if (shadow > 0)
                for (int i = 3; i >= 1; i--)
                    using (var p = (GraphicsPath)path.Clone())
                    using (var m = new Matrix())
                    using (var b = new SolidBrush(Color.FromArgb(shadow, 20, 16, 50)))
                    {
                        m.Translate(sdx * i / 3f, sdy * i / 3f);
                        p.Transform(m);
                        g.FillPath(b, p);
                    }
            using (var b = new SolidBrush(c)) g.FillPath(b, path);
            using (var tb = new TextureBrush(Grain())) g.FillPath(tb, path);
        }

        static float Wave(float x, float tile, float[] k, float[] amp, float[] ph)
        {
            float s = 0;
            for (int i = 0; i < k.Length; i++) s += amp[i] * (float)Math.Sin(2 * Math.PI * k[i] * x / tile + ph[i]);
            return s;
        }

        GraphicsPath Ridge(int tile, int bmpH, float baseY, float[] k, float[] amp, float[] ph)
        {
            var pts = new List<PointF>();
            // runs past both edges (the wave repeats), so shadows and edges join up where the layer repeats
            for (int x = -24; x <= tile + 24; x += 6) pts.Add(new PointF(x, baseY - Wave(x, tile, k, amp, ph)));
            pts.Add(new PointF(tile + 24, bmpH + 2)); pts.Add(new PointF(-24, bmpH + 2));
            var p = new GraphicsPath();
            p.AddPolygon(pts.ToArray());
            return p;
        }

        static Bitmap NewLayer(int w, int h, out Graphics g)
        {
            var b = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppPArgb);
            g = Graphics.FromImage(b);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            return b;
        }

        Scenery Build(Palette p)
        {
            var s = new Scenery { P = p };
            int tile = s.TileW = (Math.Max(W, (int)(H * 1.8f)) + 127) / 128 * 128; // a multiple of the paper grain, so it repeats seamlessly
            var r = new Random(1000 + Array.IndexOf(Palettes, p));
            Graphics g;

            // ---- sky
            // each layer stops where the one in front of it always covers it: less to draw every frame
            int skyH = (int)(H * 0.74f), farEnd = (int)(H * 0.78f), midEnd = (int)(H * 0.89f);
            s.Sky = NewLayer(W, skyH, out g);
            using (g)
            {
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, W, H), p.SkyTop, p.SkyBottom, 90f)) g.FillRectangle(b, 0, 0, W, skyH);
                if (p.Sky == 2)
                {
                    for (int i = 0; i < 80; i++)
                    {
                        float x = (float)r.NextDouble() * W, y = (float)r.NextDouble() * H * 0.6f, z = H * (0.002f + 0.004f * (float)r.NextDouble());
                        using (var b = new SolidBrush(Color.FromArgb(120 + r.Next(130), 255, 248, 220)))
                            g.FillPolygon(b, new[] { new PointF(x, y - z * 2), new PointF(x + z, y), new PointF(x, y + z * 2), new PointF(x - z, y) });
                    }
                    float mx = W * 0.8f, my = H * 0.18f, mr = H * 0.075f;
                    using (var glow = new GraphicsPath())
                    {
                        glow.AddEllipse(mx - mr * 3, my - mr * 3, mr * 6, mr * 6);
                        using (var b = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(70, 255, 240, 200), SurroundColors = new[] { Color.FromArgb(0, 255, 240, 200) } }) g.FillPath(b, glow);
                    }
                    using (var moon = new GraphicsPath())
                    {
                        moon.AddEllipse(mx - mr, my - mr, mr * 2, mr * 2);
                        var reg = new Region(moon);
                        using (var bite = new GraphicsPath()) { bite.AddEllipse(mx - mr * 0.45f, my - mr * 1.25f, mr * 2, mr * 2); reg.Exclude(bite); }
                        using (var shadowReg = reg.Clone()) using (var b = new SolidBrush(Color.FromArgb(60, 10, 10, 40))) { shadowReg.Translate(H * 0.006f, H * 0.008f); g.FillRegion(b, shadowReg); }
                        using (var b = new SolidBrush(p.Sun)) g.FillRegion(b, reg);
                        reg.Dispose();
                    }
                }
                else
                {
                    bool low = p.Sky == 1;
                    float sx = low ? W * 0.66f : W * 0.8f, sy = low ? H * 0.5f : H * 0.2f, sr = low ? H * 0.13f : H * 0.075f;
                    if (!low)
                        using (var rays = new GraphicsPath())
                        {
                            for (int i = 0; i < 12; i++)
                            {
                                double a = i * Math.PI / 6, w = 0.12;
                                rays.AddPolygon(new[] {
                                    new PointF(sx + (float)Math.Cos(a - w) * sr * 1.25f, sy + (float)Math.Sin(a - w) * sr * 1.25f),
                                    new PointF(sx + (float)Math.Cos(a) * sr * 1.85f, sy + (float)Math.Sin(a) * sr * 1.85f),
                                    new PointF(sx + (float)Math.Cos(a + w) * sr * 1.25f, sy + (float)Math.Sin(a + w) * sr * 1.25f) });
                            }
                            Paper(g, rays, p.SunRim, H * 0.004f, H * 0.006f, 14);
                        }
                    using (var rim = new GraphicsPath()) { rim.AddEllipse(sx - sr * 1.15f, sy - sr * 1.15f, sr * 2.3f, sr * 2.3f); Paper(g, rim, A(low ? 170 : 255, p.SunRim), H * 0.004f, H * 0.006f, 14); }
                    using (var sun = new GraphicsPath()) { sun.AddEllipse(sx - sr, sy - sr, sr * 2, sr * 2); Paper(g, sun, p.Sun, H * 0.005f, H * 0.008f, 18); }
                }
            }

            // ---- far mountains
            s.FarY = (int)(H * 0.34f);
            s.Far = NewLayer(tile, farEnd - s.FarY, out g);
            using (g)
            using (var ridge = Ridge(tile, farEnd - s.FarY, H * 0.24f, new float[] { 2, 3, 7 }, new[] { H * 0.07f, H * 0.04f, H * 0.018f }, new float[] { 0.5f, 2f, 1f }))
                Paper(g, ridge, p.Far, -H * 0.004f, -H * 0.012f, 16);

            // ---- middle hills with trees
            s.MidY = (int)(H * 0.46f);
            s.Mid = NewLayer(tile, midEnd - s.MidY, out g);
            using (g)
            {
                float[] k = { 1, 3, 4 }, amp = { H * 0.03f, H * 0.022f, H * 0.01f }, ph = { 0.3f, 1.4f, 2.2f };
                float baseY = H * 0.22f;
                using (var ridge = Ridge(tile, midEnd - s.MidY, baseY, k, amp, ph)) Paper(g, ridge, p.Mid, -H * 0.004f, -H * 0.012f, 18);
                int n = (int)(tile / (H * 0.075f));
                for (int i = 0; i < n; i++)
                {
                    if (r.Next(10) < 4) continue;
                    float x = (i + (float)r.NextDouble() * 0.6f) * tile / n;
                    float size = H * (0.05f + 0.035f * (float)r.NextDouble());
                    int kind = r.Next(2);
                    Color c = r.Next(2) == 0 ? p.TreeA : p.TreeB;
                    foreach (int off in new[] { -tile, 0, tile })
                    {
                        float xx = x + off;
                        if (xx < -size || xx > tile + size) continue;
                        Tree(g, xx, baseY - Wave(x, tile, k, amp, ph) + H * 0.006f, size, kind, c, p.Trunk);
                    }
                }
            }

            // ---- near ground: houses, bushes, flowers
            s.NearY = (int)(H * 0.7f);
            s.Near = NewLayer(tile, H - s.NearY, out g);
            using (g)
            {
                float[] k = { 2, 5 }, amp = { H * 0.012f, H * 0.006f }, ph = { 1f, 0.2f };
                float baseY = H * 0.15f;
                using (var ridge = Ridge(tile, H - s.NearY, baseY, k, amp, ph)) Paper(g, ridge, p.Near, -H * 0.003f, -H * 0.01f, 20);
                int houses = Math.Max(2, (int)(tile / (H * 0.55f)));
                for (int i = 0; i < houses; i++)
                {
                    float x = (i + 0.2f + 0.6f * (float)r.NextDouble()) * tile / houses;
                    float size = H * (0.075f + 0.02f * (float)r.NextDouble());
                    var roof = i % 2 == 0 ? p.Roof : p.TreeA;
                    foreach (int off in new[] { -tile, 0, tile })
                    {
                        float xx = x + off;
                        if (xx < -size || xx > tile + size) continue;
                        House(g, xx, baseY - Wave(x, tile, k, amp, ph) + H * 0.01f, size, p, roof);
                    }
                }
                using (var front = Ridge(tile, H - s.NearY, H * 0.25f, new float[] { 3, 7 }, new[] { H * 0.008f, H * 0.005f }, new float[] { 0.7f, 2.5f }))
                    Paper(g, front, p.Front, -H * 0.003f, -H * 0.008f, 20);
                // little paper flowers: five petals and a middle
                using (var petal = new SolidBrush(A(p.Sky == 2 ? 140 : 240, p.Flower)))
                using (var mid = new SolidBrush(A(p.Sky == 2 ? 140 : 255, p.Sky == 2 ? p.Window : C(0xF5B53F))))
                using (var sh = new SolidBrush(Color.FromArgb(30, 20, 16, 50)))
                    for (int i = 0; i < tile / (H * 0.07f); i++)
                    {
                        float x = (float)r.NextDouble() * tile, y = H * (0.265f + 0.025f * (float)r.NextDouble()), z = H * (0.006f + 0.003f * (float)r.NextDouble());
                        foreach (int off in new[] { -tile, 0, tile })
                        {
                            float xx = x + off;
                            if (xx < -z * 3 || xx > tile + z * 3) continue;
                            g.FillEllipse(sh, xx - z * 1.4f, y - z * 0.9f, z * 3.2f, z * 3.2f);
                            for (int q = 0; q < 5; q++)
                            {
                                double a = q * Math.PI * 2 / 5 - Math.PI / 2;
                                g.FillEllipse(petal, xx + (float)Math.Cos(a) * z - z * 0.75f, y + (float)Math.Sin(a) * z - z * 0.75f, z * 1.5f, z * 1.5f);
                            }
                            g.FillEllipse(mid, xx - z * 0.5f, y - z * 0.5f, z, z);
                        }
                    }
            }

            // ---- clouds
            for (int i = 0; i < 3; i++)
            {
                float cw = H * (0.18f + 0.06f * i), ch = cw * 0.42f;
                s.Clouds[i] = NewLayer((int)(cw * 1.1f), (int)(ch * 1.35f), out g);
                using (g)
                using (var c = new GraphicsPath(FillMode.Winding))
                {
                    c.AddEllipse(0, ch * 0.45f, cw, ch * 0.55f);
                    c.AddEllipse(cw * 0.12f, ch * 0.18f, cw * 0.42f, ch * 0.7f);
                    c.AddEllipse(cw * 0.38f, ch * 0.02f, cw * 0.4f, ch * 0.8f);
                    c.AddEllipse(cw * 0.62f, ch * 0.3f, cw * 0.3f, ch * 0.55f);
                    Paper(g, c, p.Cloud, H * 0.006f, H * 0.01f, 16);
                }
            }
            return s;
        }

        static void Tree(Graphics g, float x, float groundY, float size, int kind, Color leaf, Color trunk)
        {
            using (var t = new GraphicsPath())
            {
                t.AddRectangle(new RectangleF(x - size * 0.06f, groundY - size * 0.45f, size * 0.12f, size * 0.47f));
                Paper(g, t, trunk, 0, 0, 0);
            }
            using (var c = new GraphicsPath())
            {
                if (kind == 0) c.AddEllipse(x - size * 0.3f, groundY - size, size * 0.6f, size * 0.66f);
                else c.AddPolygon(new[] { new PointF(x, groundY - size * 1.1f), new PointF(x + size * 0.27f, groundY - size * 0.32f), new PointF(x - size * 0.27f, groundY - size * 0.32f) });
                Paper(g, c, leaf, size * 0.04f, size * 0.06f, 22);
            }
        }

        static void House(Graphics g, float x, float groundY, float s, Palette p, Color roof)
        {
            float w = s * 0.9f, h = s * 0.62f;
            using (var chim = new GraphicsPath()) { chim.AddRectangle(new RectangleF(x + w * 0.18f, groundY - h - s * 0.5f, s * 0.12f, s * 0.3f)); Paper(g, chim, roof, s * 0.03f, s * 0.04f, 18); }
            using (var body = new GraphicsPath()) { body.AddRectangle(new RectangleF(x - w / 2, groundY - h, w, h)); Paper(g, body, p.House, s * 0.04f, s * 0.05f, 22); }
            using (var rf = new GraphicsPath())
            {
                rf.AddPolygon(new[] { new PointF(x - w * 0.62f, groundY - h + s * 0.02f), new PointF(x, groundY - h - s * 0.45f), new PointF(x + w * 0.62f, groundY - h + s * 0.02f) });
                Paper(g, rf, roof, s * 0.03f, s * 0.05f, 22);
            }
            using (var b = new SolidBrush(p.Window))
            {
                g.FillRectangle(b, x - w * 0.34f, groundY - h * 0.72f, w * 0.2f, w * 0.2f);
                g.FillRectangle(b, x + w * 0.14f, groundY - h * 0.72f, w * 0.2f, w * 0.2f);
            }
            using (var b = new SolidBrush(p.Trunk)) g.FillRectangle(b, x - w * 0.09f, groundY - h * 0.5f, w * 0.18f, h * 0.5f);
        }

        // ================================================================== drawing
        SolidBrush Br(Color c)
        {
            SolidBrush b;
            if (!brushes.TryGetValue(c.ToArgb(), out b)) { b = new SolidBrush(c); brushes[c.ToArgb()] = b; }
            return b;
        }

        void Tiled(Graphics g, Bitmap bmp, int y, float dist, int tile)
        {
            int off = (int)(dist % tile);
            for (int x = -off; x < W; x += tile) g.DrawImageUnscaled(bmp, x, y);
        }

        void Backdrop(Graphics g, Scenery s)
        {
            g.CompositingMode = CompositingMode.SourceCopy; // the sky is opaque: a plain copy, no blending
            g.DrawImageUnscaled(s.Sky, 0, 0);
            g.CompositingMode = CompositingMode.SourceOver;
            DrawClouds(g, s, false);
            Tiled(g, s.Far, s.FarY, scroll * 0.1f, s.TileW);
            DrawClouds(g, s, true);
            Tiled(g, s.Mid, s.MidY, scroll * 0.35f, s.TileW);
            Tiled(g, s.Near, s.NearY, scroll * 0.9f, s.TileW);
        }

        void DrawClouds(Graphics g, Scenery s, bool near)
        {
            float margin = H * 0.35f, span = W + margin * 2;
            foreach (var c in clouds)
            {
                if ((c.Depth > 0.3f) != near) continue;
                float x = ((c.X * span - scroll * c.Depth) % span + span) % span - margin;
                g.DrawImageUnscaled(s.Clouds[c.Kind], (int)x, (int)(c.Y * H));
            }
        }

        public void Draw(Graphics g)
        {
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.SmoothingMode = SmoothingMode.None;
            if (oldScenery != null)
            {
                float k = Math.Min(1, (Time - wipeBorn) / WipeTime), e = k * k * (3 - 2 * k);
                Backdrop(g, oldScenery);
                float edge = W * (1 - e);
                var st = g.Save();
                g.SetClip(new RectangleF(edge, 0, W - edge + 1, H));
                Backdrop(g, scenery);
                g.Restore(st);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new LinearGradientBrush(new RectangleF(edge - H * 0.04f, 0, H * 0.04f, H), Color.FromArgb(0, 20, 16, 50), Color.FromArgb(70, 20, 16, 50), 0f))
                    g.FillRectangle(b, edge - H * 0.04f + 1, 0, H * 0.04f, H);
                using (var pen = new Pen(Color.FromArgb(230, 255, 255, 255), Math.Max(2, H * 0.004f))) g.DrawLine(pen, edge, 0, edge, H);
            }
            else Backdrop(g, scenery);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            foreach (var b in bits) if (b.Shape == 0) { try { DrawBit(g, b); } catch { } }
            foreach (var t in things) if (t.Kind == Thing.Ring) { try { DrawRing(g, t, false); } catch { } }
            try { DrawPlane(g); } catch { }
            foreach (var t in things) { try { if (t.Kind == Thing.Balloon) DrawBalloon(g, t); else if (t.Kind == Thing.Star) DrawStar(g, t); else DrawRing(g, t, true); } catch { } }
            foreach (var b in bits) if (b.Shape != 0) { try { DrawBit(g, b); } catch { } }
            foreach (var p in popups) { try { DrawPopup(g, p); } catch { } }
            try { DrawCaption(g); } catch { }
            try { DrawStartHint(g); } catch { }
        }

        static float Appear(float age) { return age < 0 ? 0 : (age > 0.35f ? 1 : EaseOutBack(age / 0.35f)); }

        static float EaseOutBack(float x)
        {
            x = Math.Max(0, Math.Min(1, x));
            const float c1 = 1.70158f, c3 = c1 + 1;
            return 1 + c3 * (float)Math.Pow(x - 1, 3) + c1 * (float)Math.Pow(x - 1, 2);
        }

        void DrawBalloon(Graphics g, Thing t)
        {
            float s = Appear(Time - t.Born);
            if (s <= 0.02f) return;
            float r = H * 0.06f;
            float y = t.Y + (float)Math.Sin(Time * 2.2f + t.Phase) * H * 0.01f;
            var st = g.Save();
            g.TranslateTransform(t.X, y);
            g.ScaleTransform(s * r, s * r); // unit space: balloon radius = 1
            using (var pen = new Pen(Color.FromArgb(130, Ink), 0.05f))
                g.DrawBezier(pen, 0, 1.2f, -0.3f, 1.6f, 0.3f, 2.0f, -0.05f, 2.5f);
            g.FillEllipse(Br(Color.FromArgb(55, 20, 16, 50)), -0.9f + 0.18f, -1.1f + 0.26f, 1.8f, 2.2f);
            g.FillPolygon(Br(Dark(t.C)), new[] { new PointF(-0.18f, 1.27f), new PointF(0.18f, 1.27f), new PointF(0, 1.02f) });
            g.FillEllipse(Br(t.C), -0.9f, -1.1f, 1.8f, 2.2f);
            g.FillEllipse(Br(Color.FromArgb(50, 255, 255, 255)), -0.9f, -1.1f, 1.3f, 1.9f); // paper fold: lighter left side
            g.FillEllipse(Br(Color.FromArgb(150, 255, 255, 255)), -0.6f, -0.78f, 0.3f, 0.5f);
            if (t.Label != "")
            {
                float fs = t.Label.Length > 1 ? 0.95f : 1.2f;
                g.ScaleTransform(fs / 40f, fs / 40f);
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(t.Label, balloonFont, Br(Color.FromArgb(70, 20, 16, 50)), 3, 6, sf);
                    g.DrawString(t.Label, balloonFont, Br(Color.White), 0, 2, sf);
                }
            }
            g.Restore(st);
        }

        static Color Dark(Color c) { return Color.FromArgb(c.A, c.R * 3 / 4, c.G * 3 / 4, c.B * 3 / 4); }

        static PointF[] StarPts(float cx, float cy, float r, float inner, float rotDeg, int points)
        {
            var pts = new PointF[points * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                double a = (rotDeg - 90) * Math.PI / 180 + i * Math.PI / points;
                float rr = i % 2 == 0 ? r : r * inner;
                pts[i] = new PointF(cx + (float)Math.Cos(a) * rr, cy + (float)Math.Sin(a) * rr);
            }
            return pts;
        }

        void DrawStar(Graphics g, Thing t)
        {
            float s = Appear(Time - t.Born);
            if (s <= 0.02f) return;
            float r = H * 0.055f * s, rot = (float)Math.Sin(Time * 2 + t.Phase) * 12, y = t.Y + (float)Math.Sin(Time * 1.7f + t.Phase) * H * 0.012f;
            g.FillPolygon(Br(Color.FromArgb(55, 20, 16, 50)), StarPts(t.X + r * 0.15f, y + r * 0.22f, r, 0.5f, rot, 5));
            g.FillPolygon(Br(Gold), StarPts(t.X, y, r, 0.5f, rot, 5));
            g.FillPolygon(Br(C(0xFFE389)), StarPts(t.X - r * 0.04f, y - r * 0.05f, r * 0.55f, 0.5f, rot, 5));
            float tw = (float)Math.Max(0, Math.Sin(Time * 4 + t.Phase * 3));
            if (tw > 0.2f) g.FillPolygon(Br(Color.FromArgb((int)(220 * tw), 255, 255, 255)), StarPts(t.X + r * 0.9f, y - r * 0.8f, r * 0.3f * tw, 0.25f, 0, 4));
        }

        void DrawRing(Graphics g, Thing t, bool front)
        {
            float s = Appear(Time - t.Born);
            if (s <= 0.02f) return;
            float rx = H * 0.05f * s, ry = H * 0.14f * s, y = t.Y + (float)Math.Sin(Time * 1.5f + t.Phase) * H * 0.01f;
            var rect = new RectangleF(t.X - rx, y - ry, rx * 2, ry * 2);
            float w = H * 0.024f * s;
            using (var sh = new Pen(Color.FromArgb(55, 20, 16, 50), w))
            {
                var r2 = rect; r2.Offset(H * 0.006f, H * 0.01f);
                g.DrawArc(sh, r2, front ? -90 : 90, 180);
            }
            for (int i = 0; i < 8; i++)
                using (var pen = new Pen(i % 2 == 0 ? BalloonColors[(Array.IndexOf(BalloonColors, t.C) + i / 2) % 6] : Color.White, w))
                    g.DrawArc(pen, rect, (front ? -90 : 90) + i * 22.5f, 23f);
        }

        void DrawBit(Graphics g, Bit b)
        {
            float k = b.Age / b.Life;
            int a = Math.Max(0, Math.Min(255, (int)(b.C.A * (1 - k * k))));
            switch (b.Shape)
            {
                case 0:
                {
                    float r = b.Size * (1 + b.Grow * k);
                    g.FillEllipse(Br(Color.FromArgb(a, b.C)), b.X - r, b.Y - r, r * 2, r * 2);
                    break;
                }
                case 1:
                {
                    float c = (float)Math.Cos(b.Rot) * b.Size, s = (float)Math.Sin(b.Rot) * b.Size, flip = (float)Math.Cos(b.Rot * 1.7f);
                    var pts = new[] {
                        new PointF(b.X - c + s * 0.6f * flip, b.Y - s - c * 0.6f * flip), new PointF(b.X + c + s * 0.6f * flip, b.Y + s - c * 0.6f * flip),
                        new PointF(b.X + c - s * 0.6f * flip, b.Y + s + c * 0.6f * flip), new PointF(b.X - c - s * 0.6f * flip, b.Y - s + c * 0.6f * flip) };
                    g.FillPolygon(Br(Color.FromArgb(a, b.C)), pts);
                    break;
                }
                case 2:
                    g.FillPolygon(Br(Color.FromArgb(a, b.C)), StarPts(b.X, b.Y, b.Size * (1 - k * 0.5f), 0.3f, b.Age * 200, 4));
                    break;
                default:
                {
                    float r = b.Size * (1 + k * 2.2f);
                    using (var pen = new Pen(Color.FromArgb(a * 3 / 4, Color.White), H * 0.008f * (1 - k))) g.DrawEllipse(pen, b.X - r, b.Y - r, r * 2, r * 2);
                    break;
                }
            }
        }

        // ---------------------------------------------------------- the plane (unit space: nose at +0.5, tail at -0.5)
        static readonly Color Body = C(0xEE6B5D), BodyDark = C(0xC9554B), Wing = C(0xFBEBCD), WingDark = C(0xE2CFAA),
                              Accent = C(0xF5B53F), Skin = C(0xF7CDA8), Cap = C(0x8A5A44), Glass = C(0xA8DDF2);

        void DrawPlane(Graphics g)
        {
            float x, y, ang;
            PlanePose(out x, out y, out ang);
            float S = H * 0.2f;
            var st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(ang);
            g.ScaleTransform(S, S);
            var shadow = Br(Color.FromArgb(50, 20, 16, 50));
            var st2 = g.Save();
            g.TranslateTransform(0.03f, 0.05f);
            PlaneParts(g, shadow);
            g.Restore(st2);
            PlaneParts(g, null);
            g.Restore(st);
        }

        void PlaneParts(Graphics g, Brush shadow)
        {
            Func<Color, Brush> B = c => shadow ?? Br(c);
            if (shadow == null) // scarf, fluttering behind
            {
                var pts = new List<PointF>();
                for (int i = 0; i <= 6; i++) pts.Add(new PointF(-0.02f - i * 0.055f, -0.19f + (float)Math.Sin(Time * 12 - i * 0.9) * 0.012f * i));
                for (int i = 6; i >= 0; i--) pts.Add(new PointF(-0.02f - i * 0.055f, -0.15f + (float)Math.Sin(Time * 12 - i * 0.9) * 0.012f * i + i * 0.004f));
                g.FillPolygon(B(Accent), pts.ToArray());
            }
            g.FillEllipse(B(WingDark), -0.2f, -0.05f, 0.4f, 0.09f); // far wing
            using (var fin = new GraphicsPath())
            {
                fin.AddBezier(-0.47f, -0.03f, -0.48f, -0.18f, -0.46f, -0.3f, -0.38f, -0.31f);
                fin.AddBezier(-0.38f, -0.31f, -0.32f, -0.31f, -0.3f, -0.15f, -0.22f, -0.04f);
                fin.CloseFigure();
                g.FillPath(B(BodyDark), fin);
            }
            if (shadow == null) // pilot: head, cap, goggles
            {
                g.FillEllipse(B(Skin), 0.0f - 0.085f, -0.155f - 0.085f, 0.17f, 0.17f);
                g.FillPie(B(Cap), -0.092f, -0.25f, 0.184f, 0.184f, 180, 180);
                g.FillEllipse(B(Cap), -0.11f, -0.17f, 0.06f, 0.035f);
                using (var gp = new Pen(C(0x6B4A3A), 0.014f)) { g.FillEllipse(B(Glass), 0.03f, -0.2f, 0.065f, 0.065f); g.DrawEllipse(gp, 0.03f, -0.2f, 0.065f, 0.065f); g.DrawLine(gp, -0.08f, -0.17f, 0.03f, -0.17f); }
                using (var smile = new Pen(C(0x8A4B3A), 0.012f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(smile, 0.02f, -0.14f, 0.05f, 0.04f, 10, 120);
                g.FillPolygon(B(Color.FromArgb(170, Glass)), new[] { new PointF(0.1f, -0.12f), new PointF(0.13f, -0.22f), new PointF(0.19f, -0.12f) });
            }
            using (var body = new GraphicsPath())
            {
                body.AddBezier(0.42f, 0f, 0.42f, -0.13f, 0.2f, -0.14f, 0.0f, -0.125f);
                body.AddLine(0.0f, -0.125f, -0.3f, -0.075f);
                body.AddBezier(-0.3f, -0.075f, -0.42f, -0.06f, -0.5f, -0.05f, -0.5f, 0.0f);
                body.AddBezier(-0.5f, 0.0f, -0.5f, 0.04f, -0.42f, 0.06f, -0.3f, 0.07f);
                body.AddLine(-0.3f, 0.07f, 0.0f, 0.125f);
                body.AddBezier(0.0f, 0.125f, 0.2f, 0.15f, 0.42f, 0.13f, 0.42f, 0f);
                body.CloseFigure();
                g.FillPath(B(Body), body);
                if (shadow == null)
                {
                    var st = g.Save();
                    g.SetClip(body, CombineMode.Intersect);
                    g.FillRectangle(B(Wing), -0.24f, -0.2f, 0.07f, 0.4f);
                    g.FillRectangle(B(Color.FromArgb(40, 255, 255, 255)), -0.5f, -0.2f, 1f, 0.12f); // paper fold: lighter top
                    g.Restore(st);
                }
            }
            g.FillEllipse(B(Wing), -0.5f, -0.035f, 0.2f, 0.07f); // tail wing
            using (var strut = new Pen(shadow != null ? Color.FromArgb(50, 20, 16, 50) : C(0x6B6B7B), 0.022f)) g.DrawLine(strut, 0.06f, 0.1f, 0.09f, 0.2f);
            g.FillEllipse(B(C(0x3D3D50)), 0.09f - 0.048f, 0.21f - 0.048f, 0.096f, 0.096f);
            if (shadow == null) g.FillEllipse(B(C(0xB8B8C8)), 0.09f - 0.018f, 0.21f - 0.018f, 0.036f, 0.036f);
            g.FillEllipse(B(Wing), -0.2f, 0.01f, 0.44f, 0.12f); // near wing
            if (shadow == null)
            {
                g.FillEllipse(B(C(0x59A7E8)), 0.05f, 0.035f, 0.07f, 0.07f);
                g.FillEllipse(B(Color.White), 0.072f, 0.057f, 0.026f, 0.026f);
            }
            g.FillEllipse(B(Accent), 0.37f, -0.065f, 0.12f, 0.13f); // nose
            if (shadow == null)
            {
                float blade = 0.42f * Math.Max(0.12f, Math.Abs((float)Math.Cos(prop)));
                g.FillEllipse(B(Color.FromArgb(45, 255, 255, 255)), 0.465f, -0.21f, 0.04f, 0.42f);
                g.FillEllipse(B(C(0x6B5A4A)), 0.47f, -blade / 2, 0.03f, blade);
                g.FillEllipse(B(C(0x6B5A4A)), 0.46f, -0.02f, 0.04f, 0.04f);
            }
        }

        void DrawPopup(Graphics g, Popup p)
        {
            float t = Time - p.Born;
            float s = t < 0.3f ? Math.Max(0.05f, EaseOutBack(t / 0.3f)) : 1f;
            int a = (int)(255 * Math.Max(0, Math.Min(1, (1.4f - t) / 0.4f)));
            if (a <= 0) return;
            var st = g.Save();
            g.TranslateTransform(p.X, p.Y - t * H * 0.06f);
            float k = s * H * 0.2f / 100f;
            g.ScaleTransform(k, k);
            using (var path = new GraphicsPath())
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                path.AddString(p.Text, popupFont.FontFamily, (int)FontStyle.Bold, 100f, new PointF(0, 0), sf);
                var shadowPath = (GraphicsPath)path.Clone();
                using (var m = new Matrix()) { m.Translate(5, 8); shadowPath.Transform(m); }
                g.FillPath(Br(Color.FromArgb(a * 60 / 255, 20, 16, 50)), shadowPath);
                shadowPath.Dispose();
                using (var pen = new Pen(Color.FromArgb(a, 255, 255, 255), 14) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
                g.FillPath(Br(Color.FromArgb(a, p.C)), path);
            }
            g.Restore(st);
        }

        // the scene's name on a strip of paper, when the sky changes
        void DrawCaption(Graphics g)
        {
            if (caption == null) return;
            float t = Time - captionBorn;
            const float life = 3.2f;
            if (t < 0 || t > life) return;
            int a = (int)(255 * Math.Min(1f, Math.Min(t * 5f, (life - t) * 2f)));
            using (var f = new Font(Round, H * 0.05f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                SizeF sz = g.MeasureString(caption, f);
                float w = sz.Width + H * 0.08f, h = sz.Height + H * 0.03f;
                var st = g.Save();
                g.TranslateTransform(W / 2f, H * 0.9f);
                g.RotateTransform(-2f);
                g.FillRectangle(Br(Color.FromArgb(a * 60 / 255, 20, 16, 50)), -w / 2 + H * 0.006f, -h / 2 + H * 0.01f, w, h);
                g.FillRectangle(Br(Color.FromArgb(a, 255, 250, 240)), -w / 2, -h / 2, w, h);
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(caption, f, Br(Color.FromArgb(a, Ink)), new RectangleF(-w / 2, -h / 2, w, h), sf);
                g.Restore(st);
            }
        }

        void DrawStartHint(Graphics g)
        {
            const float life = 8f;
            if (Time > life || startHint == "") return;
            int a = (int)(255 * Math.Min(1f, (life - Time) * 1.5f));
            SizeF sz = g.MeasureString(startHint, uiFont);
            float h = sz.Height + 14;
            var rect = new RectangleF((W - sz.Width) / 2 - h * 0.6f, H * 0.012f, sz.Width + h * 1.2f, h);
            g.FillRectangle(Br(Color.FromArgb(a * 235 / 255, 255, 250, 240)), rect);
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(startHint, uiFont, Br(Color.FromArgb(a, Ink)), rect, sf);
        }

        // ================================================================== previews
        void DemoScene()
        {
            SetTheme(0, false, true);
            things.Clear();
            Spawn(Thing.Balloon, W * 0.5f, H * 0.3f, "Y", 0, 0, -1);
            Spawn(Thing.Ring, W * 0.68f, H * 0.42f, "", 3, 0, -1);
            Spawn(Thing.Star, W * 0.84f, H * 0.26f, "", 0, 0, -1);
            Spawn(Thing.Balloon, W * 0.9f, H * 0.55f, "", 4, 0, -1);
            planeY = H * 0.42f;
            for (int i = 0; i < 30; i++) { Update(0.03f); foreach (var t in things) t.X += Speed * 0.03f; }
        }

        public static void Snapshot(string prefix)
        {
            for (int t = 0; t < Palettes.Length; t++)
            {
                var gm = new PilotGame(1920, 1080, t);
                gm.OnKey('Y'); gm.OnKey('5'); gm.Update(0.6f);
                for (int i = 0; i < 40; i++) gm.Update(0.03f);
                gm.OnKey('Y');
                for (int i = 0; i < 12; i++) gm.Update(0.03f);
                using (var bmp = new Bitmap(1920, 1080))
                using (var g = Graphics.FromImage(bmp)) { gm.Draw(g); bmp.Save(prefix + t + ".png", ImageFormat.Png); }
            }
            var w = new PilotGame(1280, 1024, 0);
            w.Update(0.5f); w.ParentNext(); w.Update(0.7f); w.OnWheel(0, 0); w.Update(0.5f);
            using (var bmp = new Bitmap(1280, 1024))
            using (var g = Graphics.FromImage(bmp)) { w.Draw(g); bmp.Save(prefix + "wipe.png", ImageFormat.Png); }
        }
    }
}

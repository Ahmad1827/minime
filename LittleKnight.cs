// Little Knight - a medieval / chess "baby smash" app for Windows.
// Blocks keyboard shortcuts (Win key, Alt+Tab, Alt+F4, Ctrl+Esc...) and covers every screen.
// PARENT EXIT: hold Ctrl + Alt and type  Q U I T
//
// Build (no install needed, uses the C# compiler that ships with Windows): see build.bat / build.sh
// Written for C# 5 (the csc.exe bundled with .NET Framework 4.x).
// Preview without locking anything:  LittleKnight.exe --snapshot out.png   /   --gallery out.png

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LittleKnight
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); } catch { }

            if (args.Length >= 2 && args[0] == "--snapshot") { Scene.Snapshot(args[1]); return; }
            if (args.Length >= 2 && args[0] == "--gallery") { Scene.Gallery(args[1]); return; }
            if (args.Length >= 2 && args[0] == "--mirror") { Scene.Mirror(args[1]); return; }

            bool created;
            using (var mutex = new Mutex(true, "LittleKnight_SingleInstance", out created))
            {
                if (!created) return;
                // Never crash back to the desktop because of a drawing/sound glitch.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate { };
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new LockController());
            }
        }
    }

    // ------------------------------------------------------------------ Win32
    static class Native
    {
        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
        public const uint ES_CONTINUOUS = 0x80000000, ES_SYSTEM_REQUIRED = 0x1, ES_DISPLAY_REQUIRED = 0x2;

        // Accessibility shortcuts (5x Shift = Sticky Keys, hold Shift = Filter Keys, hold NumLock = Toggle Keys)
        [StructLayout(LayoutKind.Sequential)] public struct STICKYKEYS { public uint cbSize, dwFlags; }
        [StructLayout(LayoutKind.Sequential)] public struct TOGGLEKEYS { public uint cbSize, dwFlags; }
        [StructLayout(LayoutKind.Sequential)]
        public struct FILTERKEYS { public uint cbSize, dwFlags, iWaitMSec, iDelayMSec, iRepeatMSec, iBounceMSec; }
        [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint a, uint b, ref STICKYKEYS p, uint w);
        [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint a, uint b, ref TOGGLEKEYS p, uint w);
        [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint a, uint b, ref FILTERKEYS p, uint w);
        public const uint SPI_GETFILTERKEYS = 0x32, SPI_SETFILTERKEYS = 0x33, SPI_GETTOGGLEKEYS = 0x34,
                          SPI_SETTOGGLEKEYS = 0x35, SPI_GETSTICKYKEYS = 0x3A, SPI_SETSTICKYKEYS = 0x3B;
        public const uint FEATURE_ON = 0x1, HOTKEYACTIVE = 0x4, CONFIRMHOTKEY = 0x8;
    }

    class AccessibilityGuard
    {
        Native.STICKYKEYS sk; Native.TOGGLEKEYS tk; Native.FILTERKEYS fk; bool saved;

        public void Disable()
        {
            sk.cbSize = (uint)Marshal.SizeOf(typeof(Native.STICKYKEYS));
            tk.cbSize = (uint)Marshal.SizeOf(typeof(Native.TOGGLEKEYS));
            fk.cbSize = (uint)Marshal.SizeOf(typeof(Native.FILTERKEYS));
            Native.SystemParametersInfo(Native.SPI_GETSTICKYKEYS, sk.cbSize, ref sk, 0);
            Native.SystemParametersInfo(Native.SPI_GETTOGGLEKEYS, tk.cbSize, ref tk, 0);
            Native.SystemParametersInfo(Native.SPI_GETFILTERKEYS, fk.cbSize, ref fk, 0);
            saved = true;
            // Only touch the shortcut flags, and only if the feature itself is off. Not persisted (fWinIni = 0).
            var s = sk; if ((s.dwFlags & Native.FEATURE_ON) == 0) { s.dwFlags &= ~(Native.HOTKEYACTIVE | Native.CONFIRMHOTKEY); Native.SystemParametersInfo(Native.SPI_SETSTICKYKEYS, s.cbSize, ref s, 0); }
            var t = tk; if ((t.dwFlags & Native.FEATURE_ON) == 0) { t.dwFlags &= ~(Native.HOTKEYACTIVE | Native.CONFIRMHOTKEY); Native.SystemParametersInfo(Native.SPI_SETTOGGLEKEYS, t.cbSize, ref t, 0); }
            var f = fk; if ((f.dwFlags & Native.FEATURE_ON) == 0) { f.dwFlags &= ~(Native.HOTKEYACTIVE | Native.CONFIRMHOTKEY); Native.SystemParametersInfo(Native.SPI_SETFILTERKEYS, f.cbSize, ref f, 0); }
        }

        public void Restore()
        {
            if (!saved) return;
            Native.SystemParametersInfo(Native.SPI_SETSTICKYKEYS, sk.cbSize, ref sk, 0);
            Native.SystemParametersInfo(Native.SPI_SETTOGGLEKEYS, tk.cbSize, ref tk, 0);
            Native.SystemParametersInfo(Native.SPI_SETFILTERKEYS, fk.cbSize, ref fk, 0);
            saved = false;
        }
    }

    // ------------------------------------------------------------------ Harp: synthesized plucked notes, mixed in real time
    class Harp : IDisposable
    {
        const int Rate = 22050, BufSamples = 512, NumBufs = 4;

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }
        [StructLayout(LayoutKind.Sequential)]
        struct WAVEHDR { public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public uint dwFlags, dwLoops; public IntPtr lpNext, reserved; }

        [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr h, uint dev, ref WAVEFORMATEX f, IntPtr cb, IntPtr inst, uint flags);
        [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr h);
        const uint WAVE_MAPPER = 0xFFFFFFFF, WHDR_DONE = 1;

        class Voice { public float[] S; public int Pos; public float Gain; }

        IntPtr dev;
        readonly IntPtr[] hdrs = new IntPtr[NumBufs], datas = new IntPtr[NumBufs];
        uint hdrSize; int flagsOffset;
        readonly List<Voice> voices = new List<Voice>();
        readonly object gate = new object();
        float[][] notes;
        Thread thread; volatile bool running; bool ok;

        // C major pentatonic over ~2 octaves: every combination sounds pleasant, whatever the baby presses.
        static readonly int[] Scale = { 60, 62, 64, 67, 69, 72, 74, 76, 79, 81, 84, 86 };

        public static Harp TryCreate()
        {
            try { var h = new Harp(); if (h.ok) return h; h.Dispose(); } catch { }
            return null;
        }

        Harp()
        {
            var rnd = new Random(1234);
            notes = new float[Scale.Length][];
            for (int i = 0; i < Scale.Length; i++)
                notes[i] = Pluck(440.0 * Math.Pow(2, (Scale[i] - 69) / 12.0), 1.8, rnd);

            var fmt = new WAVEFORMATEX { wFormatTag = 1, nChannels = 1, nSamplesPerSec = Rate, wBitsPerSample = 16, nBlockAlign = 2, nAvgBytesPerSec = Rate * 2, cbSize = 0 };
            if (waveOutOpen(out dev, WAVE_MAPPER, ref fmt, IntPtr.Zero, IntPtr.Zero, 0) != 0) { dev = IntPtr.Zero; return; }
            hdrSize = (uint)Marshal.SizeOf(typeof(WAVEHDR));
            flagsOffset = (int)Marshal.OffsetOf(typeof(WAVEHDR), "dwFlags");
            for (int i = 0; i < NumBufs; i++)
            {
                datas[i] = Marshal.AllocHGlobal(BufSamples * 2);
                hdrs[i] = Marshal.AllocHGlobal((int)hdrSize);
                var h = new WAVEHDR { lpData = datas[i], dwBufferLength = BufSamples * 2 };
                Marshal.StructureToPtr(h, hdrs[i], false);
                waveOutPrepareHeader(dev, hdrs[i], hdrSize);
            }
            running = true; ok = true;
            thread = new Thread(Loop) { IsBackground = true, Priority = ThreadPriority.AboveNormal };
            thread.Start();
        }

        // Karplus-Strong plucked string, softened so it sounds like a harp / lute.
        static float[] Pluck(double freq, double seconds, Random r)
        {
            int n = (int)(seconds * Rate);
            var output = new float[n];
            int p = Math.Max(2, (int)Math.Round(Rate / freq));
            var ring = new float[p];
            for (int i = 0; i < p; i++) ring[i] = (float)(r.NextDouble() * 2 - 1);
            for (int pass = 0; pass < 3; pass++) // pre-filter the noise burst: warmer, less "zingy"
                for (int i = 0; i < p; i++) ring[i] = (ring[i] + ring[(i + 1) % p] + ring[(i + p - 1) % p]) / 3f;
            int idx = 0; float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                int nx = (idx + 1) % p;
                float v = ring[idx];
                output[i] = v;
                ring[idx] = 0.497f * (ring[idx] + ring[nx]);
                idx = nx;
                peak = Math.Max(peak, Math.Abs(v));
            }
            int attack = 40, release = n / 4;
            for (int i = 0; i < n; i++)
            {
                float env = 0.42f / peak;
                if (i < attack) env *= i / (float)attack;
                if (i > n - release) env *= (n - i) / (float)release;
                output[i] *= env;
            }
            return output;
        }

        public void Play(int note, float delaySeconds, float gain)
        {
            if (!ok) return;
            note = Math.Max(0, Math.Min(notes.Length - 1, note));
            lock (gate)
            {
                if (voices.Count >= 18) voices.RemoveAt(0);
                voices.Add(new Voice { S = notes[note], Pos = -(int)(delaySeconds * Rate), Gain = gain });
            }
        }

        void Loop()
        {
            var mix = new float[BufSamples];
            var pcm = new short[BufSamples];
            var queued = new bool[NumBufs];
            while (running)
            {
                bool wrote = false;
                for (int i = 0; i < NumBufs && running; i++)
                {
                    if (queued[i] && (Marshal.ReadInt32(hdrs[i], flagsOffset) & WHDR_DONE) == 0) continue;
                    Fill(mix, pcm);
                    Marshal.Copy(pcm, 0, datas[i], BufSamples);
                    waveOutWrite(dev, hdrs[i], hdrSize);
                    queued[i] = true; wrote = true;
                }
                if (!wrote) Thread.Sleep(3);
            }
        }

        void Fill(float[] mix, short[] pcm)
        {
            Array.Clear(mix, 0, mix.Length);
            lock (gate)
            {
                for (int v = voices.Count - 1; v >= 0; v--)
                {
                    var vo = voices[v];
                    for (int j = 0; j < BufSamples; j++)
                    {
                        int k = vo.Pos + j;
                        if (k < 0) continue;
                        if (k >= vo.S.Length) break;
                        mix[j] += vo.S[k] * vo.Gain;
                    }
                    vo.Pos += BufSamples;
                    if (vo.Pos >= vo.S.Length) voices.RemoveAt(v);
                }
            }
            for (int j = 0; j < BufSamples; j++)
            {
                float x = mix[j];
                x = x / (1f + 0.35f * Math.Abs(x)); // gentle limiter so smashing never gets harsh
                pcm[j] = (short)(Math.Max(-1f, Math.Min(1f, x)) * 30000);
            }
        }

        public void Dispose()
        {
            running = false;
            if (thread != null) thread.Join(300);
            if (dev != IntPtr.Zero)
            {
                waveOutReset(dev);
                for (int i = 0; i < NumBufs; i++) if (hdrs[i] != IntPtr.Zero) waveOutUnprepareHeader(dev, hdrs[i], hdrSize);
                waveOutClose(dev);
                dev = IntPtr.Zero;
            }
            for (int i = 0; i < NumBufs; i++)
            {
                if (hdrs[i] != IntPtr.Zero) { Marshal.FreeHGlobal(hdrs[i]); hdrs[i] = IntPtr.Zero; }
                if (datas[i] != IntPtr.Zero) { Marshal.FreeHGlobal(datas[i]); datas[i] = IntPtr.Zero; }
            }
            ok = false;
        }
    }

    // ------------------------------------------------------------------ Input lock + one mirrored window per monitor
    // Every monitor gets its own full-size scene (as if it were the only screen). All scenes receive the
    // same events with the same random seed, so they show the same thing. Sound plays only once.
    class LockController : ApplicationContext
    {
        readonly List<ScreenForm> forms = new List<ScreenForm>();
        readonly System.Windows.Forms.Timer frameTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer guardTimer = new System.Windows.Forms.Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly AccessibilityGuard access = new AccessibilityGuard();
        double lastFrame;

        IntPtr hook = IntPtr.Zero;
        Native.HookProc hookProc; // kept in a field so the GC never collects it

        readonly HashSet<int> modsDown = new HashSet<int>();
        readonly Dictionary<int, double> keyLastDown = new Dictionary<int, double>();
        const string UnlockWord = "QUIT";
        int unlockIndex; double unlockLast;
        bool unlocked, finished;

        Harp harp;
        bool cursorHidden;
        string screenLayout = "";

        public LockController()
        {
            harp = Harp.TryCreate();
            BuildForms();

            hookProc = KeyboardHook;
            hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, hookProc, Native.GetModuleHandle(null), 0);
            try { access.Disable(); } catch { }
            Native.SetThreadExecutionState(Native.ES_CONTINUOUS | Native.ES_DISPLAY_REQUIRED | Native.ES_SYSTEM_REQUIRED);
            Cursor.Hide(); cursorHidden = true;

            lastFrame = clock.Elapsed.TotalSeconds;
            frameTimer.Interval = 15;
            frameTimer.Tick += delegate { Tick(); };
            frameTimer.Start();
            guardTimer.Interval = 400;
            guardTimer.Tick += delegate { Guard(); };
            guardTimer.Start();

            if (forms.Count > 0) forms[0].Scene.Welcome();
        }

        static string Layout()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var sc in Screen.AllScreens) sb.Append(sc.Bounds.ToString()).Append(';');
            return sb.ToString();
        }

        // (Re)create one window per monitor. Called at start and whenever monitors change.
        void BuildForms()
        {
            foreach (var f in forms) { f.AllowClose = true; f.Close(); f.Dispose(); }
            forms.Clear();
            screenLayout = Layout();
            int seed = Environment.TickCount;
            float time = 0;
            var screens = Screen.AllScreens;
            // primary screen first, so it is the one that plays sound
            Array.Sort(screens, (x, y) => (y.Primary ? 1 : 0) - (x.Primary ? 1 : 0));
            foreach (var sc in screens)
            {
                var f = new ScreenForm(this, sc.Bounds, seed);
                f.Scene.Time = time;
                forms.Add(f);
            }
            if (forms.Count > 0 && harp != null) forms[0].Scene.Play = harp.Play;
            foreach (var f in forms) f.Show();
            if (forms.Count > 0) { forms[0].Activate(); Native.SetForegroundWindow(forms[0].Handle); }
        }

        void Tick()
        {
            double now = clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.05, now - lastFrame);
            lastFrame = now;
            foreach (var f in forms) { try { f.Scene.Update(dt); } catch { } f.Invalidate(); }
        }

        void Guard()
        {
            if (finished) return;
            if (Layout() != screenLayout) { BuildForms(); return; } // monitor plugged in/out or resolution changed
            IntPtr fg = Native.GetForegroundWindow();
            bool ours = false;
            foreach (var f in forms) if (f.Handle == fg) ours = true;
            foreach (var f in forms) { f.TopMost = false; f.TopMost = true; }
            if (!ours && forms.Count > 0) { forms[0].Activate(); Native.SetForegroundWindow(forms[0].Handle); }
        }

        // ---------------- events coming from any window, broadcast to every scene
        public void MouseMoved(ScreenForm src, int x, int y)
        {
            float nx = x / (float)src.Scene.W, ny = y / (float)src.Scene.H;
            foreach (var f in forms) { try { f.Scene.OnMouseMove((int)(nx * f.Scene.W), (int)(ny * f.Scene.H)); } catch { } }
        }

        public void MouseClicked(ScreenForm src, int x, int y, bool right)
        {
            int col, row;
            bool onBoard = src.Scene.BoardCell(x, y, out col, out row); // decided once, on the screen that was clicked
            float nx = x / (float)src.Scene.W, ny = y / (float)src.Scene.H;
            foreach (var f in forms)
            {
                try
                {
                    if (onBoard) f.Scene.ClickSquare(col, row, !right);
                    else f.Scene.ClickOutside((int)(nx * f.Scene.W), (int)(ny * f.Scene.H));
                }
                catch { }
            }
        }

        public void MouseWheel(ScreenForm src, int x, int y)
        {
            float nx = x / (float)src.Scene.W, ny = y / (float)src.Scene.H;
            foreach (var f in forms) { try { f.Scene.OnWheel((int)(nx * f.Scene.W), (int)(ny * f.Scene.H)); } catch { } }
        }

        // ---------------- keyboard
        IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0 || unlocked) return Native.CallNextHookEx(hook, nCode, wParam, lParam);
            try
            {
                var k = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                int msg = wParam.ToInt32();
                bool down = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
                bool up = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;
                if (down) OnKey((int)k.vkCode);
                else if (up) KeyUp((int)k.vkCode);
            }
            catch { }
            return (IntPtr)1; // swallow EVERY key: nothing reaches Windows or other apps
        }

        static bool IsCtrl(int vk) { return vk == 0x11 || vk == 0xA2 || vk == 0xA3; }
        static bool IsAlt(int vk) { return vk == 0x12 || vk == 0xA4 || vk == 0xA5; }
        static bool IsShift(int vk) { return vk == 0x10 || vk == 0xA0 || vk == 0xA1; }
        static bool IsWin(int vk) { return vk == 0x5B || vk == 0x5C; }

        public bool HookActive { get { return hook != IntPtr.Zero; } }

        public void KeyUp(int vk) { modsDown.Remove(vk); keyLastDown.Remove(vk); }

        public void OnKey(int vk)
        {
            double now = clock.Elapsed.TotalSeconds;
            bool isMod = IsCtrl(vk) || IsAlt(vk) || IsShift(vk) || IsWin(vk);

            // Ignore auto-repeat (a held key fires many key-downs).
            double last;
            bool repeat = keyLastDown.TryGetValue(vk, out last) && now - last < 1.5;
            keyLastDown[vk] = now;
            if (isMod) { repeat = modsDown.Contains(vk); modsDown.Add(vk); }
            if (repeat) return;

            if (!isMod) CheckUnlock(vk, now);
            if (unlocked) return;
            foreach (var f in forms) { try { f.Scene.OnKey(vk); } catch { } }
        }

        void CheckUnlock(int vk, double now)
        {
            bool ctrl = false, alt = false;
            foreach (int m in modsDown) { if (IsCtrl(m)) ctrl = true; if (IsAlt(m)) alt = true; }
            if (!ctrl || !alt) { unlockIndex = 0; return; }

            if (unlockIndex > 0 && now - unlockLast > 3.0) unlockIndex = 0;
            if (vk == UnlockWord[unlockIndex]) { unlockIndex++; unlockLast = now; }
            else unlockIndex = (vk == UnlockWord[0]) ? 1 : 0;

            if (unlockIndex >= UnlockWord.Length) { unlocked = true; BeginFinish(); }
        }

        void BeginFinish()
        {
            if (forms.Count > 0) forms[0].BeginInvoke((MethodInvoker)Finish);
            else Finish();
        }

        // Called when the parent unlocks, or when Windows is shutting down.
        public void Finish()
        {
            if (finished) return;
            finished = true; unlocked = true;
            frameTimer.Stop(); guardTimer.Stop();
            if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            try { access.Restore(); } catch { }
            Native.SetThreadExecutionState(Native.ES_CONTINUOUS);
            if (cursorHidden) { Cursor.Show(); cursorHidden = false; }
            if (harp != null) { try { harp.Dispose(); } catch { } harp = null; }
            foreach (var f in forms) { f.AllowClose = true; try { f.Close(); } catch { } }
            ExitThread();
        }
    }

    class ScreenForm : Form
    {
        public readonly Scene Scene;
        public bool AllowClose;
        readonly LockController owner;

        public ScreenForm(LockController owner, Rectangle bounds, int seed)
        {
            this.owner = owner;
            Text = "Little Knight";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(126, 200, 242);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            Scene = new Scene(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height), seed);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            // Never let a drawing error escape: WinForms would replace the window with a permanent red X.
            try { Scene.Draw(e.Graphics); }
            catch { try { e.Graphics.ResetTransform(); e.Graphics.Clear(Color.FromArgb(126, 200, 242)); } catch { } }
        }

        protected override void OnMouseMove(MouseEventArgs e) { owner.MouseMoved(this, e.X, e.Y); }
        protected override void OnMouseDown(MouseEventArgs e) { owner.MouseClicked(this, e.X, e.Y, e.Button == MouseButtons.Right); }
        protected override void OnMouseWheel(MouseEventArgs e) { owner.MouseWheel(this, e.X, e.Y); }

        // Fallback if the keyboard hook could not be installed: at least swallow keys while focused.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!owner.HookActive) owner.OnKey((int)(keyData & Keys.KeyCode));
            return true;
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (!owner.HookActive) owner.KeyUp(e.KeyValue);
            e.Handled = true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!AllowClose && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; return; }
            if (!AllowClose) owner.Finish(); // Windows shutdown / logoff: clean up properly
            base.OnFormClosing(e);
        }
    }

    // ------------------------------------------------------------------ Vector art (flat, toy-like)
    class Layer
    {
        public GraphicsPath Path; public Region Region;
        public Color Fill = Color.Empty, Stroke = Color.Empty;
        public float StrokeW; public bool LineOnly;
    }

    // A picture is a stack of colored shapes in "unit" space (roughly -0.5..0.5).
    class Picture
    {
        public readonly List<Layer> Layers = new List<Layer>();
        public RectangleF Bounds;

        public Picture Add(GraphicsPath p, Color fill) { Layers.Add(new Layer { Path = p, Fill = fill }); return this; }
        public Picture Add(GraphicsPath p, Color fill, Color stroke, float w) { Layers.Add(new Layer { Path = p, Fill = fill, Stroke = stroke, StrokeW = w }); return this; }
        public Picture Line(GraphicsPath p, Color stroke, float w) { Layers.Add(new Layer { Path = p, Stroke = stroke, StrokeW = w, LineOnly = true }); return this; }
        public Picture Add(Region r, Color fill) { Layers.Add(new Layer { Region = r, Fill = fill }); return this; }

        public Picture Finish()
        {
            bool first = true; RectangleF b = RectangleF.Empty;
            foreach (var l in Layers)
            {
                if (l.Path == null) continue;
                var r = l.Path.GetBounds();
                b = first ? r : RectangleF.Union(b, r); first = false;
            }
            Bounds = b;
            return this;
        }

        // Draw with the given transform already applied by the caller (unit space).
        public void Draw(Graphics g, int alpha)
        {
            foreach (var l in Layers)
            {
                if (l.Region != null)
                {
                    using (var br = new SolidBrush(A(l.Fill, alpha))) g.FillRegion(br, l.Region);
                    continue;
                }
                if (l.StrokeW > 0 && l.Stroke.A > 0)
                {
                    // stroke drawn *behind* the fill = clean outer outline only
                    using (var pen = new Pen(A(l.Stroke, alpha), l.LineOnly ? l.StrokeW : l.StrokeW * 2f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawPath(pen, l.Path);
                }
                if (!l.LineOnly)
                    using (var br = new SolidBrush(A(l.Fill, alpha))) g.FillPath(br, l.Path);
            }
        }

        static Color A(Color c, int alpha) { return Color.FromArgb(Math.Max(0, Math.Min(255, c.A * alpha / 255)), c); }
    }

    static class Art
    {
        public static FontFamily BlockFont = Find("Segoe UI Black", "Arial Black", "Segoe UI");
        public static FontFamily UiFont = Find("Segoe UI", "Arial");

        static FontFamily Find(params string[] names)
        {
            foreach (var n in names) { try { return new FontFamily(n); } catch (ArgumentException) { } }
            return FontFamily.GenericSansSerif;
        }

        // ---- palette
        public static Color C(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
        public static Color Dark(Color c, float k) { return Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k)); }

        static readonly Color Gold = C(0xF5C242), GoldDark = C(0xC9971C), Brown = C(0x8B5A2B), BrownDark = C(0x5E3B1A);
        static readonly Color Red = C(0xEF5350), RedDark = C(0xB8322A), Green = C(0x66BB6A), GreenDark = C(0x3D8B3D);
        static readonly Color Blue = C(0x4D9DE0), BlueDark = C(0x2A6FB0), White = Color.White;
        const float SW = 0.022f; // outline width

        public static readonly Color[] BlockColors = {
            C(0xF25F5C), C(0xFF9F1C), C(0xF2B705), C(0x3BB273), C(0x4D9DE0), C(0x7768AE), C(0xE76FB0), C(0x17A2B8)
        };

        // ---- shape helpers
        static GraphicsPath NP() { return new GraphicsPath(FillMode.Winding); }
        public static GraphicsPath Circle(float cx, float cy, float r) { var p = NP(); p.AddEllipse(cx - r, cy - r, 2 * r, 2 * r); return p; }
        static GraphicsPath Ellipse(float x, float y, float w, float h) { var p = NP(); p.AddEllipse(x, y, w, h); return p; }
        public static GraphicsPath RRect(float x, float y, float w, float h, float r)
        {
            var p = NP();
            float d = Math.Min(r * 2, Math.Min(w, h));
            if (d <= 0) { p.AddRectangle(new RectangleF(x, y, w, h)); return p; }
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
        static PointF[] Pts(float[] xy) { var a = new PointF[xy.Length / 2]; for (int i = 0; i < a.Length; i++) a[i] = new PointF(xy[2 * i], xy[2 * i + 1]); return a; }
        static GraphicsPath Poly(params float[] xy) { var p = NP(); p.AddPolygon(Pts(xy)); return p; }
        static GraphicsPath Curve(float tension, params float[] xy) { var p = NP(); p.AddClosedCurve(Pts(xy), tension); return p; }
        static GraphicsPath Lines(params float[] xy) { var p = NP(); p.AddLines(Pts(xy)); return p; }
        static GraphicsPath Trap(float bw, float by, float tw, float ty) { return Poly(-bw, by, bw, by, tw, ty, -tw, ty); }
        static GraphicsPath Merge(params GraphicsPath[] ps) { var p = NP(); foreach (var q in ps) { p.AddPath(q, false); q.Dispose(); } return p; }
        static GraphicsPath Rot(GraphicsPath p, float deg, float cx, float cy) { using (var m = new Matrix()) { m.RotateAt(deg, new PointF(cx, cy)); p.Transform(m); } return p; }
        static GraphicsPath Move(GraphicsPath p, float dx, float dy) { using (var m = new Matrix()) { m.Translate(dx, dy); p.Transform(m); } return p; }
        public static GraphicsPath Star(float cx, float cy, float r)
        {
            var xy = new float[20];
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5; float rr = i % 2 == 0 ? r : r * 0.45f;
                xy[2 * i] = cx + (float)Math.Cos(a) * rr; xy[2 * i + 1] = cy + (float)Math.Sin(a) * rr;
            }
            return Poly(xy);
        }
        static GraphicsPath Text(string s, FontFamily fam, float height, float cx, float cy)
        {
            var p = NP();
            p.AddString(s, fam, (int)FontStyle.Bold, 1f, PointF.Empty, StringFormat.GenericTypographic);
            var b = p.GetBounds();
            if (b.Height > 0)
            {
                float k = height / b.Height;
                using (var m = new Matrix())
                {
                    m.Translate(cx, cy);
                    m.Scale(k, k);
                    m.Translate(-b.X - b.Width / 2f, -b.Y - b.Height / 2f);
                    p.Transform(m);
                }
            }
            return p;
        }

        // ---- cache
        static readonly Dictionary<string, Picture> cache = new Dictionary<string, Picture>();
        public static Picture Get(string key)
        {
            Picture p;
            if (cache.TryGetValue(key, out p)) return p;
            p = Build(key).Finish();
            cache[key] = p;
            return p;
        }

        static Picture Build(string key)
        {
            if (key.StartsWith("piece:")) return Piece(key[6] - '0', key[7] == 'w');
            if (key.StartsWith("block:")) { var parts = key.Split(':'); return Block(parts[1], BlockColors[int.Parse(parts[2]) % BlockColors.Length]); }
            switch (key)
            {
                case "apple": return Apple();
                case "bell": return Bell();
                case "castle": return Castle();
                case "drum": return Drum();
                case "egg": return Egg();
                case "flag": return Flag();
                case "gem": return Gem();
                case "heart": return Heart();
                case "icecream": return IceCream();
                case "jar": return Jar();
                case "leaf": return Leaf();
                case "moon": return Moon();
                case "note": return Note();
                case "owl": return Owl();
                case "shield": return Shield();
                case "tree": return Tree();
                case "umbrella": return Umbrella();
                case "vase": return Vase();
                case "wand": return Wand();
                case "xmark": return XMark();
                case "yoyo": return YoYo();
                case "zigzag": return Zigzag();
                case "crown": return Crown();
                case "star": return StarPic();
                case "sword": return Sword();
            }
            return StarPic();
        }

        // ---- chess pieces (0 King, 1 Queen, 2 Rook, 3 Bishop, 4 Knight, 5 Pawn)
        static Picture Piece(int type, bool white)
        {
            Color body = white ? C(0xFFF6E3) : C(0x433E6B);
            Color line = white ? C(0x8C6E4E) : C(0x1E1A38);
            var pic = new Picture();
            var parts = new List<GraphicsPath> { RRect(-0.32f, 0.35f, 0.64f, 0.15f, 0.06f), RRect(-0.25f, 0.27f, 0.5f, 0.1f, 0.04f) };
            switch (type)
            {
                case 0: // King
                    parts.Add(Trap(0.21f, 0.29f, 0.11f, -0.1f));
                    parts.Add(Poly(-0.12f, -0.08f, 0.12f, -0.08f, 0.21f, -0.3f, -0.21f, -0.3f));
                    parts.Add(RRect(-0.21f, -0.35f, 0.42f, 0.07f, 0.035f));
                    break;
                case 1: // Queen
                    parts.Add(Trap(0.21f, 0.29f, 0.1f, -0.1f));
                    parts.Add(Poly(-0.15f, -0.08f, -0.25f, -0.34f, -0.11f, -0.21f, 0f, -0.39f, 0.11f, -0.21f, 0.25f, -0.34f, 0.15f, -0.08f));
                    break;
                case 2: // Rook
                    parts.Add(Trap(0.2f, 0.29f, 0.17f, -0.18f));
                    parts.Add(RRect(-0.26f, -0.3f, 0.52f, 0.14f, 0.03f));
                    parts.Add(RRect(-0.26f, -0.44f, 0.13f, 0.17f, 0.02f));
                    parts.Add(RRect(-0.065f, -0.44f, 0.13f, 0.17f, 0.02f));
                    parts.Add(RRect(0.13f, -0.44f, 0.13f, 0.17f, 0.02f));
                    break;
                case 3: // Bishop
                    parts.Add(Trap(0.2f, 0.29f, 0.08f, -0.04f));
                    parts.Add(RRect(-0.15f, -0.08f, 0.3f, 0.07f, 0.035f));
                    parts.Add(Ellipse(-0.15f, -0.42f, 0.3f, 0.38f));
                    parts.Add(Circle(0, -0.44f, 0.055f));
                    break;
                case 4: // Knight (horse head facing left)
                    parts.Add(Curve(0.2f,
                        0.23f, 0.29f, -0.21f, 0.29f, -0.1f, 0.06f, -0.03f, -0.04f, -0.21f, 0.03f, -0.32f, -0.05f,
                        -0.28f, -0.17f, -0.12f, -0.32f, -0.05f, -0.47f, 0.04f, -0.37f, 0.17f, -0.31f, 0.27f, -0.11f, 0.27f, 0.11f));
                    break;
                default: // Pawn
                    parts.Add(Trap(0.2f, 0.29f, 0.09f, -0.04f));
                    parts.Add(RRect(-0.15f, -0.08f, 0.3f, 0.07f, 0.035f));
                    parts.Add(Circle(0, -0.25f, 0.17f));
                    break;
            }
            pic.Add(Merge(parts.ToArray()), body, line, SW);

            // details
            switch (type)
            {
                case 0:
                    pic.Add(RRect(-0.16f, -0.13f, 0.32f, 0.06f, 0.03f), Gold, GoldDark, 0.01f);
                    pic.Add(Merge(RRect(-0.035f, -0.5f, 0.07f, 0.17f, 0.02f), RRect(-0.1f, -0.46f, 0.2f, 0.065f, 0.02f)), Gold, GoldDark, 0.012f);
                    break;
                case 1:
                    pic.Add(RRect(-0.16f, -0.13f, 0.32f, 0.06f, 0.03f), Gold, GoldDark, 0.01f);
                    pic.Add(Merge(Circle(-0.25f, -0.37f, 0.05f), Circle(0, -0.43f, 0.055f), Circle(0.25f, -0.37f, 0.05f)), Gold, GoldDark, 0.012f);
                    break;
                case 2:
                    pic.Add(RRect(-0.045f, -0.08f, 0.09f, 0.16f, 0.045f), line);
                    break;
                case 3:
                    pic.Line(Lines(0.0f, -0.33f, 0.08f, -0.23f), line, 0.035f);
                    pic.Add(RRect(-0.15f, -0.08f, 0.3f, 0.07f, 0.035f), Gold, GoldDark, 0.01f);
                    break;
                case 4:
                    pic.Add(Circle(-0.11f, -0.22f, 0.035f), line);
                    pic.Line(Lines(0.06f, -0.34f, 0.2f, -0.24f, 0.25f, -0.08f, 0.25f, 0.08f), white ? C(0xD9C29C) : C(0x6A64A0), 0.04f);
                    break;
            }
            // soft shine so the pieces look like toys
            pic.Add(Ellipse(-0.17f, 0.04f, 0.06f, 0.18f), Color.FromArgb(white ? 150 : 60, 255, 255, 255));
            return pic;
        }

        // ---- toy alphabet block
        static Picture Block(string text, Color c)
        {
            var pic = new Picture();
            Color edge = Dark(c, 0.72f);
            pic.Add(RRect(-0.45f, -0.4f, 0.9f, 0.88f, 0.14f), edge);
            pic.Add(RRect(-0.45f, -0.45f, 0.9f, 0.86f, 0.14f), c);
            pic.Line(RRect(-0.37f, -0.37f, 0.74f, 0.7f, 0.1f), Color.FromArgb(110, 255, 255, 255), 0.025f);
            pic.Add(Text(text, BlockFont, 0.48f, 0.025f, 0.005f), edge);
            pic.Add(Text(text, BlockFont, 0.48f, 0f, -0.02f), White);
            return pic;
        }

        // ---- pictures for letters and treasures
        static Picture Apple()
        {
            return new Picture()
                .Line(Lines(0f, -0.18f, 0.04f, -0.4f), BrownDark, 0.05f)
                .Add(Merge(Circle(-0.12f, 0.06f, 0.3f), Circle(0.12f, 0.06f, 0.3f)), Red, RedDark, SW)
                .Add(Rot(Ellipse(0.05f, -0.44f, 0.26f, 0.12f), -25, 0.05f, -0.38f), Green, GreenDark, SW)
                .Add(Rot(Ellipse(-0.3f, -0.12f, 0.1f, 0.2f), 20, -0.25f, -0.02f), Color.FromArgb(140, 255, 255, 255));
        }

        static Picture Bell()
        {
            return new Picture()
                .Line(Circle(0f, -0.42f, 0.06f), GoldDark, 0.035f)
                .Add(Circle(0f, 0.35f, 0.08f), GoldDark)
                .Add(Merge(Ellipse(-0.24f, -0.38f, 0.48f, 0.42f), Poly(-0.24f, -0.17f, 0.24f, -0.17f, 0.36f, 0.25f, -0.36f, 0.25f), RRect(-0.42f, 0.2f, 0.84f, 0.11f, 0.05f)), Gold, GoldDark, SW)
                .Add(Rot(Ellipse(-0.18f, -0.25f, 0.08f, 0.3f), 10, -0.14f, -0.1f), Color.FromArgb(150, 255, 255, 255));
        }

        static Picture Castle()
        {
            Color wall = C(0xC9C3E6), wallDark = C(0x7D74A8), roof = C(0xF2709C), door = C(0x8B5A2B);
            return new Picture()
                .Line(Lines(-0.31f, -0.4f, -0.31f, -0.52f), BrownDark, 0.02f)
                .Add(Poly(-0.31f, -0.52f, -0.18f, -0.48f, -0.31f, -0.44f), Gold)
                .Line(Lines(0.31f, -0.4f, 0.31f, -0.52f), BrownDark, 0.02f)
                .Add(Poly(0.31f, -0.52f, 0.44f, -0.48f, 0.31f, -0.44f), Gold)
                .Add(Merge(RRect(-0.42f, -0.15f, 0.22f, 0.62f, 0.02f), RRect(0.2f, -0.15f, 0.22f, 0.62f, 0.02f),
                           RRect(-0.25f, 0.0f, 0.5f, 0.47f, 0.02f), RRect(-0.22f, -0.09f, 0.1f, 0.12f, 0.01f),
                           RRect(-0.05f, -0.09f, 0.1f, 0.12f, 0.01f), RRect(0.12f, -0.09f, 0.1f, 0.12f, 0.01f)), wall, wallDark, SW)
                .Add(Poly(-0.46f, -0.15f, -0.31f, -0.42f, -0.16f, -0.15f), roof, Dark(roof, 0.7f), SW)
                .Add(Poly(0.16f, -0.15f, 0.31f, -0.42f, 0.46f, -0.15f), roof, Dark(roof, 0.7f), SW)
                .Add(Merge(RRect(-0.1f, 0.22f, 0.2f, 0.25f, 0f), Ellipse(-0.1f, 0.12f, 0.2f, 0.2f)), door, BrownDark, SW)
                .Add(Merge(RRect(-0.34f, 0.0f, 0.06f, 0.12f, 0.03f), RRect(0.28f, 0.0f, 0.06f, 0.12f, 0.03f)), wallDark);
        }

        static Picture Drum()
        {
            return new Picture()
                .Line(Lines(-0.42f, -0.46f, 0.02f, -0.18f), BrownDark, 0.05f)
                .Line(Lines(0.42f, -0.46f, -0.02f, -0.18f), BrownDark, 0.05f)
                .Add(Merge(Circle(-0.42f, -0.46f, 0.05f), Circle(0.42f, -0.46f, 0.05f)), Gold, GoldDark, 0.01f)
                .Add(RRect(-0.36f, -0.15f, 0.72f, 0.5f, 0.06f), Red, RedDark, SW)
                .Line(Lines(-0.36f, -0.05f, -0.24f, 0.25f, -0.12f, -0.05f, 0f, 0.25f, 0.12f, -0.05f, 0.24f, 0.25f, 0.36f, -0.05f), Gold, 0.03f)
                .Add(RRect(-0.38f, 0.28f, 0.76f, 0.09f, 0.045f), Gold, GoldDark, 0.012f)
                .Add(Ellipse(-0.36f, -0.25f, 0.72f, 0.2f), C(0xFFF1D6), GoldDark, SW);
        }

        static Picture Egg()
        {
            Color egg = C(0xA8D8F0), dark = C(0x5FA3CC);
            return new Picture()
                .Add(Curve(0.5f, 0f, -0.43f, 0.21f, -0.22f, 0.3f, 0.1f, 0.18f, 0.36f, 0f, 0.42f, -0.18f, 0.36f, -0.3f, 0.1f, -0.21f, -0.22f), egg, dark, SW)
                .Add(Merge(Circle(0.08f, -0.18f, 0.05f), Circle(-0.1f, 0.05f, 0.07f), Circle(0.14f, 0.18f, 0.045f), Circle(-0.03f, 0.27f, 0.035f)), Color.FromArgb(200, 255, 255, 255));
        }

        static Picture Flag()
        {
            return new Picture()
                .Add(RRect(-0.36f, -0.44f, 0.05f, 0.94f, 0.02f), Brown, BrownDark, 0.012f)
                .Add(Circle(-0.335f, -0.46f, 0.05f), Gold, GoldDark, 0.01f)
                .Add(Curve(0.45f, -0.31f, -0.4f, 0.0f, -0.45f, 0.42f, -0.36f, 0.38f, -0.1f, 0.05f, -0.16f, -0.31f, -0.08f), Red, RedDark, SW)
                .Add(Star(0.04f, -0.25f, 0.1f), Gold);
        }

        static Picture Gem()
        {
            return new Picture()
                .Add(Poly(-0.4f, -0.12f, -0.21f, -0.34f, 0.21f, -0.34f, 0.4f, -0.12f, 0f, 0.4f), Blue, BlueDark, SW)
                .Add(Poly(-0.21f, -0.34f, 0.21f, -0.34f, 0.11f, -0.12f, -0.11f, -0.12f), C(0x9FD0FA))
                .Line(Lines(-0.4f, -0.12f, 0.4f, -0.12f), BlueDark, 0.018f)
                .Line(Lines(-0.11f, -0.12f, 0f, 0.4f, 0.11f, -0.12f), BlueDark, 0.018f)
                .Add(Star(-0.2f, -0.2f, 0.06f), Color.FromArgb(220, 255, 255, 255));
        }

        static Picture Heart()
        {
            Color h = C(0xF2547D);
            return new Picture()
                .Add(Merge(Circle(-0.16f, -0.1f, 0.21f), Circle(0.16f, -0.1f, 0.21f), Poly(-0.355f, -0.02f, 0.355f, -0.02f, 0f, 0.4f)), h, Dark(h, 0.75f), SW)
                .Add(Rot(Ellipse(-0.27f, -0.2f, 0.09f, 0.16f), 30, -0.22f, -0.12f), Color.FromArgb(150, 255, 255, 255));
        }

        static Picture IceCream()
        {
            Color cone = C(0xE8B86D), coneDark = C(0xB8863B);
            return new Picture()
                .Add(Poly(-0.21f, -0.02f, 0.21f, -0.02f, 0f, 0.47f), cone, coneDark, SW)
                .Line(Lines(-0.13f, 0.06f, 0.07f, 0.3f), coneDark, 0.015f)
                .Line(Lines(0.13f, 0.06f, -0.07f, 0.3f), coneDark, 0.015f)
                .Add(Circle(0f, -0.08f, 0.21f), C(0xF7A1C4), C(0xD4709A), SW)
                .Add(Circle(0f, -0.28f, 0.17f), C(0x9BE3C1), C(0x5DB58D), SW)
                .Add(Circle(0.02f, -0.46f, 0.05f), Red, RedDark, 0.012f);
        }

        static Picture Jar()
        {
            Color honey = C(0xF5A623);
            return new Picture()
                .Add(RRect(-0.3f, -0.25f, 0.6f, 0.68f, 0.14f), honey, Dark(honey, 0.7f), SW)
                .Add(RRect(-0.26f, -0.41f, 0.52f, 0.17f, 0.04f), Brown, BrownDark, SW)
                .Add(RRect(-0.2f, -0.01f, 0.4f, 0.22f, 0.04f), C(0xFFF1D6), Dark(honey, 0.7f), 0.012f)
                .Add(Merge(Circle(-0.04f, 0.1f, 0.05f), Circle(0.04f, 0.1f, 0.05f), Poly(-0.085f, 0.115f, 0.085f, 0.115f, 0f, 0.18f)), C(0xF2547D))
                .Add(RRect(-0.24f, -0.18f, 0.06f, 0.2f, 0.03f), Color.FromArgb(130, 255, 255, 255));
        }

        static Picture Leaf()
        {
            return new Picture()
                .Add(Rot(Curve(0.5f, 0f, -0.45f, 0.25f, -0.15f, 0.2f, 0.2f, 0f, 0.36f, -0.2f, 0.2f, -0.25f, -0.15f), 25, 0, 0), Green, GreenDark, SW)
                .Line(Rot(Lines(0f, -0.36f, 0f, 0.47f), 25, 0, 0), GreenDark, 0.025f);
        }

        static Picture Moon()
        {
            var region = new Region(Circle(0f, 0f, 0.4f));
            using (var cut = Circle(0.2f, -0.12f, 0.33f)) region.Exclude(cut);
            var pic = new Picture();
            pic.Line(Circle(0f, 0f, 0.4f), Color.Transparent, 0.001f); // bounds only (invisible)
            pic.Add(region, C(0xFFD84D));
            pic.Add(Star(0.3f, 0.26f, 0.08f), C(0xFFE98A));
            pic.Add(Star(0.36f, -0.36f, 0.06f), C(0xFFE98A));
            return pic;
        }

        static Picture Note()
        {
            Color n = C(0x7768AE);
            return new Picture().Add(Merge(
                Rot(Ellipse(-0.38f, 0.18f, 0.26f, 0.18f), -20, -0.25f, 0.27f),
                Rot(Ellipse(0.06f, 0.1f, 0.26f, 0.18f), -20, 0.19f, 0.19f),
                RRect(-0.17f, -0.35f, 0.055f, 0.64f, 0.02f),
                RRect(0.265f, -0.43f, 0.055f, 0.64f, 0.02f),
                Poly(-0.17f, -0.35f, 0.32f, -0.43f, 0.32f, -0.3f, -0.17f, -0.22f)), n, Dark(n, 0.7f), SW);
        }

        static Picture Owl()
        {
            Color body = C(0xA9744F), belly = C(0xF0D9B5), dark = C(0x6E4A30), beak = C(0xF5A623);
            return new Picture()
                .Add(Merge(Circle(-0.1f, 0.42f, 0.05f), Circle(0.1f, 0.42f, 0.05f)), beak)
                .Add(Merge(Ellipse(-0.33f, -0.36f, 0.66f, 0.8f), Poly(-0.31f, -0.2f, -0.28f, -0.48f, -0.1f, -0.33f), Poly(0.31f, -0.2f, 0.28f, -0.48f, 0.1f, -0.33f)), body, dark, SW)
                .Add(Ellipse(-0.21f, -0.04f, 0.42f, 0.44f), belly)
                .Add(Merge(Circle(-0.13f, -0.16f, 0.12f), Circle(0.13f, -0.16f, 0.12f)), White, dark, 0.012f)
                .Add(Merge(Circle(-0.11f, -0.15f, 0.06f), Circle(0.11f, -0.15f, 0.06f)), C(0x2B2B2B))
                .Add(Merge(Circle(-0.09f, -0.18f, 0.018f), Circle(0.13f, -0.18f, 0.018f)), White)
                .Add(Poly(-0.05f, -0.06f, 0.05f, -0.06f, 0f, 0.04f), beak)
                .Line(Lines(-0.1f, 0.12f, -0.05f, 0.16f, 0f, 0.12f, 0.05f, 0.16f, 0.1f, 0.12f), C(0xD7B58C), 0.02f);
        }

        static GraphicsPath ShieldPath(bool leftHalfOnly)
        {
            var p = NP();
            if (leftHalfOnly)
            {
                p.AddLine(-0.36f, -0.4f, 0f, -0.4f);
                p.AddLine(0f, -0.4f, 0f, 0.47f);
                p.AddBezier(0f, 0.47f, -0.15f, 0.38f, -0.36f, 0.25f, -0.36f, 0f);
            }
            else
            {
                p.AddLine(-0.36f, -0.4f, 0.36f, -0.4f);
                p.AddLine(0.36f, -0.4f, 0.36f, 0f);
                p.AddBezier(0.36f, 0f, 0.36f, 0.25f, 0.15f, 0.38f, 0f, 0.47f);
                p.AddBezier(0f, 0.47f, -0.15f, 0.38f, -0.36f, 0.25f, -0.36f, 0f);
            }
            p.CloseFigure();
            return p;
        }

        static Picture Shield()
        {
            return new Picture()
                .Add(ShieldPath(false), C(0x3B6FD4), GoldDark, 0.03f)
                .Add(ShieldPath(true), Red)
                .Line(ShieldPath(false), Gold, 0.04f)
                .Add(Star(0f, -0.02f, 0.17f), Gold, GoldDark, 0.012f);
        }

        static Picture Tree()
        {
            return new Picture()
                .Add(RRect(-0.07f, 0.05f, 0.14f, 0.42f, 0.03f), Brown, BrownDark, SW)
                .Add(Merge(Circle(0f, -0.2f, 0.26f), Circle(-0.2f, 0f, 0.2f), Circle(0.2f, 0f, 0.2f), Circle(0f, 0.05f, 0.2f)), Green, GreenDark, SW)
                .Add(Merge(Circle(-0.12f, -0.18f, 0.045f), Circle(0.14f, -0.05f, 0.045f), Circle(-0.18f, 0.06f, 0.045f), Circle(0.06f, -0.3f, 0.045f)), Red);
        }

        static Picture Umbrella()
        {
            var handle = NP();
            handle.AddLine(0f, 0.0f, 0f, 0.36f);
            handle.AddArc(-0.14f, 0.29f, 0.14f, 0.14f, 0, 180);
            var canopy = NP(); canopy.AddPie(-0.43f, -0.42f, 0.86f, 0.86f, 180, 180);
            var mid = NP(); mid.AddPie(-0.43f, -0.42f, 0.86f, 0.86f, 240, 60);
            return new Picture()
                .Line(handle, BrownDark, 0.045f)
                .Add(canopy, Red, RedDark, SW)
                .Add(mid, C(0xFFD23F))
                .Add(Circle(0f, -0.44f, 0.035f), BrownDark);
        }

        static Picture Vase()
        {
            return new Picture()
                .Line(Lines(0f, -0.2f, -0.17f, -0.38f), GreenDark, 0.03f)
                .Line(Lines(0f, -0.2f, 0f, -0.44f), GreenDark, 0.03f)
                .Line(Lines(0f, -0.2f, 0.17f, -0.38f), GreenDark, 0.03f)
                .Add(Merge(Circle(-0.17f, -0.39f, 0.075f), Circle(0.17f, -0.39f, 0.075f)), C(0xF2547D))
                .Add(Circle(0f, -0.45f, 0.075f), C(0xFFD23F))
                .Add(Merge(Circle(-0.17f, -0.39f, 0.03f), Circle(0.17f, -0.39f, 0.03f), Circle(0f, -0.45f, 0.03f)), White)
                .Add(Curve(0.4f, -0.1f, -0.22f, 0.1f, -0.22f, 0.12f, -0.1f, 0.28f, 0.12f, 0.2f, 0.42f, -0.2f, 0.42f, -0.28f, 0.12f, -0.12f, -0.1f), Blue, BlueDark, SW)
                .Line(Lines(-0.25f, 0.14f, 0.25f, 0.14f), Gold, 0.035f)
                .Add(RRect(-0.15f, -0.26f, 0.3f, 0.07f, 0.03f), Blue, BlueDark, SW);
        }

        static Picture Wand()
        {
            return new Picture()
                .Line(Lines(-0.32f, 0.44f, 0.1f, -0.08f), BrownDark, 0.07f)
                .Line(Lines(-0.32f, 0.44f, 0.1f, -0.08f), C(0x7768AE), 0.045f)
                .Add(Star(0.17f, -0.2f, 0.26f), Gold, GoldDark, SW)
                .Add(Star(-0.25f, -0.3f, 0.07f), C(0xF7A1C4))
                .Add(Star(0.38f, 0.18f, 0.06f), C(0x9FD0FA))
                .Add(Star(-0.05f, -0.45f, 0.05f), C(0x9BE3C1));
        }

        static Picture XMark()
        {
            Color parch = C(0xF3DFB4), parchDark = C(0xC9A66B);
            var bar1 = Move(Rot(RRect(-0.17f, -0.045f, 0.34f, 0.09f, 0.045f), 45, 0, 0), 0.12f, -0.1f);
            var bar2 = Move(Rot(RRect(-0.17f, -0.045f, 0.34f, 0.09f, 0.045f), -45, 0, 0), 0.12f, -0.1f);
            return new Picture()
                .Add(RRect(-0.44f, -0.36f, 0.88f, 0.72f, 0.06f), parch, parchDark, SW)
                .Add(Merge(Circle(-0.3f, 0.24f, 0.022f), Circle(-0.22f, 0.18f, 0.022f), Circle(-0.15f, 0.1f, 0.022f), Circle(-0.06f, 0.06f, 0.022f), Circle(0.0f, -0.01f, 0.022f)), C(0x8B5A2B))
                .Add(Merge(bar1, bar2), Red, RedDark, 0.012f);
        }

        static Picture YoYo()
        {
            return new Picture()
                .Line(Lines(0f, -0.42f, 0f, 0.0f), C(0x888888), 0.018f)
                .Line(Circle(0f, -0.45f, 0.04f), C(0x888888), 0.018f)
                .Add(Circle(0f, 0.12f, 0.34f), Red, RedDark, SW)
                .Add(Circle(0f, 0.12f, 0.22f), C(0xFF9F1C))
                .Add(Circle(0f, 0.12f, 0.07f), White);
        }

        static Picture Zigzag()
        {
            Color y = C(0xFFD23F);
            return new Picture().Add(Poly(0.06f, -0.47f, -0.25f, 0.05f, -0.02f, 0.05f, -0.12f, 0.47f, 0.27f, -0.1f, 0.03f, -0.1f, 0.18f, -0.47f), y, Dark(y, 0.75f), SW);
        }

        static Picture Crown()
        {
            return new Picture()
                .Add(Poly(-0.38f, 0.28f, 0.38f, 0.28f, 0.42f, -0.22f, 0.2f, 0.0f, 0f, -0.3f, -0.2f, 0.0f, -0.42f, -0.22f), Gold, GoldDark, SW)
                .Add(RRect(-0.38f, 0.16f, 0.76f, 0.12f, 0.03f), GoldDark)
                .Add(Circle(0f, 0.22f, 0.045f), Red)
                .Add(Circle(-0.22f, 0.22f, 0.04f), Blue)
                .Add(Circle(0.22f, 0.22f, 0.04f), Green)
                .Add(Merge(Circle(-0.42f, -0.26f, 0.055f), Circle(0f, -0.34f, 0.055f), Circle(0.42f, -0.26f, 0.055f)), Gold, GoldDark, 0.012f);
        }

        static Picture StarPic()
        {
            return new Picture()
                .Add(Star(0f, 0.03f, 0.47f), Gold, GoldDark, SW)
                .Add(Star(-0.1f, -0.06f, 0.1f), Color.FromArgb(150, 255, 255, 255));
        }

        static Picture Sword()
        {
            Color blade = C(0xDDE3EA), bladeDark = C(0x8A96A3);
            return new Picture()
                .Add(Rot(Poly(-0.06f, 0.12f, -0.06f, -0.36f, 0f, -0.48f, 0.06f, -0.36f, 0.06f, 0.12f), 30, 0, 0), blade, bladeDark, SW)
                .Line(Rot(Lines(0f, -0.38f, 0f, 0.1f), 30, 0, 0), bladeDark, 0.015f)
                .Add(Rot(RRect(-0.04f, 0.17f, 0.08f, 0.2f, 0.02f), 30, 0, 0), Brown, BrownDark, 0.012f)
                .Add(Rot(RRect(-0.22f, 0.1f, 0.44f, 0.08f, 0.04f), 30, 0, 0), Gold, GoldDark, 0.012f)
                .Add(Rot(Circle(0f, 0.41f, 0.06f), 30, 0, 0), Gold, GoldDark, 0.012f);
        }
    }

    // ------------------------------------------------------------------ Content
    class Item
    {
        public string Name, Art, Fact; public int Piece = -1;
        public Item(string name, string art, string fact) { Name = name; Art = art; Fact = fact; }
    }

    static class Content
    {
        public const int KING = 0, QUEEN = 1, ROOK = 2, BISHOP = 3, KNIGHT = 4, PAWN = 5;
        public static readonly string[] PieceNames = { "King", "Queen", "Rook", "Bishop", "Knight", "Pawn" };
        public static readonly string[] PieceFacts = {
            "The King moves one step in any direction",
            "The Queen moves any direction, as far as she likes",
            "The Rook moves in straight lines",
            "The Bishop moves diagonally",
            "The Knight jumps in an L shape",
            "The Pawn marches forward",
        };
        // little tune (scale steps) for each piece
        public static readonly int[][] PieceTunes = {
            new[] { 0, 3, 5 }, new[] { 5, 7, 8, 10 }, new[] { 3, 3, 5 }, new[] { 4, 6, 8 }, new[] { 5, 3, 4 }, new[] { 2, 3 }
        };

        public static readonly Item[] Treasures = {
            new Item("Crown", "crown", "A golden crown for the King"),
            new Item("Shield", "shield", "A shield for a brave knight"),
            new Item("Castle", "castle", "A castle with tall towers"),
            new Item("Gem", "gem", "A shiny blue gem"),
            new Item("Star", "star", "A twinkly star"),
            new Item("Sword", "sword", "A knight's sword"),
            new Item("Bell", "bell", "Ding dong, the castle bell"),
            new Item("Heart", "heart", "A big red heart"),
        };

        // A..Z
        public static readonly Item[] Letters = {
            new Item("Apple", "apple", null),
            PieceItem("Bishop", BISHOP),
            new Item("Castle", "castle", null),
            new Item("Drum", "drum", null),
            new Item("Egg", "egg", null),
            new Item("Flag", "flag", null),
            new Item("Gem", "gem", null),
            new Item("Heart", "heart", null),
            new Item("Ice cream", "icecream", null),
            new Item("Jar", "jar", null),
            PieceItem("King", KING),
            new Item("Leaf", "leaf", null),
            new Item("Moon", "moon", null),
            new Item("Note", "note", null),
            new Item("Owl", "owl", null),
            PieceItem("Pawn", PAWN),
            PieceItem("Queen", QUEEN),
            PieceItem("Rook", ROOK),
            new Item("Shield", "shield", null),
            new Item("Tree", "tree", null),
            new Item("Umbrella", "umbrella", null),
            new Item("Vase", "vase", null),
            new Item("Wand", "wand", null),
            new Item("X marks the spot", "xmark", null),
            new Item("Yo-yo", "yoyo", null),
            new Item("Zigzag", "zigzag", null),
        };

        static Item PieceItem(string name, int p) { var it = new Item(name, null, null); it.Piece = p; return it; }

        public static readonly string[] NumberWords = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };

        public static readonly Color[] Confetti = {
            Color.FromArgb(242, 95, 92), Color.FromArgb(255, 159, 28), Color.FromArgb(255, 210, 63), Color.FromArgb(59, 178, 115),
            Color.FromArgb(77, 157, 224), Color.FromArgb(119, 104, 174), Color.FromArgb(231, 111, 176), Color.White
        };
    }

    // ------------------------------------------------------------------ Scene objects
    class Sprite
    {
        public Picture Pic; public float Size; // pixels per unit
        public float X, Y, VX, VY, Rot, Wobble, Phase;
        public float Born, Life = 5f;
        public bool Fixed; // pinned to a chess square
        public float HalfW { get { return Pic.Bounds.Width * Size / 2f; } }
        public float HalfH { get { return Pic.Bounds.Height * Size / 2f; } }
    }

    class Particle { public float X, Y, VX, VY, Age, Life, Size; public Color C; }

    class MoveHint { public int Col, Row; public List<Point> Targets = new List<Point>(); public float Born, Life = 4.5f; }

    // ------------------------------------------------------------------ Scene (update + draw)
    class Scene
    {
        public int W, H;
        public float Time;
        public Action<int, float, float> Play = delegate { }; // (scale step, delay seconds, gain)

        readonly List<Sprite> sprites = new List<Sprite>();
        readonly List<Particle> parts = new List<Particle>();
        MoveHint hint;
        readonly Random rnd;              // layout: same seed on every screen, so all screens match
        readonly Random fx = new Random(); // cosmetic only (particles), may differ per screen

        string caption; float captionBorn = -100;
        float lastPress = -100;
        float mouseX = -100, mouseY = -100, lastTrailX, lastTrailY;
        int trailHue;

        float sq, boardX, boardY, frame;
        Bitmap background;

        public Scene(int w, int h, int seed) { rnd = new Random(seed); Resize(w, h); }

        public void Resize(int w, int h)
        {
            W = w; H = h;
            float avail = Math.Min(w, h);
            frame = avail * 0.04f;
            sq = (float)Math.Floor((avail * 0.94f - 2 * frame) / 8f);
            boardX = (float)Math.Floor((w - 8 * sq) / 2f);
            boardY = (float)Math.Floor((h - 8 * sq) / 2f);
            if (background != null) background.Dispose();
            background = BuildBackground();
        }

        bool Deliberate() { bool d = Time - lastPress > 1.6f; lastPress = Time; return d; }

        void Tune(int[] steps, float spacing, float gain)
        {
            for (int i = 0; i < steps.Length; i++) Play(steps[i], i * spacing, gain);
        }

        public void Welcome() { Tune(new[] { 0, 2, 3, 5, 7, 8, 10 }, 0.12f, 0.5f); }

        // ---------------------------------------------------------- input
        public void OnKey(int vk)
        {
            bool deliberate = Deliberate();
            if (vk >= 0x41 && vk <= 0x5A) SpawnLetter((char)vk);
            else if (vk >= 0x30 && vk <= 0x39) SpawnNumber(vk - 0x30);
            else if (vk >= 0x60 && vk <= 0x69) SpawnNumber(vk - 0x60);
            else if (deliberate && rnd.Next(3) > 0) PlacePieceWithMoves(rnd.Next(8), rnd.Next(8), rnd.Next(6), rnd.Next(2) == 0, true);
            else SpawnRandom(deliberate);
        }

        public void OnMouseMove(int x, int y)
        {
            mouseX = x; mouseY = y;
            float dx = x - lastTrailX, dy = y - lastTrailY;
            if (dx * dx + dy * dy > 100)
            {
                lastTrailX = x; lastTrailY = y;
                trailHue = (trailHue + 1) % (Content.Confetti.Length - 1);
                AddParticle(x, y, 30, Content.Confetti[trailHue], 0.8f, H * 0.008f, 0.2f);
            }
        }

        public void OnClick(int x, int y, bool right)
        {
            int col, row;
            if (BoardCell(x, y, out col, out row)) ClickSquare(col, row, !right);
            else ClickOutside(x, y);
        }

        public bool BoardCell(int x, int y, out int col, out int row)
        {
            col = (int)Math.Floor((x - boardX) / sq); row = (int)Math.Floor((y - boardY) / sq);
            return col >= 0 && col < 8 && row >= 0 && row < 8;
        }

        public void ClickSquare(int col, int row, bool white)
        {
            bool deliberate = Deliberate();
            PlacePieceWithMoves(col, row, rnd.Next(6), white, deliberate);
        }

        public void ClickOutside(int x, int y)
        {
            Deliberate();
            SpawnTreasure(Content.Treasures[rnd.Next(Content.Treasures.Length)], x, y);
        }

        public void OnWheel(int x, int y)
        {
            Burst(x, y, 10);
            Play(7 + fx.Next(5), 0, 0.3f);
        }

        // ---------------------------------------------------------- spawning
        PointF RandomSpot(float halfW, float halfH)
        {
            float x = halfW + (float)rnd.NextDouble() * Math.Max(1, W - 2 * halfW);
            float y = halfH + (float)rnd.NextDouble() * Math.Max(1, H * 0.8f - 2 * halfH);
            return new PointF(x, y);
        }

        Sprite NewSprite(Picture pic, float size, float x, float y)
        {
            return new Sprite
            {
                Pic = pic, Size = size, X = x, Y = y, Born = Time,
                VX = ((float)rnd.NextDouble() - 0.5f) * H * 0.02f,
                VY = -H * (0.006f + (float)rnd.NextDouble() * 0.015f),
                Wobble = 3 + (float)rnd.NextDouble() * 4, Phase = (float)(rnd.NextDouble() * 6.28),
                Life = 4.5f + (float)rnd.NextDouble() * 1.5f
            };
        }

        static Picture PiecePic(int piece, bool white) { return Art.Get("piece:" + piece + (white ? "w" : "b")); }

        void SpawnLetter(char c)
        {
            Item it = Content.Letters[c - 'A'];
            int color = rnd.Next(Art.BlockColors.Length);
            float size = H * 0.24f;
            var block = NewSprite(Art.Get("block:" + c + ":" + color), size, 0, 0);
            var pic = NewSprite(it.Piece >= 0 ? PiecePic(it.Piece, rnd.Next(2) == 0) : Art.Get(it.Art), size * 1.05f, 0, 0);

            float gap = size * 0.12f, total = block.HalfW * 2 + gap + pic.HalfW * 2;
            PointF p = RandomSpot(total / 2f + W * 0.02f, Math.Max(block.HalfH, pic.HalfH) + H * 0.03f);
            block.X = p.X - total / 2f + block.HalfW; block.Y = p.Y;
            pic.X = p.X + total / 2f - pic.HalfW; pic.Y = p.Y;
            pic.VX = block.VX; pic.VY = block.VY; pic.Phase = block.Phase + 1;
            pic.Born = Time + 0.12f;
            Add(block); Add(pic);

            Burst(p.X, p.Y, 18);
            SetCaption(it.Name.StartsWith("X ") ? "X marks the spot!" : c + " is for " + it.Name);
            int step = (c - 'A') % 8;
            Tune(new[] { step, step + 2, step + 4 }, 0.1f, 0.55f);
        }

        void SpawnNumber(int n)
        {
            int color = rnd.Next(Art.BlockColors.Length);
            float size = H * 0.2f;
            float pawnSize = Math.Min(H * 0.17f, W * 0.85f / Math.Max(1, n) / 0.66f);
            float pawnW = pawnSize * 0.66f;
            float rowW = pawnW * n;
            float cy = H * 0.18f + (float)rnd.NextDouble() * H * 0.2f;
            float cx = W / 2f + ((float)rnd.NextDouble() - 0.5f) * Math.Max(0, W - Math.Max(rowW, size) - W * 0.1f);

            var block = NewSprite(Art.Get("block:" + n + ":" + color), size, cx, cy);
            block.VX = 0; block.VY = 0;
            Add(block);
            bool white = rnd.Next(2) == 0;
            for (int i = 0; i < n; i++)
            {
                var s = NewSprite(PiecePic(Content.PAWN, white), pawnSize, cx - rowW / 2f + pawnW * (i + 0.5f), cy + size * 0.5f + pawnSize * 0.62f);
                s.Born = Time + 0.45f + i * 0.4f;
                s.Life = 5.5f;
                s.VX = 0; s.VY = 0; s.Wobble = 3;
                Add(s);
                Play(i, 0.45f + i * 0.4f, 0.5f); // one note per pawn: counting with music
            }
            Burst(cx, cy, 18);
            Play(0, 0, 0.5f);
            SetCaption(n == 0 ? "0 - zero pawns" : n + " - " + Content.NumberWords[n] + (n == 1 ? " pawn" : " pawns"));
        }

        void SpawnRandom(bool deliberate)
        {
            if (rnd.Next(10) < 6)
            {
                int p = rnd.Next(6);
                var s = NewSprite(PiecePic(p, rnd.Next(2) == 0), H * 0.22f, 0, 0);
                PointF pt = RandomSpot(s.HalfW + 10, s.HalfH + 10);
                s.X = pt.X; s.Y = pt.Y;
                Add(s);
                Burst(pt.X, pt.Y, 16);
                SetCaption(deliberate ? Content.PieceFacts[p] : "The " + Content.PieceNames[p]);
                Tune(Content.PieceTunes[p], 0.11f, 0.5f);
            }
            else
            {
                PointF pt = RandomSpot(H * 0.12f, H * 0.12f);
                SpawnTreasure(Content.Treasures[rnd.Next(Content.Treasures.Length)], pt.X, pt.Y);
            }
        }

        void SpawnTreasure(Item it, float x, float y)
        {
            Add(NewSprite(Art.Get(it.Art), H * 0.2f, x, y));
            Burst(x, y, 18);
            SetCaption(it.Fact);
            int s = rnd.Next(6);
            Tune(new[] { s, s + 5 }, 0.09f, 0.45f);
        }

        // Put a piece on a real square and light up the squares it can move to.
        void PlacePieceWithMoves(int col, int row, int piece, bool white, bool deliberate)
        {
            if (piece == Content.PAWN) row = white ? Math.Max(1, row) : Math.Min(6, row);
            foreach (var old in sprites) if (old.Fixed) old.Life = Math.Min(old.Life, Time - old.Born + 0.3f);

            float cx = boardX + (col + 0.5f) * sq, cy = boardY + (row + 0.5f) * sq;
            var s = NewSprite(PiecePic(piece, white), sq * 0.85f, cx, cy);
            s.Fixed = true; s.VX = s.VY = 0; s.Wobble = 0; s.Life = 4.5f;
            Add(s);

            hint = new MoveHint { Col = col, Row = row, Born = Time };
            hint.Targets = Moves(piece, col, row, white);
            Burst(cx, cy, 14);

            string square = "" + (char)('a' + col) + (8 - row);
            SetCaption(deliberate ? Content.PieceFacts[piece] : Content.PieceNames[piece] + " on " + square);
            Tune(Content.PieceTunes[piece], 0.11f, 0.5f);
        }

        static List<Point> Moves(int piece, int c, int r, bool white)
        {
            var list = new List<Point>();
            Action<int, int, bool> ray = (dc, dr, slide) =>
            {
                int x = c + dc, y = r + dr;
                while (x >= 0 && x < 8 && y >= 0 && y < 8) { list.Add(new Point(x, y)); if (!slide) break; x += dc; y += dr; }
            };
            int[,] orth = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
            int[,] diag = { { 1, 1 }, { -1, 1 }, { 1, -1 }, { -1, -1 } };
            switch (piece)
            {
                case Content.KING:
                    for (int i = 0; i < 4; i++) { ray(orth[i, 0], orth[i, 1], false); ray(diag[i, 0], diag[i, 1], false); }
                    break;
                case Content.QUEEN:
                    for (int i = 0; i < 4; i++) { ray(orth[i, 0], orth[i, 1], true); ray(diag[i, 0], diag[i, 1], true); }
                    break;
                case Content.ROOK: for (int i = 0; i < 4; i++) ray(orth[i, 0], orth[i, 1], true); break;
                case Content.BISHOP: for (int i = 0; i < 4; i++) ray(diag[i, 0], diag[i, 1], true); break;
                case Content.KNIGHT:
                    int[,] k = { { 1, 2 }, { 2, 1 }, { -1, 2 }, { -2, 1 }, { 1, -2 }, { 2, -1 }, { -1, -2 }, { -2, -1 } };
                    for (int i = 0; i < 8; i++) ray(k[i, 0], k[i, 1], false);
                    break;
                case Content.PAWN:
                    int dir = white ? -1 : 1, start = white ? 6 : 1;
                    if (r + dir >= 0 && r + dir < 8) list.Add(new Point(c, r + dir));
                    if (r == start) list.Add(new Point(c, r + 2 * dir));
                    break;
            }
            return list;
        }

        void Add(Sprite s)
        {
            sprites.Add(s);
            int alive = 0;
            for (int i = sprites.Count - 1; i >= 0; i--)
                if (++alive > 26) { var o = sprites[i]; o.Life = Math.Min(o.Life, Math.Max(0.01f, Time - o.Born) + 0.35f); }
        }

        void Burst(float x, float y, int n)
        {
            for (int i = 0; i < n; i++)
                AddParticle(x, y, H * 0.4f, Content.Confetti[fx.Next(Content.Confetti.Length)], 0.9f + (float)fx.NextDouble() * 0.6f, H * 0.009f, 1f);
        }

        void AddParticle(float x, float y, float speed, Color c, float life, float size, float gravity)
        {
            if (parts.Count > 600) return;
            double a = fx.NextDouble() * Math.PI * 2, v = speed * (0.3 + fx.NextDouble() * 0.7);
            parts.Add(new Particle { X = x, Y = y, VX = (float)(Math.Cos(a) * v), VY = (float)(Math.Sin(a) * v) - speed * 0.3f * gravity, Life = life, Size = size * (0.6f + (float)fx.NextDouble() * 0.8f), C = c });
        }

        void SetCaption(string t) { caption = t; captionBorn = Time; }

        // ---------------------------------------------------------- update
        public void Update(float dt)
        {
            Time += dt;
            for (int i = sprites.Count - 1; i >= 0; i--)
            {
                var s = sprites[i];
                if (Time < s.Born) continue;
                if (Time - s.Born > s.Life) { sprites.RemoveAt(i); continue; }
                s.X += s.VX * dt; s.Y += s.VY * dt;
                s.Rot = (float)Math.Sin((Time - s.Born) * 2.2 + s.Phase) * s.Wobble;
            }
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i];
                p.Age += dt;
                if (p.Age > p.Life) { parts.RemoveAt(i); continue; }
                p.X += p.VX * dt; p.Y += p.VY * dt;
                p.VX *= 0.95f; p.VY = p.VY * 0.95f + H * 0.5f * dt;
            }
            if (hint != null && Time - hint.Born > hint.Life) hint = null;
        }

        // ---------------------------------------------------------- draw
        static void DrawPic(Graphics g, Picture pic, float x, float y, float size, float rot, int alpha)
        {
            var st = g.Save();
            g.TranslateTransform(x, y);
            if (rot != 0) g.RotateTransform(rot);
            g.ScaleTransform(size, size);
            pic.Draw(g, alpha);
            g.Restore(st);
        }

        Bitmap BuildBackground()
        {
            var bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                // sky
                using (var sky = new LinearGradientBrush(new Rectangle(0, 0, W, H), Art.C(0x7EC8F2), Art.C(0xE3F5FF), 90f))
                    g.FillRectangle(sky, 0, 0, W, H);

                // sun
                float sr = H * 0.075f, sx = W * 0.08f, sy = H * 0.13f;
                using (var glow = new SolidBrush(Color.FromArgb(70, 255, 236, 140))) g.FillEllipse(glow, sx - sr * 1.6f, sy - sr * 1.6f, sr * 3.2f, sr * 3.2f);
                using (var sun = new SolidBrush(Art.C(0xFFE066))) g.FillEllipse(sun, sx - sr, sy - sr, sr * 2, sr * 2);

                // clouds
                Cloud(g, W * 0.22f, H * 0.12f, H * 0.045f);
                Cloud(g, W * 0.78f, H * 0.08f, H * 0.04f);
                Cloud(g, W * 0.9f, H * 0.3f, H * 0.035f);
                Cloud(g, W * 0.1f, H * 0.38f, H * 0.03f);

                // hills
                using (var far = new SolidBrush(Art.C(0xA8DB8C)))
                {
                    g.FillEllipse(far, -W * 0.25f, H * 0.62f, W * 0.8f, H * 0.7f);
                    g.FillEllipse(far, W * 0.45f, H * 0.66f, W * 0.85f, H * 0.7f);
                }
                using (var near = new SolidBrush(Art.C(0x86C96B)))
                {
                    g.FillEllipse(near, -W * 0.1f, H * 0.8f, W * 0.7f, H * 0.5f);
                    g.FillEllipse(near, W * 0.5f, H * 0.78f, W * 0.7f, H * 0.5f);
                }

                // castle + trees on the hills (visible beside the board on wide screens)
                float side = boardX - frame;
                if (side > H * 0.15f)
                {
                    float cs = Math.Min(side * 0.85f, H * 0.32f);
                    DrawPic(g, Art.Get("castle"), side * 0.5f, H * 0.7f - cs * 0.2f, cs, 0, 255);
                    DrawPic(g, Art.Get("tree"), W - side * 0.62f, H * 0.72f - cs * 0.1f, cs * 0.75f, 0, 255);
                    DrawPic(g, Art.Get("tree"), W - side * 0.3f, H * 0.77f - cs * 0.1f, cs * 0.55f, 0, 255);
                }

                // board frame
                var fr = new RectangleF(boardX - frame, boardY - frame, 8 * sq + 2 * frame, 8 * sq + 2 * frame);
                using (var shadowPath = Art.RRect(fr.X + frame * 0.25f, fr.Y + frame * 0.35f, fr.Width, fr.Height, frame * 0.8f))
                using (var shadow = new SolidBrush(Color.FromArgb(50, 0, 40, 0)))
                    g.FillPath(shadow, shadowPath);
                using (var framePath = Art.RRect(fr.X, fr.Y, fr.Width, fr.Height, frame * 0.8f))
                using (var fb = new SolidBrush(Art.C(0xFFF8EC)))
                    g.FillPath(fb, framePath);

                // squares (rounded board)
                using (var boardPath = Art.RRect(boardX, boardY, 8 * sq, 8 * sq, frame * 0.35f))
                {
                    g.SetClip(boardPath);
                    using (var lightSq = new SolidBrush(Art.C(0xEEEED2)))
                    using (var darkSq = new SolidBrush(Art.C(0x8DB86B)))
                        for (int r = 0; r < 8; r++)
                            for (int c = 0; c < 8; c++)
                                g.FillRectangle((r + c) % 2 == 0 ? lightSq : darkSq, boardX + c * sq, boardY + r * sq, sq + 1, sq + 1);
                    g.ResetClip();
                }

                // a-h, 1-8
                using (var f = new Font(Art.UiFont, Math.Max(6, frame * 0.5f), FontStyle.Bold, GraphicsUnit.Pixel))
                using (var tb = new SolidBrush(Art.C(0xA08C74)))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    for (int i = 0; i < 8; i++)
                    {
                        string file = ((char)('a' + i)).ToString(), rank = (8 - i).ToString();
                        g.DrawString(file, f, tb, boardX + (i + 0.5f) * sq, boardY + 8 * sq + frame / 2f, sf);
                        g.DrawString(file, f, tb, boardX + (i + 0.5f) * sq, boardY - frame / 2f, sf);
                        g.DrawString(rank, f, tb, boardX - frame / 2f, boardY + (i + 0.5f) * sq, sf);
                        g.DrawString(rank, f, tb, boardX + 8 * sq + frame / 2f, boardY + (i + 0.5f) * sq, sf);
                    }
                }
            }
            return bmp;
        }

        static void Cloud(Graphics g, float x, float y, float s)
        {
            using (var b = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
            using (var p = new GraphicsPath(FillMode.Winding))
            {
                p.AddEllipse(x - s, y - s, 2 * s, 2 * s);
                p.AddEllipse(x + s * 0.4f, y - s * 0.6f, 1.8f * s, 1.8f * s);
                p.AddEllipse(x - s * 1.9f, y - s * 0.4f, 1.6f * s, 1.5f * s);
                p.AddEllipse(x + s * 1.3f, y - s * 0.2f, 1.5f * s, 1.3f * s);
                p.AddRectangle(new RectangleF(x - s * 1.2f, y, s * 3.6f, s * 0.9f));
                g.FillPath(b, p);
            }
        }

        public void Draw(Graphics g)
        {
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImageUnscaled(background, 0, 0);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (hint != null) { try { DrawHint(g); } catch { } }
            foreach (var s in sprites) if (Time >= s.Born) { try { DrawSprite(g, s); } catch { } }

            foreach (var p in parts)
            {
                int a = Clamp((int)(255 * (1 - p.Age / p.Life)));
                float r = p.Size * (1 - 0.4f * p.Age / p.Life);
                using (var b = new SolidBrush(Color.FromArgb(a, p.C))) g.FillEllipse(b, p.X - r, p.Y - r, 2 * r, 2 * r);
            }

            try { DrawCaption(g); } catch { }
            try { DrawStartHint(g); } catch { }

            if (mouseX > -50)
            {
                float r = H * 0.014f;
                using (var ring = new Pen(Content.Confetti[trailHue], r * 0.45f)) g.DrawEllipse(ring, mouseX - r, mouseY - r, 2 * r, 2 * r);
                using (var core = new SolidBrush(Color.White)) g.FillEllipse(core, mouseX - r * 0.75f, mouseY - r * 0.75f, r * 1.5f, r * 1.5f);
            }
        }

        void DrawHint(Graphics g)
        {
            float t = Time - hint.Born;
            float fade = Math.Max(0, Math.Min(1f, Math.Min(t * 4f, (hint.Life - t) * 2f)));
            using (var src = new SolidBrush(Color.FromArgb(Clamp((int)(160 * fade)), 255, 214, 80)))
                g.FillRectangle(src, boardX + hint.Col * sq, boardY + hint.Row * sq, sq, sq);
            float pulse = 0.88f + 0.12f * (float)Math.Sin(t * 6);
            for (int i = 0; i < hint.Targets.Count; i++)
            {
                float appear = Math.Max(0, Math.Min(1, (t - 0.25f - i * 0.06f) * 5f));
                if (appear <= 0) continue;
                var p = hint.Targets[i];
                float cx = boardX + (p.X + 0.5f) * sq, cy = boardY + (p.Y + 0.5f) * sq;
                float r = sq * 0.17f * pulse * Math.Max(0.05f, EaseOutBack(appear));
                using (var b = new SolidBrush(Color.FromArgb(Clamp((int)(230 * fade)), 255, 190, 60)))
                    g.FillEllipse(b, cx - r, cy - r, 2 * r, 2 * r);
                using (var pen = new Pen(Color.FromArgb(Clamp((int)(240 * fade)), 255, 255, 255), Math.Max(1.5f, sq * 0.03f)))
                    g.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
            }
        }

        void DrawSprite(Graphics g, Sprite s)
        {
            float age = Time - s.Born;
            float scale = age < 0.4f ? EaseOutBack(age / 0.4f) : 1f;
            float alpha = Math.Max(0, Math.Min(1, (s.Life - age) / 0.8f));
            int a = Clamp((int)(255 * alpha));
            if (a <= 0 || scale <= 0.02f) return;

            float k = s.Size * scale;
            var b = s.Pic.Bounds;
            using (var sh = new SolidBrush(Color.FromArgb(Clamp(a * 45 / 255), 0, 30, 0)))
                g.FillEllipse(sh, s.X + b.X * k * 0.8f, s.Y + b.Bottom * k - k * 0.03f, b.Width * k * 0.8f, k * 0.08f);
            DrawPic(g, s.Pic, s.X, s.Y, k, s.Rot, a);
        }

        void DrawCaption(Graphics g)
        {
            if (caption == null) return;
            float t = Time - captionBorn;
            const float life = 3.5f;
            if (t > life || t < 0) return;
            float alpha = Math.Min(1f, Math.Min(t * 6f, (life - t) * 2f));
            float pop = Math.Max(0.05f, t < 0.3f ? EaseOutBack(t / 0.3f) : 1f); // never scale 0 (invalid matrix)
            int a = Clamp((int)(255 * alpha));

            float fontPx = H * 0.055f;
            using (var probe = new Font(Art.UiFont, fontPx, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                SizeF sz0 = g.MeasureString(caption, probe);
                if (sz0.Width > W * 0.9f) fontPx *= W * 0.9f / sz0.Width;
            }
            using (var f = new Font(Art.UiFont, fontPx, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                SizeF sz = g.MeasureString(caption, f);
                float padX = fontPx * 0.8f, padY = fontPx * 0.3f;
                float w = sz.Width + padX * 2, h = sz.Height + padY * 2;
                float cx = W / 2f, cy = H - h * 0.5f - H * 0.025f;

                var st = g.Save();
                g.TranslateTransform(cx, cy);
                g.ScaleTransform(pop, pop);
                var rect = new RectangleF(-w / 2, -h / 2, w, h);
                using (var sp = Art.RRect(rect.X + 3, rect.Y + 5, rect.Width, rect.Height, h / 2))
                using (var sb = new SolidBrush(Color.FromArgb(Clamp(a * 50 / 255), 0, 30, 0)))
                    g.FillPath(sb, sp);
                using (var path = Art.RRect(rect.X, rect.Y, rect.Width, rect.Height, h / 2))
                using (var bg = new SolidBrush(Color.FromArgb(Clamp(a * 245 / 255), 255, 255, 255)))
                    g.FillPath(bg, path);
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var tb = new SolidBrush(Color.FromArgb(a, 58, 58, 90)))
                    g.DrawString(caption, f, tb, rect, sf);
                g.Restore(st);
            }
        }

        void DrawStartHint(Graphics g)
        {
            const float life = 8f;
            if (Time > life) return;
            int a = Clamp((int)(255 * Math.Min(1f, (life - Time) * 1.5f)));
            string text = "Parents: to exit, hold Ctrl + Alt and type Q U I T";
            using (var f = new Font(Art.UiFont, Math.Max(12, H * 0.024f), FontStyle.Bold, GraphicsUnit.Pixel))
            {
                SizeF sz = g.MeasureString(text, f);
                float h = sz.Height + 14;
                var rect = new RectangleF((W - sz.Width) / 2 - h * 0.6f, H * 0.012f, sz.Width + h * 1.2f, h);
                using (var path = Art.RRect(rect.X, rect.Y, rect.Width, rect.Height, h / 2))
                using (var bg = new SolidBrush(Color.FromArgb(Clamp(a * 235 / 255), 255, 255, 255)))
                    g.FillPath(bg, path);
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var tb = new SolidBrush(Color.FromArgb(a, 58, 58, 90)))
                    g.DrawString(text, f, tb, rect, sf);
            }
        }

        static float EaseOutBack(float x)
        {
            x = Math.Max(0, Math.Min(1, x));
            const float c1 = 1.70158f, c3 = c1 + 1;
            return 1 + c3 * (float)Math.Pow(x - 1, 3) + c1 * (float)Math.Pow(x - 1, 2);
        }

        static int Clamp(int a) { return a < 0 ? 0 : (a > 255 ? 255 : a); }

        // ---------------------------------------------------------- offline previews (no input lock)
        public static void Snapshot(string file)
        {
            var s = new Scene(1920, 1080, 1);
            s.OnKey('D'); s.Update(0.3f);
            s.OnKey('3'); s.Update(0.2f);
            s.OnClick((int)(s.boardX + 3.5f * s.sq), (int)(s.boardY + 4.5f * s.sq), false);
            s.OnKey('O');
            s.SpawnTreasure(Content.Treasures[0], 1700, 820);
            s.SpawnTreasure(Content.Treasures[1], 220, 860);
            for (int i = 0; i < 10; i++) { s.Update(0.2f); s.OnMouseMove(1500 + i * 15, 500 + i * 8); }
            using (var bmp = new Bitmap(s.W, s.H))
            using (var g = Graphics.FromImage(bmp))
            {
                s.Draw(g);
                bmp.Save(file, ImageFormat.Png);
            }
        }

        // Two different-sized screens fed identical events: they must show the same picture.
        public static void Mirror(string file)
        {
            var a = new Scene(1920, 1080, 42); var b = new Scene(1280, 1024, 42);
            var both = new[] { a, b };
            Action<int> key = vk => { foreach (var s in both) s.OnKey(vk); };
            Action<float> step = dt => { foreach (var s in both) s.Update(dt); };
            key('K'); step(0.3f); key('5'); step(0.3f);
            int col, row; a.BoardCell((int)(a.boardX + 2.5f * a.sq), (int)(a.boardY + 5.5f * a.sq), out col, out row);
            foreach (var s in both) s.ClickSquare(col, row, true);
            foreach (var s in both) s.ClickOutside((int)(0.9f * s.W), (int)(0.75f * s.H));
            key(0x20); for (int i = 0; i < 8; i++) step(0.2f);
            using (var bmp = new Bitmap(1920 + 1280, 1080))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                a.Draw(g);
                g.TranslateTransform(1920, 28);
                b.Draw(g);
                bmp.Save(file, ImageFormat.Png);
            }
        }

        public static void Gallery(string file)
        {
            var keys = new List<string>();
            for (int p = 0; p < 6; p++) { keys.Add("piece:" + p + "w"); keys.Add("piece:" + p + "b"); }
            keys.Add("block:A:0"); keys.Add("block:B:1"); keys.Add("block:G:2"); keys.Add("block:7:3"); keys.Add("block:W:4"); keys.Add("block:Q:5");
            foreach (var it in Content.Letters) if (it.Art != null && !keys.Contains(it.Art)) keys.Add(it.Art);
            foreach (var it in Content.Treasures) if (!keys.Contains(it.Art)) keys.Add(it.Art);
            int cols = 8, cell = 200, rows = (keys.Count + cols - 1) / cols;
            using (var bmp = new Bitmap(cols * cell, rows * cell))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Art.C(0xE3F5FF));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 0; i < keys.Count; i++)
                    DrawPic(g, Art.Get(keys[i]), (i % cols + 0.5f) * cell, (i / cols + 0.5f) * cell, cell * 0.85f, 0, 255);
                bmp.Save(file, ImageFormat.Png);
            }
        }
    }
}

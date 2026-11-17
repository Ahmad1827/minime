// Little Knight - a medieval / chess "baby smash" app for Windows.
// Blocks keyboard shortcuts (Win key, Alt+Tab, Alt+F4, Ctrl+Esc...) and covers every screen.
// PARENT EXIT: hold Ctrl + Alt and type  Q U I T
//
// Build (no install needed, uses the C# compiler that ships with Windows): see build.bat / build.sh
// Written for C# 5 (the csc.exe bundled with .NET Framework 4.x).
// PARENT: Ctrl + Alt + N switches to the next painted scene (they also change by themselves every few minutes).
// Preview without locking anything:  LittleKnight.exe --snapshot out.png   /   --gallery out.png   /   --themes prefix

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
            if (args.Length >= 2 && args[0] == "--themes") { Scene.Themes(args[1]); return; }

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
        readonly int startTheme = new Random().Next(Scene.ThemeNames.Length);
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
                var f = new ScreenForm(this, sc.Bounds, seed, forms.Count > 0 ? forms[0].Scene.Theme : CurrentTheme());
                f.Scene.Time = time;
                forms.Add(f);
            }
            if (forms.Count > 0 && harp != null) forms[0].Scene.Play = harp.Play;
            foreach (var f in forms) f.Show();
            if (forms.Count > 0) { forms[0].Activate(); Native.SetForegroundWindow(forms[0].Handle); }
        }

        int lastTheme = -1;
        int CurrentTheme() { return lastTheme >= 0 ? lastTheme : startTheme; }

        void Tick()
        {
            double now = clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.05, now - lastFrame);
            lastFrame = now;
            foreach (var f in forms) { try { f.Scene.Update(dt); } catch { } f.Invalidate(); }
            if (forms.Count > 0) lastTheme = forms[0].Scene.Theme;
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
            if (vk == 'N' && CtrlAltDown())
            {
                foreach (var f in forms) { try { f.Scene.NextTheme(); } catch { } }
                return;
            }
            foreach (var f in forms) { try { f.Scene.OnKey(vk); } catch { } }
        }

        bool CtrlAltDown()
        {
            bool ctrl = false, alt = false;
            foreach (int m in modsDown) { if (IsCtrl(m)) ctrl = true; if (IsAlt(m)) alt = true; }
            return ctrl && alt;
        }

        void CheckUnlock(int vk, double now)
        {
            if (!CtrlAltDown()) { unlockIndex = 0; return; }

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

        public ScreenForm(LockController owner, Rectangle bounds, int seed, int theme)
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
            Scene = new Scene(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height), seed, theme);
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

        public static FontFamily Find(params string[] names)
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

        public Scene(int w, int h, int seed, int theme = 0) { rnd = new Random(seed); Theme = WrapTheme(theme); Resize(w, h); }

        public void Resize(int w, int h)
        {
            W = w; H = h;
            float avail = Math.Min(w, h);
            frame = avail * 0.04f;
            sq = (float)Math.Floor((avail * 0.94f - 2 * frame) / 8f);
            boardX = (float)Math.Floor((w - 8 * sq) / 2f);
            boardY = (float)Math.Floor((h - 8 * sq) / 2f);
            if (background != null) background.Dispose();
            if (oldBackground != null) { oldBackground.Dispose(); oldBackground = null; }
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
            if (Time - themeSince > ThemeSeconds) NextTheme();
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

        // ---------------------------------------------------------- painted medieval dusk background
        static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                                  (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        static float[] MakeRidge(Random r, int k, float rough)
        {
            int n = (1 << k) + 1;
            var h = new float[n];
            h[0] = (float)r.NextDouble(); h[n - 1] = (float)r.NextDouble();
            float amp = 1f;
            for (int step = n - 1; step > 1; step /= 2)
            {
                for (int i = step / 2; i < n; i += step)
                    h[i] = (h[i - step / 2] + h[i + step / 2]) / 2 + (float)(r.NextDouble() - 0.5) * amp;
                amp *= rough;
            }
            float mn = float.MaxValue, mx = float.MinValue;
            foreach (var v in h) { mn = Math.Min(mn, v); mx = Math.Max(mx, v); }
            for (int i = 0; i < n; i++) h[i] = (h[i] - mn) / Math.Max(0.0001f, mx - mn);
            return h;
        }

        float RidgeY(float[] h, float baseY, float amp, float x)
        {
            float f = Math.Max(0, Math.Min(h.Length - 1.001f, x / W * (h.Length - 1)));
            int i = (int)f; float t = f - i;
            return baseY - (h[i] * (1 - t) + h[i + 1] * t) * amp;
        }

        void FillRidge(Graphics g, float[] h, float baseY, float amp, Color top, Color bottom)
        {
            var pts = new PointF[h.Length + 2];
            for (int i = 0; i < h.Length; i++) pts[i] = new PointF(W * i / (float)(h.Length - 1), baseY - h[i] * amp);
            pts[h.Length] = new PointF(W, H + 2); pts[h.Length + 1] = new PointF(0, H + 2);
            using (var b = new LinearGradientBrush(new RectangleF(0, baseY - amp - 1, W, H - baseY + amp + 3), top, bottom, 90f))
                g.FillPolygon(b, pts);
        }

        void Mist(Graphics g, float y, float thick, Color c)
        {
            var rect = new RectangleF(0, y - thick, W, thick * 2);
            using (var b = new LinearGradientBrush(rect, Color.Transparent, Color.Transparent, 90f))
            {
                var cb = new ColorBlend(3);
                cb.Colors = new[] { Color.FromArgb(0, c), c, Color.FromArgb(0, c) };
                cb.Positions = new[] { 0f, 0.5f, 1f };
                b.InterpolationColors = cb;
                g.FillRectangle(b, rect);
            }
        }

        static void Glow(Graphics g, float x, float y, float r, Color c)
        {
            using (var p = new GraphicsPath())
            {
                p.AddEllipse(x - r, y - r, 2 * r, 2 * r);
                using (var b = new PathGradientBrush(p))
                {
                    b.CenterColor = c;
                    b.SurroundColors = new[] { Color.FromArgb(0, c) };
                    g.FillPath(b, p);
                }
            }
        }

        static void Battlement(GraphicsPath p, float x, float top, float w, float h, float m)
        {
            p.AddRectangle(new RectangleF(x, top, w, h));
            int k = Math.Max(2, (int)(w / (m * 2)));
            float sp = w / (2 * k - 1);
            for (int i = 0; i < 2 * k - 1; i += 2)
                p.AddRectangle(new RectangleF(x + i * sp, top - m * 0.9f, sp, m * 0.9f + 1));
        }

        // tower with a conical roof; returns the roof tip
        static PointF Tower(GraphicsPath p, float x, float gy, float w, float h)
        {
            p.AddRectangle(new RectangleF(x, gy - h, w, h));
            p.AddRectangle(new RectangleF(x - w * 0.08f, gy - h - w * 0.12f, w * 1.16f, w * 0.14f));
            var tip = new PointF(x + w / 2, gy - h - w * 1.5f);
            p.AddPolygon(new[] { new PointF(x - w * 0.16f, gy - h - w * 0.1f), new PointF(x + w * 1.16f, gy - h - w * 0.1f), tip });
            return tip;
        }

        static void Pennant(Graphics g, PointF tip, float s, Color pole, Color flag, bool right)
        {
            float d = right ? 1 : -1;
            using (var pen = new Pen(pole, Math.Max(1f, s * 0.012f))) g.DrawLine(pen, tip.X, tip.Y + 1, tip.X, tip.Y - s * 0.16f);
            float y = tip.Y - s * 0.16f;
            using (var b = new SolidBrush(flag))
                g.FillPolygon(b, new[] {
                    new PointF(tip.X, y), new PointF(tip.X + d * s * 0.12f, y + s * 0.01f),
                    new PointF(tip.X + d * s * 0.2f, y + s * 0.035f), new PointF(tip.X + d * s * 0.1f, y + s * 0.045f),
                    new PointF(tip.X, y + s * 0.06f) });
        }

        static void Window(Graphics g, float x, float y, float w, float h, Color c, bool glow)
        {
            if (glow) Glow(g, x + w / 2, y + h / 2, h * 1.6f, Color.FromArgb(90, c));
            using (var p = new GraphicsPath())
            {
                p.AddArc(x, y, w, w, 180, 180);
                p.AddLine(x + w, y + w / 2, x + w, y + h);
                p.AddLine(x + w, y + h, x, y + h);
                p.CloseFigure();
                using (var b = new SolidBrush(c)) g.FillPath(b, p);
            }
        }

        void Castle(Graphics g, float cx, float gy, float s, Color body, Color win, Color flag, bool detail)
        {
            var tips = new List<PointF>();
            using (var p = new GraphicsPath(FillMode.Winding))
            {
                Battlement(p, cx - 0.62f * s, gy - 0.3f * s, 1.24f * s, 0.3f * s + 2, 0.06f * s);
                Battlement(p, cx - 0.26f * s, gy - 0.62f * s, 0.52f * s, 0.62f * s, 0.06f * s);
                tips.Add(Tower(p, cx - 0.7f * s, gy, 0.2f * s, 0.56f * s));
                tips.Add(Tower(p, cx + 0.5f * s, gy, 0.2f * s, 0.56f * s));
                tips.Add(Tower(p, cx - 0.4f * s, gy, 0.15f * s, 0.78f * s));
                tips.Add(Tower(p, cx + 0.25f * s, gy, 0.15f * s, 0.72f * s));
                tips.Add(Tower(p, cx - 0.09f * s, gy, 0.18f * s, 1.02f * s));
                using (var b = new SolidBrush(body)) g.FillPath(b, p);
            }
            if (!detail) return;

            // gate with warm light inside
            float gw = 0.16f * s, gh = 0.2f * s;
            Glow(g, cx, gy - gh * 0.4f, gh * 1.8f, Color.FromArgb(110, win));
            Window(g, cx - gw / 2, gy - gh, gw, gh, Mix(win, body, 0.25f), false);
            using (var pen = new Pen(Color.FromArgb(150, body), Math.Max(1f, s * 0.008f)))
                for (int i = 1; i < 4; i++) g.DrawLine(pen, cx - gw / 2 + gw * i / 4f, gy - gh * 0.85f, cx - gw / 2 + gw * i / 4f, gy);

            float ww = 0.035f * s, wh = 0.07f * s;
            Window(g, cx - 0.17f * s, gy - 0.5f * s, ww, wh, win, true);
            Window(g, cx + 0.13f * s, gy - 0.5f * s, ww, wh, win, true);
            Window(g, cx - ww / 2, gy - 0.86f * s, ww, wh, win, true);
            Window(g, cx - 0.6f * s - ww / 2, gy - 0.44f * s, ww, wh, win, true);
            Window(g, cx + 0.6f * s - ww / 2, gy - 0.44f * s, ww, wh, win, true);
            Window(g, cx - 0.325f * s - ww / 2, gy - 0.66f * s, ww, wh, win, true);
            Window(g, cx + 0.325f * s - ww / 2, gy - 0.6f * s, ww, wh, win, true);

            for (int i = 0; i < tips.Count; i++) Pennant(g, tips[i], s, body, flag, i % 2 == 1);
        }

        static void Pine(Graphics g, float x, float gy, float h, Brush b)
        {
            float w = h * 0.36f;
            using (var p = new GraphicsPath(FillMode.Winding))
            {
                for (int i = 0; i < 3; i++)
                {
                    float top = gy - h + i * h * 0.24f, bot = gy - h * 0.12f - (2 - i) * h * 0.16f;
                    float hw = w * (0.45f + i * 0.28f);
                    p.AddPolygon(new[] { new PointF(x, top), new PointF(x + hw, bot), new PointF(x - hw, bot) });
                }
                p.AddRectangle(new RectangleF(x - w * 0.07f, gy - h * 0.2f, w * 0.14f, h * 0.22f));
                g.FillPath(b, p);
            }
        }

        void Cottage(Graphics g, float x, float gy, float s, Color body, Color win)
        {
            using (var b = new SolidBrush(body))
            {
                g.FillRectangle(b, x, gy - s * 0.5f, s, s * 0.5f + 2);
                g.FillPolygon(b, new[] { new PointF(x - s * 0.12f, gy - s * 0.48f), new PointF(x + s * 1.12f, gy - s * 0.48f), new PointF(x + s * 0.5f, gy - s * 0.95f) });
                g.FillRectangle(b, x + s * 0.68f, gy - s * 0.95f, s * 0.12f, s * 0.3f);
            }
            Window(g, x + s * 0.2f, gy - s * 0.36f, s * 0.16f, s * 0.2f, win, true);
            Window(g, x + s * 0.6f, gy - s * 0.36f, s * 0.16f, s * 0.2f, win, true);
        }

        void Cloud(Graphics g, Random r, float x, float y, float len, Color top, Color under)
        {
            for (int i = 0; i < 5; i++)
            {
                float w = len * (0.4f + (float)r.NextDouble() * 0.6f), h = len * (0.05f + (float)r.NextDouble() * 0.05f);
                float ox = x + (float)(r.NextDouble() - 0.5) * len * 0.6f, oy = y + (float)(r.NextDouble() - 0.5) * len * 0.08f;
                using (var b = new SolidBrush(top)) g.FillEllipse(b, ox - w / 2, oy - h / 2, w, h);
                using (var b = new SolidBrush(under)) g.FillEllipse(b, ox - w * 0.42f, oy, w * 0.84f, h * 0.45f);
            }
        }

        Bitmap BuildBackground()
        {
            var bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            var r = new Random(1827 + Theme * 101); // fixed per theme: same painting on every screen and every run
            float u = H / 1080f;
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                switch (Theme)
                {
                    case 1: PaintMeadow(g, r); break;
                    case 2: PaintWinter(g, r); break;
                    case 3: PaintSeaside(g, r); break;
                    default: PaintDusk(g, r); break;
                }

                // painterly grain
                var gr = new Random(7);
                int dots = W * H / 90;
                using (var lb = new SolidBrush(Color.FromArgb(9, 255, 240, 220)))
                using (var db = new SolidBrush(Color.FromArgb(11, 20, 0, 30)))
                    for (int i = 0; i < dots; i++)
                        g.FillRectangle(i % 2 == 0 ? lb : db, gr.Next(W), gr.Next(H), 2 * u, 2 * u);

                // vignette
                Color vig = VignetteFor(Theme);
                using (var vp = new GraphicsPath())
                {
                    vp.AddEllipse(-W * 0.22f, -H * 0.22f, W * 1.44f, H * 1.44f);
                    using (var vb = new PathGradientBrush(vp))
                    {
                        vb.CenterColor = Color.FromArgb(0, vig);
                        vb.SurroundColors = new[] { vig };
                        vb.FocusScales = new PointF(0.55f, 0.5f);
                        g.FillRectangle(vb, 0, 0, W, H);
                    }
                }

                PaintBoard(g, r, BoardFor(Theme));
            }
            return bmp;
        }

        // ---- theme 0: medieval dusk
        void PaintDusk(Graphics g, Random r)
        {
            float u = H / 1080f;
            float horizon = H * 0.6f;
            float side = boardX - frame;
            float sunX = W - Math.Max(side * 0.45f, W * 0.08f), sunY = horizon - H * 0.15f;

            Color skyTop = Art.C(0x141B45), skyMid = Art.C(0x4B3A7C), skyRose = Art.C(0xC46B8E), skyGold = Art.C(0xF7A76C), skyLow = Art.C(0xFFD592);
            Color haze = Art.C(0xF0B39A);
            Color win = Art.C(0xFFD27A), flag = Art.C(0xC8343F);

            // sky
            using (var sky = new LinearGradientBrush(new RectangleF(0, -1, W, horizon + 2), skyTop, skyLow, 90f))
            {
                var cb = new ColorBlend(5);
                cb.Colors = new[] { skyTop, skyMid, skyRose, skyGold, skyLow };
                cb.Positions = new[] { 0f, 0.35f, 0.66f, 0.86f, 1f };
                sky.InterpolationColors = cb;
                g.FillRectangle(sky, 0, 0, W, horizon + 1);
            }
            using (var low = new SolidBrush(skyLow)) g.FillRectangle(low, 0, horizon, W, H - horizon);

            // stars, fading toward the glow
            for (int i = 0; i < 220; i++)
            {
                float x = (float)r.NextDouble() * W, y = (float)Math.Pow(r.NextDouble(), 1.6) * horizon * 0.55f;
                int a = Clamp((int)(230 * (1 - y / (horizon * 0.55f))));
                float sz = (0.8f + (float)r.NextDouble() * 1.8f) * u;
                using (var b = new SolidBrush(Color.FromArgb(a, 255, 247, 225))) g.FillEllipse(b, x - sz / 2, y - sz / 2, sz, sz);
                if (r.Next(14) == 0) Glow(g, x, y, sz * 4, Color.FromArgb(a / 3, 255, 240, 200));
            }

            // crescent moon, opposite the sun
            float mx = Math.Max(side * 0.3f, W * 0.07f), my = H * 0.13f, mr = H * 0.045f;
            Glow(g, mx, my, mr * 3.5f, Color.FromArgb(55, 220, 225, 255));
            using (var mp = new GraphicsPath()) using (var cut = new GraphicsPath())
            {
                mp.AddEllipse(mx - mr, my - mr, 2 * mr, 2 * mr);
                cut.AddEllipse(mx - mr * 0.55f, my - mr * 1.15f, 2 * mr, 2 * mr);
                using (var reg = new Region(mp))
                {
                    reg.Exclude(cut);
                    using (var b = new SolidBrush(Art.C(0xFFF6D8))) g.FillRegion(b, reg);
                }
            }

            // setting sun, its halo and rays
            Glow(g, sunX, sunY, H * 0.55f, Color.FromArgb(120, 255, 190, 110));
            Glow(g, sunX, sunY, H * 0.2f, Color.FromArgb(170, 255, 220, 150));
            for (int i = 0; i < 9; i++)
            {
                double ang = Math.PI + 0.25 + i * (Math.PI - 0.5) / 8 + (r.NextDouble() - 0.5) * 0.1;
                float len = H * 0.9f, spread = 0.035f;
                using (var b = new SolidBrush(Color.FromArgb(16, 255, 236, 190)))
                    g.FillPolygon(b, new[] { new PointF(sunX, sunY),
                        new PointF(sunX + (float)Math.Cos(ang - spread) * len, sunY + (float)Math.Sin(ang - spread) * len),
                        new PointF(sunX + (float)Math.Cos(ang + spread) * len, sunY + (float)Math.Sin(ang + spread) * len) });
            }
            float sr = H * 0.065f;
            using (var b = new LinearGradientBrush(new RectangleF(sunX - sr, sunY - sr, 2 * sr, 2 * sr), Art.C(0xFFF3C4), Art.C(0xFFB867), 90f))
                g.FillEllipse(b, sunX - sr, sunY - sr, 2 * sr, 2 * sr);

            // long painted clouds lit from below
            for (int i = 0; i < 9; i++)
            {
                float cy = horizon * (0.25f + (float)r.NextDouble() * 0.6f);
                float t = cy / horizon;
                Color top = Color.FromArgb(150, Mix(Art.C(0x6E4F8E), Art.C(0xE58C8E), t));
                Color under = Color.FromArgb(170, Mix(Art.C(0xE89AA4), Art.C(0xFFD7A0), t));
                Cloud(g, r, (float)r.NextDouble() * W, cy, W * (0.12f + (float)r.NextDouble() * 0.15f), top, under);
            }

            // birds
            using (var pen = new Pen(Color.FromArgb(170, 60, 40, 70), Math.Max(1.2f, 2f * u)))
                for (int i = 0; i < 6; i++)
                {
                    float bx = sunX - W * 0.04f - (float)r.NextDouble() * W * 0.12f, by = horizon * (0.35f + (float)r.NextDouble() * 0.3f);
                    float bs = (6 + (float)r.NextDouble() * 6) * u;
                    g.DrawArc(pen, bx - bs, by, bs, bs * 0.8f, 200, 120);
                    g.DrawArc(pen, bx, by, bs, bs * 0.8f, 220, 120);
                }

            // distant mountain ranges with mist between them
            var far = MakeRidge(r, 7, 0.55f);
            FillRidge(g, far, horizon + H * 0.02f, H * 0.2f, Art.C(0xC890A8), Art.C(0xD9A3A6));
            Mist(g, horizon, H * 0.05f, Color.FromArgb(150, haze));

            var mid = MakeRidge(r, 7, 0.5f);
            float midBase = horizon + H * 0.08f, midAmp = H * 0.17f;
            FillRidge(g, mid, midBase, midAmp, Art.C(0x9A6E9C), Art.C(0xB88497));
            // a far-away castle on the mid range, on the sun side
            float fcx = W - Math.Max(side * 0.55f, W * 0.12f);
            float fgy = RidgeY(mid, midBase, midAmp, fcx) + H * 0.01f;
            Castle(g, fcx, fgy, H * 0.07f, Art.C(0x8E6491), win, flag, false);
            Mist(g, midBase - H * 0.01f, H * 0.05f, Color.FromArgb(120, haze));

            var near = MakeRidge(r, 6, 0.5f);
            FillRidge(g, near, horizon + H * 0.16f, H * 0.1f, Art.C(0x6C4E80), Art.C(0x5A4272));

            // lake with the sun reflected in it
            float lakeY = horizon + H * 0.2f;
            using (var b = new LinearGradientBrush(new RectangleF(0, lakeY, W, H - lakeY + 1), Art.C(0xC0809A), Art.C(0x3B2E5C), 90f))
                g.FillRectangle(b, 0, lakeY, W, H - lakeY);
            for (int i = 0; i < 26; i++)
            {
                float ry = lakeY + (float)Math.Pow(i / 26.0, 1.3) * (H - lakeY) * 0.8f + 3 * u;
                float rw = H * (0.05f + 0.1f * (float)r.NextDouble()) * (1 + i * 0.05f);
                float rx = sunX + (float)(r.NextDouble() - 0.5) * H * 0.05f;
                using (var b = new SolidBrush(Color.FromArgb(Clamp(170 - i * 5), 255, 222, 160)))
                    g.FillRectangle(b, rx - rw / 2, ry, rw, Math.Max(1.5f, 2.5f * u));
            }

            // near hills: castle hill (moon side) and village hill (sun side)
            Color hillTop = Art.C(0x3A2D58), hillBot = Art.C(0x251C3E);
            float hillW = Math.Max(side * 1.9f, W * 0.3f);
            float castleX = Math.Max(side * 0.5f, W * 0.1f), hillTopY = H * 0.68f;
            using (var b = new LinearGradientBrush(new RectangleF(0, hillTopY, W, H - hillTopY), hillTop, hillBot, 90f))
            {
                g.FillEllipse(b, castleX - hillW * 0.75f, hillTopY, hillW * 1.5f, H * 0.7f);
                g.FillEllipse(b, W - hillW * 0.95f, H * 0.76f, hillW * 1.4f, H * 0.6f);
            }

            Color sil = Art.C(0x2B2147);
            float cs = Math.Max(H * 0.12f, Math.Min(side * 0.55f, H * 0.3f));
            Castle(g, castleX, hillTopY + H * 0.012f, cs, sil, win, flag, true);

            // village and wizard tower on the sun side
            float vx = W - Math.Max(side * 0.75f, W * 0.15f), vgy = H * 0.785f;
            using (var tp = new GraphicsPath(FillMode.Winding))
            {
                var tip = Tower(tp, vx + cs * 0.5f, vgy + 2, cs * 0.13f, cs * 0.75f);
                using (var b = new SolidBrush(sil)) g.FillPath(b, tp);
                Pennant(g, tip, cs, sil, flag, false);
                Window(g, vx + cs * 0.5f + cs * 0.045f, vgy - cs * 0.6f, cs * 0.04f, cs * 0.07f, win, true);
            }
            Cottage(g, vx - cs * 0.15f, vgy + H * 0.008f, cs * 0.28f, sil, win);
            Cottage(g, vx + cs * 0.75f, vgy + H * 0.012f, cs * 0.22f, sil, win);

            // pine forests at the edges and a dark foreground
            using (var pb = new SolidBrush(Art.C(0x1E1735)))
            {
                for (int i = 0; i < 16; i++)
                {
                    float px = (float)r.NextDouble() * side * 0.9f;
                    if (i % 2 == 1) px = W - px;
                    float ph = H * (0.07f + (float)r.NextDouble() * 0.09f);
                    Pine(g, px, H * (0.86f + (float)r.NextDouble() * 0.08f), ph, pb);
                }
            }
            var fg = MakeRidge(r, 6, 0.6f);
            FillRidge(g, fg, H * 1.0f, H * 0.08f, Art.C(0x1A142E), Art.C(0x120E22));
        }

        // ---- the board, in the theme's woods and colors
        void PaintBoard(Graphics g, Random r, BoardStyle bs)
        {
            float u = H / 1080f;
            // frame with grain and inlay
            var fr = new RectangleF(boardX - frame, boardY - frame, 8 * sq + 2 * frame, 8 * sq + 2 * frame);
            using (var shadowPath = Art.RRect(fr.X + frame * 0.3f, fr.Y + frame * 0.45f, fr.Width, fr.Height, frame * 0.7f))
            using (var shadow = new SolidBrush(bs.Shadow))
                g.FillPath(shadow, shadowPath);
            using (var framePath = Art.RRect(fr.X, fr.Y, fr.Width, fr.Height, frame * 0.7f))
            using (var fb = new LinearGradientBrush(fr, bs.FrameTop, bs.FrameBottom, 60f))
                g.FillPath(fb, framePath);
            // wood grain
            using (var grain = new Pen(bs.Grain, Math.Max(1f, u)))
            using (var framePath = Art.RRect(fr.X, fr.Y, fr.Width, fr.Height, frame * 0.7f))
            {
                g.SetClip(framePath);
                for (float y = fr.Y + 3; y < fr.Bottom; y += 5 * u + (float)r.NextDouble() * 6 * u)
                    g.DrawBezier(grain, fr.X, y, fr.X + fr.Width * 0.3f, y + (float)(r.NextDouble() - 0.5) * 8 * u,
                                 fr.X + fr.Width * 0.7f, y + (float)(r.NextDouble() - 0.5) * 8 * u, fr.Right, y);
                g.ResetClip();
            }
            Color gold = bs.Inlay;
            float inset = frame * 0.18f;
            using (var gp = new Pen(gold, Math.Max(1.5f, frame * 0.07f)))
            using (var path = Art.RRect(fr.X + inset, fr.Y + inset, fr.Width - 2 * inset, fr.Height - 2 * inset, frame * 0.55f))
                g.DrawPath(gp, path);
            // gold diamonds in the corners
            using (var gb = new SolidBrush(gold))
            {
                float d = frame * 0.28f;
                foreach (var c in new[] { new PointF(fr.X + frame / 2, fr.Y + frame / 2), new PointF(fr.Right - frame / 2, fr.Y + frame / 2),
                                          new PointF(fr.X + frame / 2, fr.Bottom - frame / 2), new PointF(fr.Right - frame / 2, fr.Bottom - frame / 2) })
                    g.FillPolygon(gb, new[] { new PointF(c.X, c.Y - d), new PointF(c.X + d, c.Y), new PointF(c.X, c.Y + d), new PointF(c.X - d, c.Y) });
            }

            // squares
            using (var boardPath = Art.RRect(boardX, boardY, 8 * sq, 8 * sq, frame * 0.25f))
            {
                g.SetClip(boardPath);
                using (var lightSq = new SolidBrush(bs.Light))
                using (var darkSq = new SolidBrush(bs.Dark))
                    for (int row = 0; row < 8; row++)
                        for (int col = 0; col < 8; col++)
                            g.FillRectangle((row + col) % 2 == 0 ? lightSq : darkSq, boardX + col * sq, boardY + row * sq, sq + 1, sq + 1);
                // soft inner shading so the squares look inlaid
                using (var ib = new LinearGradientBrush(new RectangleF(boardX, boardY, 8 * sq, 8 * sq), Color.FromArgb(0, 0, 0, 0), bs.Shade, 90f))
                    g.FillRectangle(ib, boardX, boardY, 8 * sq, 8 * sq);
                g.ResetClip();
            }
            using (var gp = new Pen(gold, Math.Max(1.5f, frame * 0.06f)))
            using (var path = Art.RRect(boardX, boardY, 8 * sq, 8 * sq, frame * 0.25f))
                g.DrawPath(gp, path);

            // a-h, 1-8 in gold
            FontFamily serif = Art.Find("Georgia", "Times New Roman");
            using (var f = new Font(serif, Math.Max(6, frame * 0.5f), FontStyle.Bold, GraphicsUnit.Pixel))
            using (var tb = new SolidBrush(bs.Label))
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

        // ---------------------------------------------------------- themes
        public class BoardStyle { public Color FrameTop, FrameBottom, Grain, Inlay, Light, Dark, Shade, Label, Shadow; }

        static BoardStyle BoardFor(int theme)
        {
            switch (theme)
            {
                case 1: // garden: honey oak, cream and leaf green
                    return new BoardStyle { FrameTop = Art.C(0xD9A766), FrameBottom = Art.C(0x9C6A35), Grain = Color.FromArgb(34, 90, 50, 10),
                        Inlay = Art.C(0x3F8F3A), Light = Art.C(0xFBF5E1), Dark = Art.C(0x93C46E), Shade = Color.FromArgb(30, 20, 60, 0),
                        Label = Art.C(0xFFF6DA), Shadow = Color.FromArgb(90, 20, 60, 10) };
                case 2: // frost: silver ice, snow and frozen blue
                    return new BoardStyle { FrameTop = Art.C(0xD3E0F2), FrameBottom = Art.C(0x7F95C2), Grain = Color.FromArgb(45, 255, 255, 255),
                        Inlay = Color.White, Light = Art.C(0xF7FAFF), Dark = Art.C(0x9FB8DF), Shade = Color.FromArgb(35, 20, 40, 90),
                        Label = Art.C(0x2A3A6E), Shadow = Color.FromArgb(120, 5, 10, 40) };
                case 3: // driftwood: sand and sea glass
                    return new BoardStyle { FrameTop = Art.C(0xA88664), FrameBottom = Art.C(0x6B4F36), Grain = Color.FromArgb(40, 40, 25, 10),
                        Inlay = Art.C(0xF3E6C8), Light = Art.C(0xF8EBCF), Dark = Art.C(0x6DB5C2), Shade = Color.FromArgb(30, 0, 40, 60),
                        Label = Art.C(0xFFF0D2), Shadow = Color.FromArgb(110, 10, 30, 50) };
                default: // carved walnut with gold inlay
                    return new BoardStyle { FrameTop = Art.C(0x7A4B2A), FrameBottom = Art.C(0x4A2914), Grain = Color.FromArgb(28, 0, 0, 0),
                        Inlay = Art.C(0xE2B455), Light = Art.C(0xF3E3C0), Dark = Art.C(0xB98A5C), Shade = Color.FromArgb(35, 60, 20, 0),
                        Label = Art.C(0xF0CF83), Shadow = Color.FromArgb(120, 5, 0, 15) };
            }
        }

        static Color VignetteFor(int theme)
        {
            switch (theme)
            {
                case 1: return Color.FromArgb(70, 20, 60, 30);
                case 2: return Color.FromArgb(150, 5, 10, 40);
                case 3: return Color.FromArgb(80, 20, 40, 70);
                default: return Color.FromArgb(150, 10, 5, 25);
            }
        }

        // ---- shared painting helpers
        static void FillPoly(Graphics g, Color c, params float[] xy)
        {
            var pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(xy[2 * i], xy[2 * i + 1]);
            using (var b = new SolidBrush(c)) g.FillPolygon(b, pts);
        }

        static void FillCircle(Graphics g, Color c, float x, float y, float r)
        {
            using (var b = new SolidBrush(c)) g.FillEllipse(b, x - r, y - r, 2 * r, 2 * r);
        }

        static void FillRect(Graphics g, Color c, float x, float y, float w, float h)
        {
            using (var b = new SolidBrush(c)) g.FillRectangle(b, x, y, w, h);
        }

        void Sky(Graphics g, float bottom, Color[] cols, float[] pos)
        {
            using (var sky = new LinearGradientBrush(new RectangleF(0, -1, W, bottom + 2), cols[0], cols[cols.Length - 1], 90f))
            {
                var cb = new ColorBlend(cols.Length);
                cb.Colors = cols; cb.Positions = pos;
                sky.InterpolationColors = cb;
                g.FillRectangle(sky, 0, 0, W, bottom + 1);
            }
            FillRect(g, cols[cols.Length - 1], 0, bottom, W, H - bottom);
        }

        // vertical gradient with color stops at absolute y positions (first stop = top of the shape)
        static LinearGradientBrush Stops(float x, float w, Color[] cols, float[] ys)
        {
            float y0 = ys[0], y1 = ys[ys.Length - 1];
            var b = new LinearGradientBrush(new RectangleF(x, y0 - 1, w, y1 - y0 + 2), cols[0], cols[cols.Length - 1], 90f);
            var cb = new ColorBlend(cols.Length);
            var pos = new float[ys.Length];
            for (int i = 0; i < ys.Length; i++) pos[i] = Math.Max(0, Math.Min(1, (ys[i] - y0 + 1) / (y1 - y0 + 2)));
            pos[0] = 0; pos[pos.Length - 1] = 1;
            cb.Colors = cols; cb.Positions = pos;
            b.InterpolationColors = cb;
            return b;
        }

        // ridge whose colors change with height (snowy peaks, sunlit tops...)
        void FillRidgeStops(Graphics g, float[] h, float baseY, float amp, Color[] cols, float[] fractions)
        {
            var pts = new PointF[h.Length + 2];
            for (int i = 0; i < h.Length; i++) pts[i] = new PointF(W * i / (float)(h.Length - 1), baseY - h[i] * amp);
            pts[h.Length] = new PointF(W, H + 2); pts[h.Length + 1] = new PointF(0, H + 2);
            var ys = new float[fractions.Length];
            for (int i = 0; i < ys.Length; i++) ys[i] = baseY - amp + fractions[i] * (H + 2 - baseY + amp);
            using (var b = Stops(0, W, cols, ys)) g.FillPolygon(b, pts);
        }

        // an island / headland: a ridge that only rises between x0 and x1 and sits on baseY
        void Island(Graphics g, Random r, float x0, float x1, float baseY, float amp, Color top, Color bottom)
        {
            var h = MakeRidge(r, 5, 0.5f);
            var pts = new PointF[h.Length + 2];
            for (int i = 0; i < h.Length; i++)
            {
                float t = i / (float)(h.Length - 1);
                float env = (float)Math.Pow(Math.Sin(Math.PI * t), 0.7);
                pts[i] = new PointF(x0 + (x1 - x0) * t, baseY - (0.35f + 0.65f * h[i]) * amp * env);
            }
            pts[h.Length] = new PointF(x1, baseY + 2); pts[h.Length + 1] = new PointF(x0, baseY + 2);
            using (var b = new LinearGradientBrush(new RectangleF(x0, baseY - amp - 1, x1 - x0 + 1, amp + 4), top, bottom, 90f))
                g.FillPolygon(b, pts);
        }

        void PuffCloud(Graphics g, Random r, float x, float y, float s, Color top, Color shade)
        {
            int n = 5 + r.Next(3);
            var cs = new float[n * 3];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float rad = s * (0.22f + 0.26f * (float)Math.Sin(Math.PI * t) + 0.08f * (float)r.NextDouble());
                cs[3 * i] = x + (t - 0.5f) * s * 1.5f; cs[3 * i + 1] = y - rad * 0.5f; cs[3 * i + 2] = rad;
            }
            using (var sb = new SolidBrush(shade))
            {
                g.FillEllipse(sb, x - s * 0.95f, y - s * 0.14f, s * 1.9f, s * 0.3f);
                for (int i = 0; i < n; i++) g.FillEllipse(sb, cs[3 * i] - cs[3 * i + 2], cs[3 * i + 1] - cs[3 * i + 2] + s * 0.05f, 2 * cs[3 * i + 2], 2 * cs[3 * i + 2]);
            }
            using (var tb = new SolidBrush(top))
                for (int i = 0; i < n; i++)
                {
                    float rad = cs[3 * i + 2] * 0.94f;
                    g.FillEllipse(tb, cs[3 * i] - rad - s * 0.02f, cs[3 * i + 1] - rad - s * 0.03f, 2 * rad, 2 * rad);
                }
        }

        static void Birds(Graphics g, Random r, float x0, float x1, float y0, float y1, int n, Color c, float u)
        {
            using (var pen = new Pen(c, Math.Max(1.2f, 2f * u)))
                for (int i = 0; i < n; i++)
                {
                    float bx = x0 + (float)r.NextDouble() * (x1 - x0), by = y0 + (float)r.NextDouble() * (y1 - y0);
                    float bs = (6 + (float)r.NextDouble() * 6) * u;
                    g.DrawArc(pen, bx - bs, by, bs, bs * 0.8f, 200, 120);
                    g.DrawArc(pen, bx, by, bs, bs * 0.8f, 220, 120);
                }
        }

        // a sunlit castle: stone walls, shaded sides, colored cone roofs (snow roofs in winter)
        void CastleLit(Graphics g, float cx, float gy, float s, Color stone, Color shade, Color roof, Color roofShade,
                       Color win, Color flag, Color cap, bool glowWin, bool detail)
        {
            float m = 0.06f * s;
            using (var walls = new GraphicsPath(FillMode.Winding))
            {
                Battlement(walls, cx - 0.62f * s, gy - 0.3f * s, 1.24f * s, 0.3f * s + 2, m);
                Battlement(walls, cx - 0.26f * s, gy - 0.62f * s, 0.52f * s, 0.62f * s, m);
                using (var b = new SolidBrush(stone)) g.FillPath(b, walls);
            }
            FillRect(g, shade, cx + 0.13f * s, gy - 0.62f * s, 0.13f * s, 0.62f * s - 0.3f * s);
            FillRect(g, Color.FromArgb(120, shade), cx - 0.62f * s, gy - 0.3f * s, 1.24f * s, 0.03f * s);
            if (cap.A > 0) { BattlementCaps(g, cap, cx - 0.62f * s, gy - 0.3f * s, 1.24f * s, m); BattlementCaps(g, cap, cx - 0.26f * s, gy - 0.62f * s, 0.52f * s, m); }

            if (detail)
            {
                // stone courses
                using (var pen = new Pen(Color.FromArgb(70, shade), Math.Max(1f, s * 0.006f)))
                {
                    for (float y = gy - 0.05f * s; y > gy - 0.28f * s; y -= 0.055f * s)
                        for (float x = cx - 0.6f * s + ((int)((gy - y) / (0.055f * s)) % 2) * 0.05f * s; x < cx + 0.58f * s; x += 0.1f * s)
                            g.DrawLine(pen, x, y, x + 0.06f * s, y);
                }
            }

            float[] tx = { -0.7f, 0.5f, -0.4f, 0.25f, -0.09f }, tw = { 0.2f, 0.2f, 0.15f, 0.15f, 0.18f }, th = { 0.56f, 0.56f, 0.78f, 0.72f, 1.02f };
            var tips = new List<PointF>();
            for (int i = 0; i < 5; i++)
            {
                float x = cx + tx[i] * s, w = tw[i] * s, h = th[i] * s;
                FillRect(g, stone, x, gy - h, w, h);
                FillRect(g, shade, x + w * 0.62f, gy - h, w * 0.38f, h);
                FillRect(g, Art.Dark(stone, 0.88f), x - w * 0.08f, gy - h - w * 0.12f, w * 1.16f, w * 0.14f);
                FillRect(g, Art.Dark(shade, 0.88f), x + w * 0.62f, gy - h - w * 0.12f, w * 0.54f, w * 0.14f);
                if (cap.A > 0) FillRect(g, cap, x - w * 0.1f, gy - h - w * 0.14f, w * 1.2f, w * 0.06f);
                var tip = new PointF(x + w / 2, gy - h - w * 1.5f);
                FillPoly(g, roof, x - w * 0.16f, gy - h - w * 0.1f, x + w * 1.16f, gy - h - w * 0.1f, tip.X, tip.Y);
                FillPoly(g, roofShade, tip.X, tip.Y, x + w * 0.5f, gy - h - w * 0.1f, x + w * 1.16f, gy - h - w * 0.1f);
                tips.Add(tip);
                if (detail && h > 0.6f * s) Window(g, x + w * 0.5f - 0.02f * s, gy - h * 0.72f, 0.04f * s, 0.08f * s, win, glowWin);
            }

            if (detail)
            {
                float gw = 0.16f * s, gh = 0.2f * s;
                if (glowWin) Glow(g, cx, gy - gh * 0.4f, gh * 1.8f, Color.FromArgb(110, win));
                Window(g, cx - gw / 2 - 0.015f * s, gy - gh - 0.015f * s, gw + 0.03f * s, gh + 0.015f * s, Art.Dark(stone, 0.75f), false);
                Window(g, cx - gw / 2, gy - gh, gw, gh, glowWin ? Mix(win, stone, 0.25f) : Art.C(0x6B4428), false);
                using (var pen = new Pen(Color.FromArgb(150, glowWin ? shade : Art.C(0x3A2414)), Math.Max(1f, s * 0.008f)))
                    for (int i = 1; i < 4; i++) g.DrawLine(pen, cx - gw / 2 + gw * i / 4f, gy - gh * 0.85f, cx - gw / 2 + gw * i / 4f, gy);
                float ww = 0.035f * s, wh = 0.07f * s;
                Window(g, cx - 0.17f * s, gy - 0.5f * s, ww, wh, win, glowWin);
                Window(g, cx + 0.13f * s, gy - 0.5f * s, ww, wh, win, glowWin);
                Window(g, cx - ww / 2, gy - 0.86f * s, ww, wh, win, glowWin);
                Window(g, cx - 0.6f * s - ww / 2, gy - 0.44f * s, ww, wh, win, glowWin);
                Window(g, cx + 0.6f * s - ww / 2, gy - 0.44f * s, ww, wh, win, glowWin);
            }
            Color pole = Art.Dark(shade, 0.6f);
            for (int i = 0; i < tips.Count; i++) Pennant(g, tips[i], s, pole, flag, i % 2 == 1);
        }

        static void BattlementCaps(Graphics g, Color c, float x, float top, float w, float m)
        {
            int k = Math.Max(2, (int)(w / (m * 2)));
            float sp = w / (2 * k - 1);
            using (var b = new SolidBrush(c))
                for (int i = 0; i < 2 * k - 1; i += 2)
                    g.FillRectangle(b, x + i * sp - sp * 0.08f, top - m * 0.95f, sp * 1.16f, m * 0.32f);
        }

        void CottageLit(Graphics g, float x, float gy, float s, Color wall, Color wallShade, Color roof, Color roofShade, Color win, bool glowWin, Color cap)
        {
            FillRect(g, Art.Dark(roof, 0.75f), x + s * 0.68f, gy - s * 0.95f, s * 0.12f, s * 0.3f);
            if (cap.A > 0) FillRect(g, cap, x + s * 0.66f, gy - s * 0.97f, s * 0.16f, s * 0.05f);
            FillRect(g, wall, x, gy - s * 0.5f, s, s * 0.5f + 2);
            FillRect(g, wallShade, x + s * 0.82f, gy - s * 0.5f, s * 0.18f, s * 0.5f + 2);
            FillPoly(g, roof, x - s * 0.12f, gy - s * 0.48f, x + s * 1.12f, gy - s * 0.48f, x + s * 0.5f, gy - s * 0.95f);
            FillPoly(g, roofShade, x + s * 0.5f, gy - s * 0.95f, x + s * 0.62f, gy - s * 0.48f, x + s * 1.12f, gy - s * 0.48f);
            Window(g, x + s * 0.42f, gy - s * 0.28f, s * 0.16f, s * 0.28f, Art.C(0x6B4428), false);
            foreach (float wx in new[] { 0.13f, 0.7f })
            {
                if (glowWin) Glow(g, x + s * (wx + 0.08f), gy - s * 0.28f, s * 0.2f, Color.FromArgb(110, win));
                FillRect(g, win, x + s * wx, gy - s * 0.36f, s * 0.16f, s * 0.15f);
                using (var pen = new Pen(Art.Dark(wall, 0.6f), Math.Max(1f, s * 0.02f)))
                {
                    g.DrawLine(pen, x + s * (wx + 0.08f), gy - s * 0.36f, x + s * (wx + 0.08f), gy - s * 0.21f);
                    g.DrawLine(pen, x + s * wx, gy - s * 0.285f, x + s * (wx + 0.16f), gy - s * 0.285f);
                }
            }
        }

        static void RoundTree(Graphics g, float x, float gy, float h, Color leaf, Color leafDark, Color trunk)
        {
            FillRect(g, trunk, x - h * 0.05f, gy - h * 0.45f, h * 0.1f, h * 0.45f + 1);
            FillCircle(g, leafDark, x, gy - h * 0.66f, h * 0.3f);
            FillCircle(g, leafDark, x - h * 0.22f, gy - h * 0.5f, h * 0.22f);
            FillCircle(g, leafDark, x + h * 0.22f, gy - h * 0.5f, h * 0.22f);
            FillCircle(g, leaf, x - h * 0.05f, gy - h * 0.7f, h * 0.24f);
            FillCircle(g, leaf, x - h * 0.24f, gy - h * 0.54f, h * 0.15f);
            FillCircle(g, leaf, x + h * 0.14f, gy - h * 0.56f, h * 0.14f);
            FillCircle(g, Color.FromArgb(70, 255, 255, 220), x - h * 0.12f, gy - h * 0.8f, h * 0.09f);
        }

        static void Windmill(Graphics g, float x, float gy, float s, float angle)
        {
            FillPoly(g, Art.C(0xF2E6CC), x - s * 0.18f, gy + 2, x + s * 0.18f, gy + 2, x + s * 0.12f, gy - s * 0.75f, x - s * 0.12f, gy - s * 0.75f);
            FillPoly(g, Art.C(0xD9C8A6), x + s * 0.04f, gy + 2, x + s * 0.18f, gy + 2, x + s * 0.12f, gy - s * 0.75f, x + s * 0.03f, gy - s * 0.75f);
            FillPoly(g, Art.C(0xC8443A), x - s * 0.17f, gy - s * 0.74f, x + s * 0.17f, gy - s * 0.74f, x, gy - s * 0.97f);
            FillPoly(g, Art.C(0x9E3029), x, gy - s * 0.97f, x + s * 0.05f, gy - s * 0.74f, x + s * 0.17f, gy - s * 0.74f);
            Window(g, x - s * 0.05f, gy - s * 0.2f, s * 0.1f, s * 0.2f, Art.C(0x6B4428), false);
            Window(g, x - s * 0.03f, gy - s * 0.52f, s * 0.06f, s * 0.1f, Art.C(0x4A3B5C), false);
            float hx = x, hy = gy - s * 0.78f;
            for (int k = 0; k < 4; k++)
            {
                var st = g.Save();
                g.TranslateTransform(hx, hy);
                g.RotateTransform(angle + 90 * k);
                using (var b = new SolidBrush(Color.FromArgb(235, 255, 248, 234))) g.FillRectangle(b, s * 0.08f, 0, s * 0.55f, s * 0.13f);
                using (var pen = new Pen(Art.C(0x7A5230), Math.Max(1f, s * 0.018f)))
                {
                    g.DrawLine(pen, 0, 0, s * 0.65f, 0);
                    for (int i = 1; i < 5; i++) g.DrawLine(pen, s * (0.08f + 0.11f * i), 0, s * (0.08f + 0.11f * i), s * 0.13f);
                    g.DrawLine(pen, s * 0.08f, s * 0.13f, s * 0.63f, s * 0.13f);
                }
                g.Restore(st);
            }
            FillCircle(g, Art.C(0x7A5230), hx, hy, s * 0.035f);
        }

        static void Balloon(Graphics g, float x, float y, float s, Color a, Color b)
        {
            using (var env = new GraphicsPath(FillMode.Winding))
            {
                env.AddEllipse(x - s * 0.5f, y - s * 0.6f, s, s * 1.05f);
                env.AddPolygon(new[] { new PointF(x - s * 0.44f, y + s * 0.1f), new PointF(x + s * 0.44f, y + s * 0.1f), new PointF(x + s * 0.14f, y + s * 0.58f), new PointF(x - s * 0.14f, y + s * 0.58f) });
                using (var ab = new SolidBrush(a)) g.FillPath(ab, env);
                var st = g.Save();
                g.SetClip(env, CombineMode.Intersect);
                using (var bb = new SolidBrush(b)) g.FillEllipse(bb, x - s * 0.32f, y - s * 0.7f, s * 0.64f, s * 1.4f);
                using (var ab = new SolidBrush(a)) g.FillEllipse(ab, x - s * 0.13f, y - s * 0.7f, s * 0.26f, s * 1.4f);
                using (var hb = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) g.FillEllipse(hb, x - s * 0.42f, y - s * 0.52f, s * 0.36f, s * 0.5f);
                using (var sh = new SolidBrush(Color.FromArgb(40, 0, 0, 40))) g.FillEllipse(sh, x + s * 0.1f, y - s * 0.6f, s * 0.6f, s * 1.3f);
                g.Restore(st);
            }
            using (var pen = new Pen(Art.C(0x6B4428), Math.Max(1f, s * 0.02f)))
            {
                g.DrawLine(pen, x - s * 0.13f, y + s * 0.58f, x - s * 0.09f, y + s * 0.72f);
                g.DrawLine(pen, x + s * 0.13f, y + s * 0.58f, x + s * 0.09f, y + s * 0.72f);
            }
            FillRect(g, Art.C(0x9C6A35), x - s * 0.11f, y + s * 0.72f, s * 0.22f, s * 0.15f);
            FillRect(g, Art.C(0x7A5230), x - s * 0.11f, y + s * 0.72f, s * 0.22f, s * 0.035f);
        }

        // ---- theme 1: sunny meadow
        void PaintMeadow(Graphics g, Random r)
        {
            float u = H / 1080f, horizon = H * 0.58f, side = boardX - frame;
            float sunX = W - Math.Max(side * 0.4f, W * 0.08f), sunY = H * 0.14f;
            float castleX = Math.Max(side * 0.5f, W * 0.1f);

            Sky(g, horizon, new[] { Art.C(0x3A93DB), Art.C(0x67B8EE), Art.C(0xA6DAF6), Art.C(0xE6F6FC) }, new[] { 0f, 0.4f, 0.78f, 1f });

            // sun and its soft rays
            Glow(g, sunX, sunY, H * 0.55f, Color.FromArgb(100, 255, 248, 200));
            using (var ray = new SolidBrush(Color.FromArgb(26, 255, 252, 225)))
                for (int i = 0; i < 14; i++)
                {
                    double ang = i * Math.PI * 2 / 14 + 0.1;
                    float len = H * 0.65f, spread = 0.07f;
                    g.FillPolygon(ray, new[] { new PointF(sunX, sunY),
                        new PointF(sunX + (float)Math.Cos(ang - spread) * len, sunY + (float)Math.Sin(ang - spread) * len),
                        new PointF(sunX + (float)Math.Cos(ang + spread) * len, sunY + (float)Math.Sin(ang + spread) * len) });
                }
            Glow(g, sunX, sunY, H * 0.16f, Color.FromArgb(210, 255, 250, 215));
            float sr = H * 0.062f;
            using (var b = new LinearGradientBrush(new RectangleF(sunX - sr, sunY - sr, 2 * sr, 2 * sr), Art.C(0xFFFCE0), Art.C(0xFFD54F), 90f))
                g.FillEllipse(b, sunX - sr, sunY - sr, 2 * sr, 2 * sr);

            // rainbow over the castle
            float rbx = castleX + side * 0.1f, rby = horizon + H * 0.06f, rbr = H * 0.4f, bw = H * 0.017f;
            var bands = new[] { 0xFF5A5A, 0xFF9F40, 0xFFE05A, 0x6BD46B, 0x5AA8FF, 0x9A7BFF };
            for (int i = 0; i < bands.Length; i++)
            {
                float rad = rbr - i * bw;
                using (var pen = new Pen(Color.FromArgb(95, Art.C(bands[i])), bw + 1))
                    g.DrawArc(pen, rbx - rad, rby - rad, 2 * rad, 2 * rad, 180, 180);
            }

            for (int i = 0; i < 8; i++)
                PuffCloud(g, r, (float)r.NextDouble() * W, H * (0.1f + (float)r.NextDouble() * 0.32f), H * (0.06f + (float)r.NextDouble() * 0.06f),
                          Color.FromArgb(240, 255, 255, 255), Color.FromArgb(230, 206, 224, 242));

            Balloon(g, Math.Max(side * 0.2f, W * 0.05f), H * 0.2f, H * 0.06f, Art.C(0xEF5350), Art.C(0xFFD23F));
            Balloon(g, W - Math.Max(side * 0.82f, W * 0.15f), H * 0.34f, H * 0.04f, Art.C(0x4D9DE0), Color.White);
            Birds(g, r, W - side * 0.9f, W - side * 0.2f, H * 0.22f, H * 0.38f, 5, Color.FromArgb(150, 40, 60, 90), u);

            // far blue mountains with snowy tops
            var far = MakeRidge(r, 7, 0.55f);
            FillRidgeStops(g, far, horizon + H * 0.02f, H * 0.22f,
                new[] { Color.White, Art.C(0xDCE9F7), Art.C(0x9DBFE3), Art.C(0xB9D4EE), Art.C(0xC9DDF0) }, new[] { 0f, 0.05f, 0.12f, 0.3f, 1f });
            Mist(g, horizon + H * 0.01f, H * 0.04f, Color.FromArgb(150, 235, 246, 252));

            // green hills with little trees and a faraway castle
            var mid = MakeRidge(r, 6, 0.45f);
            float midBase = horizon + H * 0.08f, midAmp = H * 0.1f;
            FillRidge(g, mid, midBase, midAmp, Art.C(0xA6D57E), Art.C(0x86C462));
            float fcx = W - Math.Max(side * 0.55f, W * 0.12f);
            CastleLit(g, fcx, RidgeY(mid, midBase, midAmp, fcx) + H * 0.008f, H * 0.06f, Art.C(0xD9D3E3), Art.C(0xB8B0C8),
                      Art.C(0x6F8FC9), Art.C(0x5B78AE), Art.C(0x5A5070), Art.C(0xE24C4C), Color.Empty, false, false);
            for (int i = 0; i < 46; i++)
            {
                float x = (float)r.NextDouble() * W, y = RidgeY(mid, midBase, midAmp, x) + H * (0.008f + (float)r.NextDouble() * 0.05f);
                float t = H * (0.006f + (float)r.NextDouble() * 0.008f);
                FillCircle(g, Art.C(0x5E9E48), x, y, t);
                FillCircle(g, Art.C(0x79B85C), x - t * 0.3f, y - t * 0.3f, t * 0.55f);
            }

            var near = MakeRidge(r, 6, 0.5f);
            FillRidge(g, near, horizon + H * 0.18f, H * 0.07f, Art.C(0x86C960), Art.C(0x63AD47));

            // castle hill and village hill
            float hillW = Math.Max(side * 1.9f, W * 0.3f), hillTopY = H * 0.68f;
            using (var b = new LinearGradientBrush(new RectangleF(0, hillTopY, W, H - hillTopY), Art.C(0x74C450), Art.C(0x3F8F34), 90f))
            {
                g.FillEllipse(b, castleX - hillW * 0.75f, hillTopY, hillW * 1.5f, H * 0.7f);
                g.FillEllipse(b, W - hillW * 0.95f, H * 0.76f, hillW * 1.4f, H * 0.6f);
            }
            float cs = Math.Max(H * 0.12f, Math.Min(side * 0.55f, H * 0.3f));
            CastleLit(g, castleX, hillTopY + H * 0.012f, cs, Art.C(0xEDE4CF), Art.C(0xCDBE9F), Art.C(0x3F6FB5), Art.C(0x2E5490),
                      Art.C(0x3B3350), Art.C(0xE24C4C), Color.Empty, false, true);

            // village with a windmill
            float vx = W - Math.Max(side * 0.75f, W * 0.15f), vgy = H * 0.785f;
            Windmill(g, vx + cs * 0.55f, vgy + 2, cs * 0.75f, 20);
            CottageLit(g, vx - cs * 0.2f, vgy + H * 0.008f, cs * 0.3f, Art.C(0xF6EBD3), Art.C(0xDCCBA8), Art.C(0xD25A43), Art.C(0xA8432F), Art.C(0x5A6E9C), false, Color.Empty);
            CottageLit(g, vx + cs * 0.95f, vgy + H * 0.014f, cs * 0.24f, Art.C(0xF6EBD3), Art.C(0xDCCBA8), Art.C(0xE08A3C), Art.C(0xB86A2A), Art.C(0x5A6E9C), false, Color.Empty);

            // trees at the edges
            for (int i = 0; i < 12; i++)
            {
                float px = (float)r.NextDouble() * side * 0.9f;
                if (i % 2 == 1) px = W - px;
                float ph = H * (0.09f + (float)r.NextDouble() * 0.08f);
                RoundTree(g, px, H * (0.87f + (float)r.NextDouble() * 0.07f), ph, Art.C(0x6DBE4E), Art.C(0x3F8F34), Art.C(0x7A5230));
            }
            var fg = MakeRidge(r, 6, 0.6f);
            FillRidge(g, fg, H * 1.0f, H * 0.07f, Art.C(0x56A63E), Art.C(0x3E8C2F));

            // flowers and grass
            var petals = new[] { 0xFF6B6B, 0xFFD23F, 0xFFFFFF, 0xFF9ECF, 0xB28DFF, 0xFF9F40 };
            using (var grass = new Pen(Color.FromArgb(120, 40, 110, 30), Math.Max(1f, 1.5f * u)))
                for (int i = 0; i < 220; i++)
                {
                    float x = (float)r.NextDouble() * W, y = H * (0.88f + (float)r.NextDouble() * 0.12f);
                    g.DrawLine(grass, x, y, x - 2 * u, y - 7 * u);
                    g.DrawLine(grass, x, y, x + 3 * u, y - 6 * u);
                }
            for (int i = 0; i < 260; i++)
            {
                float x = (float)r.NextDouble() * W, y = H * (0.8f + (float)r.NextDouble() * 0.2f);
                float fr = (2.2f + (float)r.NextDouble() * 2.5f) * u * (0.6f + (y - H * 0.8f) / (H * 0.2f) * 0.8f);
                Color c = Art.C(petals[r.Next(petals.Length)]);
                for (int k = 0; k < 5; k++)
                {
                    double a = k * Math.PI * 2 / 5;
                    FillCircle(g, c, x + (float)Math.Cos(a) * fr, y + (float)Math.Sin(a) * fr, fr * 0.75f);
                }
                FillCircle(g, Art.C(0xFFB300), x, y, fr * 0.6f);
            }
        }

        // ---- theme 2: snowy night with northern lights
        void Aurora(Graphics g, Random r, float yBase, float amp, float len, Color c, int alpha)
        {
            float step = Math.Max(2f, 3 * H / 1080f);
            double ph = r.NextDouble() * 6;
            for (float x = 0; x < W; x += step)
            {
                double t = x / W;
                float y = yBase + (float)(Math.Sin(t * 6.0 + ph) * 0.6 + Math.Sin(t * 13.0 + ph * 2) * 0.4) * amp;
                float l = len * (0.6f + 0.4f * (float)Math.Sin(t * 9 + ph * 3));
                int a = Clamp((int)(alpha * (0.45 + 0.55 * Math.Abs(Math.Sin(t * 17 + ph)))));
                using (var b = Stops(x, step + 1, new[] { Color.FromArgb(0, c), Color.FromArgb(a / 3, c), Color.FromArgb(a, c), Color.FromArgb(0, c) },
                                     new[] { y, y + l * 0.5f, y + l * 0.88f, y + l }))
                    g.FillRectangle(b, x, y, step + 0.6f, l);
            }
        }

        static void SnowPine(Graphics g, float x, float gy, float h, Brush dark, Color snow)
        {
            Pine(g, x, gy, h, dark);
            float w = h * 0.36f;
            for (int i = 0; i < 3; i++)
            {
                float top = gy - h + i * h * 0.24f, bot = gy - h * 0.12f - (2 - i) * h * 0.16f;
                float hw = w * (0.45f + i * 0.28f);
                float sb = top + (bot - top) * 0.42f, shw = hw * 0.42f;
                FillPoly(g, snow, x, top - 1, x + shw, sb, x + shw * 0.4f, sb + h * 0.025f, x - shw * 0.3f, sb - h * 0.005f, x - shw, sb);
                FillPoly(g, snow, x - hw, bot, x + hw, bot, x + hw * 0.75f, bot - h * 0.03f, x - hw * 0.75f, bot - h * 0.03f);
            }
        }

        static void Snowman(Graphics g, float x, float gy, float s)
        {
            Color white = Art.C(0xFAFCFF), shade = Art.C(0xC9D6EE);
            FillCircle(g, Color.FromArgb(60, 40, 60, 110), x + s * 0.06f, gy, s * 0.32f);
            float[] cy = { gy - s * 0.28f, gy - s * 0.72f, gy - s * 1.04f }, cr = { s * 0.3f, s * 0.22f, s * 0.16f };
            for (int i = 0; i < 3; i++)
            {
                FillCircle(g, shade, x, cy[i], cr[i]);
                FillCircle(g, white, x - cr[i] * 0.12f, cy[i] - cr[i] * 0.1f, cr[i] * 0.88f);
            }
            using (var pen = new Pen(Art.C(0x6B4428), Math.Max(1.5f, s * 0.025f)))
            {
                g.DrawLine(pen, x - s * 0.2f, cy[1] - s * 0.02f, x - s * 0.45f, cy[1] - s * 0.2f);
                g.DrawLine(pen, x + s * 0.2f, cy[1] - s * 0.02f, x + s * 0.45f, cy[1] - s * 0.22f);
                g.DrawLine(pen, x - s * 0.38f, cy[1] - s * 0.14f, x - s * 0.44f, cy[1] - s * 0.08f);
            }
            FillRect(g, Art.C(0xD8343F), x - s * 0.19f, cy[1] - s * 0.22f, s * 0.38f, s * 0.07f);
            FillRect(g, Art.C(0xD8343F), x + s * 0.06f, cy[1] - s * 0.18f, s * 0.07f, s * 0.2f);
            Color coal = Art.C(0x2A2A3A);
            FillCircle(g, coal, x - s * 0.06f, cy[2] - s * 0.04f, s * 0.022f);
            FillCircle(g, coal, x + s * 0.05f, cy[2] - s * 0.04f, s * 0.022f);
            for (int i = 0; i < 3; i++) FillCircle(g, coal, x, cy[1] + s * (-0.06f + 0.09f * i) + s * 0.04f, s * 0.022f);
            FillPoly(g, Art.C(0xFF8A3D), x, cy[2] - s * 0.01f, x + s * 0.17f, cy[2] + s * 0.015f, x, cy[2] + s * 0.035f);
            FillRect(g, coal, x - s * 0.17f, cy[2] - s * 0.15f, s * 0.34f, s * 0.04f);
            FillRect(g, coal, x - s * 0.1f, cy[2] - s * 0.33f, s * 0.2f, s * 0.19f);
            FillRect(g, Art.C(0xD8343F), x - s * 0.1f, cy[2] - s * 0.2f, s * 0.2f, s * 0.04f);
        }

        static void Smoke(Graphics g, Random r, float x, float y, float s)
        {
            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                FillCircle(g, Color.FromArgb(Clamp((int)(70 * (1 - t))), 220, 228, 245), x + s * (0.15f * t + 0.3f * t * t) + (float)(r.NextDouble() - 0.5) * s * 0.05f,
                           y - s * 0.6f * t, s * (0.06f + 0.08f * t));
            }
        }

        void PaintWinter(Graphics g, Random r)
        {
            float u = H / 1080f, horizon = H * 0.6f, side = boardX - frame;
            float mx = Math.Max(side * 0.32f, W * 0.07f), my = H * 0.14f, mr = H * 0.055f;
            Color win = Art.C(0xFFD27A), flag = Art.C(0xD8343F), snow = Art.C(0xF1F5FF);

            Sky(g, horizon, new[] { Art.C(0x050A22), Art.C(0x0E1B4D), Art.C(0x22397A), Art.C(0x4A69A8), Art.C(0x7C97C9) }, new[] { 0f, 0.35f, 0.65f, 0.88f, 1f });

            for (int i = 0; i < 320; i++)
            {
                float x = (float)r.NextDouble() * W, y = (float)Math.Pow(r.NextDouble(), 1.4) * horizon * 0.7f;
                int a = Clamp((int)(240 * (1 - y / (horizon * 0.7f))));
                float sz = (0.8f + (float)r.NextDouble() * 2f) * u;
                FillCircle(g, Color.FromArgb(a, 235, 242, 255), x, y, sz / 2);
                if (r.Next(12) == 0) Glow(g, x, y, sz * 4, Color.FromArgb(a / 3, 200, 220, 255));
            }

            Aurora(g, r, H * 0.1f, H * 0.05f, H * 0.2f, Art.C(0x9C7BFF), 45);
            Aurora(g, r, H * 0.16f, H * 0.06f, H * 0.22f, Art.C(0x63F5B8), 75);

            // full moon
            Glow(g, mx, my, mr * 4.5f, Color.FromArgb(60, 200, 220, 255));
            Glow(g, mx, my, mr * 1.8f, Color.FromArgb(90, 230, 240, 255));
            FillCircle(g, Art.C(0xF4F6FF), mx, my, mr);
            FillCircle(g, Art.C(0xDDE2F2), mx - mr * 0.3f, my - mr * 0.2f, mr * 0.22f);
            FillCircle(g, Art.C(0xDDE2F2), mx + mr * 0.35f, my + mr * 0.25f, mr * 0.16f);
            FillCircle(g, Art.C(0xE4E8F5), mx + mr * 0.1f, my - mr * 0.45f, mr * 0.1f);
            FillCircle(g, Art.C(0xE4E8F5), mx - mr * 0.25f, my + mr * 0.45f, mr * 0.12f);

            // snowy mountains
            var far = MakeRidge(r, 7, 0.6f);
            FillRidgeStops(g, far, horizon + H * 0.02f, H * 0.24f,
                new[] { Art.C(0xF2F6FF), Art.C(0xC9D6EE), Art.C(0x8FA3CF), Art.C(0x7F95C6) }, new[] { 0f, 0.08f, 0.22f, 1f });
            Mist(g, horizon, H * 0.05f, Color.FromArgb(120, 159, 179, 221));
            var mid = MakeRidge(r, 7, 0.5f);
            float midBase = horizon + H * 0.09f, midAmp = H * 0.16f;
            FillRidgeStops(g, mid, midBase, midAmp,
                new[] { Art.C(0xE3EAF8), Art.C(0xA9BADF), Art.C(0x6E85BA), Art.C(0x627AB0) }, new[] { 0f, 0.07f, 0.2f, 1f });
            float fcx = W - Math.Max(side * 0.55f, W * 0.12f);
            CastleLit(g, fcx, RidgeY(mid, midBase, midAmp, fcx) + H * 0.01f, H * 0.07f, Art.C(0x56689C), Art.C(0x48598A),
                      snow, Art.C(0xC4D1EC), win, flag, snow, true, false);
            Mist(g, midBase - H * 0.01f, H * 0.05f, Color.FromArgb(110, 159, 179, 221));
            var near = MakeRidge(r, 6, 0.5f);
            FillRidge(g, near, horizon + H * 0.16f, H * 0.09f, Art.C(0xC5D3EE), Art.C(0x8DA2CE));

            // frozen lake with the moon in it
            float lakeY = horizon + H * 0.2f;
            using (var b = new LinearGradientBrush(new RectangleF(0, lakeY, W, H - lakeY + 1), Art.C(0x9DB3DE), Art.C(0x2F4278), 90f))
                g.FillRectangle(b, 0, lakeY, W, H - lakeY);
            for (int i = 0; i < 22; i++)
            {
                float ry = lakeY + (float)Math.Pow(i / 22.0, 1.3) * (H - lakeY) * 0.8f + 3 * u;
                float rw = H * (0.03f + 0.06f * (float)r.NextDouble()) * (1 + i * 0.05f);
                float rx = mx + (float)(r.NextDouble() - 0.5) * H * 0.04f;
                FillRect(g, Color.FromArgb(Clamp(150 - i * 5), 235, 242, 255), rx - rw / 2, ry, rw, Math.Max(1.5f, 2.5f * u));
            }
            using (var crack = new Pen(Color.FromArgb(70, 255, 255, 255), Math.Max(1f, u)))
                for (int i = 0; i < 14; i++)
                {
                    float x = (float)r.NextDouble() * W, y = lakeY + (float)r.NextDouble() * (H - lakeY) * 0.5f;
                    g.DrawLines(crack, new[] { new PointF(x, y), new PointF(x + 20 * u, y + 4 * u), new PointF(x + 34 * u, y + 2 * u), new PointF(x + 50 * u, y + 7 * u) });
                }

            // snowy hills, castle and village
            float hillW = Math.Max(side * 1.9f, W * 0.3f);
            float castleX = Math.Max(side * 0.5f, W * 0.1f), hillTopY = H * 0.68f;
            using (var b = new LinearGradientBrush(new RectangleF(0, hillTopY, W, H - hillTopY), Art.C(0xF3F7FF), Art.C(0xAFC0E2), 90f))
            {
                g.FillEllipse(b, castleX - hillW * 0.75f, hillTopY, hillW * 1.5f, H * 0.7f);
                g.FillEllipse(b, W - hillW * 0.95f, H * 0.76f, hillW * 1.4f, H * 0.6f);
            }
            float cs = Math.Max(H * 0.12f, Math.Min(side * 0.55f, H * 0.3f));
            CastleLit(g, castleX, hillTopY + H * 0.012f, cs, Art.C(0x4C5B8E), Art.C(0x3A4775), snow, Art.C(0xC4D1EC), win, flag, snow, true, true);

            float vx = W - Math.Max(side * 0.75f, W * 0.15f), vgy = H * 0.785f;
            Smoke(g, r, vx - cs * 0.2f + cs * 0.3f * 0.74f, vgy - cs * 0.3f * 0.95f, cs * 0.5f);
            Smoke(g, r, vx + cs * 0.55f + cs * 0.26f * 0.74f, vgy - cs * 0.26f * 0.95f, cs * 0.45f);
            CottageLit(g, vx - cs * 0.2f, vgy + H * 0.008f, cs * 0.3f, Art.C(0x6B5A78), Art.C(0x554763), snow, Art.C(0xC4D1EC), win, true, snow);
            CottageLit(g, vx + cs * 0.55f, vgy + H * 0.012f, cs * 0.26f, Art.C(0x7A5A5A), Art.C(0x624848), snow, Art.C(0xC4D1EC), win, true, snow);
            Snowman(g, vx + cs * 1.15f, H * 0.83f, cs * 0.32f);

            using (var pb = new SolidBrush(Art.C(0x1F3A4E)))
                for (int i = 0; i < 16; i++)
                {
                    float px = (float)r.NextDouble() * side * 0.9f;
                    if (i % 2 == 1) px = W - px;
                    float ph = H * (0.07f + (float)r.NextDouble() * 0.09f);
                    SnowPine(g, px, H * (0.86f + (float)r.NextDouble() * 0.08f), ph, pb, snow);
                }
            var fg = MakeRidge(r, 6, 0.6f);
            FillRidge(g, fg, H * 1.0f, H * 0.08f, Color.White, Art.C(0xD0DCF0));
            for (int i = 0; i < 160; i++)
            {
                float x = (float)r.NextDouble() * W, y = H * (0.9f + (float)r.NextDouble() * 0.1f);
                FillCircle(g, Color.FromArgb(150 + r.Next(100), 255, 255, 255), x, y, (0.8f + (float)r.NextDouble() * 1.5f) * u);
            }
        }

        // ---- theme 3: seaside castle in the morning
        static void Ship(Graphics g, float x, float y, float s, Color sail, Color stripe, Color flag)
        {
            FillRect(g, Color.FromArgb(50, 0, 30, 60), x - s * 0.45f, y + s * 0.03f, s * 0.9f, s * 0.05f);
            using (var pen = new Pen(Art.C(0x5A3A22), Math.Max(1.5f, s * 0.03f))) g.DrawLine(pen, x, y - s * 0.16f, x, y - s * 1.02f);
            FillRect(g, sail, x - s * 0.3f, y - s * 0.86f, s * 0.6f, s * 0.48f);
            using (var b = new SolidBrush(sail)) g.FillEllipse(b, x - s * 0.3f, y - s * 0.48f, s * 0.6f, s * 0.18f);
            FillRect(g, stripe, x - s * 0.3f, y - s * 0.72f, s * 0.6f, s * 0.1f);
            FillRect(g, sail, x - s * 0.18f, y - s * 0.98f, s * 0.36f, s * 0.1f);
            FillRect(g, Color.FromArgb(50, 120, 80, 40), x + s * 0.12f, y - s * 0.86f, s * 0.18f, s * 0.5f);
            Pennant(g, new PointF(x, y - s * 1.0f), s * 1.4f, Art.C(0x5A3A22), flag, true);
            FillPoly(g, Art.C(0x7A4A2A), x - s * 0.58f, y - s * 0.2f, x + s * 0.58f, y - s * 0.2f, x + s * 0.42f, y + s * 0.03f, x - s * 0.42f, y + s * 0.03f);
            FillRect(g, Art.C(0xE2B455), x - s * 0.52f, y - s * 0.15f, s * 1.04f, s * 0.025f);
            for (int i = 0; i < 4; i++) FillCircle(g, Art.C(0x3A2414), x - s * 0.27f + i * s * 0.18f, y - s * 0.07f, s * 0.025f);
        }

        static void Rock(Graphics g, float x, float gy, float s)
        {
            FillPoly(g, Art.C(0x5D5A6B), x - s, gy, x - s * 0.7f, gy - s * 0.55f, x - s * 0.1f, gy - s * 0.8f, x + s * 0.6f, gy - s * 0.5f, x + s, gy);
            FillPoly(g, Art.C(0x7E7B8F), x - s * 0.7f, gy - s * 0.55f, x - s * 0.1f, gy - s * 0.8f, x + s * 0.1f, gy - s * 0.3f, x - s * 0.5f, gy - s * 0.2f);
        }

        static void Lighthouse(Graphics g, float x, float gy, float s)
        {
            // beams
            using (var b = new SolidBrush(Color.FromArgb(28, 255, 240, 180)))
            {
                g.FillPolygon(b, new[] { new PointF(x, gy - s * 1.05f), new PointF(x - s * 3.5f, gy - s * 1.45f), new PointF(x - s * 3.5f, gy - s * 0.85f) });
                g.FillPolygon(b, new[] { new PointF(x, gy - s * 1.05f), new PointF(x + s * 3.5f, gy - s * 1.35f), new PointF(x + s * 3.5f, gy - s * 0.95f) });
            }
            using (var body = new GraphicsPath())
            {
                body.AddPolygon(new[] { new PointF(x - s * 0.16f, gy), new PointF(x + s * 0.16f, gy), new PointF(x + s * 0.1f, gy - s * 0.92f), new PointF(x - s * 0.1f, gy - s * 0.92f) });
                using (var wb = new SolidBrush(Art.C(0xF7F4EE))) g.FillPath(wb, body);
                var st = g.Save();
                g.SetClip(body, CombineMode.Intersect);
                for (int i = 0; i < 3; i++) FillRect(g, Art.C(0xD8443A), x - s * 0.2f, gy - s * (0.2f + 0.28f * i), s * 0.4f, s * 0.13f);
                FillRect(g, Color.FromArgb(45, 40, 40, 80), x + s * 0.03f, gy - s, s * 0.2f, s);
                g.Restore(st);
            }
            FillRect(g, Art.C(0x3A3A4A), x - s * 0.15f, gy - s * 0.95f, s * 0.3f, s * 0.04f);
            Glow(g, x, gy - s * 1.05f, s * 0.45f, Color.FromArgb(160, 255, 236, 150));
            FillRect(g, Art.C(0xFFE58A), x - s * 0.08f, gy - s * 1.13f, s * 0.16f, s * 0.18f);
            using (var pen = new Pen(Art.C(0x3A3A4A), Math.Max(1f, s * 0.015f)))
            {
                g.DrawLine(pen, x - s * 0.08f, gy - s * 1.13f, x - s * 0.08f, gy - s * 0.95f);
                g.DrawLine(pen, x + s * 0.08f, gy - s * 1.13f, x + s * 0.08f, gy - s * 0.95f);
                g.DrawLine(pen, x, gy - s * 1.13f, x, gy - s * 0.95f);
            }
            FillPoly(g, Art.C(0xD8443A), x - s * 0.12f, gy - s * 1.12f, x + s * 0.12f, gy - s * 1.12f, x, gy - s * 1.27f);
            FillCircle(g, Art.C(0x3A3A4A), x, gy - s * 1.28f, s * 0.02f);
        }

        void PaintSeaside(Graphics g, Random r)
        {
            float u = H / 1080f, horizon = H * 0.56f, side = boardX - frame;
            float sunX = W - Math.Max(side * 0.45f, W * 0.09f), sunY = horizon - H * 0.07f;

            Sky(g, horizon, new[] { Art.C(0x3F78C2), Art.C(0x7FAEDF), Art.C(0xC4D7EC), Art.C(0xF7D5B5), Art.C(0xFFE3AE) }, new[] { 0f, 0.35f, 0.65f, 0.88f, 1f });

            Glow(g, sunX, sunY, H * 0.5f, Color.FromArgb(120, 255, 214, 150));
            Glow(g, sunX, sunY, H * 0.16f, Color.FromArgb(190, 255, 236, 190));
            float sr = H * 0.06f;
            using (var b = new LinearGradientBrush(new RectangleF(sunX - sr, sunY - sr, 2 * sr, 2 * sr), Art.C(0xFFF6D6), Art.C(0xFFC978), 90f))
                g.FillEllipse(b, sunX - sr, sunY - sr, 2 * sr, 2 * sr);

            for (int i = 0; i < 7; i++)
            {
                float cy = horizon * (0.2f + (float)r.NextDouble() * 0.6f), t = cy / horizon;
                Cloud(g, r, (float)r.NextDouble() * W, cy, W * (0.1f + (float)r.NextDouble() * 0.14f),
                      Color.FromArgb(150, Mix(Art.C(0xF4F7FF), Art.C(0xF7D3D6), t)), Color.FromArgb(170, Mix(Art.C(0xFFFFFF), Art.C(0xFFE6C4), t)));
            }
            for (int i = 0; i < 4; i++)
                PuffCloud(g, r, (float)r.NextDouble() * W, H * (0.08f + (float)r.NextDouble() * 0.15f), H * (0.04f + (float)r.NextDouble() * 0.04f),
                          Color.FromArgb(235, 255, 255, 255), Color.FromArgb(220, 214, 222, 240));
            Birds(g, r, side * 0.1f, side * 0.9f, H * 0.12f, H * 0.3f, 4, Color.FromArgb(160, 60, 70, 90), u);
            Birds(g, r, W - side * 0.9f, W - side * 0.1f, H * 0.15f, H * 0.3f, 4, Color.FromArgb(160, 60, 70, 90), u);

            // faraway islands, one with a tiny castle
            Island(g, r, W * 0.18f, W * 0.42f, horizon + 1, H * 0.06f, Art.C(0x9AA6CB), Art.C(0xB6BFD9));
            Island(g, r, W * 0.7f, W * 0.86f, horizon + 1, H * 0.035f, Art.C(0xA5B0D2), Art.C(0xBEC6DD));
            Castle(g, W * 0.3f, horizon - H * 0.02f, H * 0.04f, Art.C(0x8E98BF), Art.C(0xFFE3AE), Art.C(0x8E98BF), false);

            // the sea, the sun's path and little waves
            using (var b = Stops(0, W, new[] { Art.C(0x7FC0DB), Art.C(0x3E8EBD), Art.C(0x1F5F8F), Art.C(0x174E7A) }, new[] { horizon, horizon + H * 0.08f, H * 0.8f, H + 1 }))
                g.FillRectangle(b, 0, horizon, W, H - horizon);
            for (int i = 0; i < 26; i++)
            {
                float ry = horizon + (float)Math.Pow(i / 26.0, 1.3) * (H - horizon) * 0.8f + 3 * u;
                float rw = H * (0.05f + 0.1f * (float)r.NextDouble()) * (1 + i * 0.05f);
                float rx = sunX + (float)(r.NextDouble() - 0.5) * H * 0.05f;
                FillRect(g, Color.FromArgb(Clamp(190 - i * 6), 255, 236, 180), rx - rw / 2, ry, rw, Math.Max(1.5f, 2.5f * u));
            }
            for (int i = 0; i < 320; i++)
            {
                float t = (float)Math.Pow(r.NextDouble(), 1.3), y = horizon + H * 0.01f + t * (H - horizon);
                float len = (5 + 34 * t) * u, x = (float)r.NextDouble() * W;
                using (var pen = new Pen(Color.FromArgb(50 + r.Next(70), 255, 255, 255), Math.Max(1f, (1 + 2 * t) * u)))
                    g.DrawArc(pen, x, y, len, len * 0.35f, 200, 140);
            }

            Ship(g, Math.Max(side * 0.85f, W * 0.15f), horizon + H * 0.04f, H * 0.045f, Art.C(0xFBF3E2), Art.C(0x3E7BD6), Art.C(0xFFD23F));
            Ship(g, W - Math.Max(side * 0.8f, W * 0.14f), horizon + H * 0.12f, H * 0.085f, Art.C(0xFBF3E2), Art.C(0xD8443A), Art.C(0x3E7BD6));

            // the castle cliff
            float cliffW = Math.Max(side * 1.05f, W * 0.2f), cliffTop = H * 0.66f;
            float castleX = Math.Max(side * 0.45f, W * 0.09f);
            var cliff = new List<PointF> { new PointF(-2, H + 2), new PointF(-2, cliffTop + H * 0.01f) };
            for (int i = 0; i <= 10; i++) cliff.Add(new PointF(cliffW * 0.85f * i / 10f, cliffTop + (float)(r.NextDouble() - 0.5) * H * 0.012f + (i > 7 ? (i - 7) * H * 0.008f : 0)));
            cliff.Add(new PointF(cliffW * 0.95f, cliffTop + H * 0.07f));
            cliff.Add(new PointF(cliffW * 0.9f, cliffTop + H * 0.12f));
            cliff.Add(new PointF(cliffW * 1.05f, cliffTop + H * 0.2f));
            cliff.Add(new PointF(cliffW * 1.15f, H + 2));
            using (var b = new LinearGradientBrush(new RectangleF(0, cliffTop - 1, W, H - cliffTop + 3), Art.C(0xC99E78), Art.C(0x7A5640), 90f))
                g.FillPolygon(b, cliff.ToArray());
            var st0 = g.Save();
            using (var clip = new GraphicsPath()) { clip.AddPolygon(cliff.ToArray()); g.SetClip(clip, CombineMode.Intersect); }
            for (int i = 0; i < 26; i++)
            {
                float bx = (float)r.NextDouble() * cliffW * 1.1f, by = cliffTop + H * 0.03f + (float)r.NextDouble() * (H - cliffTop);
                float bw = H * (0.04f + (float)r.NextDouble() * 0.07f), bh = bw * (0.35f + (float)r.NextDouble() * 0.25f);
                using (var b = new SolidBrush(Color.FromArgb(40, 255, 235, 200))) g.FillEllipse(b, bx - bw / 2, by - bh / 2, bw, bh);
                using (var b = new SolidBrush(Color.FromArgb(45, 70, 35, 15))) g.FillEllipse(b, bx - bw / 2 + bw * 0.08f, by, bw * 0.9f, bh * 0.5f);
            }
            using (var pen = new Pen(Color.FromArgb(60, 70, 35, 15), Math.Max(1f, 1.6f * u)))
                for (int i = 0; i < 10; i++)
                {
                    float x = (float)r.NextDouble() * cliffW, y = cliffTop + H * 0.03f + (float)r.NextDouble() * (H - cliffTop) * 0.7f;
                    g.DrawLines(pen, new[] { new PointF(x, y), new PointF(x + 6 * u, y + 18 * u), new PointF(x + 2 * u, y + 34 * u), new PointF(x + 9 * u, y + 52 * u) });
                }
            g.Restore(st0);
            using (var grass = new GraphicsPath(FillMode.Winding))
            {
                var top = new List<PointF>();
                for (int i = 2; i < 13; i++) top.Add(cliff[i]);
                for (int i = 12; i >= 2; i--) top.Add(new PointF(cliff[i].X, cliff[i].Y + H * (0.018f + 0.01f * (i % 3))));
                grass.AddPolygon(top.ToArray());
                using (var gb = new SolidBrush(Art.C(0x7DBB5A))) g.FillPath(gb, grass);
            }
            float cs = Math.Max(H * 0.12f, Math.Min(side * 0.5f, H * 0.28f));
            CastleLit(g, castleX, cliffTop + H * 0.006f, cs, Art.C(0xF1DEC4), Art.C(0xD3B596), Art.C(0xD0573F), Art.C(0xA8432F),
                      Art.C(0x4A3B5C), Art.C(0x2F6FC0), Color.Empty, false, true);

            // lighthouse on the rocks
            float lhx = W - Math.Max(side * 0.3f, W * 0.06f), rocksY = H * 0.75f;
            float ls = Math.Max(H * 0.14f, Math.Min(side * 0.4f, H * 0.24f));
            using (var b = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillEllipse(b, lhx - ls * 0.6f, rocksY + H * 0.008f, ls * 1.2f, H * 0.018f);
            Rock(g, lhx - ls * 0.2f, rocksY + H * 0.004f, ls * 0.28f);
            Rock(g, lhx + ls * 0.22f, rocksY + H * 0.006f, ls * 0.22f);
            Lighthouse(g, lhx, rocksY - H * 0.004f, ls);
            Rock(g, lhx - ls * 0.38f, rocksY + H * 0.012f, ls * 0.16f);
            Rock(g, lhx + ls * 0.02f, rocksY + H * 0.014f, ls * 0.2f);
            Rock(g, lhx + ls * 0.42f, rocksY + H * 0.012f, ls * 0.13f);

            // sandy beach with shells, starfish and a sandcastle
            var beach = MakeRidge(r, 6, 0.4f);
            float beachBase = H * 0.99f, beachAmp = H * 0.08f;
            var foamPts = new PointF[beach.Length];
            for (int i = 0; i < beach.Length; i++) foamPts[i] = new PointF(W * i / (float)(beach.Length - 1), beachBase - beach[i] * beachAmp - 2 * u);
            using (var wet = new Pen(Color.FromArgb(120, 120, 200, 210), 10 * u)) g.DrawLines(wet, foamPts);
            FillRidge(g, beach, beachBase, beachAmp, Art.C(0xF5DDA8), Art.C(0xE2BC80));
            using (var foam = new Pen(Color.FromArgb(200, 255, 255, 255), Math.Max(1.5f, 3 * u))) g.DrawLines(foam, foamPts);
            for (int i = 0; i < 6; i++)
            {
                float x = (float)r.NextDouble() * side * 0.9f;
                if (i % 2 == 1) x = W - x;
                float y = H * (0.95f + (float)r.NextDouble() * 0.04f);
                if (i < 3)
                {
                    using (var p = Art.Star(x, y, H * 0.016f)) using (var b = new SolidBrush(Art.C(0xFF8A5B))) g.FillPath(b, p);
                }
                else
                {
                    float sh = H * 0.012f;
                    using (var b = new SolidBrush(Art.C(0xF7B7C8))) g.FillPie(b, x - sh, y - sh, 2 * sh, 2 * sh, 180, 180);
                    using (var pen = new Pen(Art.C(0xD98AA0), Math.Max(1f, u)))
                        for (int k = 1; k < 4; k++) g.DrawLine(pen, x, y, x + (float)Math.Cos(Math.PI + k * Math.PI / 4) * sh, y + (float)Math.Sin(Math.PI + k * Math.PI / 4) * sh);
                }
            }
            float sx = W - Math.Max(side * 0.7f, W * 0.13f);
            CastleLit(g, sx, H * 0.965f, H * 0.075f, Art.C(0xEBCB8F), Art.C(0xCFAA6E), Art.C(0xE0BE80), Art.C(0xC59F62),
                      Art.C(0xB48F58), Art.C(0xEF5350), Color.Empty, false, true);
        }

        // ---------------------------------------------------------- theme animations
        static float Hash(int i, double k) { return (float)(Math.Abs(Math.Sin(i * k) * 43758.5453) % 1.0); }

        void DrawAmbient(Graphics g)
        {
            switch (Theme)
            {
                case 1: DrawButterflies(g); break;
                case 2: DrawSnow(g); break;
                case 3: DrawSeaLife(g); break;
                default: DrawFireflies(g); break;
            }
        }

        void DrawButterflies(Graphics g)
        {
            float side = boardX - frame;
            if (side < H * 0.05f) return;
            var wings = new[] { 0xFF8A3D, 0xFFD23F, 0xE76FB0, 0x4D9DE0, 0xFFFFFF, 0x9B6BD6 };
            for (int i = 0; i < 8; i++)
            {
                float bx = Hash(i + 1, 12.9898) * side * 0.8f + side * 0.1f;
                if (i % 2 == 1) bx = W - bx;
                float by = H * (0.45f + 0.4f * Hash(i + 1, 78.233));
                float t = Time * (0.35f + 0.15f * Hash(i + 1, 4.1)) + i * 1.3f;
                float x = bx + (float)(Math.Sin(t) * 0.7 + Math.Sin(t * 2.3) * 0.3) * side * 0.12f;
                float y = by + (float)(Math.Sin(t * 1.7) * 0.6 + Math.Cos(t * 0.9) * 0.4) * H * 0.04f;
                float s = H * 0.014f, ww = s * (0.2f + 0.8f * Math.Abs((float)Math.Sin(Time * 12 + i * 2)));
                Color c = Art.C(wings[i % wings.Length]);
                using (var b = new SolidBrush(c)) using (var d = new SolidBrush(Art.Dark(c, 0.78f)))
                {
                    g.FillEllipse(b, x - ww, y - s * 0.9f, ww, s);
                    g.FillEllipse(b, x, y - s * 0.9f, ww, s);
                    g.FillEllipse(d, x - ww * 0.8f, y - s * 0.1f, ww * 0.8f, s * 0.7f);
                    g.FillEllipse(d, x, y - s * 0.1f, ww * 0.8f, s * 0.7f);
                }
                using (var pen = new Pen(Art.C(0x3A2A20), Math.Max(1.5f, s * 0.18f))) g.DrawLine(pen, x, y - s * 0.7f, x, y + s * 0.55f);
            }
        }

        void DrawSnow(Graphics g)
        {
            using (var b = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                for (int i = 0; i < 90; i++)
                {
                    float speed = H * (0.04f + 0.05f * Hash(i + 1, 3.7));
                    float y = (Hash(i + 1, 78.233) * (H + 40) + Time * speed) % (H + 40) - 20;
                    float x = Hash(i + 1, 12.9898) * W + (float)Math.Sin(Time * 0.8 + i) * H * 0.02f;
                    float rad = H * (0.0018f + 0.0028f * Hash(i + 1, 5.3));
                    g.FillEllipse(b, x - rad, y - rad, 2 * rad, 2 * rad);
                }
        }

        void DrawSeaLife(Graphics g)
        {
            float side = boardX - frame;
            float horizon = H * 0.56f, sunX = W - Math.Max(side * 0.45f, W * 0.09f);
            // glitter on the water under the sun
            for (int i = 0; i < 16; i++)
            {
                float x = sunX + (Hash(i + 1, 12.9898) - 0.5f) * H * 0.22f;
                float y = horizon + H * 0.02f + Hash(i + 1, 78.233) * H * 0.2f;
                float tw = (float)Math.Sin(Time * 3 + i * 1.7);
                if (tw <= 0 || (x > boardX - frame - H * 0.02f && x < boardX + 8 * sq + frame + H * 0.02f)) continue;
                float rad = H * 0.006f * tw;
                Glow(g, x, y, rad * 3, Color.FromArgb(Clamp((int)(160 * tw)), 255, 245, 210));
                FillPoly(g, Color.FromArgb(Clamp((int)(230 * tw)), 255, 255, 255), x - rad * 2, y, x, y - rad * 0.4f, x + rad * 2, y, x, y + rad * 0.4f);
                FillPoly(g, Color.FromArgb(Clamp((int)(230 * tw)), 255, 255, 255), x, y - rad * 1.5f, x + rad * 0.4f, y, x, y + rad * 1.5f, x - rad * 0.4f, y);
            }
            // seagulls circling beside the board
            if (side < H * 0.05f) return;
            using (var pen = new Pen(Color.FromArgb(230, 250, 250, 255), Math.Max(1.5f, H * 0.003f)))
            using (var dark = new Pen(Color.FromArgb(200, 70, 80, 100), Math.Max(1.5f, H * 0.003f)))
                for (int i = 0; i < 5; i++)
                {
                    float bx = Hash(i + 1, 9.17) * side * 0.6f + side * 0.2f;
                    if (i % 2 == 1) bx = W - bx;
                    float by = H * (0.12f + 0.25f * Hash(i + 1, 33.1));
                    float t = Time * (0.25f + 0.1f * Hash(i + 1, 2.3)) + i * 2f;
                    float x = bx + (float)Math.Cos(t) * side * 0.18f, y = by + (float)Math.Sin(t) * H * 0.035f;
                    float s = H * 0.016f, flap = (float)Math.Sin(Time * 5 + i) * s * 0.35f;
                    var pts = new[] { new PointF(x - s, y - flap), new PointF(x - s * 0.45f, y - s * 0.3f), new PointF(x, y), new PointF(x + s * 0.45f, y - s * 0.3f), new PointF(x + s, y - flap) };
                    g.DrawLines(dark, new[] { pts[0], new PointF(pts[0].X + s * 0.15f, pts[0].Y + s * 0.02f) });
                    g.DrawCurve(pen, pts, 0.4f);
                }
        }

        // ---------------------------------------------------------- theme switching
        public static readonly string[] ThemeNames = { "Castle at Dusk", "Sunny Meadow", "Snowy Night", "Seaside Castle" };
        public const float ThemeSeconds = 240f; // a new scene every 4 minutes
        const float RevealTime = 1.6f;
        public int Theme { get; private set; }
        float themeSince;
        Bitmap oldBackground; float revealBorn = -100;

        static int WrapTheme(int t) { int n = ThemeNames.Length; return ((t % n) + n) % n; }

        public void SetTheme(int t, bool announce)
        {
            t = WrapTheme(t);
            themeSince = Time;
            if (t == Theme && background != null) return;
            Theme = t;
            if (background == null) return;
            if (oldBackground != null) oldBackground.Dispose();
            oldBackground = background;
            background = BuildBackground();
            revealBorn = Time;
            if (announce) { SetCaption(ThemeNames[t]); Tune(new[] { 7, 5, 3, 5, 7, 10 }, 0.1f, 0.45f); }
        }

        public void NextTheme() { SetTheme(Theme + 1, true); }

        // the new scene opens up from the middle in a growing circle, with a ring of sparkles at its edge
        void DrawReveal(Graphics g)
        {
            float k = (Time - revealBorn) / RevealTime;
            if (k >= 1 || k < 0) { oldBackground.Dispose(); oldBackground = null; return; }
            float e = k * k * (3 - 2 * k);
            float rad = e * ((float)Math.Sqrt((double)W * W + (double)H * H) / 2 + 4);
            using (var hole = new GraphicsPath())
            {
                hole.AddEllipse(W / 2f - rad, H / 2f - rad, 2 * rad, 2 * rad);
                var st = g.Save();
                g.SetClip(hole, CombineMode.Exclude);
                g.DrawImageUnscaled(oldBackground, 0, 0);
                g.Restore(st);
            }
            int a = Clamp((int)(230 * Math.Min(1, (1 - k) * 3)));
            for (int i = 0; i < 30; i++)
            {
                double ang = i * Math.PI * 2 / 30 + Time * 0.9;
                float x = W / 2f + (float)Math.Cos(ang) * rad, y = H / 2f + (float)Math.Sin(ang) * rad;
                Glow(g, x, y, H * 0.03f, Color.FromArgb(a / 2, 255, 225, 130));
                FillCircle(g, Color.FromArgb(a, 255, 250, 220), x, y, H * 0.005f);
            }
        }

        // fireflies drifting beside the board (animated, cheap)
        void DrawFireflies(Graphics g)
        {
            float side = boardX - frame;
            if (side < H * 0.05f) return;
            for (int i = 0; i < 18; i++)
            {
                double h1 = Math.Abs(Math.Sin(i * 12.9898) * 43758.5453) % 1.0, h2 = Math.Abs(Math.Sin(i * 78.233) * 12543.123) % 1.0;
                float bx = (float)h1 * side * 0.9f + side * 0.05f;
                if (i % 2 == 1) bx = W - bx;
                float by = H * (0.62f + 0.3f * (float)h2);
                float x = bx + (float)Math.Sin(Time * 0.7f + i) * H * 0.03f;
                float y = by + (float)Math.Sin(Time * 0.5f + i * 1.7f) * H * 0.02f;
                float pulse = 0.5f + 0.5f * (float)Math.Sin(Time * 2.2f + i * 2.1f);
                int a = Clamp((int)(220 * pulse));
                float rr = H * 0.004f;
                Glow(g, x, y, rr * 4, Color.FromArgb(a / 2, 255, 220, 120));
                using (var b = new SolidBrush(Color.FromArgb(a, 255, 245, 190))) g.FillEllipse(b, x - rr, y - rr, rr * 2, rr * 2);
            }
        }

        public void Draw(Graphics g)
        {
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImageUnscaled(background, 0, 0);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (oldBackground != null) { try { DrawReveal(g); } catch { } }

            try { DrawAmbient(g); } catch { }
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
            string text = "Parents: to exit, hold Ctrl + Alt and type Q U I T   \u00b7   Ctrl + Alt + N shows the next scene";
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

        // One picture per theme (prefix0.png ...), a smaller 5:4 screen, and a frame from the middle of a scene change.
        public static void Themes(string prefix)
        {
            for (int t = 0; t < ThemeNames.Length; t++)
            {
                var s = new Scene(1920, 1080, 1, t);
                for (int i = 0; i < 20; i++) s.Update(0.1f);
                s.OnKey('K'); s.Update(0.5f);
                s.OnClick((int)(s.boardX + 2.5f * s.sq), (int)(s.boardY + 4.5f * s.sq), false);
                s.Update(0.5f);
                using (var bmp = new Bitmap(s.W, s.H))
                using (var g = Graphics.FromImage(bmp)) { s.Draw(g); bmp.Save(prefix + t + ".png", ImageFormat.Png); }
            }
            var small = new Scene(1280, 1024, 1, 3);
            using (var bmp = new Bitmap(small.W, small.H))
            using (var g = Graphics.FromImage(bmp)) { small.Draw(g); bmp.Save(prefix + "small.png", ImageFormat.Png); }
            var sw = new Scene(1920, 1080, 1, 0);
            sw.Update(1f); sw.NextTheme(); sw.Update(0.7f);
            using (var bmp = new Bitmap(sw.W, sw.H))
            using (var g = Graphics.FromImage(bmp)) { sw.Draw(g); bmp.Save(prefix + "reveal.png", ImageFormat.Png); }
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

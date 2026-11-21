// A play session: one game, locked full-screen on every monitor until a parent types the exit code.
//   PARENT, BACK TO THE MENU: hold Ctrl + Alt and type  Q U I T
//   PARENT, NEXT SCENE:       Ctrl + Alt + N  (what "next" means is up to the game)
// Blocks every key, including the Windows key, Alt+Tab, Alt+F4, Ctrl+Esc and the Sticky Keys popup.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ForYusef
{
    // Every monitor gets its own full-size copy of the game (as if it were the only screen). All copies
    // receive the same events with the same random seed, so they show the same thing. Sound plays only once.
    class PlaySession
    {
        public const string ExitHint = "Parents: hold Ctrl + Alt and type Q U I T to go back to the menu";

        readonly GameInfo info;
        readonly Harp harp;
        readonly Action<bool> ended; // true = Windows is shutting down / logging off, so the app should quit too
        readonly int seed = Environment.TickCount & 0x7FFFFFFF;

        readonly List<ScreenForm> forms = new List<ScreenForm>();
        readonly Timer frameTimer = new Timer();
        readonly Timer guardTimer = new Timer();
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

        bool cursorHidden;
        string screenLayout = "";

        public PlaySession(GameInfo info, Harp harp, Action<bool> ended)
        {
            this.info = info; this.harp = harp; this.ended = ended;
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

            if (forms.Count > 0) { try { forms[0].Game.Start(); } catch { } }
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
            string hint = ExitHint + (string.IsNullOrEmpty(info.ParentNextHint) ? "" : "   ·   " + info.ParentNextHint);
            var screens = Screen.AllScreens;
            // primary screen first, so it is the one that plays sound
            Array.Sort(screens, (x, y) => (y.Primary ? 1 : 0) - (x.Primary ? 1 : 0));
            foreach (var sc in screens)
            {
                var game = info.Create(Math.Max(1, sc.Bounds.Width), Math.Max(1, sc.Bounds.Height), seed);
                game.StartHint = hint;
                if (forms.Count == 0 && harp != null) game.Play = harp.Play;
                else game.Play = delegate { };
                forms.Add(new ScreenForm(this, sc.Bounds, game, info.Title));
            }
            foreach (var f in forms) f.Show();
            if (forms.Count > 0) { forms[0].Activate(); Native.SetForegroundWindow(forms[0].Handle); }
        }

        void Tick()
        {
            double now = clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.05, now - lastFrame);
            lastFrame = now;
            foreach (var f in forms) { try { f.Game.Update(dt); } catch { } f.Invalidate(); }
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

        // ---------------- events coming from any window, broadcast to every copy
        public void MouseMoved(ScreenForm src, int x, int y)
        {
            float nx = x / (float)src.Game.W, ny = y / (float)src.Game.H;
            foreach (var f in forms) { try { f.Game.OnMouseMove((int)(nx * f.Game.W), (int)(ny * f.Game.H)); } catch { } }
        }

        public void MouseClicked(ScreenForm src, int x, int y, bool right)
        {
            object resolved = null;
            try { resolved = src.Game.ResolveClick(x, y, right); } catch { } // decided once, on the screen that was clicked
            float nx = x / (float)src.Game.W, ny = y / (float)src.Game.H;
            foreach (var f in forms) { try { f.Game.Click(resolved, (int)(nx * f.Game.W), (int)(ny * f.Game.H)); } catch { } }
        }

        public void MouseWheel(ScreenForm src, int x, int y)
        {
            float nx = x / (float)src.Game.W, ny = y / (float)src.Game.H;
            foreach (var f in forms) { try { f.Game.OnWheel((int)(nx * f.Game.W), (int)(ny * f.Game.H)); } catch { } }
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
                foreach (var f in forms) { try { f.Game.ParentNext(); } catch { } }
                return;
            }
            foreach (var f in forms) { try { f.Game.OnKey(vk); } catch { } }
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
            if (forms.Count > 0) forms[0].BeginInvoke((MethodInvoker)delegate { Finish(false); });
            else Finish(false);
        }

        // Called when the parent unlocks (back to the menu), or when Windows is shutting down.
        public void Finish(bool systemClosing)
        {
            if (finished) return;
            finished = true; unlocked = true;
            frameTimer.Stop(); guardTimer.Stop();
            frameTimer.Dispose(); guardTimer.Dispose();
            if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            try { access.Restore(); } catch { }
            Native.SetThreadExecutionState(Native.ES_CONTINUOUS);
            if (cursorHidden) { Cursor.Show(); cursorHidden = false; }
            foreach (var f in forms) { f.AllowClose = true; try { f.Close(); } catch { } } // a closed form disposes itself
            forms.Clear();
            ended(systemClosing);
        }
    }

    class ScreenForm : Form
    {
        public readonly IGame Game;
        public bool AllowClose;
        readonly PlaySession owner;

        public ScreenForm(PlaySession owner, Rectangle bounds, IGame game, string title)
        {
            this.owner = owner;
            Game = game;
            Text = "ForYusef - " + title;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(126, 200, 242);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            // Never let a drawing error escape: WinForms would replace the window with a permanent red X.
            try { Game.Draw(e.Graphics); }
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
            if (!AllowClose) owner.Finish(true); // Windows shutdown / logoff: clean up properly
            base.OnFormClosing(e);
        }
    }
}

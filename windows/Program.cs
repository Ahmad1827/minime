// ForYusef: a little collection of games for a baby to play on the PC with a parent.
// The app opens on a menu. Clicking a game starts it locked full-screen on every monitor
// (keyboard shortcuts blocked, so smashing the keys can't break anything).
//   PARENT, BACK TO THE MENU: hold Ctrl + Alt and type  Q U I T
//   PARENT, NEXT SCENE:       Ctrl + Alt + N
// Close the app with the x in the menu's corner.
//
// Build (no install needed, uses the C# compiler that ships with Windows): see build.bat / build.sh
// Written for C# 5 (the csc.exe bundled with .NET Framework 4.x).
// Previews without locking anything:
//   ForYusef.exe --menu out.png
//   ForYusef.exe --pilot prefix   (Little Pilot: one picture per sky, plus a sky change)
//   ForYusef.exe --snapshot out.png  /  --gallery out.png  /  --mirror out.png  /  --themes prefix   (Little Knight)

using System;
using System.Threading;
using System.Windows.Forms;
using LK = ForYusef.LittleKnight;

namespace ForYusef
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); } catch { }

            if (args.Length >= 2)
            {
                switch (args[0])
                {
                    case "--menu": MenuView.Snapshot(args[1], 1920, 1080); return;
                    case "--pilot": ForYusef.LittlePilot.PilotGame.Snapshot(args[1]); return;
                    case "--snapshot": LK.Scene.Snapshot(args[1]); return;
                    case "--gallery": LK.Scene.Gallery(args[1]); return;
                    case "--mirror": LK.Scene.Mirror(args[1]); return;
                    case "--themes": LK.Scene.Themes(args[1]); return;
                }
            }

            bool created;
            using (var mutex = new Mutex(true, "ForYusef_SingleInstance", out created))
            {
                if (!created) return;
                // Never crash back to the desktop because of a drawing/sound glitch.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate { };
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new App());
            }
        }
    }

    // Menu <-> game. One harp (sound device) for the whole app.
    class App : ApplicationContext
    {
        Harp harp;
        readonly MenuForm menu;
        PlaySession session;
        bool quitting;

        public App()
        {
            harp = Harp.TryCreate();
            menu = new MenuForm(StartGame, Quit, harp != null ? (Action<int, float, float>)harp.Play : null);
            menu.FormClosed += delegate { Quit(); };
            menu.Show();
            menu.Activate();
        }

        void StartGame(GameInfo info)
        {
            if (session != null || quitting) return;
            menu.Hide();
            try { session = new PlaySession(info, harp, Ended); }
            catch { session = null; menu.Show(); }
        }

        void Ended(bool systemClosing)
        {
            session = null;
            if (systemClosing) { Quit(); return; }
            menu.Show();
            menu.Activate();
            Native.SetForegroundWindow(menu.Handle);
        }

        void Quit()
        {
            if (quitting) return;
            quitting = true;
            if (session != null) { var s = session; session = null; s.Finish(true); }
            if (harp != null) { try { harp.Dispose(); } catch { } harp = null; }
            try { if (!menu.IsDisposed) menu.Close(); } catch { }
            ExitThread();
        }
    }
}

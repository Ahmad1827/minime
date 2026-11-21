package com.foryusef;

import android.app.Activity;
import android.app.ActivityManager;
import android.os.Build;
import android.os.Bundle;
import android.view.KeyEvent;
import android.view.View;
import android.view.Window;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import android.view.WindowManager;
import android.window.OnBackInvokedDispatcher;

/**
 * Runs one game, pinned to the screen (Android "screen pinning" / lock task),
 * so Home, Recents and the notification shade do nothing until a parent goes back to the menu.
 * PARENT, BACK TO THE MENU: hold one finger on the top-left corner and one on the top-right corner for 3 seconds.
 * PARENT, NEXT SCENE: hold one finger on each bottom corner for 1.5 seconds.
 */
public final class GameActivity extends Activity implements GameView.Listener {
    private final Harp harp = new Harp();
    private GameView view;
    private boolean exiting;
    static final String EXTRA_GAME = "game";

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        Window w = getWindow();
        w.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON | WindowManager.LayoutParams.FLAG_FULLSCREEN);
        if (Build.VERSION.SDK_INT >= 28)
            w.getAttributes().layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
        int index = getIntent().getIntExtra(EXTRA_GAME, 0);
        GameInfo info = Catalog.ALL[Math.max(0, Math.min(Catalog.ALL.length - 1, index))];
        view = new GameView(this, info, harp, this);
        setContentView(view);
        if (Build.VERSION.SDK_INT >= 33)
            getOnBackInvokedDispatcher().registerOnBackInvokedCallback(OnBackInvokedDispatcher.PRIORITY_OVERLAY, () -> { });
        view.post(this::pin);
    }

    private void pin() {
        if (exiting) return;
        try {
            ActivityManager am = (ActivityManager) getSystemService(ACTIVITY_SERVICE);
            if (am == null || am.getLockTaskModeState() == ActivityManager.LOCK_TASK_MODE_NONE) startLockTask();
        } catch (RuntimeException ignored) {
            // pinning unavailable: the game still works, Home just isn't blocked
        }
    }

    @Override protected void onResume() {
        super.onResume();
        hideBars();
        view.resetClock();
        harp.start();
    }

    @Override protected void onPause() {
        harp.stop();
        super.onPause();
    }

    @Override public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) hideBars();
    }

    @SuppressWarnings("deprecation")
    private void hideBars() {
        if (Build.VERSION.SDK_INT >= 30) {
            getWindow().setDecorFitsSystemWindows(false);
            WindowInsetsController c = getWindow().getInsetsController();
            if (c != null) {
                c.hide(WindowInsets.Type.systemBars());
                c.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
            }
        } else {
            getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                | View.SYSTEM_UI_FLAG_FULLSCREEN | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_LAYOUT_STABLE);
        }
    }

    @Override public void onUnlock() {
        if (exiting) return;
        exiting = true;
        try { stopLockTask(); } catch (RuntimeException ignored) { }
        finish(); // back to the menu
    }

    @Override public boolean onKeyDown(int keyCode, KeyEvent event) {
        return view.key(keyCode, event) || super.onKeyDown(keyCode, event);
    }

    @Override public boolean onKeyUp(int keyCode, KeyEvent event) {
        boolean system = keyCode == KeyEvent.KEYCODE_VOLUME_UP || keyCode == KeyEvent.KEYCODE_VOLUME_DOWN || keyCode == KeyEvent.KEYCODE_VOLUME_MUTE;
        return !system || super.onKeyUp(keyCode, event);
    }

    @SuppressWarnings("deprecation")
    @Override public void onBackPressed() { /* the baby can't back out */ }
}

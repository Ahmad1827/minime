package com.minime.littleknight;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.PointF;
import android.graphics.RectF;
import android.util.SparseArray;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.View;

/** Full-screen view: runs the scene every frame and turns touches and keys into scene events. */
final class GameView extends View {
    /** Parent exit: keep one finger on the top-left corner and one on the top-right corner for this long. */
    static final float UNLOCK_SECONDS = 3f;
    /** Parent scene change: keep one finger on each bottom corner for this long. */
    static final float THEME_SECONDS = 1.5f;
    static final String START_HINT = "Parents: hold both top corners 3 s to exit  \u00b7  both bottom corners for the next scene";

    interface Listener { void onUnlock(); }

    private final Harp harp;
    private final Listener listener;
    private Scene scene;
    private boolean welcomed;
    private long lastNanos;
    private boolean nextWhite = true;

    private static final class Finger { float x, y, trailX, trailY; }
    private final SparseArray<Finger> fingers = new SparseArray<>();
    private float holdTime = -1, themeHold = -1;
    private boolean themeArmed = true;
    private final Paint ring = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF arc = new RectF();

    GameView(Context ctx, Harp harp, Listener listener) {
        super(ctx);
        this.harp = harp;
        this.listener = listener;
        setFocusable(true);
        setFocusableInTouchMode(true);
        setKeepScreenOn(true);
        setHapticFeedbackEnabled(false);
        setSoundEffectsEnabled(false);
        ring.setStyle(Paint.Style.STROKE);
        ring.setStrokeCap(Paint.Cap.ROUND);
    }

    @Override protected void onSizeChanged(int w, int h, int oldw, int oldh) {
        if (w <= 0 || h <= 0) return;
        if (scene == null) {
            scene = new Scene(w, h, new java.util.Random().nextInt(Scene.THEME_NAMES.length));
            scene.sound = harp::play;
            scene.startHint = START_HINT;
        } else {
            float t = scene.time;
            scene.resize(w, h);
            scene.time = t;
        }
        if (!welcomed) { welcomed = true; scene.welcome(); }
    }

    @Override protected void onDraw(Canvas canvas) {
        long now = System.nanoTime();
        float dt = lastNanos == 0 ? 0 : Math.min(0.05f, (now - lastNanos) / 1e9f);
        lastNanos = now;
        if (scene != null) {
            try { scene.update(dt); } catch (RuntimeException ignored) { }
            scene.fingers.clear();
            for (int i = 0; i < fingers.size(); i++) { Finger f = fingers.valueAt(i); scene.fingers.add(new PointF(f.x, f.y)); }
            try { scene.draw(canvas); } catch (RuntimeException e) { canvas.drawColor(Color.rgb(20, 27, 69)); }
            checkUnlock(canvas, dt);
        }
        postInvalidateOnAnimation();
    }

    // ---------------------------------------------------------- parent exit
    private boolean inCorner(Finger f, boolean left, boolean top) {
        float zw = Math.max(getWidth() * 0.09f, getHeight() * 0.14f), zh = getHeight() * 0.16f;
        return (top ? f.y < zh : f.y > getHeight() - zh) && (left ? f.x < zw : f.x > getWidth() - zw);
    }

    private boolean bothCorners(boolean top) {
        boolean left = false, right = false;
        for (int i = 0; i < fingers.size(); i++) {
            Finger f = fingers.valueAt(i);
            if (inCorner(f, true, top)) left = true;
            if (inCorner(f, false, top)) right = true;
        }
        return left && right;
    }

    private void rings(Canvas c, float k, boolean top, int color) {
        float r = getHeight() * 0.06f;
        ring.setStrokeWidth(r * 0.22f);
        for (int side = 0; side < 2; side++) {
            float cx = side == 0 ? r * 1.6f : getWidth() - r * 1.6f, cy = top ? r * 1.6f : getHeight() - r * 1.6f;
            arc.set(cx - r, cy - r, cx + r, cy + r);
            ring.setColor(Color.argb(110, 255, 255, 255));
            c.drawCircle(cx, cy, r, ring);
            ring.setColor(color);
            c.drawArc(arc, -90, 360 * k, false, ring);
        }
    }

    private void checkUnlock(Canvas c, float dt) {
        // bottom corners: next scene (once per hold)
        if (!bothCorners(false)) { themeHold = -1; themeArmed = true; }
        else if (themeArmed) {
            themeHold = themeHold < 0 ? 0 : themeHold + dt;
            rings(c, Math.min(1f, themeHold / THEME_SECONDS), false, Color.rgb(120, 200, 255));
            if (themeHold >= THEME_SECONDS) {
                themeHold = -1; themeArmed = false;
                try { scene.nextTheme(); } catch (RuntimeException ignored) { }
            }
        }

        // top corners: exit
        if (!bothCorners(true)) { holdTime = -1; return; }
        holdTime = holdTime < 0 ? 0 : holdTime + dt;
        rings(c, Math.min(1f, holdTime / UNLOCK_SECONDS), true, Color.rgb(255, 210, 63));
        if (holdTime >= UNLOCK_SECONDS) {
            holdTime = -1;
            fingers.clear();
            listener.onUnlock();
        }
    }

    // ---------------------------------------------------------- touch
    @Override public boolean onTouchEvent(MotionEvent e) {
        if (scene == null) return true;
        int action = e.getActionMasked();
        switch (action) {
            case MotionEvent.ACTION_DOWN:
            case MotionEvent.ACTION_POINTER_DOWN: {
                int i = e.getActionIndex();
                Finger f = new Finger();
                f.x = f.trailX = e.getX(i); f.y = f.trailY = e.getY(i);
                fingers.put(e.getPointerId(i), f);
                try { scene.tap(f.x, f.y, nextWhite); } catch (RuntimeException ignored) { }
                nextWhite = !nextWhite;
                break;
            }
            case MotionEvent.ACTION_MOVE: {
                float step = scene.H * 0.012f;
                for (int i = 0; i < e.getPointerCount(); i++) {
                    Finger f = fingers.get(e.getPointerId(i));
                    if (f == null) continue;
                    f.x = e.getX(i); f.y = e.getY(i);
                    float dx = f.x - f.trailX, dy = f.y - f.trailY;
                    if (dx * dx + dy * dy > step * step) {
                        f.trailX = f.x; f.trailY = f.y;
                        scene.trail(f.x, f.y);
                    }
                }
                break;
            }
            case MotionEvent.ACTION_POINTER_UP:
                fingers.remove(e.getPointerId(e.getActionIndex()));
                break;
            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_CANCEL:
                fingers.clear();
                break;
        }
        return true;
    }

    // ---------------------------------------------------------- hardware keyboard (tablets with a keyboard, Chromebooks)
    /** Returns true when the key was eaten. Volume keys are left to the system so parents can still use them. */
    boolean key(int code, KeyEvent e) {
        if (code == KeyEvent.KEYCODE_VOLUME_UP || code == KeyEvent.KEYCODE_VOLUME_DOWN || code == KeyEvent.KEYCODE_VOLUME_MUTE)
            return false;
        if (scene == null || e.getRepeatCount() > 0) return true;
        if (code == KeyEvent.KEYCODE_BACK || code == KeyEvent.KEYCODE_HOME || code == KeyEvent.KEYCODE_APP_SWITCH) return true;
        try {
            if (code >= KeyEvent.KEYCODE_A && code <= KeyEvent.KEYCODE_Z) scene.onLetter((char) ('A' + code - KeyEvent.KEYCODE_A));
            else if (code >= KeyEvent.KEYCODE_0 && code <= KeyEvent.KEYCODE_9) scene.onNumber(code - KeyEvent.KEYCODE_0);
            else if (code >= KeyEvent.KEYCODE_NUMPAD_0 && code <= KeyEvent.KEYCODE_NUMPAD_9) scene.onNumber(code - KeyEvent.KEYCODE_NUMPAD_0);
            else if (!KeyEvent.isModifierKey(code)) scene.onOtherKey();
        } catch (RuntimeException ignored) { }
        return true;
    }

    void resetClock() { lastNanos = 0; fingers.clear(); holdTime = -1; themeHold = -1; themeArmed = true; }
}

package com.foryusef.littleknight;

import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.PointF;
import android.view.KeyEvent;

import com.foryusef.Game;
import com.foryusef.GameInfo;
import com.foryusef.Sound;

import java.util.List;

/** Connects Little Knight's Scene to the ForYusef shell. */
public final class LittleKnightGame implements Game {
    public static final GameInfo INFO = new GameInfo(
        "Little Knight", "Letters, numbers and chess pieces", Color.rgb(232, 170, 60),
        "both bottom corners for the next scene",
        LittleKnightGame::new,
        g -> ((LittleKnightGame) g).demo());

    private final Scene scene;
    private boolean nextWhite = true;

    private LittleKnightGame(int w, int h, Sound sound) {
        scene = new Scene(w, h, new java.util.Random().nextInt(Scene.THEME_NAMES.length));
        scene.sound = sound::play;
    }

    @Override public void resize(int w, int h) { float t = scene.time; scene.resize(w, h); scene.time = t; }
    @Override public void setStartHint(String hint) { scene.startHint = hint; }
    @Override public void start() { scene.welcome(); }
    @Override public void update(float dt) { scene.update(dt); }

    @Override public void draw(Canvas c, List<PointF> fingers) {
        scene.fingers.clear();
        scene.fingers.addAll(fingers);
        scene.draw(c);
    }

    @Override public void touchDown(float x, float y) { scene.tap(x, y, nextWhite); nextWhite = !nextWhite; }
    @Override public void touchDrag(float x, float y) { scene.trail(x, y); }
    @Override public void parentNext() { scene.nextTheme(); }

    @Override public void key(int code) {
        if (code >= KeyEvent.KEYCODE_A && code <= KeyEvent.KEYCODE_Z) scene.onLetter((char) ('A' + code - KeyEvent.KEYCODE_A));
        else if (code >= KeyEvent.KEYCODE_0 && code <= KeyEvent.KEYCODE_9) scene.onNumber(code - KeyEvent.KEYCODE_0);
        else if (code >= KeyEvent.KEYCODE_NUMPAD_0 && code <= KeyEvent.KEYCODE_NUMPAD_9) scene.onNumber(code - KeyEvent.KEYCODE_NUMPAD_0);
        else if (!KeyEvent.isModifierKey(code)) scene.onOtherKey();
    }

    /** Menu card: a few things already on the board. */
    private void demo() {
        scene.onLetter('K'); scene.update(0.4f);
        scene.onNumber(3); scene.update(0.3f);
        scene.tap(scene.boardX + 3.5f * scene.sq, scene.boardY + 4.5f * scene.sq, true);
        for (int i = 0; i < 12; i++) scene.update(0.12f);
    }
}

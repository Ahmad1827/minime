package com.foryusef;

import android.graphics.Canvas;
import android.graphics.PointF;

import java.util.List;

/**
 * What a ForYusef game looks like to the shell. All calls come on the UI thread.
 *
 * ADDING A GAME
 *   1. Make a package com.foryusef.<name> with a class that implements Game.
 *   2. Add its GameInfo to Catalog.ALL.
 *   The menu card, screen pinning, the parent exit and the sound come for free.
 */
public interface Game {
    void resize(int w, int h);
    void setStartHint(String parentHint);    // parent instructions to show for the first few seconds
    void start();                             // the game just opened (a welcome tune, for example)
    void update(float dt);                    // seconds since the last frame (at most 0.05)
    void draw(Canvas c, List<PointF> fingers); // whole screen, every frame; fingers = where touches are right now

    void touchDown(float x, float y);         // a finger touched the screen (several fingers = several calls)
    void touchDrag(float x, float y);         // a finger moved a little way while touching
    void key(int keyCode);                    // hardware keyboard: any key except volume/back/home (repeats filtered)
    void parentNext();                        // parent hold on both bottom corners: next scene / level
}

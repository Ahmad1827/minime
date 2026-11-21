package com.foryusef;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.DashPathEffect;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.PointF;
import android.graphics.RadialGradient;
import android.graphics.Rect;
import android.graphics.RectF;
import android.graphics.Shader;
import android.graphics.Typeface;
import android.view.MotionEvent;
import android.view.View;

import java.util.ArrayList;
import java.util.Collections;

/** Draws the menu (title, game cards, a hint for parents) and turns a tap on a card into a launch. */
final class MenuView extends View {
    interface Launcher { void launch(int index); }

    private static final int MIN_CARDS = 3; // empty "more games soon" cards fill the row until there are this many games
    private static final int INK = Color.rgb(52, 54, 92);
    private static final int[] TITLE_COLORS = {
        Color.rgb(240, 98, 98), Color.rgb(247, 160, 60), Color.rgb(242, 200, 40),
        Color.rgb(96, 190, 100), Color.rgb(70, 160, 230), Color.rgb(150, 110, 220),
    };
    private static final Typeface TITLE_FONT = Typeface.create("sans-serif-black", Typeface.NORMAL);
    private static final Typeface UI_FONT = Typeface.create("sans-serif", Typeface.BOLD);
    private static final Typeface UI_LIGHT = Typeface.create("sans-serif", Typeface.NORMAL);

    private static final class Card { GameInfo info; RectF rect = new RectF(); Bitmap thumb; float press; }

    private final Sound sound;
    private final Launcher launcher;
    private final ArrayList<Card> cards = new ArrayList<>();
    private Bitmap background;
    private int W, H;
    private float time;
    private long lastNanos;
    private int pressed = -1;

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final Paint text = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF r = new RectF();

    MenuView(Context ctx, Sound sound, Launcher launcher) {
        super(ctx);
        this.sound = sound;
        this.launcher = launcher;
        setHapticFeedbackEnabled(false);
    }

    void resume() { pressed = -1; lastNanos = 0; invalidate(); }

    // ---------------------------------------------------------- layout
    @Override protected void onSizeChanged(int w, int h, int oldw, int oldh) {
        if (w <= 0 || h <= 0) return;
        W = w; H = h;
        for (Card c : cards) if (c.thumb != null) c.thumb.recycle();
        cards.clear();
        int n = Math.max(MIN_CARDS, Catalog.ALL.length);
        int cols = Math.min(n, 4), rows = (n + cols - 1) / cols;
        float gap = Math.min(W, H) * 0.04f;
        float top = H * 0.30f, bottom = H * 0.86f;
        float cw = (W * 0.88f - gap * (cols - 1)) / cols;
        float ch = cw * (10f / 16f) + cw * 0.24f;
        float maxH = (bottom - top - gap * (rows - 1)) / rows;
        if (ch > maxH) { cw *= maxH / ch; ch = maxH; }
        float x0 = (W - (cols * cw + (cols - 1) * gap)) / 2f;
        float y0 = top + (bottom - top - (rows * ch + (rows - 1) * gap)) / 2f;
        for (int i = 0; i < n; i++) {
            int c = i % cols, row = i / cols;
            int inRow = Math.min(cols, n - row * cols);
            float rowX = x0 + (cols - inRow) * (cw + gap) / 2f;
            Card card = new Card();
            card.info = i < Catalog.ALL.length ? Catalog.ALL[i] : null;
            card.rect.set(rowX + c * (cw + gap), y0 + row * (ch + gap), rowX + c * (cw + gap) + cw, y0 + row * (ch + gap) + ch);
            if (card.info != null) card.thumb = thumbnail(card.info, (int) (cw * 0.94f), (int) (cw * 0.94f * 10f / 16f));
            cards.add(card);
        }
        if (background != null) background.recycle();
        background = buildBackground();
    }

    /** A real frame of the game, so the card shows exactly what the baby will see. */
    private static Bitmap thumbnail(GameInfo info, int w, int h) {
        try {
            Game g = info.factory.create(1440, 900, (s, d, v) -> { });
            g.setStartHint("");
            info.demo.run(g);
            Bitmap big = Bitmap.createBitmap(1440, 900, Bitmap.Config.ARGB_8888);
            g.draw(new Canvas(big), Collections.<PointF>emptyList());
            Bitmap small = Bitmap.createScaledBitmap(big, Math.max(1, w), Math.max(1, h), true);
            if (small != big) big.recycle();
            return small;
        } catch (RuntimeException | OutOfMemoryError e) {
            return null;
        }
    }

    private Bitmap buildBackground() {
        Bitmap bmp = Bitmap.createBitmap(W, H, Bitmap.Config.ARGB_8888);
        Canvas g = new Canvas(bmp);
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setShader(new LinearGradient(0, 0, 0, H, new int[] { Color.rgb(120, 196, 242), Color.rgb(196, 228, 246), Color.rgb(255, 228, 196) },
            new float[] { 0f, 0.55f, 1f }, Shader.TileMode.CLAMP));
        g.drawRect(0, 0, W, H, p);
        float sx = W * 0.12f, sy = H * 0.12f, sr = H * 0.32f;
        p.setShader(new RadialGradient(sx, sy, sr, Color.argb(150, 255, 245, 200), Color.argb(0, 255, 245, 200), Shader.TileMode.CLAMP));
        g.drawCircle(sx, sy, sr, p);
        p.setShader(null);
        p.setColor(Color.rgb(255, 250, 222));
        g.drawCircle(sx, sy, H * 0.06f, p);
        hill(g, p, H * 0.86f, H * 0.07f, 0.9f, 0.4f, Color.rgb(150, 210, 130));
        hill(g, p, H * 0.91f, H * 0.06f, 1.4f, 2.1f, Color.rgb(112, 186, 104));
        return bmp;
    }

    private void hill(Canvas g, Paint p, float baseY, float amp, float freq, float phase, int color) {
        Path path = new Path();
        path.moveTo(0, H);
        for (int i = 0; i <= 40; i++)
            path.lineTo(W * i / 40f, baseY - amp * (float) (0.5 + 0.5 * Math.sin(i / 40.0 * Math.PI * 2 * freq + phase)));
        path.lineTo(W, H);
        path.close();
        p.setColor(color);
        g.drawPath(path, p);
    }

    // ---------------------------------------------------------- touch
    private int cardAt(float x, float y) {
        for (int i = 0; i < cards.size(); i++) if (cards.get(i).info != null && cards.get(i).rect.contains(x, y)) return i;
        return -1;
    }

    @Override public boolean onTouchEvent(MotionEvent e) {
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                pressed = cardAt(e.getX(), e.getY());
                if (pressed >= 0) sound.play(pressed % 12, 0, 0.25f);
                break;
            case MotionEvent.ACTION_MOVE:
                if (pressed >= 0 && cardAt(e.getX(), e.getY()) != pressed) pressed = -1;
                break;
            case MotionEvent.ACTION_UP:
                if (pressed >= 0 && cardAt(e.getX(), e.getY()) == pressed) {
                    int i = pressed;
                    sound.play(5, 0, 0.3f); sound.play(7, 0.1f, 0.3f);
                    launcher.launch(i);
                }
                pressed = -1;
                break;
            case MotionEvent.ACTION_CANCEL:
                pressed = -1;
                break;
        }
        return true;
    }

    // ---------------------------------------------------------- drawing
    @Override protected void onDraw(Canvas g) {
        if (background == null) return;
        long now = System.nanoTime();
        float dt = lastNanos == 0 ? 0 : Math.min(0.05f, (now - lastNanos) / 1e9f);
        lastNanos = now;
        time += dt;
        for (int i = 0; i < cards.size(); i++) {
            Card c = cards.get(i);
            float target = i == pressed ? 1f : 0f;
            c.press = c.press < target ? Math.min(target, c.press + dt * 9f) : Math.max(target, c.press - dt * 6f);
        }

        g.drawBitmap(background, 0, 0, null);
        drawClouds(g);
        drawTitle(g);
        for (Card c : cards) drawCard(g, c);
        drawFooter(g);
        postInvalidateOnAnimation();
    }

    private void drawClouds(Canvas g) {
        paint.setStyle(Paint.Style.FILL);
        paint.setColor(Color.argb(215, 255, 255, 255));
        for (int i = 0; i < 5; i++) {
            float s = H * (0.05f + 0.015f * (i % 3));
            float span = W + s * 8;
            float x = (float) ((i * 0.23 * span + time * H * (0.008 + 0.003 * i)) % span) - s * 4;
            float y = H * (0.06f + 0.05f * ((i * 7) % 4));
            r.set(x, y, x + s * 2.4f, y + s); g.drawOval(r, paint);
            r.set(x + s * 0.5f, y - s * 0.45f, x + s * 1.8f, y + s * 0.65f); g.drawOval(r, paint);
            r.set(x + s * 1.2f, y - s * 0.25f, x + s * 2.2f, y + s * 0.65f); g.drawOval(r, paint);
        }
    }

    /** "For Yusef": every letter its own color, gently bobbing. */
    private void drawTitle(Canvas g) {
        String title = "For Yusef";
        text.setTypeface(TITLE_FONT);
        text.setTextAlign(Paint.Align.LEFT);
        float em = H * 0.13f;
        text.setTextSize(em);
        float total = text.measureText(title);
        if (total > W * 0.8f) { em *= W * 0.8f / total; text.setTextSize(em); total = text.measureText(title); }
        float x = (W - total) / 2f, baseY = H * 0.07f + em * 0.9f;
        int color = 0;
        for (int i = 0; i < title.length(); i++) {
            String ch = title.substring(i, i + 1);
            float w = text.measureText(ch);
            if (!ch.equals(" ")) {
                float bob = (float) Math.sin(time * 2.2 + i * 0.7) * H * 0.008f;
                text.setStyle(Paint.Style.STROKE);
                text.setStrokeJoin(Paint.Join.ROUND);
                text.setStrokeWidth(em * 0.14f);
                text.setColor(Color.WHITE);
                g.drawText(ch, x, baseY + bob, text);
                text.setStyle(Paint.Style.FILL);
                text.setColor(TITLE_COLORS[color++ % TITLE_COLORS.length]);
                g.drawText(ch, x, baseY + bob, text);
            }
            x += w;
        }
        text.setStyle(Paint.Style.FILL);
        text.setTypeface(UI_FONT);
        text.setTextAlign(Paint.Align.CENTER);
        text.setTextSize(H * 0.032f);
        text.setColor(withAlpha(INK, 200));
        g.drawText("Pick a game to play together", W / 2f, baseY + em * 0.45f, text);
    }

    private void drawCard(Canvas g, Card card) {
        RectF rc = card.rect;
        float cw = rc.width(), rad = cw * 0.06f;
        g.save();
        float k = 1 - 0.03f * card.press;
        g.scale(k, k, rc.centerX(), rc.centerY());
        if (card.info == null) { drawSoonCard(g, rc, rad); g.restore(); return; }

        paint.setStyle(Paint.Style.FILL);
        paint.setColor(Color.argb(45, 30, 40, 80));
        r.set(rc); r.offset(cw * 0.01f, cw * 0.02f);
        g.drawRoundRect(r, rad, rad, paint);
        paint.setColor(Color.WHITE);
        g.drawRoundRect(rc, rad, rad, paint);

        float pad = cw * 0.03f;
        RectF pic = new RectF(rc.left + pad, rc.top + pad, rc.right - pad, rc.top + pad + (cw - pad * 2) * 10f / 16f);
        g.save();
        Path clip = new Path();
        clip.addRoundRect(pic, rad * 0.7f, rad * 0.7f, Path.Direction.CW);
        g.clipPath(clip);
        if (card.thumb != null) g.drawBitmap(card.thumb, new Rect(0, 0, card.thumb.getWidth(), card.thumb.getHeight()), pic, paint);
        else { paint.setColor(card.info.accent); g.drawRect(pic, paint); }
        g.restore();

        // play button on the picture
        float pr = cw * 0.075f, cx = pic.centerX(), cy = pic.centerY();
        paint.setColor(Color.argb(225, 255, 255, 255));
        g.drawCircle(cx, cy, pr, paint);
        paint.setColor(card.info.accent);
        Path tri = new Path();
        tri.moveTo(cx - pr * 0.32f, cy - pr * 0.45f); tri.lineTo(cx - pr * 0.32f, cy + pr * 0.45f); tri.lineTo(cx + pr * 0.48f, cy); tri.close();
        g.drawPath(tri, paint);

        float capTop = pic.bottom, capH = rc.bottom - capTop, dot = capH * 0.32f;
        g.drawCircle(rc.left + pad * 1.6f + dot / 2, capTop + capH / 2, dot / 2, paint);
        float tx = rc.left + pad * 1.6f + dot * 1.45f;
        text.setTextAlign(Paint.Align.LEFT);
        text.setTypeface(UI_FONT);
        text.setTextSize(capH * 0.30f);
        text.setColor(INK);
        g.drawText(card.info.title, tx, capTop + capH * 0.48f, text);
        text.setTypeface(UI_LIGHT);
        text.setTextSize(capH * 0.19f);
        text.setColor(withAlpha(INK, 170));
        g.drawText(card.info.tagline, tx, capTop + capH * 0.78f, text);

        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(cw * 0.012f);
        paint.setColor(card.info.accent);
        g.drawRoundRect(rc, rad, rad, paint);
        paint.setStyle(Paint.Style.FILL);
        g.restore();
    }

    private void drawSoonCard(Canvas g, RectF rc, float rad) {
        float cw = rc.width();
        paint.setStyle(Paint.Style.FILL);
        paint.setColor(Color.argb(110, 255, 255, 255));
        g.drawRoundRect(rc, rad, rad, paint);
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(cw * 0.008f);
        paint.setColor(withAlpha(INK, 90));
        paint.setPathEffect(new DashPathEffect(new float[] { cw * 0.03f, cw * 0.02f }, 0));
        g.drawRoundRect(rc, rad, rad, paint);
        paint.setPathEffect(null);
        float pr = cw * 0.1f, cy = rc.top + rc.height() * 0.42f, cx = rc.centerX();
        paint.setStrokeWidth(pr * 0.14f);
        paint.setStrokeCap(Paint.Cap.ROUND);
        paint.setColor(withAlpha(INK, 110));
        g.drawCircle(cx, cy, pr, paint);
        g.drawLine(cx - pr * 0.45f, cy, cx + pr * 0.45f, cy, paint);
        g.drawLine(cx, cy - pr * 0.45f, cx, cy + pr * 0.45f, paint);
        paint.setStyle(Paint.Style.FILL);
        text.setTypeface(UI_FONT);
        text.setTextAlign(Paint.Align.CENTER);
        text.setTextSize(rc.height() * 0.075f);
        text.setColor(withAlpha(INK, 150));
        g.drawText("More games soon", cx, cy + pr * 1.4f + rc.height() * 0.075f, text);
    }

    private void drawFooter(Canvas g) {
        String s = "Tap a game to start  ·  In a game, hold both top corners for 3 seconds to come back here";
        text.setTypeface(UI_FONT);
        text.setTextAlign(Paint.Align.CENTER);
        text.setTextSize(Math.max(12, H * 0.026f));
        float tw = text.measureText(s);
        if (tw > W * 0.9f) { text.setTextSize(text.getTextSize() * W * 0.9f / tw); tw = text.measureText(s); }
        float h = text.getTextSize() * 1.9f;
        r.set((W - tw) / 2 - h * 0.6f, H * 0.94f - h / 2, (W + tw) / 2 + h * 0.6f, H * 0.94f + h / 2);
        paint.setStyle(Paint.Style.FILL);
        paint.setColor(Color.argb(215, 255, 255, 255));
        g.drawRoundRect(r, h / 2, h / 2, paint);
        text.setColor(INK);
        g.drawText(s, W / 2f, r.centerY() - (text.ascent() + text.descent()) / 2, text);
    }

    private static int withAlpha(int c, int a) { return Color.argb(a, Color.red(c), Color.green(c), Color.blue(c)); }
}

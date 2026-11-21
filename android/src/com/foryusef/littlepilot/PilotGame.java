package com.foryusef.littlepilot;

import android.graphics.Bitmap;
import android.graphics.BitmapShader;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.PointF;
import android.graphics.RadialGradient;
import android.graphics.RectF;
import android.graphics.Shader;
import android.graphics.Typeface;
import android.view.KeyEvent;

import com.foryusef.Game;
import com.foryusef.GameInfo;
import com.foryusef.Sound;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Random;

/**
 * Little Pilot: a cut-paper sky where a little propeller plane flies by itself, and every touch or key
 * puts something in the sky for it to fly into. Touch: a balloon right there, and the plane follows your finger.
 * Keyboard: letters make letter balloons, numbers make that many counting balloons, space makes a loop-the-loop.
 * Same game and art as windows/Games/LittlePilot/LittlePilot.cs.
 */
public final class PilotGame implements Game {
    public static final GameInfo INFO = new GameInfo(
        "Little Pilot", "Fly, pop balloons, catch stars", Color.rgb(84, 166, 228),
        "both bottom corners to change the sky",
        PilotGame::new,
        g -> ((PilotGame) g).demo());

    // ================================================================== art data
    static final class Palette {
        final String name; final int sky; // 0 sun high, 1 sun low and big, 2 moon and stars
        final int skyTop, skyBottom, sun, sunRim, far, mid, near, front, cloud, trunk, treeA, treeB, house, roof, window, flower;
        Palette(String name, int sky, int... c) {
            this.name = name; this.sky = sky;
            skyTop = c[0]; skyBottom = c[1]; sun = c[2]; sunRim = c[3]; far = c[4]; mid = c[5]; near = c[6]; front = c[7]; cloud = c[8];
            trunk = c[9]; treeA = c[10]; treeB = c[11]; house = c[12]; roof = c[13]; window = c[14]; flower = c[15];
        }
    }

    private static int C(int rgb) { return 0xFF000000 | rgb; }
    private static int A(int a, int c) { return (Math.max(0, Math.min(255, a)) << 24) | (c & 0xFFFFFF); }

    static final Palette[] PALETTES = {
        new Palette("Sunny Morning", 0, C(0x9FD7F2), C(0xFFF4DE), C(0xFFD25E), C(0xFFE9A6), C(0xA9C2E6), C(0x9BD6A4), C(0x6CBF7C), C(0x4FA86A), C(0xFFFFFF),
            C(0x9C6B4E), C(0x4E9E62), C(0x83CC78), C(0xF8E6C8), C(0xE8775F), C(0x86BEE3), C(0xFFF1A8)),
        new Palette("Pink Sunset", 1, C(0x8C7CC6), C(0xFFC28C), C(0xFF8D5C), C(0xFFB98F), C(0xB27DAE), C(0xE39A7C), C(0xC57067), C(0xA65A5A), C(0xFFE0D8),
            C(0x6E4A55), C(0x8E4F63), C(0xB8687A), C(0xFCE1C6), C(0x7B4B7A), C(0xFFE08A), C(0xFFD3B0)),
        new Palette("Starry Night", 2, C(0x1B2356), C(0x44528F), C(0xFFF2C4), C(0xFFF8E0), C(0x34407B), C(0x2D5C6E), C(0x224A58), C(0x1A3A46), C(0x5F6DAA),
            C(0x2A3440), C(0x1E4A4A), C(0x2E6661), C(0x4B5B8C), C(0x2C3466), C(0xFFD86B), C(0x8FA0D8)),
    };

    private static final int[] BALLOON = { C(0xEE6B5D), C(0xF5B53F), C(0x5CC39B), C(0x59A7E8), C(0xA588DD), C(0xF489B4) };
    private static final int GOLD = C(0xF7C948), INK = C(0x3A3A5E), SHADOW = Color.rgb(20, 16, 50);
    private static final float LOOP_TIME = 1.2f, WIPE_TIME = 1.5f, THEME_SECONDS = 240f;
    private static final Typeface ROUND = Typeface.create("sans-serif-black", Typeface.NORMAL);
    private static final Typeface UI = Typeface.create("sans-serif", Typeface.BOLD);

    static final class Thing { static final int BALLOON = 0, STAR = 1, RING = 2; int kind, note, color; float x, y, born, phase; String label = ""; }
    static final class Bit { int shape; float x, y, vx, vy, rot, vr, age, life, size, grow; int c; } // 0 puff, 1 confetti, 2 sparkle, 3 ring wave
    static final class Popup { String text; float x, y, born; int c; }
    static final class CloudSpot { float x, y, depth; int kind; }

    static final class Scenery {
        Palette p; Bitmap sky, far, mid, near; final Bitmap[] clouds = new Bitmap[3];
        int tileW, farY, midY, nearY;
        void recycle() {
            for (Bitmap b : new Bitmap[] { sky, far, mid, near }) if (b != null) b.recycle();
            for (Bitmap b : clouds) if (b != null) b.recycle();
        }
    }

    // ================================================================== state
    private int W, H;
    private float time;
    private final Sound sound;
    private String startHint = "";
    private final Random rnd = new Random();
    private final Random fx = new Random();

    private final ArrayList<Thing> things = new ArrayList<>();
    private final ArrayList<Bit> bits = new ArrayList<>();
    private final ArrayList<Popup> popups = new ArrayList<>();
    private final ArrayList<CloudSpot> clouds = new ArrayList<>();
    private Scenery scenery, oldScenery;
    private int theme; private float themeSince, wipeBorn = -100;
    private String caption; private float captionBorn = -100;

    private float scroll, planeX, planeY, planeVY, prop, puffTimer, loopBorn = -100, rainbowUntil = -100;
    private int rainbowHue;
    private float followX = -1, followY = -1, followUntil = -100;

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint text = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF rect = new RectF();
    private static Bitmap grain;

    private PilotGame(int w, int h, Sound sound) {
        this.sound = sound;
        theme = rnd.nextInt(PALETTES.length);
        for (int i = 0; i < 8; i++) {
            CloudSpot c = new CloudSpot();
            c.x = rnd.nextFloat(); c.y = 0.05f + 0.32f * rnd.nextFloat(); c.depth = i % 2 == 0 ? 0.18f : 0.5f; c.kind = i % 3;
            clouds.add(c);
        }
        Collections.sort(clouds, (a, b) -> Float.compare(a.depth, b.depth));
        resize(w, h);
        spawn(Thing.BALLOON, W * 0.62f, H * 0.36f, "", 0, 0, -1);
        spawn(Thing.STAR, W * 0.8f, H * 0.5f, "", 4, 0, -1);
        spawn(Thing.BALLOON, W * 0.97f, H * 0.3f, "", 2, 2, -1);
    }

    // ================================================================== Game
    @Override public void resize(int w, int h) {
        W = Math.max(1, w); H = Math.max(1, h);
        planeX = W * 0.3f; planeY = H * 0.4f;
        things.clear(); bits.clear(); popups.clear();
        if (scenery != null) scenery.recycle();
        if (oldScenery != null) { oldScenery.recycle(); oldScenery = null; }
        scenery = build(PALETTES[theme]);
    }

    @Override public void setStartHint(String hint) { startHint = hint == null ? "" : hint; }
    @Override public void start() { tune(new int[] { 0, 2, 4, 7, 9, 11 }, 0.11f, 0.45f); }
    @Override public void parentNext() { setTheme(theme + 1, true, false); }

    @Override public void touchDown(float x, float y) {
        follow(x, y);
        spawn(Thing.BALLOON, x, Math.max(H * 0.1f, Math.min(H * 0.7f, y)), "", rnd.nextInt(BALLOON.length), rnd.nextInt(12), 0);
    }

    @Override public void touchDrag(float x, float y) { follow(x, y); }

    private void follow(float x, float y) { followX = x; followY = y; followUntil = time + 1.5f; }

    @Override public void key(int code) {
        if (code >= KeyEvent.KEYCODE_A && code <= KeyEvent.KEYCODE_Z) {
            int i = code - KeyEvent.KEYCODE_A;
            spawn(Thing.BALLOON, nextX(), randomY(), String.valueOf((char) ('A' + i)), i % BALLOON.length, i % 12, 0);
        } else if (code >= KeyEvent.KEYCODE_0 && code <= KeyEvent.KEYCODE_9) count(code - KeyEvent.KEYCODE_0);
        else if (code >= KeyEvent.KEYCODE_NUMPAD_0 && code <= KeyEvent.KEYCODE_NUMPAD_9) count(code - KeyEvent.KEYCODE_NUMPAD_0);
        else if (code == KeyEvent.KEYCODE_SPACE || code == KeyEvent.KEYCODE_ENTER) loop();
        else if (code >= KeyEvent.KEYCODE_DPAD_UP && code <= KeyEvent.KEYCODE_DPAD_RIGHT) spawn(Thing.STAR, nextX(), randomY(), "", 0, 4 + rnd.nextInt(6), 0);
        else if (!KeyEvent.isModifierKey(code)) {
            int r = rnd.nextInt(10);
            if (r < 4) spawn(Thing.STAR, nextX(), randomY(), "", 0, 4 + rnd.nextInt(6), 0);
            else if (r < 6) spawn(Thing.RING, nextX(), H * (0.25f + 0.3f * rnd.nextFloat()), "", rnd.nextInt(BALLOON.length), 0, 0);
            else if (r < 8) {
                float x = nextX(), y = randomY();
                for (int k = 0; k < 3; k++) spawn(Thing.BALLOON, x + k * H * 0.09f, y + (k == 1 ? -H * 0.07f : 0), "", rnd.nextInt(BALLOON.length), 3 + k * 2, k * 0.12f);
            } else loop();
        }
    }

    // ================================================================== spawning
    private float randomY() { return H * (0.16f + 0.46f * rnd.nextFloat()); }

    private float nextX() {
        float x = W * 0.78f;
        for (Thing t : things) if (t.x > x - H * 0.14f && t.x < W) x = t.x + H * 0.15f;
        return x < W - H * 0.08f ? x : W * 0.78f;
    }

    private void spawn(int kind, float x, float y, String label, int color, int note, float delay) {
        if (things.size() >= 20) things.remove(0);
        Thing t = new Thing();
        t.kind = kind; t.x = x; t.y = y; t.label = label; t.color = ((color % 6) + 6) % 6; t.note = Math.max(0, Math.min(11, note));
        t.born = time + Math.max(0, delay); t.phase = rnd.nextFloat() * 6.28f;
        things.add(t);
        if (delay >= 0) sound.play(Math.min(11, note + 2), Math.max(0, delay), 0.18f);
    }

    private void count(int n) {
        if (n == 0) n = 10;
        float x0 = Math.max(W * 0.5f, planeX + H * 0.3f);
        for (int i = 0; i < n; i++)
            spawn(Thing.BALLOON, x0 + i * H * 0.15f, H * (0.38f + 0.1f * (float) Math.sin(i * 0.9)), String.valueOf(i + 1), i, i, i * 0.1f);
    }

    private void loop() {
        if (time - loopBorn < LOOP_TIME) return;
        loopBorn = time;
        tune(new int[] { 4, 7, 9, 11, 9 }, 0.12f, 0.3f);
    }

    private void tune(int[] steps, float spacing, float gain) { for (int i = 0; i < steps.length; i++) sound.play(steps[i], i * spacing, gain); }

    // ================================================================== update
    private float speed() { return H * 0.34f; }

    private final float[] pose = new float[3];

    private float[] planePose() {
        float tilt = Math.max(-22, Math.min(22, planeVY / H * 45f));
        float k = (time - loopBorn) / LOOP_TIME;
        if (k >= 0 && k < 1) {
            float e = k * k * (3 - 2 * k), th = e * 6.2832f, R = H * 0.13f;
            pose[0] = planeX + R * (float) Math.sin(th); pose[1] = planeY - R * (1 - (float) Math.cos(th)); pose[2] = tilt - 360f * e;
        } else { pose[0] = planeX; pose[1] = planeY; pose[2] = tilt; }
        return pose;
    }

    @Override public void update(float dt) {
        time += dt;
        scroll += speed() * dt;
        prop += dt * 40f;
        if (time - themeSince > THEME_SECONDS) setTheme(theme + 1, true, false);

        for (int i = things.size() - 1; i >= 0; i--) {
            Thing t = things.get(i);
            t.x -= speed() * dt;
            if (t.x < -H * 0.25f) things.remove(i);
        }

        float ty, tx = W * 0.3f;
        if (time < followUntil && followY >= 0) {
            ty = followY;
            tx = Math.max(W * 0.12f, Math.min(W * 0.62f, followX));
        } else {
            Thing best = null;
            for (Thing t : things) if (time >= t.born && t.x > planeX - H * 0.02f && (best == null || t.x < best.x)) best = t;
            ty = best != null ? best.y : H * (0.38f + 0.1f * (float) Math.sin(time * 0.6));
        }
        ty = Math.max(H * 0.1f, Math.min(H * 0.7f, ty));
        float want = Math.max(-H * 1.1f, Math.min(H * 1.1f, (ty - planeY) * 3.4f));
        planeVY += (want - planeVY) * Math.min(1, dt * 6f);
        planeY += planeVY * dt;
        planeX += (tx - planeX) * Math.min(1, dt * 2.2f);

        float[] p = planePose();
        float px = p[0], py = p[1], S = H * 0.2f;
        double rad = Math.toRadians(p[2]);
        float cos = (float) Math.cos(rad), sin = (float) Math.sin(rad);
        float hx = px + cos * S * 0.2f, hy = py + sin * S * 0.2f;
        for (int i = things.size() - 1; i >= 0; i--) {
            Thing t = things.get(i);
            if (time < t.born) continue;
            float dx = t.x - hx, dy = t.y - hy;
            boolean hit = t.kind == Thing.RING ? Math.abs(dx) < H * 0.04f && Math.abs(dy) < H * 0.1f : dx * dx + dy * dy < H * H * 0.085f * 0.085f;
            if (hit) { things.remove(i); collect(t); }
        }

        puffTimer -= dt;
        if (puffTimer <= 0) {
            puffTimer = 0.05f;
            boolean rainbow = time < rainbowUntil;
            Bit b = new Bit();
            b.shape = 0; b.x = px - cos * S * 0.52f; b.y = py - sin * S * 0.52f; b.vx = -speed() * 0.55f; b.vy = (fx.nextFloat() - 0.5f) * H * 0.03f;
            b.size = H * (rainbow ? 0.016f : 0.012f); b.grow = 1.6f; b.life = rainbow ? 1.3f : 0.9f;
            b.c = rainbow ? BALLOON[rainbowHue++ % BALLOON.length] : A(PALETTES[theme].sky == 2 ? 150 : 225, Color.WHITE);
            bits.add(b);
        }

        for (int i = bits.size() - 1; i >= 0; i--) {
            Bit b = bits.get(i);
            b.age += dt;
            if (b.age >= b.life) { bits.remove(i); continue; }
            b.x += b.vx * dt; b.y += b.vy * dt; b.rot += b.vr * dt;
            if (b.shape == 1) { b.vy += H * 1.1f * dt; b.vx *= 1 - dt * 0.8f; }
            if (b.shape == 2) { b.vx *= 1 - dt * 2f; b.vy *= 1 - dt * 2f; }
        }
        for (int i = popups.size() - 1; i >= 0; i--) if (time - popups.get(i).born > 1.4f) popups.remove(i);
        if (oldScenery != null && time - wipeBorn > WIPE_TIME) { oldScenery.recycle(); oldScenery = null; }
    }

    private void collect(Thing t) {
        int tc = BALLOON[t.color];
        if (t.kind == Thing.BALLOON) {
            sound.play(t.note, 0, 0.55f);
            for (int i = 0; i < 16; i++) {
                double a = fx.nextDouble() * Math.PI * 2; float sp = H * (0.25f + 0.45f * fx.nextFloat());
                Bit b = new Bit();
                b.shape = 1; b.x = t.x; b.y = t.y; b.vx = (float) Math.cos(a) * sp - speed() * 0.3f; b.vy = (float) Math.sin(a) * sp - H * 0.2f;
                b.rot = fx.nextFloat() * 6; b.vr = (fx.nextFloat() - 0.5f) * 16; b.size = H * 0.016f; b.life = 1.1f;
                b.c = i % 3 == 0 ? Color.WHITE : (i % 3 == 1 ? tc : BALLOON[fx.nextInt(6)]);
                bits.add(b);
            }
            Bit w = new Bit(); w.shape = 3; w.x = t.x; w.y = t.y; w.vx = -speed() * 0.3f; w.size = H * 0.05f; w.life = 0.4f; w.c = tc; bits.add(w);
            if (!t.label.isEmpty()) { Popup p = new Popup(); p.text = t.label; p.x = t.x; p.y = t.y; p.born = time; p.c = tc; popups.add(p); }
        } else if (t.kind == Thing.STAR) {
            sound.play(t.note, 0, 0.45f); sound.play(Math.min(11, t.note + 2), 0.09f, 0.4f); sound.play(Math.min(11, t.note + 4), 0.18f, 0.35f);
            for (int i = 0; i < 12; i++) {
                double a = i * Math.PI * 2 / 12; float sp = H * 0.5f;
                Bit b = new Bit();
                b.shape = 2; b.x = t.x; b.y = t.y; b.vx = (float) Math.cos(a) * sp - speed() * 0.3f; b.vy = (float) Math.sin(a) * sp;
                b.size = H * 0.018f; b.life = 0.8f; b.c = i % 2 == 0 ? GOLD : Color.WHITE;
                bits.add(b);
            }
        } else {
            rainbowUntil = time + 3.5f;
            tune(new int[] { 0, 2, 4, 5, 7, 9, 11 }, 0.07f, 0.35f);
            Bit w = new Bit(); w.shape = 3; w.x = t.x; w.y = t.y; w.vx = -speed() * 0.5f; w.size = H * 0.09f; w.life = 0.5f; w.c = tc; bits.add(w);
        }
    }

    // ================================================================== scenes
    private void setTheme(int t, boolean announce, boolean instant) {
        t = ((t % PALETTES.length) + PALETTES.length) % PALETTES.length;
        themeSince = time;
        if (t == theme) return;
        theme = t;
        if (oldScenery != null) { oldScenery.recycle(); oldScenery = null; }
        if (instant) { scenery.recycle(); scenery = build(PALETTES[t]); return; }
        oldScenery = scenery;
        scenery = build(PALETTES[t]);
        wipeBorn = time;
        if (announce) { caption = PALETTES[t].name; captionBorn = time; tune(new int[] { 9, 7, 4, 7, 9, 11 }, 0.1f, 0.4f); }
    }

    private static Bitmap grain() {
        if (grain != null) return grain;
        Random r = new Random(99);
        int[] px = new int[128 * 128];
        for (int i = 0; i < px.length; i++) {
            int v = r.nextInt(100);
            px[i] = v < 30 ? Color.argb(6 + r.nextInt(10), 0, 0, 0) : v < 55 ? Color.argb(8 + r.nextInt(12), 255, 255, 255) : 0;
        }
        return grain = Bitmap.createBitmap(px, 128, 128, Bitmap.Config.ARGB_8888);
    }

    /** A paper cut-out: soft shadow, flat color, paper grain. */
    private static void paper(Canvas g, Path path, int color, float sdx, float sdy, int shadow) {
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        if (shadow > 0) {
            p.setColor(A(shadow, SHADOW));
            for (int i = 3; i >= 1; i--) {
                Path s = new Path(path);
                s.offset(sdx * i / 3f, sdy * i / 3f);
                g.drawPath(s, p);
            }
        }
        p.setColor(color);
        g.drawPath(path, p);
        p.setColor(Color.WHITE);
        p.setShader(new BitmapShader(grain(), Shader.TileMode.REPEAT, Shader.TileMode.REPEAT));
        g.drawPath(path, p);
    }

    private static float wave(float x, float tile, float[] k, float[] amp, float[] ph) {
        float s = 0;
        for (int i = 0; i < k.length; i++) s += amp[i] * (float) Math.sin(2 * Math.PI * k[i] * x / tile + ph[i]);
        return s;
    }

    private static Path ridge(int tile, int bmpH, float baseY, float[] k, float[] amp, float[] ph) {
        Path p = new Path();
        p.moveTo(-24, bmpH + 2);
        for (int x = -24; x <= tile + 24; x += 6) p.lineTo(x, baseY - wave(x, tile, k, amp, ph));
        p.lineTo(tile + 24, bmpH + 2);
        p.close();
        return p;
    }

    private static Path oval(float l, float t, float r, float b) { Path p = new Path(); p.addOval(new RectF(l, t, r, b), Path.Direction.CW); return p; }

    private Scenery build(Palette p) {
        Scenery s = new Scenery();
        s.p = p;
        int tile = s.tileW = (Math.max(W, (int) (H * 1.8f)) + 127) / 128 * 128;
        Random r = new Random(1000 + p.sky);
        int skyH = (int) (H * 0.74f), farEnd = (int) (H * 0.78f), midEnd = (int) (H * 0.89f);
        Paint q = new Paint(Paint.ANTI_ALIAS_FLAG);

        // ---- sky
        s.sky = Bitmap.createBitmap(W, skyH, Bitmap.Config.ARGB_8888);
        Canvas g = new Canvas(s.sky);
        q.setShader(new LinearGradient(0, 0, 0, H, p.skyTop, p.skyBottom, Shader.TileMode.CLAMP));
        g.drawRect(0, 0, W, skyH, q);
        q.setShader(null);
        if (p.sky == 2) {
            for (int i = 0; i < 80; i++) {
                float x = r.nextFloat() * W, y = r.nextFloat() * H * 0.6f, z = H * (0.002f + 0.004f * r.nextFloat());
                q.setColor(Color.argb(120 + r.nextInt(130), 255, 248, 220));
                Path d = new Path(); d.moveTo(x, y - z * 2); d.lineTo(x + z, y); d.lineTo(x, y + z * 2); d.lineTo(x - z, y); d.close();
                g.drawPath(d, q);
            }
            float mx = W * 0.8f, my = H * 0.18f, mr = H * 0.075f;
            q.setShader(new RadialGradient(mx, my, mr * 3, Color.argb(70, 255, 240, 200), Color.argb(0, 255, 240, 200), Shader.TileMode.CLAMP));
            g.drawCircle(mx, my, mr * 3, q);
            q.setShader(null);
            Path moon = oval(mx - mr, my - mr, mx + mr, my + mr);
            moon.op(oval(mx - mr * 0.45f, my - mr * 1.25f, mx + mr * 1.55f, my + mr * 0.75f), Path.Op.DIFFERENCE);
            Path ms = new Path(moon); ms.offset(H * 0.006f, H * 0.008f);
            q.setColor(Color.argb(60, 10, 10, 40)); g.drawPath(ms, q);
            q.setColor(p.sun); g.drawPath(moon, q);
        } else {
            boolean low = p.sky == 1;
            float sx = low ? W * 0.66f : W * 0.8f, sy = low ? H * 0.5f : H * 0.2f, sr = low ? H * 0.13f : H * 0.075f;
            if (!low) {
                Path rays = new Path();
                for (int i = 0; i < 12; i++) {
                    double a = i * Math.PI / 6, w = 0.12;
                    rays.moveTo(sx + (float) Math.cos(a - w) * sr * 1.25f, sy + (float) Math.sin(a - w) * sr * 1.25f);
                    rays.lineTo(sx + (float) Math.cos(a) * sr * 1.85f, sy + (float) Math.sin(a) * sr * 1.85f);
                    rays.lineTo(sx + (float) Math.cos(a + w) * sr * 1.25f, sy + (float) Math.sin(a + w) * sr * 1.25f);
                    rays.close();
                }
                paper(g, rays, p.sunRim, H * 0.004f, H * 0.006f, 14);
            }
            paper(g, oval(sx - sr * 1.15f, sy - sr * 1.15f, sx + sr * 1.15f, sy + sr * 1.15f), A(low ? 170 : 255, p.sunRim), H * 0.004f, H * 0.006f, 14);
            paper(g, oval(sx - sr, sy - sr, sx + sr, sy + sr), p.sun, H * 0.005f, H * 0.008f, 18);
        }

        // ---- far mountains
        s.farY = (int) (H * 0.34f);
        s.far = Bitmap.createBitmap(tile, farEnd - s.farY, Bitmap.Config.ARGB_8888);
        g = new Canvas(s.far);
        paper(g, ridge(tile, farEnd - s.farY, H * 0.24f, new float[] { 2, 3, 7 }, new float[] { H * 0.07f, H * 0.04f, H * 0.018f }, new float[] { 0.5f, 2f, 1f }),
            p.far, -H * 0.004f, -H * 0.012f, 16);

        // ---- middle hills with trees
        s.midY = (int) (H * 0.46f);
        s.mid = Bitmap.createBitmap(tile, midEnd - s.midY, Bitmap.Config.ARGB_8888);
        g = new Canvas(s.mid);
        {
            float[] k = { 1, 3, 4 }, amp = { H * 0.03f, H * 0.022f, H * 0.01f }, ph = { 0.3f, 1.4f, 2.2f };
            float baseY = H * 0.22f;
            paper(g, ridge(tile, midEnd - s.midY, baseY, k, amp, ph), p.mid, -H * 0.004f, -H * 0.012f, 18);
            int n = (int) (tile / (H * 0.075f));
            for (int i = 0; i < n; i++) {
                if (r.nextInt(10) < 4) continue;
                float x = (i + r.nextFloat() * 0.6f) * tile / n;
                float size = H * (0.05f + 0.035f * r.nextFloat());
                int kind = r.nextInt(2);
                int c = r.nextInt(2) == 0 ? p.treeA : p.treeB;
                for (int off : new int[] { -tile, 0, tile }) {
                    float xx = x + off;
                    if (xx < -size || xx > tile + size) continue;
                    tree(g, xx, baseY - wave(x, tile, k, amp, ph) + H * 0.006f, size, kind, c, p.trunk);
                }
            }
        }

        // ---- near ground: houses, flowers
        s.nearY = (int) (H * 0.7f);
        s.near = Bitmap.createBitmap(tile, H - s.nearY, Bitmap.Config.ARGB_8888);
        g = new Canvas(s.near);
        {
            float[] k = { 2, 5 }, amp = { H * 0.012f, H * 0.006f }, ph = { 1f, 0.2f };
            float baseY = H * 0.15f;
            paper(g, ridge(tile, H - s.nearY, baseY, k, amp, ph), p.near, -H * 0.003f, -H * 0.01f, 20);
            int houses = Math.max(2, (int) (tile / (H * 0.55f)));
            for (int i = 0; i < houses; i++) {
                float x = (i + 0.2f + 0.6f * r.nextFloat()) * tile / houses;
                float size = H * (0.075f + 0.02f * r.nextFloat());
                int roof = i % 2 == 0 ? p.roof : p.treeA;
                for (int off : new int[] { -tile, 0, tile }) {
                    float xx = x + off;
                    if (xx < -size || xx > tile + size) continue;
                    house(g, xx, baseY - wave(x, tile, k, amp, ph) + H * 0.01f, size, p, roof);
                }
            }
            paper(g, ridge(tile, H - s.nearY, H * 0.25f, new float[] { 3, 7 }, new float[] { H * 0.008f, H * 0.005f }, new float[] { 0.7f, 2.5f }),
                p.front, -H * 0.003f, -H * 0.008f, 20);
            Paint petal = new Paint(Paint.ANTI_ALIAS_FLAG), mid = new Paint(Paint.ANTI_ALIAS_FLAG), sh = new Paint(Paint.ANTI_ALIAS_FLAG);
            petal.setColor(A(p.sky == 2 ? 140 : 240, p.flower));
            mid.setColor(A(p.sky == 2 ? 140 : 255, p.sky == 2 ? p.window : C(0xF5B53F)));
            sh.setColor(Color.argb(30, 20, 16, 50));
            for (int i = 0; i < tile / (H * 0.07f); i++) {
                float x = r.nextFloat() * tile, y = H * (0.265f + 0.025f * r.nextFloat()), z = H * (0.006f + 0.003f * r.nextFloat());
                for (int off : new int[] { -tile, 0, tile }) {
                    float xx = x + off;
                    if (xx < -z * 3 || xx > tile + z * 3) continue;
                    g.drawCircle(xx + z * 0.2f, y + z * 0.7f, z * 1.6f, sh);
                    for (int qq = 0; qq < 5; qq++) {
                        double a = qq * Math.PI * 2 / 5 - Math.PI / 2;
                        g.drawCircle(xx + (float) Math.cos(a) * z, y + (float) Math.sin(a) * z, z * 0.75f, petal);
                    }
                    g.drawCircle(xx, y, z * 0.5f, mid);
                }
            }
        }

        // ---- clouds
        for (int i = 0; i < 3; i++) {
            float cw = H * (0.18f + 0.06f * i), ch = cw * 0.42f;
            s.clouds[i] = Bitmap.createBitmap((int) (cw * 1.1f), (int) (ch * 1.35f), Bitmap.Config.ARGB_8888);
            g = new Canvas(s.clouds[i]);
            Path c = new Path();
            c.addOval(new RectF(0, ch * 0.45f, cw, ch), Path.Direction.CW);
            c.addOval(new RectF(cw * 0.12f, ch * 0.18f, cw * 0.54f, ch * 0.88f), Path.Direction.CW);
            c.addOval(new RectF(cw * 0.38f, ch * 0.02f, cw * 0.78f, ch * 0.82f), Path.Direction.CW);
            c.addOval(new RectF(cw * 0.62f, ch * 0.3f, cw * 0.92f, ch * 0.85f), Path.Direction.CW);
            paper(g, c, p.cloud, H * 0.006f, H * 0.01f, 16);
        }
        return s;
    }

    private static void tree(Canvas g, float x, float groundY, float size, int kind, int leaf, int trunk) {
        Path t = new Path();
        t.addRect(x - size * 0.06f, groundY - size * 0.45f, x + size * 0.06f, groundY + size * 0.02f, Path.Direction.CW);
        paper(g, t, trunk, 0, 0, 0);
        Path c;
        if (kind == 0) c = oval(x - size * 0.3f, groundY - size, x + size * 0.3f, groundY - size * 0.34f);
        else { c = new Path(); c.moveTo(x, groundY - size * 1.1f); c.lineTo(x + size * 0.27f, groundY - size * 0.32f); c.lineTo(x - size * 0.27f, groundY - size * 0.32f); c.close(); }
        paper(g, c, leaf, size * 0.04f, size * 0.06f, 22);
    }

    private static void house(Canvas g, float x, float groundY, float s, Palette p, int roof) {
        float w = s * 0.9f, h = s * 0.62f;
        Path chim = new Path(); chim.addRect(x + w * 0.18f, groundY - h - s * 0.5f, x + w * 0.18f + s * 0.12f, groundY - h - s * 0.2f, Path.Direction.CW);
        paper(g, chim, roof, s * 0.03f, s * 0.04f, 18);
        Path body = new Path(); body.addRect(x - w / 2, groundY - h, x + w / 2, groundY, Path.Direction.CW);
        paper(g, body, p.house, s * 0.04f, s * 0.05f, 22);
        Path rf = new Path();
        rf.moveTo(x - w * 0.62f, groundY - h + s * 0.02f); rf.lineTo(x, groundY - h - s * 0.45f); rf.lineTo(x + w * 0.62f, groundY - h + s * 0.02f); rf.close();
        paper(g, rf, roof, s * 0.03f, s * 0.05f, 22);
        Paint q = new Paint(Paint.ANTI_ALIAS_FLAG);
        q.setColor(p.window);
        g.drawRect(x - w * 0.34f, groundY - h * 0.72f, x - w * 0.14f, groundY - h * 0.72f + w * 0.2f, q);
        g.drawRect(x + w * 0.14f, groundY - h * 0.72f, x + w * 0.34f, groundY - h * 0.72f + w * 0.2f, q);
        q.setColor(p.trunk);
        g.drawRect(x - w * 0.09f, groundY - h * 0.5f, x + w * 0.09f, groundY, q);
    }

    // ================================================================== drawing
    private void tiled(Canvas g, Bitmap bmp, int y, float dist, int tile) {
        int off = (int) (dist % tile);
        for (int x = -off; x < W; x += tile) g.drawBitmap(bmp, x, y, null);
    }

    private void backdrop(Canvas g, Scenery s) {
        g.drawBitmap(s.sky, 0, 0, null);
        drawClouds(g, s, false);
        tiled(g, s.far, s.farY, scroll * 0.1f, s.tileW);
        drawClouds(g, s, true);
        tiled(g, s.mid, s.midY, scroll * 0.35f, s.tileW);
        tiled(g, s.near, s.nearY, scroll * 0.9f, s.tileW);
    }

    private void drawClouds(Canvas g, Scenery s, boolean near) {
        float margin = H * 0.35f, span = W + margin * 2;
        for (CloudSpot c : clouds) {
            if ((c.depth > 0.3f) != near) continue;
            float x = ((c.x * span - scroll * c.depth) % span + span) % span - margin;
            g.drawBitmap(s.clouds[c.kind], (int) x, (int) (c.y * H), null);
        }
    }

    @Override public void draw(Canvas g, List<PointF> fingers) {
        if (!fingers.isEmpty()) follow(fingers.get(0).x, fingers.get(0).y);
        if (oldScenery != null) {
            float k = Math.min(1, (time - wipeBorn) / WIPE_TIME), e = k * k * (3 - 2 * k);
            backdrop(g, oldScenery);
            float edge = W * (1 - e);
            g.save();
            g.clipRect(edge, 0, W, H);
            backdrop(g, scenery);
            g.restore();
            paint.setStyle(Paint.Style.FILL);
            paint.setShader(new LinearGradient(edge - H * 0.04f, 0, edge, 0, A(0, SHADOW), A(70, SHADOW), Shader.TileMode.CLAMP));
            g.drawRect(edge - H * 0.04f, 0, edge, H, paint);
            paint.setShader(null);
            paint.setColor(Color.argb(230, 255, 255, 255));
            g.drawRect(edge - Math.max(1, H * 0.002f), 0, edge + Math.max(1, H * 0.002f), H, paint);
        } else backdrop(g, scenery);

        paint.setStyle(Paint.Style.FILL);
        for (Bit b : bits) if (b.shape == 0) drawBit(g, b);
        for (Thing t : things) if (t.kind == Thing.RING) drawRing(g, t, false);
        try { drawPlane(g); } catch (RuntimeException ignored) { }
        for (Thing t : things) {
            if (t.kind == Thing.BALLOON) drawBalloon(g, t);
            else if (t.kind == Thing.STAR) drawStar(g, t);
            else drawRing(g, t, true);
        }
        for (Bit b : bits) if (b.shape != 0) drawBit(g, b);
        for (Popup p : popups) drawPopup(g, p);
        drawCaption(g);
        drawStartHint(g);
        for (PointF f : fingers) { // a soft ring under each finger
            paint.setStyle(Paint.Style.STROKE);
            paint.setStrokeWidth(H * 0.008f);
            paint.setColor(Color.argb(200, 255, 255, 255));
            g.drawCircle(f.x, f.y, H * 0.03f, paint);
            paint.setStyle(Paint.Style.FILL);
        }
    }

    private static float appear(float age) { return age < 0 ? 0 : (age > 0.35f ? 1 : easeOutBack(age / 0.35f)); }

    private static float easeOutBack(float x) {
        x = Math.max(0, Math.min(1, x));
        final float c1 = 1.70158f, c3 = c1 + 1;
        return 1 + c3 * (float) Math.pow(x - 1, 3) + c1 * (float) Math.pow(x - 1, 2);
    }

    private static int dark(int c) { return Color.argb(Color.alpha(c), Color.red(c) * 3 / 4, Color.green(c) * 3 / 4, Color.blue(c) * 3 / 4); }

    private void fillOval(Canvas g, float l, float t, float w, float h, int color) {
        paint.setColor(color);
        rect.set(l, t, l + w, t + h);
        g.drawOval(rect, paint);
    }

    private void drawBalloon(Canvas g, Thing t) {
        float s = appear(time - t.born);
        if (s <= 0.02f) return;
        float r = H * 0.06f, y = t.y + (float) Math.sin(time * 2.2f + t.phase) * H * 0.01f;
        int c = BALLOON[t.color];
        g.save();
        g.translate(t.x, y);
        g.scale(s * r, s * r); // unit space: balloon radius = 1
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(0.05f);
        paint.setColor(A(130, INK));
        Path str = new Path(); str.moveTo(0, 1.2f); str.cubicTo(-0.3f, 1.6f, 0.3f, 2.0f, -0.05f, 2.5f);
        g.drawPath(str, paint);
        paint.setStyle(Paint.Style.FILL);
        fillOval(g, -0.9f + 0.18f, -1.1f + 0.26f, 1.8f, 2.2f, A(55, SHADOW));
        paint.setColor(dark(c));
        Path knot = new Path(); knot.moveTo(-0.18f, 1.27f); knot.lineTo(0.18f, 1.27f); knot.lineTo(0, 1.02f); knot.close();
        g.drawPath(knot, paint);
        fillOval(g, -0.9f, -1.1f, 1.8f, 2.2f, c);
        fillOval(g, -0.9f, -1.1f, 1.3f, 1.9f, Color.argb(50, 255, 255, 255));
        fillOval(g, -0.6f, -0.78f, 0.3f, 0.5f, Color.argb(150, 255, 255, 255));
        g.restore();
        if (!t.label.isEmpty()) {
            text.setTypeface(ROUND);
            text.setTextAlign(Paint.Align.CENTER);
            text.setTextSize(r * s * (t.label.length() > 1 ? 0.95f : 1.2f));
            float by = y - (text.ascent() + text.descent()) / 2;
            text.setColor(A(70, SHADOW));
            g.drawText(t.label, t.x + r * s * 0.07f, by + r * s * 0.12f, text);
            text.setColor(Color.WHITE);
            g.drawText(t.label, t.x, by, text);
        }
    }

    private static Path starPath(float cx, float cy, float r, float inner, float rotDeg, int points) {
        Path p = new Path();
        for (int i = 0; i < points * 2; i++) {
            double a = Math.toRadians(rotDeg - 90) + i * Math.PI / points;
            float rr = i % 2 == 0 ? r : r * inner;
            float x = cx + (float) Math.cos(a) * rr, y = cy + (float) Math.sin(a) * rr;
            if (i == 0) p.moveTo(x, y); else p.lineTo(x, y);
        }
        p.close();
        return p;
    }

    private void drawStar(Canvas g, Thing t) {
        float s = appear(time - t.born);
        if (s <= 0.02f) return;
        float r = H * 0.055f * s, rot = (float) Math.sin(time * 2 + t.phase) * 12, y = t.y + (float) Math.sin(time * 1.7f + t.phase) * H * 0.012f;
        paint.setColor(A(55, SHADOW)); g.drawPath(starPath(t.x + r * 0.15f, y + r * 0.22f, r, 0.5f, rot, 5), paint);
        paint.setColor(GOLD); g.drawPath(starPath(t.x, y, r, 0.5f, rot, 5), paint);
        paint.setColor(C(0xFFE389)); g.drawPath(starPath(t.x - r * 0.04f, y - r * 0.05f, r * 0.55f, 0.5f, rot, 5), paint);
        float tw = (float) Math.max(0, Math.sin(time * 4 + t.phase * 3));
        if (tw > 0.2f) { paint.setColor(Color.argb((int) (220 * tw), 255, 255, 255)); g.drawPath(starPath(t.x + r * 0.9f, y - r * 0.8f, r * 0.3f * tw, 0.25f, 0, 4), paint); }
    }

    private void drawRing(Canvas g, Thing t, boolean front) {
        float s = appear(time - t.born);
        if (s <= 0.02f) return;
        float rx = H * 0.05f * s, ry = H * 0.14f * s, y = t.y + (float) Math.sin(time * 1.5f + t.phase) * H * 0.01f;
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(H * 0.024f * s);
        paint.setStrokeCap(Paint.Cap.BUTT);
        float start = front ? -90 : 90;
        rect.set(t.x - rx + H * 0.006f, y - ry + H * 0.01f, t.x + rx + H * 0.006f, y + ry + H * 0.01f);
        paint.setColor(A(55, SHADOW));
        g.drawArc(rect, start, 180, false, paint);
        rect.set(t.x - rx, y - ry, t.x + rx, y + ry);
        for (int i = 0; i < 8; i++) {
            paint.setColor(i % 2 == 0 ? BALLOON[(t.color + i / 2) % 6] : Color.WHITE);
            g.drawArc(rect, start + i * 22.5f, 23f, false, paint);
        }
        paint.setStyle(Paint.Style.FILL);
    }

    private void drawBit(Canvas g, Bit b) {
        float k = b.age / b.life;
        int a = Math.max(0, Math.min(255, (int) (Color.alpha(b.c) * (1 - k * k))));
        paint.setColor(A(a, b.c));
        switch (b.shape) {
            case 0: g.drawCircle(b.x, b.y, b.size * (1 + b.grow * k), paint); break;
            case 1: {
                float c = (float) Math.cos(b.rot) * b.size, s = (float) Math.sin(b.rot) * b.size, f = (float) Math.cos(b.rot * 1.7f) * 0.6f;
                Path p = new Path();
                p.moveTo(b.x - c + s * f, b.y - s - c * f); p.lineTo(b.x + c + s * f, b.y + s - c * f);
                p.lineTo(b.x + c - s * f, b.y + s + c * f); p.lineTo(b.x - c - s * f, b.y - s + c * f); p.close();
                g.drawPath(p, paint);
                break;
            }
            case 2: g.drawPath(starPath(b.x, b.y, b.size * (1 - k * 0.5f), 0.3f, b.age * 200, 4), paint); break;
            default: {
                paint.setStyle(Paint.Style.STROKE);
                paint.setStrokeWidth(Math.max(0.5f, H * 0.008f * (1 - k)));
                paint.setColor(Color.argb(a * 3 / 4, 255, 255, 255));
                g.drawCircle(b.x, b.y, b.size * (1 + k * 2.2f), paint);
                paint.setStyle(Paint.Style.FILL);
            }
        }
    }

    // ---------------------------------------------------------- the plane (unit space: nose at +0.5, tail at -0.5)
    private static final int BODY = C(0xEE6B5D), BODY_DARK = C(0xC9554B), WING = C(0xFBEBCD), WING_DARK = C(0xE2CFAA),
        ACCENT = C(0xF5B53F), SKIN = C(0xF7CDA8), CAP = C(0x8A5A44), GLASS = C(0xA8DDF2);
    private final Path body = new Path(), fin = new Path();

    private void drawPlane(Canvas g) {
        float[] p = planePose();
        float S = H * 0.2f;
        g.save();
        g.translate(p[0], p[1]);
        g.rotate(p[2]);
        g.scale(S, S);
        g.save();
        g.translate(0.03f, 0.05f);
        planeParts(g, true);
        g.restore();
        planeParts(g, false);
        g.restore();
    }

    private int col(boolean shadow, int c) { return shadow ? A(50, SHADOW) : c; }

    private void planeParts(Canvas g, boolean shadow) {
        paint.setStyle(Paint.Style.FILL);
        if (!shadow) { // scarf, fluttering behind
            Path sc = new Path();
            for (int i = 0; i <= 6; i++) {
                float x = -0.02f - i * 0.055f, y = -0.19f + (float) Math.sin(time * 12 - i * 0.9) * 0.012f * i;
                if (i == 0) sc.moveTo(x, y); else sc.lineTo(x, y);
            }
            for (int i = 6; i >= 0; i--) sc.lineTo(-0.02f - i * 0.055f, -0.15f + (float) Math.sin(time * 12 - i * 0.9) * 0.012f * i + i * 0.004f);
            sc.close();
            paint.setColor(ACCENT); g.drawPath(sc, paint);
        }
        fillOval(g, -0.2f, -0.05f, 0.4f, 0.09f, col(shadow, WING_DARK)); // far wing
        fin.rewind();
        fin.moveTo(-0.47f, -0.03f);
        fin.cubicTo(-0.48f, -0.18f, -0.46f, -0.3f, -0.38f, -0.31f);
        fin.cubicTo(-0.32f, -0.31f, -0.3f, -0.15f, -0.22f, -0.04f);
        fin.close();
        paint.setColor(col(shadow, BODY_DARK)); g.drawPath(fin, paint);
        if (!shadow) { // pilot
            fillOval(g, -0.085f, -0.24f, 0.17f, 0.17f, SKIN);
            paint.setColor(CAP);
            rect.set(-0.092f, -0.25f, 0.092f, -0.066f);
            g.drawArc(rect, 180, 180, true, paint);
            fillOval(g, -0.11f, -0.17f, 0.06f, 0.035f, CAP);
            fillOval(g, 0.03f, -0.2f, 0.065f, 0.065f, GLASS);
            paint.setStyle(Paint.Style.STROKE);
            paint.setStrokeWidth(0.014f);
            paint.setColor(C(0x6B4A3A));
            rect.set(0.03f, -0.2f, 0.095f, -0.135f); g.drawOval(rect, paint);
            g.drawLine(-0.08f, -0.17f, 0.03f, -0.17f, paint);
            paint.setStrokeWidth(0.012f);
            paint.setStrokeCap(Paint.Cap.ROUND);
            paint.setColor(C(0x8A4B3A));
            rect.set(0.02f, -0.14f, 0.07f, -0.1f); g.drawArc(rect, 10, 120, false, paint);
            paint.setStyle(Paint.Style.FILL);
            Path ws = new Path(); ws.moveTo(0.1f, -0.12f); ws.lineTo(0.13f, -0.22f); ws.lineTo(0.19f, -0.12f); ws.close();
            paint.setColor(A(170, GLASS)); g.drawPath(ws, paint);
        }
        body.rewind();
        body.moveTo(0.42f, 0f);
        body.cubicTo(0.42f, -0.13f, 0.2f, -0.14f, 0.0f, -0.125f);
        body.lineTo(-0.3f, -0.075f);
        body.cubicTo(-0.42f, -0.06f, -0.5f, -0.05f, -0.5f, 0.0f);
        body.cubicTo(-0.5f, 0.04f, -0.42f, 0.06f, -0.3f, 0.07f);
        body.lineTo(0.0f, 0.125f);
        body.cubicTo(0.2f, 0.15f, 0.42f, 0.13f, 0.42f, 0f);
        body.close();
        paint.setColor(col(shadow, BODY)); g.drawPath(body, paint);
        if (!shadow) {
            g.save();
            g.clipPath(body);
            paint.setColor(WING); g.drawRect(-0.24f, -0.2f, -0.17f, 0.2f, paint);
            paint.setColor(Color.argb(40, 255, 255, 255)); g.drawRect(-0.5f, -0.2f, 0.5f, -0.08f, paint);
            g.restore();
        }
        fillOval(g, -0.5f, -0.035f, 0.2f, 0.07f, col(shadow, WING)); // tail wing
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(0.022f);
        paint.setStrokeCap(Paint.Cap.BUTT);
        paint.setColor(col(shadow, C(0x6B6B7B)));
        g.drawLine(0.06f, 0.1f, 0.09f, 0.2f, paint);
        paint.setStyle(Paint.Style.FILL);
        fillOval(g, 0.09f - 0.048f, 0.21f - 0.048f, 0.096f, 0.096f, col(shadow, C(0x3D3D50)));
        if (!shadow) fillOval(g, 0.09f - 0.018f, 0.21f - 0.018f, 0.036f, 0.036f, C(0xB8B8C8));
        fillOval(g, -0.2f, 0.01f, 0.44f, 0.12f, col(shadow, WING)); // near wing
        if (!shadow) {
            fillOval(g, 0.05f, 0.035f, 0.07f, 0.07f, C(0x59A7E8));
            fillOval(g, 0.072f, 0.057f, 0.026f, 0.026f, Color.WHITE);
        }
        fillOval(g, 0.37f, -0.065f, 0.12f, 0.13f, col(shadow, ACCENT)); // nose
        if (!shadow) {
            float blade = 0.42f * Math.max(0.12f, Math.abs((float) Math.cos(prop)));
            fillOval(g, 0.465f, -0.21f, 0.04f, 0.42f, Color.argb(45, 255, 255, 255));
            fillOval(g, 0.47f, -blade / 2, 0.03f, blade, C(0x6B5A4A));
            fillOval(g, 0.46f, -0.02f, 0.04f, 0.04f, C(0x6B5A4A));
        }
    }

    private void drawPopup(Canvas g, Popup p) {
        float t = time - p.born;
        float s = t < 0.3f ? Math.max(0.05f, easeOutBack(t / 0.3f)) : 1f;
        int a = (int) (255 * Math.max(0, Math.min(1, (1.4f - t) / 0.4f)));
        if (a <= 0) return;
        float size = H * 0.2f * s, x = p.x, y = p.y - t * H * 0.06f;
        text.setTypeface(ROUND);
        text.setTextAlign(Paint.Align.CENTER);
        text.setTextSize(size);
        float by = y - (text.ascent() + text.descent()) / 2;
        text.setStyle(Paint.Style.FILL);
        text.setColor(A(a * 60 / 255, SHADOW));
        g.drawText(p.text, x + size * 0.05f, by + size * 0.08f, text);
        text.setStyle(Paint.Style.STROKE);
        text.setStrokeJoin(Paint.Join.ROUND);
        text.setStrokeWidth(size * 0.14f);
        text.setColor(Color.argb(a, 255, 255, 255));
        g.drawText(p.text, x, by, text);
        text.setStyle(Paint.Style.FILL);
        text.setColor(A(a, p.c));
        g.drawText(p.text, x, by, text);
    }

    /** The scene's name on a strip of paper, when the sky changes. */
    private void drawCaption(Canvas g) {
        if (caption == null) return;
        float t = time - captionBorn;
        final float life = 3.2f;
        if (t < 0 || t > life) return;
        int a = (int) (255 * Math.min(1f, Math.min(t * 5f, (life - t) * 2f)));
        text.setTypeface(ROUND);
        text.setTextAlign(Paint.Align.CENTER);
        text.setTextSize(H * 0.05f);
        float w = text.measureText(caption) + H * 0.08f, h = H * 0.05f * 1.3f + H * 0.03f;
        g.save();
        g.translate(W / 2f, H * 0.9f);
        g.rotate(-2f);
        paint.setColor(A(a * 60 / 255, SHADOW));
        g.drawRect(-w / 2 + H * 0.006f, -h / 2 + H * 0.01f, w / 2 + H * 0.006f, h / 2 + H * 0.01f, paint);
        paint.setColor(Color.argb(a, 255, 250, 240));
        g.drawRect(-w / 2, -h / 2, w / 2, h / 2, paint);
        text.setColor(A(a, INK));
        g.drawText(caption, 0, -(text.ascent() + text.descent()) / 2, text);
        g.restore();
    }

    private void drawStartHint(Canvas g) {
        final float life = 8f;
        if (time > life || startHint.isEmpty()) return;
        int a = (int) (255 * Math.min(1f, (life - time) * 1.5f));
        text.setTypeface(UI);
        text.setTextAlign(Paint.Align.CENTER);
        text.setTextSize(Math.max(12, H * 0.03f));
        float tw = text.measureText(startHint);
        if (tw > W * 0.85f) { text.setTextSize(text.getTextSize() * W * 0.85f / tw); tw = text.measureText(startHint); }
        float h = text.getTextSize() * 1.8f;
        paint.setColor(Color.argb(a * 235 / 255, 255, 250, 240));
        g.drawRect((W - tw) / 2 - h * 0.6f, H * 0.012f, (W + tw) / 2 + h * 0.6f, H * 0.012f + h, paint);
        text.setColor(A(a, INK));
        g.drawText(startHint, W / 2f, H * 0.012f + h / 2 - (text.ascent() + text.descent()) / 2, text);
    }

    // ================================================================== menu card
    private void demo() {
        setTheme(0, false, true);
        things.clear();
        spawn(Thing.BALLOON, W * 0.5f, H * 0.3f, "Y", 0, 0, -1);
        spawn(Thing.RING, W * 0.68f, H * 0.42f, "", 3, 0, -1);
        spawn(Thing.STAR, W * 0.84f, H * 0.26f, "", 0, 0, -1);
        spawn(Thing.BALLOON, W * 0.9f, H * 0.55f, "", 4, 0, -1);
        planeY = H * 0.42f;
        for (int i = 0; i < 30; i++) { update(0.03f); for (Thing t : things) t.x += speed() * 0.03f; }
    }
}

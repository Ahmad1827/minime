package com.foryusef.littleknight;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.LinearGradient;
import android.graphics.Matrix;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.PointF;
import android.graphics.RadialGradient;
import android.graphics.RectF;
import android.graphics.Shader;

import java.util.ArrayList;
import java.util.Random;

final class Item {
    final String name, art, fact; int piece = -1;
    Item(String name, String art, String fact) { this.name = name; this.art = art; this.fact = fact; }
}

final class Content {
    private Content() {}
    static final int KING = 0, QUEEN = 1, ROOK = 2, BISHOP = 3, KNIGHT = 4, PAWN = 5;
    static final String[] PIECE_NAMES = { "King", "Queen", "Rook", "Bishop", "Knight", "Pawn" };
    static final String[] PIECE_FACTS = {
        "The King moves one step in any direction",
        "The Queen moves any direction, as far as she likes",
        "The Rook moves in straight lines",
        "The Bishop moves diagonally",
        "The Knight jumps in an L shape",
        "The Pawn marches forward",
    };
    // little tune (scale steps) for each piece
    static final int[][] PIECE_TUNES = { { 0, 3, 5 }, { 5, 7, 8, 10 }, { 3, 3, 5 }, { 4, 6, 8 }, { 5, 3, 4 }, { 2, 3 } };

    static final Item[] TREASURES = {
        new Item("Crown", "crown", "A golden crown for the King"),
        new Item("Shield", "shield", "A shield for a brave knight"),
        new Item("Castle", "castle", "A castle with tall towers"),
        new Item("Gem", "gem", "A shiny blue gem"),
        new Item("Star", "star", "A twinkly star"),
        new Item("Sword", "sword", "A knight's sword"),
        new Item("Bell", "bell", "Ding dong, the castle bell"),
        new Item("Heart", "heart", "A big red heart"),
    };

    static Item pieceItem(String name, int p) { Item it = new Item(name, null, null); it.piece = p; return it; }

    // A..Z
    static final Item[] LETTERS = {
        new Item("Apple", "apple", null), pieceItem("Bishop", BISHOP), new Item("Castle", "castle", null),
        new Item("Drum", "drum", null), new Item("Egg", "egg", null), new Item("Flag", "flag", null),
        new Item("Gem", "gem", null), new Item("Heart", "heart", null), new Item("Ice cream", "icecream", null),
        new Item("Jar", "jar", null), pieceItem("King", KING), new Item("Leaf", "leaf", null),
        new Item("Moon", "moon", null), new Item("Note", "note", null), new Item("Owl", "owl", null),
        pieceItem("Pawn", PAWN), pieceItem("Queen", QUEEN), pieceItem("Rook", ROOK),
        new Item("Shield", "shield", null), new Item("Tree", "tree", null), new Item("Umbrella", "umbrella", null),
        new Item("Vase", "vase", null), new Item("Wand", "wand", null), new Item("X marks the spot", "xmark", null),
        new Item("Yo-yo", "yoyo", null), new Item("Zigzag", "zigzag", null),
    };

    static final String[] NUMBER_WORDS = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };

    static final int[] CONFETTI = {
        Color.rgb(242, 95, 92), Color.rgb(255, 159, 28), Color.rgb(255, 210, 63), Color.rgb(59, 178, 115),
        Color.rgb(77, 157, 224), Color.rgb(119, 104, 174), Color.rgb(231, 111, 176), Color.WHITE
    };
}

final class Sprite {
    Pic pic; float size; // pixels per unit
    float x, y, vx, vy, rot, wobble, phase;
    float born, life = 5f;
    boolean fixed; // pinned to a chess square
    float halfW() { return pic.bounds.width() * size / 2f; }
    float halfH() { return pic.bounds.height() * size / 2f; }
}

final class Particle { float x, y, vx, vy, age, life, size; int c; }

final class MoveHint { int col, row; ArrayList<int[]> targets = new ArrayList<>(); float born, life = 4.5f; }

interface Sound { void play(int step, float delay, float gain); }

/** Update + draw. All calls on the UI thread. */
final class Scene {
    int W, H;
    float time;
    Sound sound = (s, d, g) -> { };

    private final ArrayList<Sprite> sprites = new ArrayList<>();
    private final ArrayList<Particle> parts = new ArrayList<>();
    private MoveHint hint;
    private final Random rnd = new Random();
    private final Random fx = new Random();

    private String caption; private float captionBorn = -100;
    private float lastPress = -100;
    private int trailHue;
    final ArrayList<PointF> fingers = new ArrayList<>();
    String startHint = "";

    float sq, boardX, boardY, frame;
    private Bitmap background;

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint text = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF rect = new RectF();

    Scene(int w, int h) { this(w, h, 0); }
    Scene(int w, int h, int theme) { this.theme = wrapTheme(theme); resize(w, h); }

    void resize(int w, int h) {
        W = Math.max(1, w); H = Math.max(1, h);
        float avail = Math.min(W, H);
        frame = avail * 0.04f;
        sq = (float) Math.floor((avail * 0.94f - 2 * frame) / 8f);
        boardX = (float) Math.floor((W - 8 * sq) / 2f);
        boardY = (float) Math.floor((H - 8 * sq) / 2f);
        if (background != null) background.recycle();
        if (oldBackground != null) { oldBackground.recycle(); oldBackground = null; }
        background = buildBackground();
        sprites.clear(); parts.clear(); hint = null;
    }

    private boolean deliberate() { boolean d = time - lastPress > 1.6f; lastPress = time; return d; }

    private void tune(int[] steps, float spacing, float gain) {
        for (int i = 0; i < steps.length; i++) sound.play(steps[i], i * spacing, gain);
    }

    void welcome() { tune(new int[] { 0, 2, 3, 5, 7, 8, 10 }, 0.12f, 0.5f); }

    // ---------------------------------------------------------- input
    void onLetter(char c) { deliberate(); spawnLetter(c); }
    void onNumber(int n) { deliberate(); spawnNumber(n); }

    void onOtherKey() {
        boolean d = deliberate();
        if (d && rnd.nextInt(3) > 0) placePieceWithMoves(rnd.nextInt(8), rnd.nextInt(8), rnd.nextInt(6), rnd.nextBoolean(), true);
        else spawnRandom(d);
    }

    void trail(float x, float y) {
        trailHue = (trailHue + 1) % (Content.CONFETTI.length - 1);
        addParticle(x, y, 30, Content.CONFETTI[trailHue], 0.8f, H * 0.008f, 0.2f);
    }

    /** A finger touched the screen: piece on the board, treasure anywhere else. */
    void tap(float x, float y, boolean white) {
        int col = (int) Math.floor((x - boardX) / sq), row = (int) Math.floor((y - boardY) / sq);
        boolean d = deliberate();
        if (col >= 0 && col < 8 && row >= 0 && row < 8) placePieceWithMoves(col, row, rnd.nextInt(6), white, d);
        else spawnTreasure(Content.TREASURES[rnd.nextInt(Content.TREASURES.length)], x, y);
    }

    // ---------------------------------------------------------- spawning
    private PointF randomSpot(float halfW, float halfH) {
        float x = halfW + rnd.nextFloat() * Math.max(1, W - 2 * halfW);
        float y = halfH + rnd.nextFloat() * Math.max(1, H * 0.8f - 2 * halfH);
        return new PointF(x, y);
    }

    private Sprite newSprite(Pic pic, float size, float x, float y) {
        Sprite s = new Sprite();
        s.pic = pic; s.size = size; s.x = x; s.y = y; s.born = time;
        s.vx = (rnd.nextFloat() - 0.5f) * H * 0.02f;
        s.vy = -H * (0.006f + rnd.nextFloat() * 0.015f);
        s.wobble = 3 + rnd.nextFloat() * 4; s.phase = rnd.nextFloat() * 6.28f;
        s.life = 4.5f + rnd.nextFloat() * 1.5f;
        return s;
    }

    private static Pic piecePic(int piece, boolean white) { return Art.get("piece:" + piece + (white ? "w" : "b")); }

    private void spawnLetter(char c) {
        Item it = Content.LETTERS[c - 'A'];
        int color = rnd.nextInt(Art.BLOCK_COLORS.length);
        float size = H * 0.24f;
        Sprite block = newSprite(Art.get("block:" + c + ":" + color), size, 0, 0);
        Sprite pic = newSprite(it.piece >= 0 ? piecePic(it.piece, rnd.nextBoolean()) : Art.get(it.art), size * 1.05f, 0, 0);

        float gap = size * 0.12f, total = block.halfW() * 2 + gap + pic.halfW() * 2;
        PointF p = randomSpot(total / 2f + W * 0.02f, Math.max(block.halfH(), pic.halfH()) + H * 0.03f);
        block.x = p.x - total / 2f + block.halfW(); block.y = p.y;
        pic.x = p.x + total / 2f - pic.halfW(); pic.y = p.y;
        pic.vx = block.vx; pic.vy = block.vy; pic.phase = block.phase + 1;
        pic.born = time + 0.12f;
        add(block); add(pic);

        burst(p.x, p.y, 18);
        setCaption(it.name.startsWith("X ") ? "X marks the spot!" : c + " is for " + it.name);
        int step = (c - 'A') % 8;
        tune(new int[] { step, step + 2, step + 4 }, 0.1f, 0.55f);
    }

    private void spawnNumber(int n) {
        int color = rnd.nextInt(Art.BLOCK_COLORS.length);
        float size = H * 0.2f;
        float pawnSize = Math.min(H * 0.17f, W * 0.85f / Math.max(1, n) / 0.66f);
        float pawnW = pawnSize * 0.66f;
        float rowW = pawnW * n;
        float cy = H * 0.18f + rnd.nextFloat() * H * 0.2f;
        float cx = W / 2f + (rnd.nextFloat() - 0.5f) * Math.max(0, W - Math.max(rowW, size) - W * 0.1f);

        Sprite block = newSprite(Art.get("block:" + n + ":" + color), size, cx, cy);
        block.vx = 0; block.vy = 0;
        add(block);
        boolean white = rnd.nextBoolean();
        for (int i = 0; i < n; i++) {
            Sprite s = newSprite(piecePic(Content.PAWN, white), pawnSize, cx - rowW / 2f + pawnW * (i + 0.5f), cy + size * 0.5f + pawnSize * 0.62f);
            s.born = time + 0.45f + i * 0.4f;
            s.life = 5.5f;
            s.vx = 0; s.vy = 0; s.wobble = 3;
            add(s);
            sound.play(i, 0.45f + i * 0.4f, 0.5f); // one note per pawn: counting with music
        }
        burst(cx, cy, 18);
        sound.play(0, 0, 0.5f);
        setCaption(n == 0 ? "0 - zero pawns" : n + " - " + Content.NUMBER_WORDS[n] + (n == 1 ? " pawn" : " pawns"));
    }

    private void spawnRandom(boolean deliberate) {
        if (rnd.nextInt(10) < 6) {
            int p = rnd.nextInt(6);
            Sprite s = newSprite(piecePic(p, rnd.nextBoolean()), H * 0.22f, 0, 0);
            PointF pt = randomSpot(s.halfW() + 10, s.halfH() + 10);
            s.x = pt.x; s.y = pt.y;
            add(s);
            burst(pt.x, pt.y, 16);
            setCaption(deliberate ? Content.PIECE_FACTS[p] : "The " + Content.PIECE_NAMES[p]);
            tune(Content.PIECE_TUNES[p], 0.11f, 0.5f);
        } else {
            PointF pt = randomSpot(H * 0.12f, H * 0.12f);
            spawnTreasure(Content.TREASURES[rnd.nextInt(Content.TREASURES.length)], pt.x, pt.y);
        }
    }

    private void spawnTreasure(Item it, float x, float y) {
        add(newSprite(Art.get(it.art), H * 0.2f, x, y));
        burst(x, y, 18);
        setCaption(it.fact);
        int s = rnd.nextInt(6);
        tune(new int[] { s, s + 5 }, 0.09f, 0.45f);
    }

    // Put a piece on a real square and light up the squares it can move to.
    private void placePieceWithMoves(int col, int row, int piece, boolean white, boolean deliberate) {
        if (piece == Content.PAWN) row = white ? Math.max(1, row) : Math.min(6, row);
        for (Sprite old : sprites) if (old.fixed) old.life = Math.min(old.life, time - old.born + 0.3f);

        float cx = boardX + (col + 0.5f) * sq, cy = boardY + (row + 0.5f) * sq;
        Sprite s = newSprite(piecePic(piece, white), sq * 0.85f, cx, cy);
        s.fixed = true; s.vx = s.vy = 0; s.wobble = 0; s.life = 4.5f;
        add(s);

        hint = new MoveHint();
        hint.col = col; hint.row = row; hint.born = time;
        hint.targets = moves(piece, col, row, white);
        burst(cx, cy, 14);

        String square = "" + (char) ('a' + col) + (8 - row);
        setCaption(deliberate ? Content.PIECE_FACTS[piece] : Content.PIECE_NAMES[piece] + " on " + square);
        tune(Content.PIECE_TUNES[piece], 0.11f, 0.5f);
    }

    private static void ray(ArrayList<int[]> list, int c, int r, int dc, int dr, boolean slide) {
        int x = c + dc, y = r + dr;
        while (x >= 0 && x < 8 && y >= 0 && y < 8) { list.add(new int[] { x, y }); if (!slide) break; x += dc; y += dr; }
    }

    private static ArrayList<int[]> moves(int piece, int c, int r, boolean white) {
        ArrayList<int[]> list = new ArrayList<>();
        int[][] orth = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
        int[][] diag = { { 1, 1 }, { -1, 1 }, { 1, -1 }, { -1, -1 } };
        switch (piece) {
            case Content.KING:
                for (int i = 0; i < 4; i++) { ray(list, c, r, orth[i][0], orth[i][1], false); ray(list, c, r, diag[i][0], diag[i][1], false); }
                break;
            case Content.QUEEN:
                for (int i = 0; i < 4; i++) { ray(list, c, r, orth[i][0], orth[i][1], true); ray(list, c, r, diag[i][0], diag[i][1], true); }
                break;
            case Content.ROOK: for (int i = 0; i < 4; i++) ray(list, c, r, orth[i][0], orth[i][1], true); break;
            case Content.BISHOP: for (int i = 0; i < 4; i++) ray(list, c, r, diag[i][0], diag[i][1], true); break;
            case Content.KNIGHT:
                int[][] k = { { 1, 2 }, { 2, 1 }, { -1, 2 }, { -2, 1 }, { 1, -2 }, { 2, -1 }, { -1, -2 }, { -2, -1 } };
                for (int i = 0; i < 8; i++) ray(list, c, r, k[i][0], k[i][1], false);
                break;
            case Content.PAWN:
                int dir = white ? -1 : 1, start = white ? 6 : 1;
                if (r + dir >= 0 && r + dir < 8) list.add(new int[] { c, r + dir });
                if (r == start) list.add(new int[] { c, r + 2 * dir });
                break;
        }
        return list;
    }

    private void add(Sprite s) {
        sprites.add(s);
        int alive = 0;
        for (int i = sprites.size() - 1; i >= 0; i--)
            if (++alive > 26) { Sprite o = sprites.get(i); o.life = Math.min(o.life, Math.max(0.01f, time - o.born) + 0.35f); }
    }

    private void burst(float x, float y, int n) {
        for (int i = 0; i < n; i++)
            addParticle(x, y, H * 0.4f, Content.CONFETTI[fx.nextInt(Content.CONFETTI.length)], 0.9f + fx.nextFloat() * 0.6f, H * 0.009f, 1f);
    }

    private void addParticle(float x, float y, float speed, int c, float life, float size, float gravity) {
        if (parts.size() > 600) return;
        double a = fx.nextDouble() * Math.PI * 2, v = speed * (0.3 + fx.nextDouble() * 0.7);
        Particle p = new Particle();
        p.x = x; p.y = y; p.vx = (float) (Math.cos(a) * v); p.vy = (float) (Math.sin(a) * v) - speed * 0.3f * gravity;
        p.life = life; p.size = size * (0.6f + fx.nextFloat() * 0.8f); p.c = c;
        parts.add(p);
    }

    private void setCaption(String t) { caption = t; captionBorn = time; }

    // ---------------------------------------------------------- update
    void update(float dt) {
        time += dt;
        for (int i = sprites.size() - 1; i >= 0; i--) {
            Sprite s = sprites.get(i);
            if (time < s.born) continue;
            if (time - s.born > s.life) { sprites.remove(i); continue; }
            s.x += s.vx * dt; s.y += s.vy * dt;
            s.rot = (float) Math.sin((time - s.born) * 2.2 + s.phase) * s.wobble;
        }
        for (int i = parts.size() - 1; i >= 0; i--) {
            Particle p = parts.get(i);
            p.age += dt;
            if (p.age > p.life) { parts.remove(i); continue; }
            p.x += p.vx * dt; p.y += p.vy * dt;
            p.vx *= 0.95f; p.vy = p.vy * 0.95f + H * 0.5f * dt;
        }
        if (hint != null && time - hint.born > hint.life) hint = null;
        if (time - themeSince > THEME_SECONDS) nextTheme();
    }

    // ---------------------------------------------------------- painted medieval dusk background
    static int mix(int a, int b, float t) {
        t = Math.max(0, Math.min(1, t));
        return Color.argb((int) (Color.alpha(a) + (Color.alpha(b) - Color.alpha(a)) * t), (int) (Color.red(a) + (Color.red(b) - Color.red(a)) * t),
                          (int) (Color.green(a) + (Color.green(b) - Color.green(a)) * t), (int) (Color.blue(a) + (Color.blue(b) - Color.blue(a)) * t));
    }

    private static float[] makeRidge(Random r, int k, float rough) {
        int n = (1 << k) + 1;
        float[] h = new float[n];
        h[0] = r.nextFloat(); h[n - 1] = r.nextFloat();
        float amp = 1f;
        for (int step = n - 1; step > 1; step /= 2) {
            for (int i = step / 2; i < n; i += step)
                h[i] = (h[i - step / 2] + h[i + step / 2]) / 2 + (float) (r.nextDouble() - 0.5) * amp;
            amp *= rough;
        }
        float mn = Float.MAX_VALUE, mx = -Float.MAX_VALUE;
        for (float v : h) { mn = Math.min(mn, v); mx = Math.max(mx, v); }
        for (int i = 0; i < n; i++) h[i] = (h[i] - mn) / Math.max(0.0001f, mx - mn);
        return h;
    }

    private float ridgeY(float[] h, float baseY, float amp, float x) {
        float f = Math.max(0, Math.min(h.length - 1.001f, x / W * (h.length - 1)));
        int i = (int) f; float t = f - i;
        return baseY - (h[i] * (1 - t) + h[i + 1] * t) * amp;
    }

    private static Paint fillPaint() { Paint p = new Paint(Paint.ANTI_ALIAS_FLAG); p.setStyle(Paint.Style.FILL); return p; }
    private static Paint solid(int c) { Paint p = fillPaint(); p.setColor(c); return p; }
    private static Paint vgrad(float y0, float y1, int top, int bottom) {
        Paint p = fillPaint();
        p.setShader(new LinearGradient(0, y0, 0, y1, top, bottom, Shader.TileMode.CLAMP));
        return p;
    }

    private void fillRidge(Canvas g, float[] h, float baseY, float amp, int top, int bottom) {
        Path p = new Path();
        p.moveTo(0, baseY - h[0] * amp);
        for (int i = 1; i < h.length; i++) p.lineTo(W * i / (float) (h.length - 1), baseY - h[i] * amp);
        p.lineTo(W, H + 2); p.lineTo(0, H + 2); p.close();
        g.drawPath(p, vgrad(baseY - amp - 1, H + 2, top, bottom));
    }

    private void mist(Canvas g, float y, float thick, int c) {
        Paint p = fillPaint();
        p.setShader(new LinearGradient(0, y - thick, 0, y + thick, new int[] { Art.withAlpha(0, c), c, Art.withAlpha(0, c) },
                                       new float[] { 0f, 0.5f, 1f }, Shader.TileMode.CLAMP));
        g.drawRect(0, y - thick, W, y + thick, p);
    }

    private static void glow(Canvas g, Paint p, float x, float y, float r, int c) {
        if (r <= 0.5f) return;
        p.setShader(new RadialGradient(x, y, r, c, Art.withAlpha(0, c), Shader.TileMode.CLAMP));
        g.drawCircle(x, y, r, p);
        p.setShader(null);
    }

    private static void glow(Canvas g, float x, float y, float r, int c) { glow(g, fillPaint(), x, y, r, c); }

    private static void battlement(Path p, float x, float top, float w, float h, float m) {
        p.addRect(x, top, x + w, top + h, Path.Direction.CW);
        int k = Math.max(2, (int) (w / (m * 2)));
        float sp = w / (2 * k - 1);
        for (int i = 0; i < 2 * k - 1; i += 2)
            p.addRect(x + i * sp, top - m * 0.9f, x + i * sp + sp, top + 1, Path.Direction.CW);
    }

    // tower with a conical roof; returns the roof tip
    private static PointF tower(Path p, float x, float gy, float w, float h) {
        p.addRect(x, gy - h, x + w, gy, Path.Direction.CW);
        p.addRect(x - w * 0.08f, gy - h - w * 0.12f, x - w * 0.08f + w * 1.16f, gy - h + w * 0.02f, Path.Direction.CW);
        PointF tip = new PointF(x + w / 2, gy - h - w * 1.5f);
        p.addPath(Art.poly(x - w * 0.16f, gy - h - w * 0.1f, x + w * 1.16f, gy - h - w * 0.1f, tip.x, tip.y));
        return tip;
    }

    private static void pennant(Canvas g, PointF tip, float s, int pole, int flag, boolean right) {
        float d = right ? 1 : -1;
        Paint pen = new Paint(Paint.ANTI_ALIAS_FLAG);
        pen.setStyle(Paint.Style.STROKE); pen.setColor(pole); pen.setStrokeWidth(Math.max(1f, s * 0.012f));
        g.drawLine(tip.x, tip.y + 1, tip.x, tip.y - s * 0.16f, pen);
        float y = tip.y - s * 0.16f;
        g.drawPath(Art.poly(tip.x, y, tip.x + d * s * 0.12f, y + s * 0.01f, tip.x + d * s * 0.2f, y + s * 0.035f,
                            tip.x + d * s * 0.1f, y + s * 0.045f, tip.x, y + s * 0.06f), solid(flag));
    }

    private static void window(Canvas g, float x, float y, float w, float h, int c, boolean glow) {
        if (glow) glow(g, x + w / 2, y + h / 2, h * 1.6f, Art.withAlpha(90, c));
        Path p = new Path();
        p.moveTo(x, y + w / 2);
        p.arcTo(new RectF(x, y, x + w, y + w), 180, 180, false);
        p.lineTo(x + w, y + h);
        p.lineTo(x, y + h);
        p.close();
        g.drawPath(p, solid(c));
    }

    private void castle(Canvas g, float cx, float gy, float s, int body, int win, int flag, boolean detail) {
        ArrayList<PointF> tips = new ArrayList<>();
        Path p = Art.NP();
        battlement(p, cx - 0.62f * s, gy - 0.3f * s, 1.24f * s, 0.3f * s + 2, 0.06f * s);
        battlement(p, cx - 0.26f * s, gy - 0.62f * s, 0.52f * s, 0.62f * s, 0.06f * s);
        tips.add(tower(p, cx - 0.7f * s, gy, 0.2f * s, 0.56f * s));
        tips.add(tower(p, cx + 0.5f * s, gy, 0.2f * s, 0.56f * s));
        tips.add(tower(p, cx - 0.4f * s, gy, 0.15f * s, 0.78f * s));
        tips.add(tower(p, cx + 0.25f * s, gy, 0.15f * s, 0.72f * s));
        tips.add(tower(p, cx - 0.09f * s, gy, 0.18f * s, 1.02f * s));
        g.drawPath(p, solid(body));
        if (!detail) return;

        // gate with warm light inside
        float gw = 0.16f * s, gh = 0.2f * s;
        glow(g, cx, gy - gh * 0.4f, gh * 1.8f, Art.withAlpha(110, win));
        window(g, cx - gw / 2, gy - gh, gw, gh, mix(win, body, 0.25f), false);
        Paint pen = new Paint(Paint.ANTI_ALIAS_FLAG);
        pen.setStyle(Paint.Style.STROKE); pen.setColor(Art.withAlpha(150, body)); pen.setStrokeWidth(Math.max(1f, s * 0.008f));
        for (int i = 1; i < 4; i++) g.drawLine(cx - gw / 2 + gw * i / 4f, gy - gh * 0.85f, cx - gw / 2 + gw * i / 4f, gy, pen);

        float ww = 0.035f * s, wh = 0.07f * s;
        window(g, cx - 0.17f * s, gy - 0.5f * s, ww, wh, win, true);
        window(g, cx + 0.13f * s, gy - 0.5f * s, ww, wh, win, true);
        window(g, cx - ww / 2, gy - 0.86f * s, ww, wh, win, true);
        window(g, cx - 0.6f * s - ww / 2, gy - 0.44f * s, ww, wh, win, true);
        window(g, cx + 0.6f * s - ww / 2, gy - 0.44f * s, ww, wh, win, true);
        window(g, cx - 0.325f * s - ww / 2, gy - 0.66f * s, ww, wh, win, true);
        window(g, cx + 0.325f * s - ww / 2, gy - 0.6f * s, ww, wh, win, true);

        for (int i = 0; i < tips.size(); i++) pennant(g, tips.get(i), s, body, flag, i % 2 == 1);
    }

    private static void pine(Canvas g, float x, float gy, float h, Paint b) {
        float w = h * 0.36f;
        Path p = Art.NP();
        for (int i = 0; i < 3; i++) {
            float top = gy - h + i * h * 0.24f, bot = gy - h * 0.12f - (2 - i) * h * 0.16f;
            float hw = w * (0.45f + i * 0.28f);
            p.addPath(Art.poly(x, top, x + hw, bot, x - hw, bot));
        }
        p.addRect(x - w * 0.07f, gy - h * 0.2f, x + w * 0.07f, gy + h * 0.02f, Path.Direction.CW);
        g.drawPath(p, b);
    }

    private void cottage(Canvas g, float x, float gy, float s, int body, int win) {
        Paint b = solid(body);
        g.drawRect(x, gy - s * 0.5f, x + s, gy + 2, b);
        g.drawPath(Art.poly(x - s * 0.12f, gy - s * 0.48f, x + s * 1.12f, gy - s * 0.48f, x + s * 0.5f, gy - s * 0.95f), b);
        g.drawRect(x + s * 0.68f, gy - s * 0.95f, x + s * 0.8f, gy - s * 0.65f, b);
        window(g, x + s * 0.2f, gy - s * 0.36f, s * 0.16f, s * 0.2f, win, true);
        window(g, x + s * 0.6f, gy - s * 0.36f, s * 0.16f, s * 0.2f, win, true);
    }

    private void cloud(Canvas g, Random r, float x, float y, float len, int top, int under) {
        Paint t = solid(top), u = solid(under);
        for (int i = 0; i < 5; i++) {
            float w = len * (0.4f + r.nextFloat() * 0.6f), h = len * (0.05f + r.nextFloat() * 0.05f);
            float ox = x + (float) (r.nextDouble() - 0.5) * len * 0.6f, oy = y + (float) (r.nextDouble() - 0.5) * len * 0.08f;
            g.drawOval(ox - w / 2, oy - h / 2, ox + w / 2, oy + h / 2, t);
            g.drawOval(ox - w * 0.42f, oy, ox + w * 0.42f, oy + h * 0.45f, u);
        }
    }

    private static Path rr(float x, float y, float w, float h, float r) { return Art.rrect(x, y, w, h, r); }

    private Bitmap buildBackground() {
        Bitmap bmp = Bitmap.createBitmap(W, H, Bitmap.Config.ARGB_8888);
        Canvas g = new Canvas(bmp);
        Random r = new Random(1827 + theme * 101); // fixed per theme: same painting every run
        float u = H / 1080f;
        switch (theme) {
            case 1: paintMeadow(g, r); break;
            case 2: paintWinter(g, r); break;
            case 3: paintSeaside(g, r); break;
            default: paintDusk(g, r); break;
        }
        int vig = vignetteFor(theme);

        // painterly grain
        Random gr = new Random(7);
        int dots = W * H / 90;
        Paint lb = solid(Color.argb(9, 255, 240, 220)), db = solid(Color.argb(11, 20, 0, 30));
        lb.setAntiAlias(false); db.setAntiAlias(false);
        float dsz = Math.max(1f, 2 * u);
        for (int i = 0; i < dots; i++) {
            float x = gr.nextInt(W), y = gr.nextInt(H);
            g.drawRect(x, y, x + dsz, y + dsz, i % 2 == 0 ? lb : db);
        }

        // vignette
        Paint vb = fillPaint();
        RadialGradient vg = new RadialGradient(0, 0, 100, new int[] { Art.withAlpha(0, vig), Art.withAlpha(0, vig), vig },
                                               new float[] { 0f, 0.52f, 1f }, Shader.TileMode.CLAMP);
        Matrix vm = new Matrix();
        vm.setScale(W * 0.72f / 100f, H * 0.72f / 100f);
        vm.postTranslate(W / 2f, H / 2f);
        vg.setLocalMatrix(vm);
        vb.setShader(vg);
        g.drawRect(0, 0, W, H, vb);

        paintBoard(g, r, boardFor(theme));
        return bmp;
    }

    // ---- theme 0: medieval dusk
    private void paintDusk(Canvas g, Random r) {
        float u = H / 1080f;
        float horizon = H * 0.6f;
        float side = boardX - frame;
        float sunX = W - Math.max(side * 0.45f, W * 0.08f), sunY = horizon - H * 0.15f;

        int skyTop = Art.C(0x141B45), skyMid = Art.C(0x4B3A7C), skyRose = Art.C(0xC46B8E), skyGold = Art.C(0xF7A76C), skyLow = Art.C(0xFFD592);
        int haze = Art.C(0xF0B39A);
        int win = Art.C(0xFFD27A), flag = Art.C(0xC8343F);
        Paint p = fillPaint();

        // sky
        p.setShader(new LinearGradient(0, -1, 0, horizon + 1, new int[] { skyTop, skyMid, skyRose, skyGold, skyLow },
                                       new float[] { 0f, 0.35f, 0.66f, 0.86f, 1f }, Shader.TileMode.CLAMP));
        g.drawRect(0, 0, W, horizon + 1, p);
        p.setShader(null);
        g.drawRect(0, horizon, W, H, solid(skyLow));

        // stars, fading toward the glow
        for (int i = 0; i < 220; i++) {
            float x = r.nextFloat() * W, y = (float) Math.pow(r.nextDouble(), 1.6) * horizon * 0.55f;
            int a = clamp((int) (230 * (1 - y / (horizon * 0.55f))));
            float sz = (0.8f + r.nextFloat() * 1.8f) * u;
            p.setColor(Color.argb(a, 255, 247, 225));
            g.drawCircle(x, y, sz / 2, p);
            if (r.nextInt(14) == 0) glow(g, x, y, sz * 4, Color.argb(a / 3, 255, 240, 200));
        }

        // crescent moon, opposite the sun
        float mx = Math.max(side * 0.3f, W * 0.07f), my = H * 0.13f, mr = H * 0.045f;
        glow(g, mx, my, mr * 3.5f, Color.argb(55, 220, 225, 255));
        Path moon = Art.circle(mx, my, mr);
        moon.op(Art.ellipse(mx - mr * 0.55f, my - mr * 1.15f, 2 * mr, 2 * mr), Path.Op.DIFFERENCE);
        g.drawPath(moon, solid(Art.C(0xFFF6D8)));

        // setting sun, its halo and rays
        glow(g, sunX, sunY, H * 0.55f, Color.argb(120, 255, 190, 110));
        glow(g, sunX, sunY, H * 0.2f, Color.argb(170, 255, 220, 150));
        Paint ray = solid(Color.argb(16, 255, 236, 190));
        for (int i = 0; i < 9; i++) {
            double ang = Math.PI + 0.25 + i * (Math.PI - 0.5) / 8 + (r.nextDouble() - 0.5) * 0.1;
            float len = H * 0.9f; double spread = 0.035;
            g.drawPath(Art.poly(sunX, sunY,
                sunX + (float) Math.cos(ang - spread) * len, sunY + (float) Math.sin(ang - spread) * len,
                sunX + (float) Math.cos(ang + spread) * len, sunY + (float) Math.sin(ang + spread) * len), ray);
        }
        float sr = H * 0.065f;
        g.drawCircle(sunX, sunY, sr, vgrad(sunY - sr, sunY + sr, Art.C(0xFFF3C4), Art.C(0xFFB867)));

        // long painted clouds lit from below
        for (int i = 0; i < 9; i++) {
            float cy = horizon * (0.25f + r.nextFloat() * 0.6f);
            float t = cy / horizon;
            int top = Art.withAlpha(150, mix(Art.C(0x6E4F8E), Art.C(0xE58C8E), t));
            int under = Art.withAlpha(170, mix(Art.C(0xE89AA4), Art.C(0xFFD7A0), t));
            cloud(g, r, r.nextFloat() * W, cy, W * (0.12f + r.nextFloat() * 0.15f), top, under);
        }

        // birds
        Paint pen = new Paint(Paint.ANTI_ALIAS_FLAG);
        pen.setStyle(Paint.Style.STROKE); pen.setColor(Color.argb(170, 60, 40, 70)); pen.setStrokeWidth(Math.max(1.2f, 2f * u));
        for (int i = 0; i < 6; i++) {
            float bx = sunX - W * 0.04f - r.nextFloat() * W * 0.12f, by = horizon * (0.35f + r.nextFloat() * 0.3f);
            float bs = (6 + r.nextFloat() * 6) * u;
            g.drawArc(new RectF(bx - bs, by, bx, by + bs * 0.8f), 200, 120, false, pen);
            g.drawArc(new RectF(bx, by, bx + bs, by + bs * 0.8f), 220, 120, false, pen);
        }

        // distant mountain ranges with mist between them
        float[] far = makeRidge(r, 7, 0.55f);
        fillRidge(g, far, horizon + H * 0.02f, H * 0.2f, Art.C(0xC890A8), Art.C(0xD9A3A6));
        mist(g, horizon, H * 0.05f, Art.withAlpha(150, haze));

        float[] mid = makeRidge(r, 7, 0.5f);
        float midBase = horizon + H * 0.08f, midAmp = H * 0.17f;
        fillRidge(g, mid, midBase, midAmp, Art.C(0x9A6E9C), Art.C(0xB88497));
        // a far-away castle on the mid range, on the sun side
        float fcx = W - Math.max(side * 0.55f, W * 0.12f);
        float fgy = ridgeY(mid, midBase, midAmp, fcx) + H * 0.01f;
        castle(g, fcx, fgy, H * 0.07f, Art.C(0x8E6491), win, flag, false);
        mist(g, midBase - H * 0.01f, H * 0.05f, Art.withAlpha(120, haze));

        float[] near = makeRidge(r, 6, 0.5f);
        fillRidge(g, near, horizon + H * 0.16f, H * 0.1f, Art.C(0x6C4E80), Art.C(0x5A4272));

        // lake with the sun reflected in it
        float lakeY = horizon + H * 0.2f;
        g.drawRect(0, lakeY, W, H, vgrad(lakeY, H + 1, Art.C(0xC0809A), Art.C(0x3B2E5C)));
        for (int i = 0; i < 26; i++) {
            float ry = lakeY + (float) Math.pow(i / 26.0, 1.3) * (H - lakeY) * 0.8f + 3 * u;
            float rw = H * (0.05f + 0.1f * r.nextFloat()) * (1 + i * 0.05f);
            float rx = sunX + (float) (r.nextDouble() - 0.5) * H * 0.05f;
            p.setColor(Color.argb(clamp(170 - i * 5), 255, 222, 160));
            g.drawRect(rx - rw / 2, ry, rx + rw / 2, ry + Math.max(1.5f, 2.5f * u), p);
        }

        // near hills: castle hill (moon side) and village hill (sun side)
        int hillTop = Art.C(0x3A2D58), hillBot = Art.C(0x251C3E);
        float hillW = Math.max(side * 1.9f, W * 0.3f);
        float castleX = Math.max(side * 0.5f, W * 0.1f), hillTopY = H * 0.68f;
        Paint hill = vgrad(hillTopY, H, hillTop, hillBot);
        g.drawOval(castleX - hillW * 0.75f, hillTopY, castleX + hillW * 0.75f, hillTopY + H * 0.7f, hill);
        g.drawOval(W - hillW * 0.95f, H * 0.76f, W - hillW * 0.95f + hillW * 1.4f, H * 0.76f + H * 0.6f, hill);

        int sil = Art.C(0x2B2147);
        float cs = Math.max(H * 0.12f, Math.min(side * 0.55f, H * 0.3f));
        castle(g, castleX, hillTopY + H * 0.012f, cs, sil, win, flag, true);

        // village and wizard tower on the sun side
        float vx = W - Math.max(side * 0.75f, W * 0.15f), vgy = H * 0.785f;
        Path tp = Art.NP();
        PointF tip = tower(tp, vx + cs * 0.5f, vgy + 2, cs * 0.13f, cs * 0.75f);
        g.drawPath(tp, solid(sil));
        pennant(g, tip, cs, sil, flag, false);
        window(g, vx + cs * 0.5f + cs * 0.045f, vgy - cs * 0.6f, cs * 0.04f, cs * 0.07f, win, true);
        cottage(g, vx - cs * 0.15f, vgy + H * 0.008f, cs * 0.28f, sil, win);
        cottage(g, vx + cs * 0.75f, vgy + H * 0.012f, cs * 0.22f, sil, win);

        // pine forests at the edges and a dark foreground
        Paint pb = solid(Art.C(0x1E1735));
        for (int i = 0; i < 16; i++) {
            float px = r.nextFloat() * side * 0.9f;
            if (i % 2 == 1) px = W - px;
            float ph = H * (0.07f + r.nextFloat() * 0.09f);
            pine(g, px, H * (0.86f + r.nextFloat() * 0.08f), ph, pb);
        }
        float[] fg = makeRidge(r, 6, 0.6f);
        fillRidge(g, fg, H * 1.0f, H * 0.08f, Art.C(0x1A142E), Art.C(0x120E22));
    }

    // ---- the board, in the theme's woods and colors
    private void paintBoard(Canvas g, Random r, BoardStyle bs) {
        float u = H / 1080f;
        // frame with grain and inlay
        RectF fr = new RectF(boardX - frame, boardY - frame, boardX + 8 * sq + frame, boardY + 8 * sq + frame);
        g.drawPath(rr(fr.left + frame * 0.3f, fr.top + frame * 0.45f, fr.width(), fr.height(), frame * 0.7f), solid(bs.shadow));
        Path framePath = rr(fr.left, fr.top, fr.width(), fr.height(), frame * 0.7f);
        Paint fb = fillPaint();
        // GDI+ 60 degree gradient across the frame rect
        double ang = Math.toRadians(60);
        float hx = (float) Math.cos(ang) * fr.width() / 2, hy = (float) Math.sin(ang) * fr.height() / 2;
        fb.setShader(new LinearGradient(fr.centerX() - hx, fr.centerY() - hy, fr.centerX() + hx, fr.centerY() + hy,
                                        bs.frameTop, bs.frameBottom, Shader.TileMode.CLAMP));
        g.drawPath(framePath, fb);
        // wood grain
        Paint grain = new Paint(Paint.ANTI_ALIAS_FLAG);
        grain.setStyle(Paint.Style.STROKE); grain.setColor(bs.grain); grain.setStrokeWidth(Math.max(1f, u));
        g.save();
        g.clipPath(framePath);
        for (float y = fr.top + 3; y < fr.bottom; y += 5 * u + r.nextFloat() * 6 * u) {
            Path gp = new Path();
            gp.moveTo(fr.left, y);
            gp.cubicTo(fr.left + fr.width() * 0.3f, y + (float) (r.nextDouble() - 0.5) * 8 * u,
                       fr.left + fr.width() * 0.7f, y + (float) (r.nextDouble() - 0.5) * 8 * u, fr.right, y);
            g.drawPath(gp, grain);
        }
        g.restore();
        int gold = bs.inlay;
        float inset = frame * 0.18f;
        Paint gpen = new Paint(Paint.ANTI_ALIAS_FLAG);
        gpen.setStyle(Paint.Style.STROKE); gpen.setColor(gold); gpen.setStrokeWidth(Math.max(1.5f, frame * 0.07f));
        g.drawPath(rr(fr.left + inset, fr.top + inset, fr.width() - 2 * inset, fr.height() - 2 * inset, frame * 0.55f), gpen);
        // gold diamonds in the corners
        Paint gb = solid(gold);
        float d = frame * 0.28f;
        float[][] corners = { { fr.left + frame / 2, fr.top + frame / 2 }, { fr.right - frame / 2, fr.top + frame / 2 },
                              { fr.left + frame / 2, fr.bottom - frame / 2 }, { fr.right - frame / 2, fr.bottom - frame / 2 } };
        for (float[] c : corners) g.drawPath(Art.poly(c[0], c[1] - d, c[0] + d, c[1], c[0], c[1] + d, c[0] - d, c[1]), gb);

        // squares
        Path boardPath = rr(boardX, boardY, 8 * sq, 8 * sq, frame * 0.25f);
        g.save();
        g.clipPath(boardPath);
        Paint lightSq = solid(bs.light), darkSq = solid(bs.dark);
        lightSq.setAntiAlias(false); darkSq.setAntiAlias(false);
        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
                g.drawRect(boardX + col * sq, boardY + row * sq, boardX + col * sq + sq + 1, boardY + row * sq + sq + 1, (row + col) % 2 == 0 ? lightSq : darkSq);
        // soft inner shading so the squares look inlaid
        g.drawRect(boardX, boardY, boardX + 8 * sq, boardY + 8 * sq, vgrad(boardY, boardY + 8 * sq, Color.argb(0, 0, 0, 0), bs.shade));
        g.restore();
        gpen.setStrokeWidth(Math.max(1.5f, frame * 0.06f));
        g.drawPath(boardPath, gpen);

        // a-h, 1-8 in gold
        Paint tb = new Paint(Paint.ANTI_ALIAS_FLAG);
        tb.setTypeface(Art.SERIF); tb.setTextSize(Math.max(6, frame * 0.5f)); tb.setColor(bs.label); tb.setTextAlign(Paint.Align.CENTER);
        float mid0 = -(tb.ascent() + tb.descent()) / 2;
        for (int i = 0; i < 8; i++) {
            String file = String.valueOf((char) ('a' + i)), rank = String.valueOf(8 - i);
            g.drawText(file, boardX + (i + 0.5f) * sq, boardY + 8 * sq + frame / 2f + mid0, tb);
            g.drawText(file, boardX + (i + 0.5f) * sq, boardY - frame / 2f + mid0, tb);
            g.drawText(rank, boardX - frame / 2f, boardY + (i + 0.5f) * sq + mid0, tb);
            g.drawText(rank, boardX + 8 * sq + frame / 2f, boardY + (i + 0.5f) * sq + mid0, tb);
        }
    }

    // ---------------------------------------------------------- themes
    static final class BoardStyle {
        final int frameTop, frameBottom, grain, inlay, light, dark, shade, label, shadow;
        BoardStyle(int frameTop, int frameBottom, int grain, int inlay, int light, int dark, int shade, int label, int shadow) {
            this.frameTop = frameTop; this.frameBottom = frameBottom; this.grain = grain; this.inlay = inlay;
            this.light = light; this.dark = dark; this.shade = shade; this.label = label; this.shadow = shadow;
        }
    }

    static BoardStyle boardFor(int theme) {
        switch (theme) {
            case 1: // garden: honey oak, cream and leaf green
                return new BoardStyle(Art.C(0xD9A766), Art.C(0x9C6A35), Color.argb(34, 90, 50, 10), Art.C(0x3F8F3A), Art.C(0xFBF5E1),
                                      Art.C(0x93C46E), Color.argb(30, 20, 60, 0), Art.C(0xFFF6DA), Color.argb(90, 20, 60, 10));
            case 2: // frost: silver ice, snow and frozen blue
                return new BoardStyle(Art.C(0xD3E0F2), Art.C(0x7F95C2), Color.argb(45, 255, 255, 255), Color.WHITE, Art.C(0xF7FAFF),
                                      Art.C(0x9FB8DF), Color.argb(35, 20, 40, 90), Art.C(0x2A3A6E), Color.argb(120, 5, 10, 40));
            case 3: // driftwood: sand and sea glass
                return new BoardStyle(Art.C(0xA88664), Art.C(0x6B4F36), Color.argb(40, 40, 25, 10), Art.C(0xF3E6C8), Art.C(0xF8EBCF),
                                      Art.C(0x6DB5C2), Color.argb(30, 0, 40, 60), Art.C(0xFFF0D2), Color.argb(110, 10, 30, 50));
            default: // carved walnut with gold inlay
                return new BoardStyle(Art.C(0x7A4B2A), Art.C(0x4A2914), Color.argb(28, 0, 0, 0), Art.C(0xE2B455), Art.C(0xF3E3C0),
                                      Art.C(0xB98A5C), Color.argb(35, 60, 20, 0), Art.C(0xF0CF83), Color.argb(120, 5, 0, 15));
        }
    }

    static int vignetteFor(int theme) {
        switch (theme) {
            case 1: return Color.argb(70, 20, 60, 30);
            case 2: return Color.argb(150, 5, 10, 40);
            case 3: return Color.argb(80, 20, 40, 70);
            default: return Color.argb(150, 10, 5, 25);
        }
    }

    // ---- shared painting helpers
    private static void fillPoly(Canvas g, int c, float... xy) { g.drawPath(Art.poly(xy), solid(c)); }
    private static void fillCircle(Canvas g, int c, float x, float y, float r) { if (r > 0) g.drawCircle(x, y, r, solid(c)); }
    private static void fillRect(Canvas g, int c, float x, float y, float w, float h) { g.drawRect(x, y, x + w, y + h, solid(c)); }
    private static void fillOval(Canvas g, int c, float x, float y, float w, float h) { g.drawOval(x, y, x + w, y + h, solid(c)); }
    private static Paint stroke(int c, float width) {
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setStyle(Paint.Style.STROKE); p.setColor(c); p.setStrokeWidth(width);
        return p;
    }

    private void sky(Canvas g, float bottom, int[] cols, float[] pos) {
        Paint p = fillPaint();
        p.setShader(new LinearGradient(0, -1, 0, bottom + 1, cols, pos, Shader.TileMode.CLAMP));
        g.drawRect(0, 0, W, bottom + 1, p);
        g.drawRect(0, bottom, W, H, solid(cols[cols.length - 1]));
    }

    // vertical gradient with color stops at absolute y positions
    private static Paint stops(int[] cols, float[] ys) {
        float y0 = ys[0], y1 = ys[ys.length - 1];
        float[] pos = new float[ys.length];
        for (int i = 0; i < ys.length; i++) pos[i] = Math.max(0, Math.min(1, (ys[i] - y0) / Math.max(0.001f, y1 - y0)));
        pos[0] = 0; pos[pos.length - 1] = 1;
        Paint p = fillPaint();
        p.setShader(new LinearGradient(0, y0, 0, y1, cols, pos, Shader.TileMode.CLAMP));
        return p;
    }

    // ridge whose colors change with height (snowy peaks, sunlit tops...)
    private void fillRidgeStops(Canvas g, float[] h, float baseY, float amp, int[] cols, float[] fractions) {
        Path p = new Path();
        p.moveTo(0, baseY - h[0] * amp);
        for (int i = 1; i < h.length; i++) p.lineTo(W * i / (float) (h.length - 1), baseY - h[i] * amp);
        p.lineTo(W, H + 2); p.lineTo(0, H + 2); p.close();
        float[] ys = new float[fractions.length];
        for (int i = 0; i < ys.length; i++) ys[i] = baseY - amp + fractions[i] * (H + 2 - baseY + amp);
        g.drawPath(p, stops(cols, ys));
    }

    // an island / headland: a ridge that only rises between x0 and x1 and sits on baseY
    private void island(Canvas g, Random r, float x0, float x1, float baseY, float amp, int top, int bottom) {
        float[] h = makeRidge(r, 5, 0.5f);
        Path p = new Path();
        for (int i = 0; i < h.length; i++) {
            float t = i / (float) (h.length - 1);
            float env = (float) Math.pow(Math.sin(Math.PI * t), 0.7);
            float x = x0 + (x1 - x0) * t, y = baseY - (0.35f + 0.65f * h[i]) * amp * env;
            if (i == 0) p.moveTo(x, y); else p.lineTo(x, y);
        }
        p.lineTo(x1, baseY + 2); p.lineTo(x0, baseY + 2); p.close();
        g.drawPath(p, vgrad(baseY - amp - 1, baseY + 3, top, bottom));
    }

    private void puffCloud(Canvas g, Random r, float x, float y, float s, int top, int shade) {
        int n = 5 + r.nextInt(3);
        float[] cs = new float[n * 3];
        for (int i = 0; i < n; i++) {
            float t = i / (float) (n - 1);
            float rad = s * (0.22f + 0.26f * (float) Math.sin(Math.PI * t) + 0.08f * r.nextFloat());
            cs[3 * i] = x + (t - 0.5f) * s * 1.5f; cs[3 * i + 1] = y - rad * 0.5f; cs[3 * i + 2] = rad;
        }
        Paint sb = solid(shade), tb = solid(top);
        g.drawOval(x - s * 0.95f, y - s * 0.14f, x + s * 0.95f, y + s * 0.16f, sb);
        for (int i = 0; i < n; i++) g.drawCircle(cs[3 * i], cs[3 * i + 1] + s * 0.05f, cs[3 * i + 2], sb);
        for (int i = 0; i < n; i++) g.drawCircle(cs[3 * i] - s * 0.02f, cs[3 * i + 1] - s * 0.03f, cs[3 * i + 2] * 0.94f, tb);
    }

    private static void birds(Canvas g, Random r, float x0, float x1, float y0, float y1, int n, int c, float u) {
        Paint pen = stroke(c, Math.max(1.2f, 2f * u));
        for (int i = 0; i < n; i++) {
            float bx = x0 + r.nextFloat() * (x1 - x0), by = y0 + r.nextFloat() * (y1 - y0);
            float bs = (6 + r.nextFloat() * 6) * u;
            g.drawArc(new RectF(bx - bs, by, bx, by + bs * 0.8f), 200, 120, false, pen);
            g.drawArc(new RectF(bx, by, bx + bs, by + bs * 0.8f), 220, 120, false, pen);
        }
    }

    // a sunlit castle: stone walls, shaded sides, colored cone roofs (snow roofs in winter)
    private void castleLit(Canvas g, float cx, float gy, float s, int stone, int shade, int roof, int roofShade,
                           int win, int flag, int cap, boolean glowWin, boolean detail) {
        float m = 0.06f * s;
        Path walls = Art.NP();
        battlement(walls, cx - 0.62f * s, gy - 0.3f * s, 1.24f * s, 0.3f * s + 2, m);
        battlement(walls, cx - 0.26f * s, gy - 0.62f * s, 0.52f * s, 0.62f * s, m);
        g.drawPath(walls, solid(stone));
        fillRect(g, shade, cx + 0.13f * s, gy - 0.62f * s, 0.13f * s, 0.62f * s - 0.3f * s);
        fillRect(g, Art.withAlpha(120, shade), cx - 0.62f * s, gy - 0.3f * s, 1.24f * s, 0.03f * s);
        if (Color.alpha(cap) > 0) {
            battlementCaps(g, cap, cx - 0.62f * s, gy - 0.3f * s, 1.24f * s, m);
            battlementCaps(g, cap, cx - 0.26f * s, gy - 0.62f * s, 0.52f * s, m);
        }

        if (detail) {
            // stone courses
            Paint pen = stroke(Art.withAlpha(70, shade), Math.max(1f, s * 0.006f));
            for (float y = gy - 0.05f * s; y > gy - 0.28f * s; y -= 0.055f * s)
                for (float x = cx - 0.6f * s + ((int) ((gy - y) / (0.055f * s)) % 2) * 0.05f * s; x < cx + 0.58f * s; x += 0.1f * s)
                    g.drawLine(x, y, x + 0.06f * s, y, pen);
        }

        float[] tx = { -0.7f, 0.5f, -0.4f, 0.25f, -0.09f }, tw = { 0.2f, 0.2f, 0.15f, 0.15f, 0.18f }, th = { 0.56f, 0.56f, 0.78f, 0.72f, 1.02f };
        ArrayList<PointF> tips = new ArrayList<>();
        for (int i = 0; i < 5; i++) {
            float x = cx + tx[i] * s, w = tw[i] * s, h = th[i] * s;
            fillRect(g, stone, x, gy - h, w, h);
            fillRect(g, shade, x + w * 0.62f, gy - h, w * 0.38f, h);
            fillRect(g, Art.dark(stone, 0.88f), x - w * 0.08f, gy - h - w * 0.12f, w * 1.16f, w * 0.14f);
            fillRect(g, Art.dark(shade, 0.88f), x + w * 0.62f, gy - h - w * 0.12f, w * 0.54f, w * 0.14f);
            if (Color.alpha(cap) > 0) fillRect(g, cap, x - w * 0.1f, gy - h - w * 0.14f, w * 1.2f, w * 0.06f);
            PointF tip = new PointF(x + w / 2, gy - h - w * 1.5f);
            fillPoly(g, roof, x - w * 0.16f, gy - h - w * 0.1f, x + w * 1.16f, gy - h - w * 0.1f, tip.x, tip.y);
            fillPoly(g, roofShade, tip.x, tip.y, x + w * 0.5f, gy - h - w * 0.1f, x + w * 1.16f, gy - h - w * 0.1f);
            tips.add(tip);
            if (detail && h > 0.6f * s) window(g, x + w * 0.5f - 0.02f * s, gy - h * 0.72f, 0.04f * s, 0.08f * s, win, glowWin);
        }

        if (detail) {
            float gw = 0.16f * s, gh = 0.2f * s;
            if (glowWin) glow(g, cx, gy - gh * 0.4f, gh * 1.8f, Art.withAlpha(110, win));
            window(g, cx - gw / 2 - 0.015f * s, gy - gh - 0.015f * s, gw + 0.03f * s, gh + 0.015f * s, Art.dark(stone, 0.75f), false);
            window(g, cx - gw / 2, gy - gh, gw, gh, glowWin ? mix(win, stone, 0.25f) : Art.C(0x6B4428), false);
            Paint pen = stroke(Art.withAlpha(150, glowWin ? shade : Art.C(0x3A2414)), Math.max(1f, s * 0.008f));
            for (int i = 1; i < 4; i++) g.drawLine(cx - gw / 2 + gw * i / 4f, gy - gh * 0.85f, cx - gw / 2 + gw * i / 4f, gy, pen);
            float ww = 0.035f * s, wh = 0.07f * s;
            window(g, cx - 0.17f * s, gy - 0.5f * s, ww, wh, win, glowWin);
            window(g, cx + 0.13f * s, gy - 0.5f * s, ww, wh, win, glowWin);
            window(g, cx - ww / 2, gy - 0.86f * s, ww, wh, win, glowWin);
            window(g, cx - 0.6f * s - ww / 2, gy - 0.44f * s, ww, wh, win, glowWin);
            window(g, cx + 0.6f * s - ww / 2, gy - 0.44f * s, ww, wh, win, glowWin);
        }
        int pole = Art.dark(shade, 0.6f);
        for (int i = 0; i < tips.size(); i++) pennant(g, tips.get(i), s, pole, flag, i % 2 == 1);
    }

    private static void battlementCaps(Canvas g, int c, float x, float top, float w, float m) {
        int k = Math.max(2, (int) (w / (m * 2)));
        float sp = w / (2 * k - 1);
        Paint b = solid(c);
        for (int i = 0; i < 2 * k - 1; i += 2)
            g.drawRect(x + i * sp - sp * 0.08f, top - m * 0.95f, x + i * sp - sp * 0.08f + sp * 1.16f, top - m * 0.95f + m * 0.32f, b);
    }

    private void cottageLit(Canvas g, float x, float gy, float s, int wall, int wallShade, int roof, int roofShade, int win, boolean glowWin, int cap) {
        fillRect(g, Art.dark(roof, 0.75f), x + s * 0.68f, gy - s * 0.95f, s * 0.12f, s * 0.3f);
        if (Color.alpha(cap) > 0) fillRect(g, cap, x + s * 0.66f, gy - s * 0.97f, s * 0.16f, s * 0.05f);
        fillRect(g, wall, x, gy - s * 0.5f, s, s * 0.5f + 2);
        fillRect(g, wallShade, x + s * 0.82f, gy - s * 0.5f, s * 0.18f, s * 0.5f + 2);
        fillPoly(g, roof, x - s * 0.12f, gy - s * 0.48f, x + s * 1.12f, gy - s * 0.48f, x + s * 0.5f, gy - s * 0.95f);
        fillPoly(g, roofShade, x + s * 0.5f, gy - s * 0.95f, x + s * 0.62f, gy - s * 0.48f, x + s * 1.12f, gy - s * 0.48f);
        window(g, x + s * 0.42f, gy - s * 0.28f, s * 0.16f, s * 0.28f, Art.C(0x6B4428), false);
        Paint pen = stroke(Art.dark(wall, 0.6f), Math.max(1f, s * 0.02f));
        for (float wx : new float[] { 0.13f, 0.7f }) {
            if (glowWin) glow(g, x + s * (wx + 0.08f), gy - s * 0.28f, s * 0.2f, Art.withAlpha(110, win));
            fillRect(g, win, x + s * wx, gy - s * 0.36f, s * 0.16f, s * 0.15f);
            g.drawLine(x + s * (wx + 0.08f), gy - s * 0.36f, x + s * (wx + 0.08f), gy - s * 0.21f, pen);
            g.drawLine(x + s * wx, gy - s * 0.285f, x + s * (wx + 0.16f), gy - s * 0.285f, pen);
        }
    }

    private static void roundTree(Canvas g, float x, float gy, float h, int leaf, int leafDark, int trunk) {
        fillRect(g, trunk, x - h * 0.05f, gy - h * 0.45f, h * 0.1f, h * 0.45f + 1);
        fillCircle(g, leafDark, x, gy - h * 0.66f, h * 0.3f);
        fillCircle(g, leafDark, x - h * 0.22f, gy - h * 0.5f, h * 0.22f);
        fillCircle(g, leafDark, x + h * 0.22f, gy - h * 0.5f, h * 0.22f);
        fillCircle(g, leaf, x - h * 0.05f, gy - h * 0.7f, h * 0.24f);
        fillCircle(g, leaf, x - h * 0.24f, gy - h * 0.54f, h * 0.15f);
        fillCircle(g, leaf, x + h * 0.14f, gy - h * 0.56f, h * 0.14f);
        fillCircle(g, Color.argb(70, 255, 255, 220), x - h * 0.12f, gy - h * 0.8f, h * 0.09f);
    }

    private static void windmill(Canvas g, float x, float gy, float s, float angle) {
        fillPoly(g, Art.C(0xF2E6CC), x - s * 0.18f, gy + 2, x + s * 0.18f, gy + 2, x + s * 0.12f, gy - s * 0.75f, x - s * 0.12f, gy - s * 0.75f);
        fillPoly(g, Art.C(0xD9C8A6), x + s * 0.04f, gy + 2, x + s * 0.18f, gy + 2, x + s * 0.12f, gy - s * 0.75f, x + s * 0.03f, gy - s * 0.75f);
        fillPoly(g, Art.C(0xC8443A), x - s * 0.17f, gy - s * 0.74f, x + s * 0.17f, gy - s * 0.74f, x, gy - s * 0.97f);
        fillPoly(g, Art.C(0x9E3029), x, gy - s * 0.97f, x + s * 0.05f, gy - s * 0.74f, x + s * 0.17f, gy - s * 0.74f);
        window(g, x - s * 0.05f, gy - s * 0.2f, s * 0.1f, s * 0.2f, Art.C(0x6B4428), false);
        window(g, x - s * 0.03f, gy - s * 0.52f, s * 0.06f, s * 0.1f, Art.C(0x4A3B5C), false);
        float hx = x, hy = gy - s * 0.78f;
        Paint sail = solid(Color.argb(235, 255, 248, 234));
        Paint pen = stroke(Art.C(0x7A5230), Math.max(1f, s * 0.018f));
        for (int k = 0; k < 4; k++) {
            g.save();
            g.translate(hx, hy);
            g.rotate(angle + 90 * k);
            g.drawRect(s * 0.08f, 0, s * 0.63f, s * 0.13f, sail);
            g.drawLine(0, 0, s * 0.65f, 0, pen);
            for (int i = 1; i < 5; i++) g.drawLine(s * (0.08f + 0.11f * i), 0, s * (0.08f + 0.11f * i), s * 0.13f, pen);
            g.drawLine(s * 0.08f, s * 0.13f, s * 0.63f, s * 0.13f, pen);
            g.restore();
        }
        fillCircle(g, Art.C(0x7A5230), hx, hy, s * 0.035f);
    }

    private static void balloon(Canvas g, float x, float y, float s, int a, int b) {
        Path env = Art.NP();
        env.addOval(new RectF(x - s * 0.5f, y - s * 0.6f, x + s * 0.5f, y + s * 0.45f), Path.Direction.CW);
        env.addPath(Art.poly(x - s * 0.44f, y + s * 0.1f, x + s * 0.44f, y + s * 0.1f, x + s * 0.14f, y + s * 0.58f, x - s * 0.14f, y + s * 0.58f));
        g.drawPath(env, solid(a));
        g.save();
        g.clipPath(env);
        fillOval(g, b, x - s * 0.32f, y - s * 0.7f, s * 0.64f, s * 1.4f);
        fillOval(g, a, x - s * 0.13f, y - s * 0.7f, s * 0.26f, s * 1.4f);
        fillOval(g, Color.argb(60, 255, 255, 255), x - s * 0.42f, y - s * 0.52f, s * 0.36f, s * 0.5f);
        fillOval(g, Color.argb(40, 0, 0, 40), x + s * 0.1f, y - s * 0.6f, s * 0.6f, s * 1.3f);
        g.restore();
        Paint pen = stroke(Art.C(0x6B4428), Math.max(1f, s * 0.02f));
        g.drawLine(x - s * 0.13f, y + s * 0.58f, x - s * 0.09f, y + s * 0.72f, pen);
        g.drawLine(x + s * 0.13f, y + s * 0.58f, x + s * 0.09f, y + s * 0.72f, pen);
        fillRect(g, Art.C(0x9C6A35), x - s * 0.11f, y + s * 0.72f, s * 0.22f, s * 0.15f);
        fillRect(g, Art.C(0x7A5230), x - s * 0.11f, y + s * 0.72f, s * 0.22f, s * 0.035f);
    }

    // ---- theme 1: sunny meadow
    private void paintMeadow(Canvas g, Random r) {
        float u = H / 1080f, horizon = H * 0.58f, side = boardX - frame;
        float sunX = W - Math.max(side * 0.4f, W * 0.08f), sunY = H * 0.14f;
        float castleX = Math.max(side * 0.5f, W * 0.1f);

        sky(g, horizon, new int[] { Art.C(0x3A93DB), Art.C(0x67B8EE), Art.C(0xA6DAF6), Art.C(0xE6F6FC) }, new float[] { 0f, 0.4f, 0.78f, 1f });

        // sun and its soft rays
        glow(g, sunX, sunY, H * 0.55f, Color.argb(100, 255, 248, 200));
        Paint ray = solid(Color.argb(26, 255, 252, 225));
        for (int i = 0; i < 14; i++) {
            double ang = i * Math.PI * 2 / 14 + 0.1;
            float len = H * 0.65f; double spread = 0.07;
            g.drawPath(Art.poly(sunX, sunY,
                sunX + (float) Math.cos(ang - spread) * len, sunY + (float) Math.sin(ang - spread) * len,
                sunX + (float) Math.cos(ang + spread) * len, sunY + (float) Math.sin(ang + spread) * len), ray);
        }
        glow(g, sunX, sunY, H * 0.16f, Color.argb(210, 255, 250, 215));
        float sr = H * 0.062f;
        g.drawCircle(sunX, sunY, sr, vgrad(sunY - sr, sunY + sr, Art.C(0xFFFCE0), Art.C(0xFFD54F)));

        // rainbow over the castle
        float rbx = castleX + side * 0.1f, rby = horizon + H * 0.06f, rbr = H * 0.4f, bw = H * 0.017f;
        int[] bands = { 0xFF5A5A, 0xFF9F40, 0xFFE05A, 0x6BD46B, 0x5AA8FF, 0x9A7BFF };
        for (int i = 0; i < bands.length; i++) {
            float rad = rbr - i * bw;
            g.drawArc(new RectF(rbx - rad, rby - rad, rbx + rad, rby + rad), 180, 180, false, stroke(Art.withAlpha(95, bands[i]), bw + 1));
        }

        for (int i = 0; i < 8; i++)
            puffCloud(g, r, r.nextFloat() * W, H * (0.1f + r.nextFloat() * 0.32f), H * (0.06f + r.nextFloat() * 0.06f),
                      Color.argb(240, 255, 255, 255), Color.argb(230, 206, 224, 242));

        balloon(g, Math.max(side * 0.2f, W * 0.05f), H * 0.2f, H * 0.06f, Art.C(0xEF5350), Art.C(0xFFD23F));
        balloon(g, W - Math.max(side * 0.82f, W * 0.15f), H * 0.34f, H * 0.04f, Art.C(0x4D9DE0), Color.WHITE);
        birds(g, r, W - side * 0.9f, W - side * 0.2f, H * 0.22f, H * 0.38f, 5, Color.argb(150, 40, 60, 90), u);

        // far blue mountains with snowy tops
        float[] far = makeRidge(r, 7, 0.55f);
        fillRidgeStops(g, far, horizon + H * 0.02f, H * 0.22f,
            new int[] { Color.WHITE, Art.C(0xDCE9F7), Art.C(0x9DBFE3), Art.C(0xB9D4EE), Art.C(0xC9DDF0) }, new float[] { 0f, 0.05f, 0.12f, 0.3f, 1f });
        mist(g, horizon + H * 0.01f, H * 0.04f, Color.argb(150, 235, 246, 252));

        // green hills with little trees and a faraway castle
        float[] mid = makeRidge(r, 6, 0.45f);
        float midBase = horizon + H * 0.08f, midAmp = H * 0.1f;
        fillRidge(g, mid, midBase, midAmp, Art.C(0xA6D57E), Art.C(0x86C462));
        float fcx = W - Math.max(side * 0.55f, W * 0.12f);
        castleLit(g, fcx, ridgeY(mid, midBase, midAmp, fcx) + H * 0.008f, H * 0.06f, Art.C(0xD9D3E3), Art.C(0xB8B0C8),
                  Art.C(0x6F8FC9), Art.C(0x5B78AE), Art.C(0x5A5070), Art.C(0xE24C4C), 0, false, false);
        for (int i = 0; i < 46; i++) {
            float x = r.nextFloat() * W, y = ridgeY(mid, midBase, midAmp, x) + H * (0.008f + r.nextFloat() * 0.05f);
            float t = H * (0.006f + r.nextFloat() * 0.008f);
            fillCircle(g, Art.C(0x5E9E48), x, y, t);
            fillCircle(g, Art.C(0x79B85C), x - t * 0.3f, y - t * 0.3f, t * 0.55f);
        }

        float[] near = makeRidge(r, 6, 0.5f);
        fillRidge(g, near, horizon + H * 0.18f, H * 0.07f, Art.C(0x86C960), Art.C(0x63AD47));

        // castle hill and village hill
        float hillW = Math.max(side * 1.9f, W * 0.3f), hillTopY = H * 0.68f;
        Paint hill = vgrad(hillTopY, H, Art.C(0x74C450), Art.C(0x3F8F34));
        g.drawOval(castleX - hillW * 0.75f, hillTopY, castleX + hillW * 0.75f, hillTopY + H * 0.7f, hill);
        g.drawOval(W - hillW * 0.95f, H * 0.76f, W - hillW * 0.95f + hillW * 1.4f, H * 0.76f + H * 0.6f, hill);
        float cs = Math.max(H * 0.12f, Math.min(side * 0.55f, H * 0.3f));
        castleLit(g, castleX, hillTopY + H * 0.012f, cs, Art.C(0xEDE4CF), Art.C(0xCDBE9F), Art.C(0x3F6FB5), Art.C(0x2E5490),
                  Art.C(0x3B3350), Art.C(0xE24C4C), 0, false, true);

        // village with a windmill
        float vx = W - Math.max(side * 0.75f, W * 0.15f), vgy = H * 0.785f;
        windmill(g, vx + cs * 0.55f, vgy + 2, cs * 0.75f, 20);
        cottageLit(g, vx - cs * 0.2f, vgy + H * 0.008f, cs * 0.3f, Art.C(0xF6EBD3), Art.C(0xDCCBA8), Art.C(0xD25A43), Art.C(0xA8432F), Art.C(0x5A6E9C), false, 0);
        cottageLit(g, vx + cs * 0.95f, vgy + H * 0.014f, cs * 0.24f, Art.C(0xF6EBD3), Art.C(0xDCCBA8), Art.C(0xE08A3C), Art.C(0xB86A2A), Art.C(0x5A6E9C), false, 0);

        // trees at the edges
        for (int i = 0; i < 12; i++) {
            float px = r.nextFloat() * side * 0.9f;
            if (i % 2 == 1) px = W - px;
            float ph = H * (0.09f + r.nextFloat() * 0.08f);
            roundTree(g, px, H * (0.87f + r.nextFloat() * 0.07f), ph, Art.C(0x6DBE4E), Art.C(0x3F8F34), Art.C(0x7A5230));
        }
        float[] fg = makeRidge(r, 6, 0.6f);
        fillRidge(g, fg, H * 1.0f, H * 0.07f, Art.C(0x56A63E), Art.C(0x3E8C2F));

        // flowers and grass
        int[] petals = { 0xFF6B6B, 0xFFD23F, 0xFFFFFF, 0xFF9ECF, 0xB28DFF, 0xFF9F40 };
        Paint grass = stroke(Color.argb(120, 40, 110, 30), Math.max(1f, 1.5f * u));
        for (int i = 0; i < 220; i++) {
            float x = r.nextFloat() * W, y = H * (0.88f + r.nextFloat() * 0.12f);
            g.drawLine(x, y, x - 2 * u, y - 7 * u, grass);
            g.drawLine(x, y, x + 3 * u, y - 6 * u, grass);
        }
        for (int i = 0; i < 260; i++) {
            float x = r.nextFloat() * W, y = H * (0.8f + r.nextFloat() * 0.2f);
            float fr = (2.2f + r.nextFloat() * 2.5f) * u * (0.6f + (y - H * 0.8f) / (H * 0.2f) * 0.8f);
            int c = Art.C(petals[r.nextInt(petals.length)]);
            for (int k = 0; k < 5; k++) {
                double a = k * Math.PI * 2 / 5;
                fillCircle(g, c, x + (float) Math.cos(a) * fr, y + (float) Math.sin(a) * fr, fr * 0.75f);
            }
            fillCircle(g, Art.C(0xFFB300), x, y, fr * 0.6f);
        }
    }

    // ---- theme 2: snowy night with northern lights
    private void aurora(Canvas g, Random r, float yBase, float amp, float len, int c, int alpha) {
        float step = Math.max(2f, 3 * H / 1080f);
        double ph = r.nextDouble() * 6;
        Paint p = fillPaint();
        for (float x = 0; x < W; x += step) {
            double t = x / W;
            float y = yBase + (float) (Math.sin(t * 6.0 + ph) * 0.6 + Math.sin(t * 13.0 + ph * 2) * 0.4) * amp;
            float l = len * (0.6f + 0.4f * (float) Math.sin(t * 9 + ph * 3));
            int a = clamp((int) (alpha * (0.45 + 0.55 * Math.abs(Math.sin(t * 17 + ph)))));
            p.setShader(new LinearGradient(0, y, 0, y + l, new int[] { Art.withAlpha(0, c), Art.withAlpha(a / 3, c), Art.withAlpha(a, c), Art.withAlpha(0, c) },
                                           new float[] { 0f, 0.5f, 0.88f, 1f }, Shader.TileMode.CLAMP));
            g.drawRect(x, y, x + step + 0.6f, y + l, p);
        }
    }

    private static void snowPine(Canvas g, float x, float gy, float h, Paint dark, int snow) {
        pine(g, x, gy, h, dark);
        float w = h * 0.36f;
        for (int i = 0; i < 3; i++) {
            float top = gy - h + i * h * 0.24f, bot = gy - h * 0.12f - (2 - i) * h * 0.16f;
            float hw = w * (0.45f + i * 0.28f);
            float sb = top + (bot - top) * 0.42f, shw = hw * 0.42f;
            fillPoly(g, snow, x, top - 1, x + shw, sb, x + shw * 0.4f, sb + h * 0.025f, x - shw * 0.3f, sb - h * 0.005f, x - shw, sb);
            fillPoly(g, snow, x - hw, bot, x + hw, bot, x + hw * 0.75f, bot - h * 0.03f, x - hw * 0.75f, bot - h * 0.03f);
        }
    }

    private static void snowman(Canvas g, float x, float gy, float s) {
        int white = Art.C(0xFAFCFF), shade = Art.C(0xC9D6EE);
        fillCircle(g, Color.argb(60, 40, 60, 110), x + s * 0.06f, gy, s * 0.32f);
        float[] cy = { gy - s * 0.28f, gy - s * 0.72f, gy - s * 1.04f }, cr = { s * 0.3f, s * 0.22f, s * 0.16f };
        for (int i = 0; i < 3; i++) {
            fillCircle(g, shade, x, cy[i], cr[i]);
            fillCircle(g, white, x - cr[i] * 0.12f, cy[i] - cr[i] * 0.1f, cr[i] * 0.88f);
        }
        Paint pen = stroke(Art.C(0x6B4428), Math.max(1.5f, s * 0.025f));
        g.drawLine(x - s * 0.2f, cy[1] - s * 0.02f, x - s * 0.45f, cy[1] - s * 0.2f, pen);
        g.drawLine(x + s * 0.2f, cy[1] - s * 0.02f, x + s * 0.45f, cy[1] - s * 0.22f, pen);
        g.drawLine(x - s * 0.38f, cy[1] - s * 0.14f, x - s * 0.44f, cy[1] - s * 0.08f, pen);
        fillRect(g, Art.C(0xD8343F), x - s * 0.19f, cy[1] - s * 0.22f, s * 0.38f, s * 0.07f);
        fillRect(g, Art.C(0xD8343F), x + s * 0.06f, cy[1] - s * 0.18f, s * 0.07f, s * 0.2f);
        int coal = Art.C(0x2A2A3A);
        fillCircle(g, coal, x - s * 0.06f, cy[2] - s * 0.04f, s * 0.022f);
        fillCircle(g, coal, x + s * 0.05f, cy[2] - s * 0.04f, s * 0.022f);
        for (int i = 0; i < 3; i++) fillCircle(g, coal, x, cy[1] + s * (-0.06f + 0.09f * i) + s * 0.04f, s * 0.022f);
        fillPoly(g, Art.C(0xFF8A3D), x, cy[2] - s * 0.01f, x + s * 0.17f, cy[2] + s * 0.015f, x, cy[2] + s * 0.035f);
        fillRect(g, coal, x - s * 0.17f, cy[2] - s * 0.15f, s * 0.34f, s * 0.04f);
        fillRect(g, coal, x - s * 0.1f, cy[2] - s * 0.33f, s * 0.2f, s * 0.19f);
        fillRect(g, Art.C(0xD8343F), x - s * 0.1f, cy[2] - s * 0.2f, s * 0.2f, s * 0.04f);
    }

    private static void smoke(Canvas g, Random r, float x, float y, float s) {
        for (int i = 0; i < 6; i++) {
            float t = i / 5f;
            fillCircle(g, Color.argb(clamp((int) (70 * (1 - t))), 220, 228, 245), x + s * (0.15f * t + 0.3f * t * t) + (r.nextFloat() - 0.5f) * s * 0.05f,
                       y - s * 0.6f * t, s * (0.06f + 0.08f * t));
        }
    }

    private void paintWinter(Canvas g, Random r) {
        float u = H / 1080f, horizon = H * 0.6f, side = boardX - frame;
        float mx = Math.max(side * 0.32f, W * 0.07f), my = H * 0.14f, mr = H * 0.055f;
        int win = Art.C(0xFFD27A), flag = Art.C(0xD8343F), snow = Art.C(0xF1F5FF);

        sky(g, horizon, new int[] { Art.C(0x050A22), Art.C(0x0E1B4D), Art.C(0x22397A), Art.C(0x4A69A8), Art.C(0x7C97C9) }, new float[] { 0f, 0.35f, 0.65f, 0.88f, 1f });

        for (int i = 0; i < 320; i++) {
            float x = r.nextFloat() * W, y = (float) Math.pow(r.nextDouble(), 1.4) * horizon * 0.7f;
            int a = clamp((int) (240 * (1 - y / (horizon * 0.7f))));
            float sz = (0.8f + r.nextFloat() * 2f) * u;
            fillCircle(g, Color.argb(a, 235, 242, 255), x, y, sz / 2);
            if (r.nextInt(12) == 0) glow(g, x, y, sz * 4, Color.argb(a / 3, 200, 220, 255));
        }

        aurora(g, r, H * 0.1f, H * 0.05f, H * 0.2f, Art.C(0x9C7BFF), 45);
        aurora(g, r, H * 0.16f, H * 0.06f, H * 0.22f, Art.C(0x63F5B8), 75);

        // full moon
        glow(g, mx, my, mr * 4.5f, Color.argb(60, 200, 220, 255));
        glow(g, mx, my, mr * 1.8f, Color.argb(90, 230, 240, 255));
        fillCircle(g, Art.C(0xF4F6FF), mx, my, mr);
        fillCircle(g, Art.C(0xDDE2F2), mx - mr * 0.3f, my - mr * 0.2f, mr * 0.22f);
        fillCircle(g, Art.C(0xDDE2F2), mx + mr * 0.35f, my + mr * 0.25f, mr * 0.16f);
        fillCircle(g, Art.C(0xE4E8F5), mx + mr * 0.1f, my - mr * 0.45f, mr * 0.1f);
        fillCircle(g, Art.C(0xE4E8F5), mx - mr * 0.25f, my + mr * 0.45f, mr * 0.12f);

        // snowy mountains
        float[] far = makeRidge(r, 7, 0.6f);
        fillRidgeStops(g, far, horizon + H * 0.02f, H * 0.24f,
            new int[] { Art.C(0xF2F6FF), Art.C(0xC9D6EE), Art.C(0x8FA3CF), Art.C(0x7F95C6) }, new float[] { 0f, 0.08f, 0.22f, 1f });
        mist(g, horizon, H * 0.05f, Color.argb(120, 159, 179, 221));
        float[] mid = makeRidge(r, 7, 0.5f);
        float midBase = horizon + H * 0.09f, midAmp = H * 0.16f;
        fillRidgeStops(g, mid, midBase, midAmp,
            new int[] { Art.C(0xE3EAF8), Art.C(0xA9BADF), Art.C(0x6E85BA), Art.C(0x627AB0) }, new float[] { 0f, 0.07f, 0.2f, 1f });
        float fcx = W - Math.max(side * 0.55f, W * 0.12f);
        castleLit(g, fcx, ridgeY(mid, midBase, midAmp, fcx) + H * 0.01f, H * 0.07f, Art.C(0x56689C), Art.C(0x48598A),
                  snow, Art.C(0xC4D1EC), win, flag, snow, true, false);
        mist(g, midBase - H * 0.01f, H * 0.05f, Color.argb(110, 159, 179, 221));
        float[] near = makeRidge(r, 6, 0.5f);
        fillRidge(g, near, horizon + H * 0.16f, H * 0.09f, Art.C(0xC5D3EE), Art.C(0x8DA2CE));

        // frozen lake with the moon in it
        float lakeY = horizon + H * 0.2f;
        g.drawRect(0, lakeY, W, H, vgrad(lakeY, H + 1, Art.C(0x9DB3DE), Art.C(0x2F4278)));
        for (int i = 0; i < 22; i++) {
            float ry = lakeY + (float) Math.pow(i / 22.0, 1.3) * (H - lakeY) * 0.8f + 3 * u;
            float rw = H * (0.03f + 0.06f * r.nextFloat()) * (1 + i * 0.05f);
            float rx = mx + (r.nextFloat() - 0.5f) * H * 0.04f;
            fillRect(g, Color.argb(clamp(150 - i * 5), 235, 242, 255), rx - rw / 2, ry, rw, Math.max(1.5f, 2.5f * u));
        }
        Paint crack = stroke(Color.argb(70, 255, 255, 255), Math.max(1f, u));
        for (int i = 0; i < 14; i++) {
            float x = r.nextFloat() * W, y = lakeY + r.nextFloat() * (H - lakeY) * 0.5f;
            g.drawPath(Art.lines(x, y, x + 20 * u, y + 4 * u, x + 34 * u, y + 2 * u, x + 50 * u, y + 7 * u), crack);
        }

        // snowy hills, castle and village
        float hillW = Math.max(side * 1.9f, W * 0.3f);
        float castleX = Math.max(side * 0.5f, W * 0.1f), hillTopY = H * 0.68f;
        Paint hill = vgrad(hillTopY, H, Art.C(0xF3F7FF), Art.C(0xAFC0E2));
        g.drawOval(castleX - hillW * 0.75f, hillTopY, castleX + hillW * 0.75f, hillTopY + H * 0.7f, hill);
        g.drawOval(W - hillW * 0.95f, H * 0.76f, W - hillW * 0.95f + hillW * 1.4f, H * 0.76f + H * 0.6f, hill);
        float cs = Math.max(H * 0.12f, Math.min(side * 0.55f, H * 0.3f));
        castleLit(g, castleX, hillTopY + H * 0.012f, cs, Art.C(0x4C5B8E), Art.C(0x3A4775), snow, Art.C(0xC4D1EC), win, flag, snow, true, true);

        float vx = W - Math.max(side * 0.75f, W * 0.15f), vgy = H * 0.785f;
        smoke(g, r, vx - cs * 0.2f + cs * 0.3f * 0.74f, vgy - cs * 0.3f * 0.95f, cs * 0.5f);
        smoke(g, r, vx + cs * 0.55f + cs * 0.26f * 0.74f, vgy - cs * 0.26f * 0.95f, cs * 0.45f);
        cottageLit(g, vx - cs * 0.2f, vgy + H * 0.008f, cs * 0.3f, Art.C(0x6B5A78), Art.C(0x554763), snow, Art.C(0xC4D1EC), win, true, snow);
        cottageLit(g, vx + cs * 0.55f, vgy + H * 0.012f, cs * 0.26f, Art.C(0x7A5A5A), Art.C(0x624848), snow, Art.C(0xC4D1EC), win, true, snow);
        snowman(g, vx + cs * 1.15f, H * 0.83f, cs * 0.32f);

        Paint pb = solid(Art.C(0x1F3A4E));
        for (int i = 0; i < 16; i++) {
            float px = r.nextFloat() * side * 0.9f;
            if (i % 2 == 1) px = W - px;
            float ph = H * (0.07f + r.nextFloat() * 0.09f);
            snowPine(g, px, H * (0.86f + r.nextFloat() * 0.08f), ph, pb, snow);
        }
        float[] fg = makeRidge(r, 6, 0.6f);
        fillRidge(g, fg, H * 1.0f, H * 0.08f, Color.WHITE, Art.C(0xD0DCF0));
        for (int i = 0; i < 160; i++) {
            float x = r.nextFloat() * W, y = H * (0.9f + r.nextFloat() * 0.1f);
            fillCircle(g, Color.argb(150 + r.nextInt(100), 255, 255, 255), x, y, (0.8f + r.nextFloat() * 1.5f) * u);
        }
    }

    // ---- theme 3: seaside castle in the morning
    private static void ship(Canvas g, float x, float y, float s, int sail, int stripe, int flag) {
        fillRect(g, Color.argb(50, 0, 30, 60), x - s * 0.45f, y + s * 0.03f, s * 0.9f, s * 0.05f);
        g.drawLine(x, y - s * 0.16f, x, y - s * 1.02f, stroke(Art.C(0x5A3A22), Math.max(1.5f, s * 0.03f)));
        fillRect(g, sail, x - s * 0.3f, y - s * 0.86f, s * 0.6f, s * 0.48f);
        fillOval(g, sail, x - s * 0.3f, y - s * 0.48f, s * 0.6f, s * 0.18f);
        fillRect(g, stripe, x - s * 0.3f, y - s * 0.72f, s * 0.6f, s * 0.1f);
        fillRect(g, sail, x - s * 0.18f, y - s * 0.98f, s * 0.36f, s * 0.1f);
        fillRect(g, Color.argb(50, 120, 80, 40), x + s * 0.12f, y - s * 0.86f, s * 0.18f, s * 0.5f);
        pennant(g, new PointF(x, y - s * 1.0f), s * 1.4f, Art.C(0x5A3A22), flag, true);
        fillPoly(g, Art.C(0x7A4A2A), x - s * 0.58f, y - s * 0.2f, x + s * 0.58f, y - s * 0.2f, x + s * 0.42f, y + s * 0.03f, x - s * 0.42f, y + s * 0.03f);
        fillRect(g, Art.C(0xE2B455), x - s * 0.52f, y - s * 0.15f, s * 1.04f, s * 0.025f);
        for (int i = 0; i < 4; i++) fillCircle(g, Art.C(0x3A2414), x - s * 0.27f + i * s * 0.18f, y - s * 0.07f, s * 0.025f);
    }

    private static void rock(Canvas g, float x, float gy, float s) {
        fillPoly(g, Art.C(0x5D5A6B), x - s, gy, x - s * 0.7f, gy - s * 0.55f, x - s * 0.1f, gy - s * 0.8f, x + s * 0.6f, gy - s * 0.5f, x + s, gy);
        fillPoly(g, Art.C(0x7E7B8F), x - s * 0.7f, gy - s * 0.55f, x - s * 0.1f, gy - s * 0.8f, x + s * 0.1f, gy - s * 0.3f, x - s * 0.5f, gy - s * 0.2f);
    }

    private static void lighthouse(Canvas g, float x, float gy, float s) {
        Paint beam = solid(Color.argb(28, 255, 240, 180));
        g.drawPath(Art.poly(x, gy - s * 1.05f, x - s * 3.5f, gy - s * 1.45f, x - s * 3.5f, gy - s * 0.85f), beam);
        g.drawPath(Art.poly(x, gy - s * 1.05f, x + s * 3.5f, gy - s * 1.35f, x + s * 3.5f, gy - s * 0.95f), beam);
        Path body = Art.poly(x - s * 0.16f, gy, x + s * 0.16f, gy, x + s * 0.1f, gy - s * 0.92f, x - s * 0.1f, gy - s * 0.92f);
        g.drawPath(body, solid(Art.C(0xF7F4EE)));
        g.save();
        g.clipPath(body);
        for (int i = 0; i < 3; i++) fillRect(g, Art.C(0xD8443A), x - s * 0.2f, gy - s * (0.2f + 0.28f * i), s * 0.4f, s * 0.13f);
        fillRect(g, Color.argb(45, 40, 40, 80), x + s * 0.03f, gy - s, s * 0.2f, s);
        g.restore();
        fillRect(g, Art.C(0x3A3A4A), x - s * 0.15f, gy - s * 0.95f, s * 0.3f, s * 0.04f);
        glow(g, x, gy - s * 1.05f, s * 0.45f, Color.argb(160, 255, 236, 150));
        fillRect(g, Art.C(0xFFE58A), x - s * 0.08f, gy - s * 1.13f, s * 0.16f, s * 0.18f);
        Paint pen = stroke(Art.C(0x3A3A4A), Math.max(1f, s * 0.015f));
        g.drawLine(x - s * 0.08f, gy - s * 1.13f, x - s * 0.08f, gy - s * 0.95f, pen);
        g.drawLine(x + s * 0.08f, gy - s * 1.13f, x + s * 0.08f, gy - s * 0.95f, pen);
        g.drawLine(x, gy - s * 1.13f, x, gy - s * 0.95f, pen);
        fillPoly(g, Art.C(0xD8443A), x - s * 0.12f, gy - s * 1.12f, x + s * 0.12f, gy - s * 1.12f, x, gy - s * 1.27f);
        fillCircle(g, Art.C(0x3A3A4A), x, gy - s * 1.28f, s * 0.02f);
    }

    private void paintSeaside(Canvas g, Random r) {
        float u = H / 1080f, horizon = H * 0.56f, side = boardX - frame;
        float sunX = W - Math.max(side * 0.45f, W * 0.09f), sunY = horizon - H * 0.07f;

        sky(g, horizon, new int[] { Art.C(0x3F78C2), Art.C(0x7FAEDF), Art.C(0xC4D7EC), Art.C(0xF7D5B5), Art.C(0xFFE3AE) }, new float[] { 0f, 0.35f, 0.65f, 0.88f, 1f });

        glow(g, sunX, sunY, H * 0.5f, Color.argb(120, 255, 214, 150));
        glow(g, sunX, sunY, H * 0.16f, Color.argb(190, 255, 236, 190));
        float sr = H * 0.06f;
        g.drawCircle(sunX, sunY, sr, vgrad(sunY - sr, sunY + sr, Art.C(0xFFF6D6), Art.C(0xFFC978)));

        for (int i = 0; i < 7; i++) {
            float cy = horizon * (0.2f + r.nextFloat() * 0.6f), t = cy / horizon;
            cloud(g, r, r.nextFloat() * W, cy, W * (0.1f + r.nextFloat() * 0.14f),
                  Art.withAlpha(150, mix(Art.C(0xF4F7FF), Art.C(0xF7D3D6), t)), Art.withAlpha(170, mix(Art.C(0xFFFFFF), Art.C(0xFFE6C4), t)));
        }
        for (int i = 0; i < 4; i++)
            puffCloud(g, r, r.nextFloat() * W, H * (0.08f + r.nextFloat() * 0.15f), H * (0.04f + r.nextFloat() * 0.04f),
                      Color.argb(235, 255, 255, 255), Color.argb(220, 214, 222, 240));
        birds(g, r, side * 0.1f, side * 0.9f, H * 0.12f, H * 0.3f, 4, Color.argb(160, 60, 70, 90), u);
        birds(g, r, W - side * 0.9f, W - side * 0.1f, H * 0.15f, H * 0.3f, 4, Color.argb(160, 60, 70, 90), u);

        // faraway islands, one with a tiny castle
        island(g, r, W * 0.18f, W * 0.42f, horizon + 1, H * 0.06f, Art.C(0x9AA6CB), Art.C(0xB6BFD9));
        island(g, r, W * 0.7f, W * 0.86f, horizon + 1, H * 0.035f, Art.C(0xA5B0D2), Art.C(0xBEC6DD));
        castle(g, W * 0.3f, horizon - H * 0.02f, H * 0.04f, Art.C(0x8E98BF), Art.C(0xFFE3AE), Art.C(0x8E98BF), false);

        // the sea, the sun's path and little waves
        g.drawRect(0, horizon, W, H, stops(new int[] { Art.C(0x7FC0DB), Art.C(0x3E8EBD), Art.C(0x1F5F8F), Art.C(0x174E7A) },
                                           new float[] { horizon, horizon + H * 0.08f, H * 0.8f, H + 1 }));
        for (int i = 0; i < 26; i++) {
            float ry = horizon + (float) Math.pow(i / 26.0, 1.3) * (H - horizon) * 0.8f + 3 * u;
            float rw = H * (0.05f + 0.1f * r.nextFloat()) * (1 + i * 0.05f);
            float rx = sunX + (r.nextFloat() - 0.5f) * H * 0.05f;
            fillRect(g, Color.argb(clamp(190 - i * 6), 255, 236, 180), rx - rw / 2, ry, rw, Math.max(1.5f, 2.5f * u));
        }
        for (int i = 0; i < 320; i++) {
            float t = (float) Math.pow(r.nextDouble(), 1.3), y = horizon + H * 0.01f + t * (H - horizon);
            float len = (5 + 34 * t) * u, x = r.nextFloat() * W;
            g.drawArc(new RectF(x, y, x + len, y + len * 0.35f), 200, 140, false,
                      stroke(Color.argb(50 + r.nextInt(70), 255, 255, 255), Math.max(1f, (1 + 2 * t) * u)));
        }

        ship(g, Math.max(side * 0.85f, W * 0.15f), horizon + H * 0.04f, H * 0.045f, Art.C(0xFBF3E2), Art.C(0x3E7BD6), Art.C(0xFFD23F));
        ship(g, W - Math.max(side * 0.8f, W * 0.14f), horizon + H * 0.12f, H * 0.085f, Art.C(0xFBF3E2), Art.C(0xD8443A), Art.C(0x3E7BD6));

        // the castle cliff
        float cliffW = Math.max(side * 1.05f, W * 0.2f), cliffTop = H * 0.66f;
        float castleX = Math.max(side * 0.45f, W * 0.09f);
        ArrayList<PointF> cliff = new ArrayList<>();
        cliff.add(new PointF(-2, H + 2)); cliff.add(new PointF(-2, cliffTop + H * 0.01f));
        for (int i = 0; i <= 10; i++)
            cliff.add(new PointF(cliffW * 0.85f * i / 10f, cliffTop + (r.nextFloat() - 0.5f) * H * 0.012f + (i > 7 ? (i - 7) * H * 0.008f : 0)));
        cliff.add(new PointF(cliffW * 0.95f, cliffTop + H * 0.07f));
        cliff.add(new PointF(cliffW * 0.9f, cliffTop + H * 0.12f));
        cliff.add(new PointF(cliffW * 1.05f, cliffTop + H * 0.2f));
        cliff.add(new PointF(cliffW * 1.15f, H + 2));
        Path cp = new Path();
        cp.moveTo(cliff.get(0).x, cliff.get(0).y);
        for (int i = 1; i < cliff.size(); i++) cp.lineTo(cliff.get(i).x, cliff.get(i).y);
        cp.close();
        g.drawPath(cp, vgrad(cliffTop - 1, H + 2, Art.C(0xC99E78), Art.C(0x7A5640)));
        g.save();
        g.clipPath(cp);
        for (int i = 0; i < 26; i++) {
            float bx = r.nextFloat() * cliffW * 1.1f, by = cliffTop + H * 0.03f + r.nextFloat() * (H - cliffTop);
            float bw = H * (0.04f + r.nextFloat() * 0.07f), bh = bw * (0.35f + r.nextFloat() * 0.25f);
            fillOval(g, Color.argb(40, 255, 235, 200), bx - bw / 2, by - bh / 2, bw, bh);
            fillOval(g, Color.argb(45, 70, 35, 15), bx - bw / 2 + bw * 0.08f, by, bw * 0.9f, bh * 0.5f);
        }
        Paint crack = stroke(Color.argb(60, 70, 35, 15), Math.max(1f, 1.6f * u));
        for (int i = 0; i < 10; i++) {
            float x = r.nextFloat() * cliffW, y = cliffTop + H * 0.03f + r.nextFloat() * (H - cliffTop) * 0.7f;
            g.drawPath(Art.lines(x, y, x + 6 * u, y + 18 * u, x + 2 * u, y + 34 * u, x + 9 * u, y + 52 * u), crack);
        }
        g.restore();
        Path grass = new Path();
        for (int i = 2; i < 13; i++) { PointF q = cliff.get(i); if (i == 2) grass.moveTo(q.x, q.y); else grass.lineTo(q.x, q.y); }
        for (int i = 12; i >= 2; i--) { PointF q = cliff.get(i); grass.lineTo(q.x, q.y + H * (0.018f + 0.01f * (i % 3))); }
        grass.close();
        g.drawPath(grass, solid(Art.C(0x7DBB5A)));
        float cs = Math.max(H * 0.12f, Math.min(side * 0.5f, H * 0.28f));
        castleLit(g, castleX, cliffTop + H * 0.006f, cs, Art.C(0xF1DEC4), Art.C(0xD3B596), Art.C(0xD0573F), Art.C(0xA8432F),
                  Art.C(0x4A3B5C), Art.C(0x2F6FC0), 0, false, true);

        // lighthouse on the rocks
        float lhx = W - Math.max(side * 0.3f, W * 0.06f), rocksY = H * 0.75f;
        float ls = Math.max(H * 0.14f, Math.min(side * 0.4f, H * 0.24f));
        fillOval(g, Color.argb(120, 255, 255, 255), lhx - ls * 0.6f, rocksY + H * 0.008f, ls * 1.2f, H * 0.018f);
        rock(g, lhx - ls * 0.2f, rocksY + H * 0.004f, ls * 0.28f);
        rock(g, lhx + ls * 0.22f, rocksY + H * 0.006f, ls * 0.22f);
        lighthouse(g, lhx, rocksY - H * 0.004f, ls);
        rock(g, lhx - ls * 0.38f, rocksY + H * 0.012f, ls * 0.16f);
        rock(g, lhx + ls * 0.02f, rocksY + H * 0.014f, ls * 0.2f);
        rock(g, lhx + ls * 0.42f, rocksY + H * 0.012f, ls * 0.13f);

        // sandy beach with shells, starfish and a sandcastle
        float[] beach = makeRidge(r, 6, 0.4f);
        float beachBase = H * 0.99f, beachAmp = H * 0.08f;
        Path foam = new Path();
        for (int i = 0; i < beach.length; i++) {
            float x = W * i / (float) (beach.length - 1), y = beachBase - beach[i] * beachAmp - 2 * u;
            if (i == 0) foam.moveTo(x, y); else foam.lineTo(x, y);
        }
        g.drawPath(foam, stroke(Color.argb(120, 120, 200, 210), 10 * u));
        fillRidge(g, beach, beachBase, beachAmp, Art.C(0xF5DDA8), Art.C(0xE2BC80));
        g.drawPath(foam, stroke(Color.argb(200, 255, 255, 255), Math.max(1.5f, 3 * u)));
        for (int i = 0; i < 6; i++) {
            float x = r.nextFloat() * side * 0.9f;
            if (i % 2 == 1) x = W - x;
            float y = H * (0.95f + r.nextFloat() * 0.04f);
            if (i < 3) g.drawPath(Art.star(x, y, H * 0.016f), solid(Art.C(0xFF8A5B)));
            else {
                float sh = H * 0.012f;
                g.drawArc(new RectF(x - sh, y - sh, x + sh, y + sh), 180, 180, true, solid(Art.C(0xF7B7C8)));
                Paint pen = stroke(Art.C(0xD98AA0), Math.max(1f, u));
                for (int k = 1; k < 4; k++)
                    g.drawLine(x, y, x + (float) Math.cos(Math.PI + k * Math.PI / 4) * sh, y + (float) Math.sin(Math.PI + k * Math.PI / 4) * sh, pen);
            }
        }
        float sx = W - Math.max(side * 0.7f, W * 0.13f);
        castleLit(g, sx, H * 0.965f, H * 0.075f, Art.C(0xEBCB8F), Art.C(0xCFAA6E), Art.C(0xE0BE80), Art.C(0xC59F62),
                  Art.C(0xB48F58), Art.C(0xEF5350), 0, false, true);
    }

    // ---------------------------------------------------------- theme animations
    private static float hash(int i, double k) { return (float) (Math.abs(Math.sin(i * k) * 43758.5453) % 1.0); }

    private void drawAmbient(Canvas g) {
        switch (theme) {
            case 1: drawButterflies(g); break;
            case 2: drawSnow(g); break;
            case 3: drawSeaLife(g); break;
            default: drawFireflies(g); break;
        }
    }

    private void drawButterflies(Canvas g) {
        float side = boardX - frame;
        if (side < H * 0.05f) return;
        int[] wings = { 0xFF8A3D, 0xFFD23F, 0xE76FB0, 0x4D9DE0, 0xFFFFFF, 0x9B6BD6 };
        for (int i = 0; i < 8; i++) {
            float bx = hash(i + 1, 12.9898) * side * 0.8f + side * 0.1f;
            if (i % 2 == 1) bx = W - bx;
            float by = H * (0.45f + 0.4f * hash(i + 1, 78.233));
            float t = time * (0.35f + 0.15f * hash(i + 1, 4.1)) + i * 1.3f;
            float x = bx + (float) (Math.sin(t) * 0.7 + Math.sin(t * 2.3) * 0.3) * side * 0.12f;
            float y = by + (float) (Math.sin(t * 1.7) * 0.6 + Math.cos(t * 0.9) * 0.4) * H * 0.04f;
            float s = H * 0.014f, ww = s * (0.2f + 0.8f * Math.abs((float) Math.sin(time * 12 + i * 2)));
            int c = Art.C(wings[i % wings.length]);
            paint.setStyle(Paint.Style.FILL);
            paint.setColor(c);
            g.drawOval(x - ww, y - s * 0.9f, x, y + s * 0.1f, paint);
            g.drawOval(x, y - s * 0.9f, x + ww, y + s * 0.1f, paint);
            paint.setColor(Art.dark(c, 0.78f));
            g.drawOval(x - ww * 0.8f, y - s * 0.1f, x, y + s * 0.6f, paint);
            g.drawOval(x, y - s * 0.1f, x + ww * 0.8f, y + s * 0.6f, paint);
            paint.setStyle(Paint.Style.STROKE);
            paint.setStrokeWidth(Math.max(1.5f, s * 0.18f));
            paint.setColor(Art.C(0x3A2A20));
            g.drawLine(x, y - s * 0.7f, x, y + s * 0.55f, paint);
        }
        paint.setStyle(Paint.Style.FILL);
    }

    private void drawSnow(Canvas g) {
        paint.setStyle(Paint.Style.FILL);
        paint.setColor(Color.argb(200, 255, 255, 255));
        for (int i = 0; i < 90; i++) {
            float speed = H * (0.04f + 0.05f * hash(i + 1, 3.7));
            float y = (hash(i + 1, 78.233) * (H + 40) + time * speed) % (H + 40) - 20;
            float x = hash(i + 1, 12.9898) * W + (float) Math.sin(time * 0.8 + i) * H * 0.02f;
            g.drawCircle(x, y, H * (0.0018f + 0.0028f * hash(i + 1, 5.3)), paint);
        }
    }

    private void drawSeaLife(Canvas g) {
        float side = boardX - frame;
        float horizon = H * 0.56f, sunX = W - Math.max(side * 0.45f, W * 0.09f);
        // glitter on the water under the sun
        paint.setStyle(Paint.Style.FILL);
        for (int i = 0; i < 16; i++) {
            float x = sunX + (hash(i + 1, 12.9898) - 0.5f) * H * 0.22f;
            float y = horizon + H * 0.02f + hash(i + 1, 78.233) * H * 0.2f;
            float tw = (float) Math.sin(time * 3 + i * 1.7);
            if (tw <= 0 || (x > boardX - frame - H * 0.02f && x < boardX + 8 * sq + frame + H * 0.02f)) continue;
            float rad = H * 0.006f * tw;
            glow(g, paint, x, y, rad * 3, Color.argb(clamp((int) (160 * tw)), 255, 245, 210));
            paint.setColor(Color.argb(clamp((int) (230 * tw)), 255, 255, 255));
            g.drawPath(Art.poly(x - rad * 2, y, x, y - rad * 0.4f, x + rad * 2, y, x, y + rad * 0.4f), paint);
            g.drawPath(Art.poly(x, y - rad * 1.5f, x + rad * 0.4f, y, x, y + rad * 1.5f, x - rad * 0.4f, y), paint);
        }
        // seagulls circling beside the board
        if (side < H * 0.05f) return;
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(Math.max(1.5f, H * 0.003f));
        paint.setStrokeCap(Paint.Cap.ROUND);
        for (int i = 0; i < 5; i++) {
            float bx = hash(i + 1, 9.17) * side * 0.6f + side * 0.2f;
            if (i % 2 == 1) bx = W - bx;
            float by = H * (0.12f + 0.25f * hash(i + 1, 33.1));
            float t = time * (0.25f + 0.1f * hash(i + 1, 2.3)) + i * 2f;
            float x = bx + (float) Math.cos(t) * side * 0.18f, y = by + (float) Math.sin(t) * H * 0.035f;
            float s = H * 0.016f, flap = (float) Math.sin(time * 5 + i) * s * 0.35f;
            Path gull = new Path();
            gull.moveTo(x - s, y - flap);
            gull.quadTo(x - s * 0.45f, y - s * 0.45f, x, y);
            gull.quadTo(x + s * 0.45f, y - s * 0.45f, x + s, y - flap);
            paint.setColor(Color.argb(230, 250, 250, 255));
            g.drawPath(gull, paint);
            paint.setColor(Color.argb(200, 70, 80, 100));
            g.drawLine(x - s, y - flap, x - s * 0.85f, y - flap + s * 0.02f, paint);
        }
        paint.setStrokeCap(Paint.Cap.BUTT);
        paint.setStyle(Paint.Style.FILL);
    }

    // ---------------------------------------------------------- theme switching
    static final String[] THEME_NAMES = { "Castle at Dusk", "Sunny Meadow", "Snowy Night", "Seaside Castle" };
    static final float THEME_SECONDS = 240f; // a new scene every 4 minutes
    private static final float REVEAL_TIME = 1.6f;
    private int theme;
    private float themeSince;
    private Bitmap oldBackground; private float revealBorn = -100;

    static int wrapTheme(int t) { int n = THEME_NAMES.length; return ((t % n) + n) % n; }

    int theme() { return theme; }

    void setTheme(int t, boolean announce) {
        t = wrapTheme(t);
        themeSince = time;
        if (t == theme && background != null) return;
        theme = t;
        if (background == null) return;
        if (oldBackground != null) oldBackground.recycle();
        oldBackground = background;
        background = buildBackground();
        revealBorn = time;
        if (announce) { setCaption(THEME_NAMES[t]); tune(new int[] { 7, 5, 3, 5, 7, 10 }, 0.1f, 0.45f); }
    }

    void nextTheme() { setTheme(theme + 1, true); }

    // the new scene opens up from the middle in a growing circle, with a ring of sparkles at its edge
    @SuppressWarnings("deprecation")
    private void drawReveal(Canvas g) {
        float k = (time - revealBorn) / REVEAL_TIME;
        if (k >= 1 || k < 0) { oldBackground.recycle(); oldBackground = null; return; }
        float e = k * k * (3 - 2 * k);
        float rad = e * ((float) Math.sqrt((double) W * W + (double) H * H) / 2 + 4);
        Path hole = new Path();
        hole.addCircle(W / 2f, H / 2f, Math.max(0.5f, rad), Path.Direction.CW);
        g.save();
        if (android.os.Build.VERSION.SDK_INT >= 26) g.clipOutPath(hole);
        else g.clipPath(hole, android.graphics.Region.Op.DIFFERENCE);
        g.drawBitmap(oldBackground, 0, 0, null);
        g.restore();
        int a = clamp((int) (230 * Math.min(1, (1 - k) * 3)));
        paint.setStyle(Paint.Style.FILL);
        for (int i = 0; i < 30; i++) {
            double ang = i * Math.PI * 2 / 30 + time * 0.9;
            float x = W / 2f + (float) Math.cos(ang) * rad, y = H / 2f + (float) Math.sin(ang) * rad;
            glow(g, paint, x, y, H * 0.03f, Color.argb(a / 2, 255, 225, 130));
            paint.setColor(Color.argb(a, 255, 250, 220));
            g.drawCircle(x, y, H * 0.005f, paint);
        }
    }

    // ---------------------------------------------------------- draw
    // fireflies drifting beside the board (animated, cheap)
    private void drawFireflies(Canvas g) {
        float side = boardX - frame;
        if (side < H * 0.05f) return;
        for (int i = 0; i < 18; i++) {
            double h1 = Math.abs(Math.sin(i * 12.9898) * 43758.5453) % 1.0, h2 = Math.abs(Math.sin(i * 78.233) * 12543.123) % 1.0;
            float bx = (float) h1 * side * 0.9f + side * 0.05f;
            if (i % 2 == 1) bx = W - bx;
            float by = H * (0.62f + 0.3f * (float) h2);
            float x = bx + (float) Math.sin(time * 0.7f + i) * H * 0.03f;
            float y = by + (float) Math.sin(time * 0.5f + i * 1.7f) * H * 0.02f;
            float pulse = 0.5f + 0.5f * (float) Math.sin(time * 2.2f + i * 2.1f);
            int a = clamp((int) (220 * pulse));
            float rr = H * 0.004f;
            glow(g, paint, x, y, rr * 4, Color.argb(a / 2, 255, 220, 120));
            paint.setColor(Color.argb(a, 255, 245, 190));
            g.drawCircle(x, y, rr, paint);
        }
    }

    void draw(Canvas g) {
        g.drawBitmap(background, 0, 0, null);
        paint.setStyle(Paint.Style.FILL);
        if (oldBackground != null) { try { drawReveal(g); } catch (RuntimeException ignored) { } }

        try { drawAmbient(g); } catch (RuntimeException ignored) { }
        if (hint != null) { try { drawHint(g); } catch (RuntimeException ignored) { } }
        for (Sprite s : sprites) if (time >= s.born) { try { drawSprite(g, s); } catch (RuntimeException ignored) { } }

        paint.setStyle(Paint.Style.FILL);
        for (Particle p : parts) {
            int a = clamp((int) (255 * (1 - p.age / p.life)));
            float r = p.size * (1 - 0.4f * p.age / p.life);
            paint.setColor(Art.withAlpha(a, p.c));
            g.drawCircle(p.x, p.y, r, paint);
        }

        try { drawCaption(g); } catch (RuntimeException ignored) { }
        try { drawStartHint(g); } catch (RuntimeException ignored) { }

        float r = H * 0.022f;
        for (PointF f : fingers) {
            paint.setStyle(Paint.Style.STROKE);
            paint.setStrokeWidth(r * 0.45f);
            paint.setColor(Content.CONFETTI[trailHue]);
            g.drawCircle(f.x, f.y, r, paint);
            paint.setStyle(Paint.Style.FILL);
            paint.setColor(Color.argb(200, 255, 255, 255));
            g.drawCircle(f.x, f.y, r * 0.75f, paint);
        }
        paint.setStyle(Paint.Style.FILL);
    }

    private void drawHint(Canvas g) {
        float t = time - hint.born;
        float fade = Math.max(0, Math.min(1f, Math.min(t * 4f, (hint.life - t) * 2f)));
        paint.setStyle(Paint.Style.FILL);
        paint.setColor(Color.argb(clamp((int) (160 * fade)), 255, 214, 80));
        g.drawRect(boardX + hint.col * sq, boardY + hint.row * sq, boardX + hint.col * sq + sq, boardY + hint.row * sq + sq, paint);
        float pulse = 0.88f + 0.12f * (float) Math.sin(t * 6);
        for (int i = 0; i < hint.targets.size(); i++) {
            float appear = Math.max(0, Math.min(1, (t - 0.25f - i * 0.06f) * 5f));
            if (appear <= 0) continue;
            int[] p = hint.targets.get(i);
            float cx = boardX + (p[0] + 0.5f) * sq, cy = boardY + (p[1] + 0.5f) * sq;
            float r = sq * 0.17f * pulse * Math.max(0.05f, easeOutBack(appear));
            paint.setStyle(Paint.Style.FILL);
            paint.setColor(Color.argb(clamp((int) (230 * fade)), 255, 190, 60));
            g.drawCircle(cx, cy, r, paint);
            paint.setStyle(Paint.Style.STROKE);
            paint.setStrokeWidth(Math.max(1.5f, sq * 0.03f));
            paint.setColor(Color.argb(clamp((int) (240 * fade)), 255, 255, 255));
            g.drawCircle(cx, cy, r, paint);
        }
        paint.setStyle(Paint.Style.FILL);
    }

    private void drawSprite(Canvas g, Sprite s) {
        float age = time - s.born;
        float scale = age < 0.4f ? easeOutBack(age / 0.4f) : 1f;
        float alpha = Math.max(0, Math.min(1, (s.life - age) / 0.8f));
        int a = clamp((int) (255 * alpha));
        if (a <= 0 || scale <= 0.02f) return;

        float k = s.size * scale;
        RectF b = s.pic.bounds;
        paint.setStyle(Paint.Style.FILL);
        paint.setColor(Color.argb(clamp(a * 45 / 255), 0, 30, 0));
        float sx = s.x + b.left * k * 0.8f, sy = s.y + b.bottom * k - k * 0.03f;
        g.drawOval(sx, sy, sx + b.width() * k * 0.8f, sy + k * 0.08f, paint);

        g.save();
        g.translate(s.x, s.y);
        if (s.rot != 0) g.rotate(s.rot);
        g.scale(k, k);
        s.pic.draw(g, a);
        g.restore();
    }

    private void pill(Canvas g, float x, float y, float w, float h, int color) {
        paint.setStyle(Paint.Style.FILL);
        paint.setColor(color);
        rect.set(x, y, x + w, y + h);
        g.drawRoundRect(rect, h / 2, h / 2, paint);
    }

    private void drawCaption(Canvas g) {
        if (caption == null) return;
        float t = time - captionBorn;
        final float life = 3.5f;
        if (t > life || t < 0) return;
        float alpha = Math.min(1f, Math.min(t * 6f, (life - t) * 2f));
        float pop = Math.max(0.05f, t < 0.3f ? easeOutBack(t / 0.3f) : 1f);
        int a = clamp((int) (255 * alpha));

        float fontPx = H * 0.055f;
        text.setTypeface(Art.UI_FONT);
        text.setTextAlign(Paint.Align.CENTER);
        text.setTextSize(fontPx);
        float tw = text.measureText(caption);
        if (tw > W * 0.82f) { fontPx *= W * 0.82f / tw; text.setTextSize(fontPx); tw = text.measureText(caption); }
        float th = text.descent() - text.ascent();
        float padX = fontPx * 0.8f, padY = fontPx * 0.3f;
        float w = tw + padX * 2, h = th + padY * 2;
        float cx = W / 2f, cy = H - h * 0.5f - H * 0.025f;

        g.save();
        g.translate(cx, cy);
        g.scale(pop, pop);
        pill(g, -w / 2 + 3, -h / 2 + 5, w, h, Color.argb(clamp(a * 50 / 255), 0, 30, 0));
        pill(g, -w / 2, -h / 2, w, h, Color.argb(clamp(a * 245 / 255), 255, 255, 255));
        text.setColor(Color.argb(a, 58, 58, 90));
        g.drawText(caption, 0, -(text.ascent() + text.descent()) / 2, text);
        g.restore();
    }

    private void drawStartHint(Canvas g) {
        final float life = 8f;
        if (time > life || startHint.isEmpty()) return;
        int a = clamp((int) (255 * Math.min(1f, (life - time) * 1.5f)));
        text.setTypeface(Art.UI_FONT);
        text.setTextAlign(Paint.Align.CENTER);
        text.setTextSize(Math.max(12, H * 0.03f));
        float tw = text.measureText(startHint);
        if (tw > W * 0.85f) { text.setTextSize(text.getTextSize() * W * 0.85f / tw); tw = text.measureText(startHint); }
        float h = (text.descent() - text.ascent()) + 14;
        pill(g, (W - tw) / 2 - h * 0.6f, H * 0.012f, tw + h * 1.2f, h, Color.argb(clamp(a * 235 / 255), 255, 255, 255));
        text.setColor(Color.argb(a, 58, 58, 90));
        g.drawText(startHint, W / 2f, H * 0.012f + h / 2 - (text.ascent() + text.descent()) / 2, text);
    }

    static float easeOutBack(float x) {
        x = Math.max(0, Math.min(1, x));
        final float c1 = 1.70158f, c3 = c1 + 1;
        return 1 + c3 * (float) Math.pow(x - 1, 3) + c1 * (float) Math.pow(x - 1, 2);
    }

    static int clamp(int a) { return a < 0 ? 0 : (a > 255 ? 255 : a); }
}

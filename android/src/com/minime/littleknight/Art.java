package com.minime.littleknight;

import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Matrix;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.graphics.Typeface;

import java.util.ArrayList;
import java.util.HashMap;

/** One colored shape of a picture. */
final class Layer {
    Path path; int fill, stroke; float strokeW; boolean lineOnly;
}

/** A picture is a stack of colored shapes in "unit" space (roughly -0.5..0.5). */
final class Pic {
    final ArrayList<Layer> layers = new ArrayList<>();
    final RectF bounds = new RectF();

    Pic add(Path p, int fill) { Layer l = new Layer(); l.path = p; l.fill = fill; layers.add(l); return this; }
    Pic add(Path p, int fill, int stroke, float w) { Layer l = new Layer(); l.path = p; l.fill = fill; l.stroke = stroke; l.strokeW = w; layers.add(l); return this; }
    Pic line(Path p, int stroke, float w) { Layer l = new Layer(); l.path = p; l.stroke = stroke; l.strokeW = w; l.lineOnly = true; layers.add(l); return this; }

    Pic finish() {
        boolean first = true; RectF r = new RectF();
        for (Layer l : layers) {
            l.path.computeBounds(r, true);
            if (first) bounds.set(r); else bounds.union(r);
            first = false;
        }
        return this;
    }

    private static final Paint FILL = new Paint(Paint.ANTI_ALIAS_FLAG);
    private static final Paint STROKE = new Paint(Paint.ANTI_ALIAS_FLAG);
    static {
        FILL.setStyle(Paint.Style.FILL);
        STROKE.setStyle(Paint.Style.STROKE);
        STROKE.setStrokeJoin(Paint.Join.ROUND);
        STROKE.setStrokeCap(Paint.Cap.ROUND);
    }

    /** Draw with the transform already applied by the caller (unit space). UI thread only. */
    void draw(Canvas c, int alpha) {
        for (Layer l : layers) {
            if (l.strokeW > 0 && Color.alpha(l.stroke) > 0) {
                // stroke drawn *behind* the fill = clean outer outline only
                STROKE.setColor(Art.A(l.stroke, alpha));
                STROKE.setStrokeWidth(l.lineOnly ? l.strokeW : l.strokeW * 2f);
                c.drawPath(l.path, STROKE);
            }
            if (!l.lineOnly) {
                FILL.setColor(Art.A(l.fill, alpha));
                c.drawPath(l.path, FILL);
            }
        }
    }
}

final class Art {
    private Art() {}

    static final Typeface BLOCK_FONT = Typeface.create("sans-serif-black", Typeface.NORMAL);
    static final Typeface UI_FONT = Typeface.create("sans-serif", Typeface.BOLD);
    static final Typeface SERIF = Typeface.create(Typeface.SERIF, Typeface.BOLD);

    // ---- palette
    static int C(int rgb) { return 0xFF000000 | rgb; }
    static int argb(int a, int r, int g, int b) { return Color.argb(a, r, g, b); }
    static int withAlpha(int a, int c) { return (Math.max(0, Math.min(255, a)) << 24) | (c & 0xFFFFFF); }
    static int A(int c, int alpha) { return withAlpha(Color.alpha(c) * alpha / 255, c); }
    static int dark(int c, float k) {
        return Color.argb(Color.alpha(c), (int) (Color.red(c) * k), (int) (Color.green(c) * k), (int) (Color.blue(c) * k));
    }

    static final int GOLD = C(0xF5C242), GOLD_DARK = C(0xC9971C), BROWN = C(0x8B5A2B), BROWN_DARK = C(0x5E3B1A);
    static final int RED = C(0xEF5350), RED_DARK = C(0xB8322A), GREEN = C(0x66BB6A), GREEN_DARK = C(0x3D8B3D);
    static final int BLUE = C(0x4D9DE0), BLUE_DARK = C(0x2A6FB0), WHITE = Color.WHITE;
    static final float SW = 0.022f; // outline width

    static final int[] BLOCK_COLORS = {
        C(0xF25F5C), C(0xFF9F1C), C(0xF2B705), C(0x3BB273), C(0x4D9DE0), C(0x7768AE), C(0xE76FB0), C(0x17A2B8)
    };

    // ---- shape helpers (every sub-shape runs clockwise, so merged shapes always union)
    static Path NP() { Path p = new Path(); p.setFillType(Path.FillType.WINDING); return p; }
    static Path circle(float cx, float cy, float r) { Path p = NP(); p.addCircle(cx, cy, r, Path.Direction.CW); return p; }
    static Path ellipse(float x, float y, float w, float h) { Path p = NP(); p.addOval(new RectF(x, y, x + w, y + h), Path.Direction.CW); return p; }

    static Path rrect(float x, float y, float w, float h, float r) {
        Path p = NP();
        float d = Math.min(r * 2, Math.min(w, h));
        if (d <= 0) { p.addRect(x, y, x + w, y + h, Path.Direction.CW); return p; }
        p.moveTo(x, y + d / 2);
        p.arcTo(new RectF(x, y, x + d, y + d), 180, 90, false);
        p.arcTo(new RectF(x + w - d, y, x + w, y + d), 270, 90, false);
        p.arcTo(new RectF(x + w - d, y + h - d, x + w, y + h), 0, 90, false);
        p.arcTo(new RectF(x, y + h - d, x + d, y + h), 90, 90, false);
        p.close();
        return p;
    }

    static float[] clockwise(float[] xy) {
        double area = 0; int n = xy.length / 2;
        for (int i = 0; i < n; i++) { int j = (i + 1) % n; area += xy[2 * i] * xy[2 * j + 1] - xy[2 * j] * xy[2 * i + 1]; }
        if (area >= 0) return xy;
        float[] r = new float[xy.length];
        for (int i = 0; i < n; i++) { r[2 * i] = xy[2 * (n - 1 - i)]; r[2 * i + 1] = xy[2 * (n - 1 - i) + 1]; }
        return r;
    }

    static Path poly(float... xy) {
        xy = clockwise(xy);
        Path p = NP();
        p.moveTo(xy[0], xy[1]);
        for (int i = 2; i < xy.length; i += 2) p.lineTo(xy[i], xy[i + 1]);
        p.close();
        return p;
    }

    /** Closed cardinal spline through the points (same as GDI+ AddClosedCurve). */
    static Path curve(float tension, float... xy) {
        xy = clockwise(xy);
        int n = xy.length / 2;
        float t = tension * 0.3f;
        Path p = NP();
        p.moveTo(xy[0], xy[1]);
        for (int i = 0; i < n; i++) {
            int i0 = (i + n - 1) % n, i2 = (i + 1) % n, i3 = (i + 2) % n;
            float x1 = xy[2 * i], y1 = xy[2 * i + 1], x2 = xy[2 * i2], y2 = xy[2 * i2 + 1];
            p.cubicTo(x1 + t * (x2 - xy[2 * i0]), y1 + t * (y2 - xy[2 * i0 + 1]),
                      x2 - t * (xy[2 * i3] - x1), y2 - t * (xy[2 * i3 + 1] - y1), x2, y2);
        }
        p.close();
        return p;
    }

    static Path lines(float... xy) {
        Path p = NP();
        p.moveTo(xy[0], xy[1]);
        for (int i = 2; i < xy.length; i += 2) p.lineTo(xy[i], xy[i + 1]);
        return p;
    }

    static Path trap(float bw, float by, float tw, float ty) { return poly(-bw, by, bw, by, tw, ty, -tw, ty); }
    static Path merge(Path... ps) { Path p = NP(); for (Path q : ps) p.addPath(q); return p; }
    static Path rot(Path p, float deg, float cx, float cy) { Matrix m = new Matrix(); m.setRotate(deg, cx, cy); p.transform(m); return p; }
    static Path move(Path p, float dx, float dy) { p.offset(dx, dy); return p; }

    static Path star(float cx, float cy, float r) {
        float[] xy = new float[20];
        for (int i = 0; i < 10; i++) {
            double a = -Math.PI / 2 + i * Math.PI / 5; float rr = i % 2 == 0 ? r : r * 0.45f;
            xy[2 * i] = cx + (float) Math.cos(a) * rr; xy[2 * i + 1] = cy + (float) Math.sin(a) * rr;
        }
        return poly(xy);
    }

    static Path text(String s, Typeface tf, float height, float cx, float cy) {
        Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        paint.setTypeface(tf);
        paint.setTextSize(100);
        Path p = NP();
        paint.getTextPath(s, 0, s.length(), 0, 0, p);
        RectF b = new RectF();
        p.computeBounds(b, true);
        if (b.height() > 0) {
            float k = height / b.height();
            Matrix m = new Matrix();
            m.setTranslate(-b.centerX(), -b.centerY());
            m.postScale(k, k);
            m.postTranslate(cx, cy);
            p.transform(m);
        }
        return p;
    }

    // ---- cache
    private static final HashMap<String, Pic> cache = new HashMap<>();

    static Pic get(String key) {
        Pic p = cache.get(key);
        if (p != null) return p;
        p = build(key).finish();
        cache.put(key, p);
        return p;
    }

    private static Pic build(String key) {
        if (key.startsWith("piece:")) return piece(key.charAt(6) - '0', key.charAt(7) == 'w');
        if (key.startsWith("block:")) {
            String[] parts = key.split(":");
            return block(parts[1], BLOCK_COLORS[Integer.parseInt(parts[2]) % BLOCK_COLORS.length]);
        }
        switch (key) {
            case "apple": return apple();
            case "bell": return bell();
            case "castle": return castle();
            case "drum": return drum();
            case "egg": return egg();
            case "flag": return flag();
            case "gem": return gem();
            case "heart": return heart();
            case "icecream": return iceCream();
            case "jar": return jar();
            case "leaf": return leaf();
            case "moon": return moon();
            case "note": return note();
            case "owl": return owl();
            case "shield": return shield();
            case "tree": return tree();
            case "umbrella": return umbrella();
            case "vase": return vase();
            case "wand": return wand();
            case "xmark": return xMark();
            case "yoyo": return yoYo();
            case "zigzag": return zigzag();
            case "crown": return crown();
            case "star": return starPic();
            case "sword": return sword();
        }
        return starPic();
    }

    // ---- chess pieces (0 King, 1 Queen, 2 Rook, 3 Bishop, 4 Knight, 5 Pawn)
    private static Pic piece(int type, boolean white) {
        int body = white ? C(0xFFF6E3) : C(0x433E6B);
        int line = white ? C(0x8C6E4E) : C(0x1E1A38);
        Pic pic = new Pic();
        ArrayList<Path> parts = new ArrayList<>();
        parts.add(rrect(-0.32f, 0.35f, 0.64f, 0.15f, 0.06f));
        parts.add(rrect(-0.25f, 0.27f, 0.5f, 0.1f, 0.04f));
        switch (type) {
            case 0: // King
                parts.add(trap(0.21f, 0.29f, 0.11f, -0.1f));
                parts.add(poly(-0.12f, -0.08f, 0.12f, -0.08f, 0.21f, -0.3f, -0.21f, -0.3f));
                parts.add(rrect(-0.21f, -0.35f, 0.42f, 0.07f, 0.035f));
                break;
            case 1: // Queen
                parts.add(trap(0.21f, 0.29f, 0.1f, -0.1f));
                parts.add(poly(-0.15f, -0.08f, -0.25f, -0.34f, -0.11f, -0.21f, 0f, -0.39f, 0.11f, -0.21f, 0.25f, -0.34f, 0.15f, -0.08f));
                break;
            case 2: // Rook
                parts.add(trap(0.2f, 0.29f, 0.17f, -0.18f));
                parts.add(rrect(-0.26f, -0.3f, 0.52f, 0.14f, 0.03f));
                parts.add(rrect(-0.26f, -0.44f, 0.13f, 0.17f, 0.02f));
                parts.add(rrect(-0.065f, -0.44f, 0.13f, 0.17f, 0.02f));
                parts.add(rrect(0.13f, -0.44f, 0.13f, 0.17f, 0.02f));
                break;
            case 3: // Bishop
                parts.add(trap(0.2f, 0.29f, 0.08f, -0.04f));
                parts.add(rrect(-0.15f, -0.08f, 0.3f, 0.07f, 0.035f));
                parts.add(ellipse(-0.15f, -0.42f, 0.3f, 0.38f));
                parts.add(circle(0, -0.44f, 0.055f));
                break;
            case 4: // Knight (horse head facing left)
                parts.add(curve(0.2f,
                    0.23f, 0.29f, -0.21f, 0.29f, -0.1f, 0.06f, -0.03f, -0.04f, -0.21f, 0.03f, -0.32f, -0.05f,
                    -0.28f, -0.17f, -0.12f, -0.32f, -0.05f, -0.47f, 0.04f, -0.37f, 0.17f, -0.31f, 0.27f, -0.11f, 0.27f, 0.11f));
                break;
            default: // Pawn
                parts.add(trap(0.2f, 0.29f, 0.09f, -0.04f));
                parts.add(rrect(-0.15f, -0.08f, 0.3f, 0.07f, 0.035f));
                parts.add(circle(0, -0.25f, 0.17f));
                break;
        }
        pic.add(merge(parts.toArray(new Path[0])), body, line, SW);

        switch (type) {
            case 0:
                pic.add(rrect(-0.16f, -0.13f, 0.32f, 0.06f, 0.03f), GOLD, GOLD_DARK, 0.01f);
                pic.add(merge(rrect(-0.035f, -0.5f, 0.07f, 0.17f, 0.02f), rrect(-0.1f, -0.46f, 0.2f, 0.065f, 0.02f)), GOLD, GOLD_DARK, 0.012f);
                break;
            case 1:
                pic.add(rrect(-0.16f, -0.13f, 0.32f, 0.06f, 0.03f), GOLD, GOLD_DARK, 0.01f);
                pic.add(merge(circle(-0.25f, -0.37f, 0.05f), circle(0, -0.43f, 0.055f), circle(0.25f, -0.37f, 0.05f)), GOLD, GOLD_DARK, 0.012f);
                break;
            case 2:
                pic.add(rrect(-0.045f, -0.08f, 0.09f, 0.16f, 0.045f), line);
                break;
            case 3:
                pic.line(lines(0.0f, -0.33f, 0.08f, -0.23f), line, 0.035f);
                pic.add(rrect(-0.15f, -0.08f, 0.3f, 0.07f, 0.035f), GOLD, GOLD_DARK, 0.01f);
                break;
            case 4:
                pic.add(circle(-0.11f, -0.22f, 0.035f), line);
                pic.line(lines(0.06f, -0.34f, 0.2f, -0.24f, 0.25f, -0.08f, 0.25f, 0.08f), white ? C(0xD9C29C) : C(0x6A64A0), 0.04f);
                break;
        }
        // soft shine so the pieces look like toys
        pic.add(ellipse(-0.17f, 0.04f, 0.06f, 0.18f), argb(white ? 150 : 60, 255, 255, 255));
        return pic;
    }

    // ---- toy alphabet block
    private static Pic block(String text, int c) {
        Pic pic = new Pic();
        int edge = dark(c, 0.72f);
        pic.add(rrect(-0.45f, -0.4f, 0.9f, 0.88f, 0.14f), edge);
        pic.add(rrect(-0.45f, -0.45f, 0.9f, 0.86f, 0.14f), c);
        pic.line(rrect(-0.37f, -0.37f, 0.74f, 0.7f, 0.1f), argb(110, 255, 255, 255), 0.025f);
        pic.add(text(text, BLOCK_FONT, 0.48f, 0.025f, 0.005f), edge);
        pic.add(text(text, BLOCK_FONT, 0.48f, 0f, -0.02f), WHITE);
        return pic;
    }

    // ---- pictures for letters and treasures
    private static Pic apple() {
        return new Pic()
            .line(lines(0f, -0.18f, 0.04f, -0.4f), BROWN_DARK, 0.05f)
            .add(merge(circle(-0.12f, 0.06f, 0.3f), circle(0.12f, 0.06f, 0.3f)), RED, RED_DARK, SW)
            .add(rot(ellipse(0.05f, -0.44f, 0.26f, 0.12f), -25, 0.05f, -0.38f), GREEN, GREEN_DARK, SW)
            .add(rot(ellipse(-0.3f, -0.12f, 0.1f, 0.2f), 20, -0.25f, -0.02f), argb(140, 255, 255, 255));
    }

    private static Pic bell() {
        return new Pic()
            .line(circle(0f, -0.42f, 0.06f), GOLD_DARK, 0.035f)
            .add(circle(0f, 0.35f, 0.08f), GOLD_DARK)
            .add(merge(ellipse(-0.24f, -0.38f, 0.48f, 0.42f), poly(-0.24f, -0.17f, 0.24f, -0.17f, 0.36f, 0.25f, -0.36f, 0.25f), rrect(-0.42f, 0.2f, 0.84f, 0.11f, 0.05f)), GOLD, GOLD_DARK, SW)
            .add(rot(ellipse(-0.18f, -0.25f, 0.08f, 0.3f), 10, -0.14f, -0.1f), argb(150, 255, 255, 255));
    }

    private static Pic castle() {
        int wall = C(0xC9C3E6), wallDark = C(0x7D74A8), roof = C(0xF2709C), door = C(0x8B5A2B);
        return new Pic()
            .line(lines(-0.31f, -0.4f, -0.31f, -0.52f), BROWN_DARK, 0.02f)
            .add(poly(-0.31f, -0.52f, -0.18f, -0.48f, -0.31f, -0.44f), GOLD)
            .line(lines(0.31f, -0.4f, 0.31f, -0.52f), BROWN_DARK, 0.02f)
            .add(poly(0.31f, -0.52f, 0.44f, -0.48f, 0.31f, -0.44f), GOLD)
            .add(merge(rrect(-0.42f, -0.15f, 0.22f, 0.62f, 0.02f), rrect(0.2f, -0.15f, 0.22f, 0.62f, 0.02f),
                       rrect(-0.25f, 0.0f, 0.5f, 0.47f, 0.02f), rrect(-0.22f, -0.09f, 0.1f, 0.12f, 0.01f),
                       rrect(-0.05f, -0.09f, 0.1f, 0.12f, 0.01f), rrect(0.12f, -0.09f, 0.1f, 0.12f, 0.01f)), wall, wallDark, SW)
            .add(poly(-0.46f, -0.15f, -0.31f, -0.42f, -0.16f, -0.15f), roof, dark(roof, 0.7f), SW)
            .add(poly(0.16f, -0.15f, 0.31f, -0.42f, 0.46f, -0.15f), roof, dark(roof, 0.7f), SW)
            .add(merge(rrect(-0.1f, 0.22f, 0.2f, 0.25f, 0f), ellipse(-0.1f, 0.12f, 0.2f, 0.2f)), door, BROWN_DARK, SW)
            .add(merge(rrect(-0.34f, 0.0f, 0.06f, 0.12f, 0.03f), rrect(0.28f, 0.0f, 0.06f, 0.12f, 0.03f)), wallDark);
    }

    private static Pic drum() {
        return new Pic()
            .line(lines(-0.42f, -0.46f, 0.02f, -0.18f), BROWN_DARK, 0.05f)
            .line(lines(0.42f, -0.46f, -0.02f, -0.18f), BROWN_DARK, 0.05f)
            .add(merge(circle(-0.42f, -0.46f, 0.05f), circle(0.42f, -0.46f, 0.05f)), GOLD, GOLD_DARK, 0.01f)
            .add(rrect(-0.36f, -0.15f, 0.72f, 0.5f, 0.06f), RED, RED_DARK, SW)
            .line(lines(-0.36f, -0.05f, -0.24f, 0.25f, -0.12f, -0.05f, 0f, 0.25f, 0.12f, -0.05f, 0.24f, 0.25f, 0.36f, -0.05f), GOLD, 0.03f)
            .add(rrect(-0.38f, 0.28f, 0.76f, 0.09f, 0.045f), GOLD, GOLD_DARK, 0.012f)
            .add(ellipse(-0.36f, -0.25f, 0.72f, 0.2f), C(0xFFF1D6), GOLD_DARK, SW);
    }

    private static Pic egg() {
        int egg = C(0xA8D8F0), dark = C(0x5FA3CC);
        return new Pic()
            .add(curve(0.5f, 0f, -0.43f, 0.21f, -0.22f, 0.3f, 0.1f, 0.18f, 0.36f, 0f, 0.42f, -0.18f, 0.36f, -0.3f, 0.1f, -0.21f, -0.22f), egg, dark, SW)
            .add(merge(circle(0.08f, -0.18f, 0.05f), circle(-0.1f, 0.05f, 0.07f), circle(0.14f, 0.18f, 0.045f), circle(-0.03f, 0.27f, 0.035f)), argb(200, 255, 255, 255));
    }

    private static Pic flag() {
        return new Pic()
            .add(rrect(-0.36f, -0.44f, 0.05f, 0.94f, 0.02f), BROWN, BROWN_DARK, 0.012f)
            .add(circle(-0.335f, -0.46f, 0.05f), GOLD, GOLD_DARK, 0.01f)
            .add(curve(0.45f, -0.31f, -0.4f, 0.0f, -0.45f, 0.42f, -0.36f, 0.38f, -0.1f, 0.05f, -0.16f, -0.31f, -0.08f), RED, RED_DARK, SW)
            .add(star(0.04f, -0.25f, 0.1f), GOLD);
    }

    private static Pic gem() {
        return new Pic()
            .add(poly(-0.4f, -0.12f, -0.21f, -0.34f, 0.21f, -0.34f, 0.4f, -0.12f, 0f, 0.4f), BLUE, BLUE_DARK, SW)
            .add(poly(-0.21f, -0.34f, 0.21f, -0.34f, 0.11f, -0.12f, -0.11f, -0.12f), C(0x9FD0FA))
            .line(lines(-0.4f, -0.12f, 0.4f, -0.12f), BLUE_DARK, 0.018f)
            .line(lines(-0.11f, -0.12f, 0f, 0.4f, 0.11f, -0.12f), BLUE_DARK, 0.018f)
            .add(star(-0.2f, -0.2f, 0.06f), argb(220, 255, 255, 255));
    }

    private static Pic heart() {
        int h = C(0xF2547D);
        return new Pic()
            .add(merge(circle(-0.16f, -0.1f, 0.21f), circle(0.16f, -0.1f, 0.21f), poly(-0.355f, -0.02f, 0.355f, -0.02f, 0f, 0.4f)), h, dark(h, 0.75f), SW)
            .add(rot(ellipse(-0.27f, -0.2f, 0.09f, 0.16f), 30, -0.22f, -0.12f), argb(150, 255, 255, 255));
    }

    private static Pic iceCream() {
        int cone = C(0xE8B86D), coneDark = C(0xB8863B);
        return new Pic()
            .add(poly(-0.21f, -0.02f, 0.21f, -0.02f, 0f, 0.47f), cone, coneDark, SW)
            .line(lines(-0.13f, 0.06f, 0.07f, 0.3f), coneDark, 0.015f)
            .line(lines(0.13f, 0.06f, -0.07f, 0.3f), coneDark, 0.015f)
            .add(circle(0f, -0.08f, 0.21f), C(0xF7A1C4), C(0xD4709A), SW)
            .add(circle(0f, -0.28f, 0.17f), C(0x9BE3C1), C(0x5DB58D), SW)
            .add(circle(0.02f, -0.46f, 0.05f), RED, RED_DARK, 0.012f);
    }

    private static Pic jar() {
        int honey = C(0xF5A623);
        return new Pic()
            .add(rrect(-0.3f, -0.25f, 0.6f, 0.68f, 0.14f), honey, dark(honey, 0.7f), SW)
            .add(rrect(-0.26f, -0.41f, 0.52f, 0.17f, 0.04f), BROWN, BROWN_DARK, SW)
            .add(rrect(-0.2f, -0.01f, 0.4f, 0.22f, 0.04f), C(0xFFF1D6), dark(honey, 0.7f), 0.012f)
            .add(merge(circle(-0.04f, 0.1f, 0.05f), circle(0.04f, 0.1f, 0.05f), poly(-0.085f, 0.115f, 0.085f, 0.115f, 0f, 0.18f)), C(0xF2547D))
            .add(rrect(-0.24f, -0.18f, 0.06f, 0.2f, 0.03f), argb(130, 255, 255, 255));
    }

    private static Pic leaf() {
        return new Pic()
            .add(rot(curve(0.5f, 0f, -0.45f, 0.25f, -0.15f, 0.2f, 0.2f, 0f, 0.36f, -0.2f, 0.2f, -0.25f, -0.15f), 25, 0, 0), GREEN, GREEN_DARK, SW)
            .line(rot(lines(0f, -0.36f, 0f, 0.47f), 25, 0, 0), GREEN_DARK, 0.025f);
    }

    private static Pic moon() {
        Path crescent = circle(0f, 0f, 0.4f);
        crescent.op(circle(0.2f, -0.12f, 0.33f), Path.Op.DIFFERENCE);
        return new Pic()
            .line(circle(0f, 0f, 0.4f), Color.TRANSPARENT, 0.001f) // bounds only (invisible)
            .add(crescent, C(0xFFD84D))
            .add(star(0.3f, 0.26f, 0.08f), C(0xFFE98A))
            .add(star(0.36f, -0.36f, 0.06f), C(0xFFE98A));
    }

    private static Pic note() {
        int n = C(0x7768AE);
        return new Pic().add(merge(
            rot(ellipse(-0.38f, 0.18f, 0.26f, 0.18f), -20, -0.25f, 0.27f),
            rot(ellipse(0.06f, 0.1f, 0.26f, 0.18f), -20, 0.19f, 0.19f),
            rrect(-0.17f, -0.35f, 0.055f, 0.64f, 0.02f),
            rrect(0.265f, -0.43f, 0.055f, 0.64f, 0.02f),
            poly(-0.17f, -0.35f, 0.32f, -0.43f, 0.32f, -0.3f, -0.17f, -0.22f)), n, dark(n, 0.7f), SW);
    }

    private static Pic owl() {
        int body = C(0xA9744F), belly = C(0xF0D9B5), dark = C(0x6E4A30), beak = C(0xF5A623);
        return new Pic()
            .add(merge(circle(-0.1f, 0.42f, 0.05f), circle(0.1f, 0.42f, 0.05f)), beak)
            .add(merge(ellipse(-0.33f, -0.36f, 0.66f, 0.8f), poly(-0.31f, -0.2f, -0.28f, -0.48f, -0.1f, -0.33f), poly(0.31f, -0.2f, 0.28f, -0.48f, 0.1f, -0.33f)), body, dark, SW)
            .add(ellipse(-0.21f, -0.04f, 0.42f, 0.44f), belly)
            .add(merge(circle(-0.13f, -0.16f, 0.12f), circle(0.13f, -0.16f, 0.12f)), WHITE, dark, 0.012f)
            .add(merge(circle(-0.11f, -0.15f, 0.06f), circle(0.11f, -0.15f, 0.06f)), C(0x2B2B2B))
            .add(merge(circle(-0.09f, -0.18f, 0.018f), circle(0.13f, -0.18f, 0.018f)), WHITE)
            .add(poly(-0.05f, -0.06f, 0.05f, -0.06f, 0f, 0.04f), beak)
            .line(lines(-0.1f, 0.12f, -0.05f, 0.16f, 0f, 0.12f, 0.05f, 0.16f, 0.1f, 0.12f), C(0xD7B58C), 0.02f);
    }

    private static Path shieldPath(boolean leftHalfOnly) {
        Path p = NP();
        p.moveTo(-0.36f, -0.4f);
        if (leftHalfOnly) {
            p.lineTo(0f, -0.4f);
            p.lineTo(0f, 0.47f);
            p.cubicTo(-0.15f, 0.38f, -0.36f, 0.25f, -0.36f, 0f);
        } else {
            p.lineTo(0.36f, -0.4f);
            p.lineTo(0.36f, 0f);
            p.cubicTo(0.36f, 0.25f, 0.15f, 0.38f, 0f, 0.47f);
            p.cubicTo(-0.15f, 0.38f, -0.36f, 0.25f, -0.36f, 0f);
        }
        p.close();
        return p;
    }

    private static Pic shield() {
        return new Pic()
            .add(shieldPath(false), C(0x3B6FD4), GOLD_DARK, 0.03f)
            .add(shieldPath(true), RED)
            .line(shieldPath(false), GOLD, 0.04f)
            .add(star(0f, -0.02f, 0.17f), GOLD, GOLD_DARK, 0.012f);
    }

    private static Pic tree() {
        return new Pic()
            .add(rrect(-0.07f, 0.05f, 0.14f, 0.42f, 0.03f), BROWN, BROWN_DARK, SW)
            .add(merge(circle(0f, -0.2f, 0.26f), circle(-0.2f, 0f, 0.2f), circle(0.2f, 0f, 0.2f), circle(0f, 0.05f, 0.2f)), GREEN, GREEN_DARK, SW)
            .add(merge(circle(-0.12f, -0.18f, 0.045f), circle(0.14f, -0.05f, 0.045f), circle(-0.18f, 0.06f, 0.045f), circle(0.06f, -0.3f, 0.045f)), RED);
    }

    private static Path pie(float x, float y, float w, float h, float start, float sweep) {
        Path p = NP();
        p.moveTo(x + w / 2, y + h / 2);
        p.arcTo(new RectF(x, y, x + w, y + h), start, sweep, false);
        p.close();
        return p;
    }

    private static Pic umbrella() {
        Path handle = NP();
        handle.moveTo(0f, 0f);
        handle.lineTo(0f, 0.36f);
        handle.arcTo(new RectF(-0.14f, 0.29f, 0f, 0.43f), 0, 180, false);
        return new Pic()
            .line(handle, BROWN_DARK, 0.045f)
            .add(pie(-0.43f, -0.42f, 0.86f, 0.86f, 180, 180), RED, RED_DARK, SW)
            .add(pie(-0.43f, -0.42f, 0.86f, 0.86f, 240, 60), C(0xFFD23F))
            .add(circle(0f, -0.44f, 0.035f), BROWN_DARK);
    }

    private static Pic vase() {
        return new Pic()
            .line(lines(0f, -0.2f, -0.17f, -0.38f), GREEN_DARK, 0.03f)
            .line(lines(0f, -0.2f, 0f, -0.44f), GREEN_DARK, 0.03f)
            .line(lines(0f, -0.2f, 0.17f, -0.38f), GREEN_DARK, 0.03f)
            .add(merge(circle(-0.17f, -0.39f, 0.075f), circle(0.17f, -0.39f, 0.075f)), C(0xF2547D))
            .add(circle(0f, -0.45f, 0.075f), C(0xFFD23F))
            .add(merge(circle(-0.17f, -0.39f, 0.03f), circle(0.17f, -0.39f, 0.03f), circle(0f, -0.45f, 0.03f)), WHITE)
            .add(curve(0.4f, -0.1f, -0.22f, 0.1f, -0.22f, 0.12f, -0.1f, 0.28f, 0.12f, 0.2f, 0.42f, -0.2f, 0.42f, -0.28f, 0.12f, -0.12f, -0.1f), BLUE, BLUE_DARK, SW)
            .line(lines(-0.25f, 0.14f, 0.25f, 0.14f), GOLD, 0.035f)
            .add(rrect(-0.15f, -0.26f, 0.3f, 0.07f, 0.03f), BLUE, BLUE_DARK, SW);
    }

    private static Pic wand() {
        return new Pic()
            .line(lines(-0.32f, 0.44f, 0.1f, -0.08f), BROWN_DARK, 0.07f)
            .line(lines(-0.32f, 0.44f, 0.1f, -0.08f), C(0x7768AE), 0.045f)
            .add(star(0.17f, -0.2f, 0.26f), GOLD, GOLD_DARK, SW)
            .add(star(-0.25f, -0.3f, 0.07f), C(0xF7A1C4))
            .add(star(0.38f, 0.18f, 0.06f), C(0x9FD0FA))
            .add(star(-0.05f, -0.45f, 0.05f), C(0x9BE3C1));
    }

    private static Pic xMark() {
        int parch = C(0xF3DFB4), parchDark = C(0xC9A66B);
        Path bar1 = move(rot(rrect(-0.17f, -0.045f, 0.34f, 0.09f, 0.045f), 45, 0, 0), 0.12f, -0.1f);
        Path bar2 = move(rot(rrect(-0.17f, -0.045f, 0.34f, 0.09f, 0.045f), -45, 0, 0), 0.12f, -0.1f);
        return new Pic()
            .add(rrect(-0.44f, -0.36f, 0.88f, 0.72f, 0.06f), parch, parchDark, SW)
            .add(merge(circle(-0.3f, 0.24f, 0.022f), circle(-0.22f, 0.18f, 0.022f), circle(-0.15f, 0.1f, 0.022f), circle(-0.06f, 0.06f, 0.022f), circle(0.0f, -0.01f, 0.022f)), C(0x8B5A2B))
            .add(merge(bar1, bar2), RED, RED_DARK, 0.012f);
    }

    private static Pic yoYo() {
        return new Pic()
            .line(lines(0f, -0.42f, 0f, 0.0f), C(0x888888), 0.018f)
            .line(circle(0f, -0.45f, 0.04f), C(0x888888), 0.018f)
            .add(circle(0f, 0.12f, 0.34f), RED, RED_DARK, SW)
            .add(circle(0f, 0.12f, 0.22f), C(0xFF9F1C))
            .add(circle(0f, 0.12f, 0.07f), WHITE);
    }

    private static Pic zigzag() {
        int y = C(0xFFD23F);
        return new Pic().add(poly(0.06f, -0.47f, -0.25f, 0.05f, -0.02f, 0.05f, -0.12f, 0.47f, 0.27f, -0.1f, 0.03f, -0.1f, 0.18f, -0.47f), y, dark(y, 0.75f), SW);
    }

    private static Pic crown() {
        return new Pic()
            .add(poly(-0.38f, 0.28f, 0.38f, 0.28f, 0.42f, -0.22f, 0.2f, 0.0f, 0f, -0.3f, -0.2f, 0.0f, -0.42f, -0.22f), GOLD, GOLD_DARK, SW)
            .add(rrect(-0.38f, 0.16f, 0.76f, 0.12f, 0.03f), GOLD_DARK)
            .add(circle(0f, 0.22f, 0.045f), RED)
            .add(circle(-0.22f, 0.22f, 0.04f), BLUE)
            .add(circle(0.22f, 0.22f, 0.04f), GREEN)
            .add(merge(circle(-0.42f, -0.26f, 0.055f), circle(0f, -0.34f, 0.055f), circle(0.42f, -0.26f, 0.055f)), GOLD, GOLD_DARK, 0.012f);
    }

    private static Pic starPic() {
        return new Pic()
            .add(star(0f, 0.03f, 0.47f), GOLD, GOLD_DARK, SW)
            .add(star(-0.1f, -0.06f, 0.1f), argb(150, 255, 255, 255));
    }

    private static Pic sword() {
        int blade = C(0xDDE3EA), bladeDark = C(0x8A96A3);
        return new Pic()
            .add(rot(poly(-0.06f, 0.12f, -0.06f, -0.36f, 0f, -0.48f, 0.06f, -0.36f, 0.06f, 0.12f), 30, 0, 0), blade, bladeDark, SW)
            .line(rot(lines(0f, -0.38f, 0f, 0.1f), 30, 0, 0), bladeDark, 0.015f)
            .add(rot(rrect(-0.04f, 0.17f, 0.08f, 0.2f, 0.02f), 30, 0, 0), BROWN, BROWN_DARK, 0.012f)
            .add(rot(rrect(-0.22f, 0.1f, 0.44f, 0.08f, 0.04f), 30, 0, 0), GOLD, GOLD_DARK, 0.012f)
            .add(rot(circle(0f, 0.41f, 0.06f), 30, 0, 0), GOLD, GOLD_DARK, 0.012f);
    }
}

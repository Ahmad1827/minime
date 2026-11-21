package com.foryusef;

/** One card on the menu. */
public final class GameInfo {
    public interface Factory { Game create(int w, int h, Sound sound); }
    public interface Demo { void run(Game g); }

    public final String title, tagline, parentNextHint;
    public final int accent;
    public final Factory factory;
    public final Demo demo;

    public GameInfo(String title, String tagline, int accent, String parentNextHint, Factory factory, Demo demo) {
        this.title = title; this.tagline = tagline; this.accent = accent;
        this.parentNextHint = parentNextHint; this.factory = factory; this.demo = demo;
    }
}

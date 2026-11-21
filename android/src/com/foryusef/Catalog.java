package com.foryusef;

import com.foryusef.littleknight.LittleKnightGame;
import com.foryusef.littlepilot.PilotGame;

/** Every game on the menu, in order. */
final class Catalog {
    static final GameInfo[] ALL = {
        LittleKnightGame.INFO,
        PilotGame.INFO,
        // next game goes here
    };

    private Catalog() { }
}

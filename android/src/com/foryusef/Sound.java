package com.foryusef;

/** Harp notes: (note 0..11 of a pentatonic scale, delay in seconds, volume 0..1). */
public interface Sound { void play(int step, float delay, float gain); }

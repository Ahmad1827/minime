package com.minime.littleknight;

import android.media.AudioAttributes;
import android.media.AudioFormat;
import android.media.AudioTrack;
import android.os.Build;
import android.os.Process;

import java.util.ArrayList;
import java.util.Random;

/** Synthesized plucked harp notes, mixed in real time and streamed to an AudioTrack. */
final class Harp {
    private static final int RATE = 22050, BUF = 512;

    // C major pentatonic over ~2 octaves: every combination sounds pleasant, whatever the baby presses.
    private static final int[] SCALE = { 60, 62, 64, 67, 69, 72, 74, 76, 79, 81, 84, 86 };

    private static final class Voice { float[] s; int pos; float gain; }

    private final float[][] notes = new float[SCALE.length][];
    private final ArrayList<Voice> voices = new ArrayList<>();
    private final Object gate = new Object();
    private Thread thread;
    private volatile boolean running;

    Harp() {
        Random rnd = new Random(1234);
        for (int i = 0; i < SCALE.length; i++)
            notes[i] = pluck(440.0 * Math.pow(2, (SCALE[i] - 69) / 12.0), 1.8, rnd);
    }

    // Karplus-Strong plucked string, softened so it sounds like a harp / lute.
    private static float[] pluck(double freq, double seconds, Random r) {
        int n = (int) (seconds * RATE);
        float[] out = new float[n];
        int p = Math.max(2, (int) Math.round(RATE / freq));
        float[] ring = new float[p];
        for (int i = 0; i < p; i++) ring[i] = (float) (r.nextDouble() * 2 - 1);
        for (int pass = 0; pass < 3; pass++) // pre-filter the noise burst: warmer, less "zingy"
            for (int i = 0; i < p; i++) ring[i] = (ring[i] + ring[(i + 1) % p] + ring[(i + p - 1) % p]) / 3f;
        int idx = 0; float peak = 0.0001f;
        for (int i = 0; i < n; i++) {
            int nx = (idx + 1) % p;
            float v = ring[idx];
            out[i] = v;
            ring[idx] = 0.497f * (ring[idx] + ring[nx]);
            idx = nx;
            peak = Math.max(peak, Math.abs(v));
        }
        int attack = 40, release = n / 4;
        for (int i = 0; i < n; i++) {
            float env = 0.42f / peak;
            if (i < attack) env *= i / (float) attack;
            if (i > n - release) env *= (n - i) / (float) release;
            out[i] *= env;
        }
        return out;
    }

    void play(int note, float delaySeconds, float gain) {
        if (!running) return;
        note = Math.max(0, Math.min(notes.length - 1, note));
        Voice v = new Voice();
        v.s = notes[note]; v.pos = -(int) (delaySeconds * RATE); v.gain = gain;
        synchronized (gate) {
            if (voices.size() >= 18) voices.remove(0);
            voices.add(v);
        }
    }

    synchronized void start() {
        if (running) return;
        running = true;
        thread = new Thread(this::loop, "harp");
        thread.start();
    }

    synchronized void stop() {
        running = false;
        if (thread != null) {
            try { thread.join(500); } catch (InterruptedException ignored) { }
            thread = null;
        }
        synchronized (gate) { voices.clear(); }
    }

    private void loop() {
        AudioTrack track = null;
        try {
            Process.setThreadPriority(Process.THREAD_PRIORITY_URGENT_AUDIO);
            int min = AudioTrack.getMinBufferSize(RATE, AudioFormat.CHANNEL_OUT_MONO, AudioFormat.ENCODING_PCM_16BIT);
            AudioTrack.Builder b = new AudioTrack.Builder()
                .setAudioAttributes(new AudioAttributes.Builder()
                    .setUsage(AudioAttributes.USAGE_GAME)
                    .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC).build())
                .setAudioFormat(new AudioFormat.Builder()
                    .setSampleRate(RATE)
                    .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                    .setChannelMask(AudioFormat.CHANNEL_OUT_MONO).build())
                .setBufferSizeInBytes(Math.max(min, BUF * 2 * 3))
                .setTransferMode(AudioTrack.MODE_STREAM);
            if (Build.VERSION.SDK_INT >= 26) b.setPerformanceMode(AudioTrack.PERFORMANCE_MODE_LOW_LATENCY);
            track = b.build();
            track.play();
            float[] mix = new float[BUF];
            short[] pcm = new short[BUF];
            while (running) {
                fill(mix, pcm);
                if (track.write(pcm, 0, BUF) < 0) break;
            }
        } catch (Throwable ignored) {
            // no sound is better than a crash
        } finally {
            if (track != null) {
                try { track.pause(); track.flush(); track.release(); } catch (Throwable ignored) { }
            }
        }
    }

    private void fill(float[] mix, short[] pcm) {
        java.util.Arrays.fill(mix, 0f);
        synchronized (gate) {
            for (int v = voices.size() - 1; v >= 0; v--) {
                Voice vo = voices.get(v);
                for (int j = 0; j < BUF; j++) {
                    int k = vo.pos + j;
                    if (k < 0) continue;
                    if (k >= vo.s.length) break;
                    mix[j] += vo.s[k] * vo.gain;
                }
                vo.pos += BUF;
                if (vo.pos >= vo.s.length) voices.remove(v);
            }
        }
        for (int j = 0; j < BUF; j++) {
            float x = mix[j];
            x = x / (1f + 0.35f * Math.abs(x)); // gentle limiter so smashing never gets harsh
            pcm[j] = (short) (Math.max(-1f, Math.min(1f, x)) * 30000);
        }
    }
}

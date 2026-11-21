// Harp: synthesized plucked notes, mixed in real time. Shared by every game (one sound device for the app).

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace ForYusef
{
    // ------------------------------------------------------------------ Harp: synthesized plucked notes, mixed in real time
    class Harp : IDisposable
    {
        const int Rate = 22050, BufSamples = 512, NumBufs = 4;

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }
        [StructLayout(LayoutKind.Sequential)]
        struct WAVEHDR { public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public uint dwFlags, dwLoops; public IntPtr lpNext, reserved; }

        [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr h, uint dev, ref WAVEFORMATEX f, IntPtr cb, IntPtr inst, uint flags);
        [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr h);
        const uint WAVE_MAPPER = 0xFFFFFFFF, WHDR_DONE = 1;

        class Voice { public float[] S; public int Pos; public float Gain; }

        IntPtr dev;
        readonly IntPtr[] hdrs = new IntPtr[NumBufs], datas = new IntPtr[NumBufs];
        uint hdrSize; int flagsOffset;
        readonly List<Voice> voices = new List<Voice>();
        readonly object gate = new object();
        float[][] notes;
        Thread thread; volatile bool running; bool ok;

        // C major pentatonic over ~2 octaves: every combination sounds pleasant, whatever the baby presses.
        static readonly int[] Scale = { 60, 62, 64, 67, 69, 72, 74, 76, 79, 81, 84, 86 };

        public static Harp TryCreate()
        {
            try { var h = new Harp(); if (h.ok) return h; h.Dispose(); } catch { }
            return null;
        }

        Harp()
        {
            var rnd = new Random(1234);
            notes = new float[Scale.Length][];
            for (int i = 0; i < Scale.Length; i++)
                notes[i] = Pluck(440.0 * Math.Pow(2, (Scale[i] - 69) / 12.0), 1.8, rnd);

            var fmt = new WAVEFORMATEX { wFormatTag = 1, nChannels = 1, nSamplesPerSec = Rate, wBitsPerSample = 16, nBlockAlign = 2, nAvgBytesPerSec = Rate * 2, cbSize = 0 };
            if (waveOutOpen(out dev, WAVE_MAPPER, ref fmt, IntPtr.Zero, IntPtr.Zero, 0) != 0) { dev = IntPtr.Zero; return; }
            hdrSize = (uint)Marshal.SizeOf(typeof(WAVEHDR));
            flagsOffset = (int)Marshal.OffsetOf(typeof(WAVEHDR), "dwFlags");
            for (int i = 0; i < NumBufs; i++)
            {
                datas[i] = Marshal.AllocHGlobal(BufSamples * 2);
                hdrs[i] = Marshal.AllocHGlobal((int)hdrSize);
                var h = new WAVEHDR { lpData = datas[i], dwBufferLength = BufSamples * 2 };
                Marshal.StructureToPtr(h, hdrs[i], false);
                waveOutPrepareHeader(dev, hdrs[i], hdrSize);
            }
            running = true; ok = true;
            thread = new Thread(Loop) { IsBackground = true, Priority = ThreadPriority.AboveNormal };
            thread.Start();
        }

        // Karplus-Strong plucked string, softened so it sounds like a harp / lute.
        static float[] Pluck(double freq, double seconds, Random r)
        {
            int n = (int)(seconds * Rate);
            var output = new float[n];
            int p = Math.Max(2, (int)Math.Round(Rate / freq));
            var ring = new float[p];
            for (int i = 0; i < p; i++) ring[i] = (float)(r.NextDouble() * 2 - 1);
            for (int pass = 0; pass < 3; pass++) // pre-filter the noise burst: warmer, less "zingy"
                for (int i = 0; i < p; i++) ring[i] = (ring[i] + ring[(i + 1) % p] + ring[(i + p - 1) % p]) / 3f;
            int idx = 0; float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                int nx = (idx + 1) % p;
                float v = ring[idx];
                output[i] = v;
                ring[idx] = 0.497f * (ring[idx] + ring[nx]);
                idx = nx;
                peak = Math.Max(peak, Math.Abs(v));
            }
            int attack = 40, release = n / 4;
            for (int i = 0; i < n; i++)
            {
                float env = 0.42f / peak;
                if (i < attack) env *= i / (float)attack;
                if (i > n - release) env *= (n - i) / (float)release;
                output[i] *= env;
            }
            return output;
        }

        public void Play(int note, float delaySeconds, float gain)
        {
            if (!ok) return;
            note = Math.Max(0, Math.Min(notes.Length - 1, note));
            lock (gate)
            {
                if (voices.Count >= 18) voices.RemoveAt(0);
                voices.Add(new Voice { S = notes[note], Pos = -(int)(delaySeconds * Rate), Gain = gain });
            }
        }

        void Loop()
        {
            var mix = new float[BufSamples];
            var pcm = new short[BufSamples];
            var queued = new bool[NumBufs];
            while (running)
            {
                bool wrote = false;
                for (int i = 0; i < NumBufs && running; i++)
                {
                    if (queued[i] && (Marshal.ReadInt32(hdrs[i], flagsOffset) & WHDR_DONE) == 0) continue;
                    Fill(mix, pcm);
                    Marshal.Copy(pcm, 0, datas[i], BufSamples);
                    waveOutWrite(dev, hdrs[i], hdrSize);
                    queued[i] = true; wrote = true;
                }
                if (!wrote) Thread.Sleep(3);
            }
        }

        void Fill(float[] mix, short[] pcm)
        {
            Array.Clear(mix, 0, mix.Length);
            lock (gate)
            {
                for (int v = voices.Count - 1; v >= 0; v--)
                {
                    var vo = voices[v];
                    for (int j = 0; j < BufSamples; j++)
                    {
                        int k = vo.Pos + j;
                        if (k < 0) continue;
                        if (k >= vo.S.Length) break;
                        mix[j] += vo.S[k] * vo.Gain;
                    }
                    vo.Pos += BufSamples;
                    if (vo.Pos >= vo.S.Length) voices.RemoveAt(v);
                }
            }
            for (int j = 0; j < BufSamples; j++)
            {
                float x = mix[j];
                x = x / (1f + 0.35f * Math.Abs(x)); // gentle limiter so smashing never gets harsh
                pcm[j] = (short)(Math.Max(-1f, Math.Min(1f, x)) * 30000);
            }
        }

        public void Dispose()
        {
            running = false;
            if (thread != null) thread.Join(300);
            if (dev != IntPtr.Zero)
            {
                waveOutReset(dev);
                for (int i = 0; i < NumBufs; i++) if (hdrs[i] != IntPtr.Zero) waveOutUnprepareHeader(dev, hdrs[i], hdrSize);
                waveOutClose(dev);
                dev = IntPtr.Zero;
            }
            for (int i = 0; i < NumBufs; i++)
            {
                if (hdrs[i] != IntPtr.Zero) { Marshal.FreeHGlobal(hdrs[i]); hdrs[i] = IntPtr.Zero; }
                if (datas[i] != IntPtr.Zero) { Marshal.FreeHGlobal(datas[i]); datas[i] = IntPtr.Zero; }
            }
            ok = false;
        }
    }
}

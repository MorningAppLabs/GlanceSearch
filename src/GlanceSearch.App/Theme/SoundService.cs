using System.IO;
using System.Media;
using System.Windows;
using Serilog;

namespace GlanceSearch.App.Theme;

/// <summary>
/// Plays in-app notification and feedback sounds.
/// Respects the user's SoundEffects setting.
/// Sounds are generated programmatically — no external audio files needed.
/// </summary>
public static class SoundService
{
    private static bool _enabled = true;

    public static void SetEnabled(bool enabled) => _enabled = enabled;

    /// <summary>Short camera shutter-style click — played when a region is captured.</summary>
    public static void PlayCapture()
    {
        if (!_enabled) return;
        try
        {
            PlayTone(frequency: 1200, durationMs: 40, fadeDurationMs: 15, volume: 0.30);
            PlayTone(frequency: 800, durationMs: 30, fadeDurationMs: 12, volume: 0.20);
        }
        catch (Exception ex) { Log.Debug(ex, "SoundService: PlayCapture failed"); }
    }

    /// <summary>Soft positive confirmation click.</summary>
    public static void PlaySuccess()
    {
        if (!_enabled) return;
        try { PlayTone(frequency: 1000, durationMs: 60, fadeDurationMs: 20, volume: 0.18); }
        catch (Exception ex) { Log.Debug(ex, "SoundService: PlaySuccess failed"); }
    }

    /// <summary>Short low-pitched click for button presses.</summary>
    public static void PlayClick()
    {
        if (!_enabled) return;
        try { PlayTone(frequency: 750, durationMs: 30, fadeDurationMs: 12, volume: 0.15); }
        catch (Exception ex) { Log.Debug(ex, "SoundService: PlayClick failed"); }
    }

    /// <summary>
    /// Generates and plays a simple sine-wave tone as a WAV in memory.
    /// No external audio file dependencies.
    /// </summary>
    private static void PlayTone(int frequency, int durationMs, int fadeDurationMs, double volume)
    {
        const int sampleRate = 44100;
        var sampleCount = sampleRate * durationMs / 1000;
        var fadeCount = sampleRate * fadeDurationMs / 1000;

        var wavData = new byte[sampleCount * 2]; // 16-bit mono
        for (int i = 0; i < sampleCount; i++)
        {
            // Sine wave sample
            var sample = volume * Math.Sin(2.0 * Math.PI * frequency * i / sampleRate);

            // Apply fade-in (first fadeCount samples) and fade-out (last fadeCount samples)
            if (i < fadeCount)
                sample *= (double)i / fadeCount;
            else if (i > sampleCount - fadeCount)
                sample *= (double)(sampleCount - i) / fadeCount;

            var pcmSample = (short)(sample * short.MaxValue);
            wavData[i * 2] = (byte)(pcmSample & 0xFF);
            wavData[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
        }

        // Build a minimal WAV file in memory
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // RIFF header
        bw.Write("RIFF"u8.ToArray());
        bw.Write(36 + wavData.Length);   // chunk size
        bw.Write("WAVE"u8.ToArray());

        // fmt chunk
        bw.Write("fmt "u8.ToArray());
        bw.Write(16);           // chunk size (PCM)
        bw.Write((short)1);     // PCM format
        bw.Write((short)1);     // mono
        bw.Write(sampleRate);   // sample rate
        bw.Write(sampleRate * 2); // byte rate (sampleRate * channels * bitsPerSample/8)
        bw.Write((short)2);     // block align
        bw.Write((short)16);    // bits per sample

        // data chunk
        bw.Write("data"u8.ToArray());
        bw.Write(wavData.Length);
        bw.Write(wavData);

        ms.Position = 0;
        var player = new SoundPlayer(ms);
        player.Play(); // async, non-blocking
    }
}

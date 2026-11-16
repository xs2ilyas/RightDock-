using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RightDock.Services
{
    internal readonly struct SoundOption
    {
        internal SoundOption(string key, string name)
        {
            Key = key;
            Name = name;
        }

        internal string Key { get; }

        internal string Name { get; }
    }

    internal static class BellSynth
    {
        internal const string DefaultFocus = "classic";
        internal const string DefaultReminder = "soft";

        private const int SampleRate = 44100;
        private const double Peak = 0.55;

        private sealed class Voice
        {
            internal SoundOption Option;
            internal double Fundamental;
            internal double Duration;
            internal double Decay;
            internal double Attack;
            internal double[] Ratios;
            internal double[] Gains;
            internal double Beating;
        }

        private static readonly Voice[] Voices =
        {
            new Voice
            {
                Option = new SoundOption("classic", "Classic Bell"),
                Fundamental = 880, Duration = 1.6, Decay = 3.2, Attack = 0.002,
                Ratios = new[] { 1.0, 2.0, 2.4, 3.0 }, Gains = new[] { 1.0, 0.5, 0.25, 0.15 },
                Beating = 0,
            },
            new Voice
            {
                Option = new SoundOption("soft", "Soft Chime"),
                Fundamental = 660, Duration = 1.9, Decay = 2.4, Attack = 0.02,
                Ratios = new[] { 1.0, 2.0, 3.0 }, Gains = new[] { 1.0, 0.28, 0.12 },
                Beating = 0,
            },
            new Voice
            {
                Option = new SoundOption("glass", "Glass Bell"),
                Fundamental = 1320, Duration = 1.4, Decay = 4.2, Attack = 0.001,
                Ratios = new[] { 1.0, 1.52, 2.03, 2.55, 3.07 }, Gains = new[] { 1.0, 0.6, 0.35, 0.2, 0.1 },
                Beating = 0,
            },
            new Voice
            {
                Option = new SoundOption("marimba", "Marimba"),
                Fundamental = 523.25, Duration = 1.0, Decay = 5.2, Attack = 0.001,
                Ratios = new[] { 1.0, 3.93, 9.2 }, Gains = new[] { 1.0, 0.3, 0.06 },
                Beating = 0,
            },
            new Voice
            {
                Option = new SoundOption("temple", "Temple Bowl"),
                Fundamental = 220, Duration = 2.6, Decay = 1.5, Attack = 0.004,
                Ratios = new[] { 1.0, 2.0, 3.0 }, Gains = new[] { 1.0, 0.35, 0.12 },
                Beating = 1.1,
            },
            new Voice
            {
                Option = new SoundOption("digital", "Digital Chime"),
                Fundamental = 1046.5, Duration = 0.85, Decay = 6.5, Attack = 0.001,
                Ratios = new[] { 1.0, 3.0, 5.0, 7.0 }, Gains = new[] { 1.0, 0.35, 0.18, 0.08 },
                Beating = 0,
            },
            new Voice
            {
                Option = new SoundOption("harp", "Harp Pluck"),
                Fundamental = 784, Duration = 1.4, Decay = 3.4, Attack = 0.001,
                Ratios = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 }, Gains = new[] { 1.0, 0.45, 0.22, 0.1, 0.05 },
                Beating = 0,
            },
            new Voice
            {
                Option = new SoundOption("musicbox", "Music Box"),
                Fundamental = 1568, Duration = 1.3, Decay = 4.4, Attack = 0.001,
                Ratios = new[] { 1.0, 2.0, 2.7, 4.2 }, Gains = new[] { 1.0, 0.4, 0.2, 0.08 },
                Beating = 0,
            },
            new Voice
            {
                Option = new SoundOption("resonant", "Deep Resonant"),
                Fundamental = 440, Duration = 2.2, Decay = 2.2, Attack = 0.004,
                Ratios = new[] { 1.0, 2.0, 2.67, 4.0 }, Gains = new[] { 1.0, 0.4, 0.25, 0.12 },
                Beating = 0.6,
            },
            new Voice
            {
                Option = new SoundOption("bright", "Bright Bell"),
                Fundamental = 1174.66, Duration = 1.2, Decay = 4.8, Attack = 0.001,
                Ratios = new[] { 1.0, 1.5, 2.0, 3.0 }, Gains = new[] { 1.0, 0.5, 0.25, 0.1 },
                Beating = 0,
            },
        };

        internal static IReadOnlyList<SoundOption> Catalog
        {
            get
            {
                var options = new SoundOption[Voices.Length];
                for (int i = 0; i < Voices.Length; i++) options[i] = Voices[i].Option;
                return options;
            }
        }

        internal static bool IsKnown(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            foreach (Voice voice in Voices)
            {
                if (string.Equals(voice.Option.Key, key, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        internal static byte[] CreateWav(string key)
        {
            Voice voice = Find(key) ?? Voices[0];
            int samples = (int)(voice.Duration * SampleRate);
            var data = new double[samples];
            int attack = Math.Max(1, (int)(voice.Attack * SampleRate));
            int release = Math.Min(samples, (int)(0.06 * SampleRate));

            for (int i = 0; i < samples; i++)
            {
                double t = (double)i / SampleRate;
                double envelope = Math.Exp(-voice.Decay * t);
                if (i < attack) envelope *= (double)i / attack;
                if (i >= samples - release)
                {
                    envelope *= (double)(samples - i) / release;
                }

                double value = 0;
                for (int p = 0; p < voice.Ratios.Length; p++)
                {
                    double phase = 2 * Math.PI * voice.Fundamental * voice.Ratios[p] * t + p * 0.7;
                    value += voice.Gains[p] * Math.Sin(phase);
                }

                if (voice.Beating > 0)
                {
                    value += 0.35 * Math.Sin(2 * Math.PI * (voice.Fundamental + voice.Beating) * t);
                }

                data[i] = value * envelope;
            }

            double peak = 0;
            foreach (double sample in data) peak = Math.Max(peak, Math.Abs(sample));
            double scale = peak > 0 ? Peak / peak : 0;

            using (var stream = new MemoryStream(44 + samples * 2))
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(samples * 2);

                foreach (double sample in data)
                {
                    double scaled = sample * scale;
                    scaled = Math.Max(-1, Math.Min(1, scaled));
                    writer.Write((short)Math.Round(scaled * short.MaxValue));
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static Voice Find(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            foreach (Voice voice in Voices)
            {
                if (string.Equals(voice.Option.Key, key, StringComparison.OrdinalIgnoreCase)) return voice;
            }

            return null;
        }
    }
}
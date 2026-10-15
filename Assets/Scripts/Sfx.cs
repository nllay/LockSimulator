using System;
using UnityEngine;

namespace LockSimulator
{
    /// <summary>程序化音效：用短促的噪声脉冲加金属共振模拟锁具里的咔哒声，而不是电子提示音。</summary>
    public static class Sfx
    {
        const int Rate = 44100;
        static readonly System.Random Rng = new System.Random(7);
        const float Tau = Mathf.PI * 2f;

        static float Noise() { return (float)(Rng.NextDouble() * 2.0 - 1.0); }
        static float Env(float t, float k) { return Mathf.Exp(-t * k); }
        static float Sine(float t, float f) { return Mathf.Sin(Tau * f * t); }

        static AudioClip Make(string name, float duration, Func<float, float> f)
        {
            int n = Mathf.CeilToInt(Rate * duration);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Tick(float t, float t0, float amp)
        {
            float x = t - t0;
            if (x < 0f) return 0f;
            return (Noise() * Env(x, 700f) * 0.5f + Sine(x, 1800f) * Env(x, 300f) * 0.2f) * amp;
        }

        public static AudioClip Set()
        {
            return Make("Set", 0.10f, t => Noise() * Env(t, 900f) * 0.8f + Sine(t, 3100f) * Env(t, 450f) * 0.35f + Sine(t, 1250f) * Env(t, 160f) * 0.35f);
        }

        public static AudioClip FalseSet()
        {
            return Make("FalseSet", 0.15f, t => Noise() * Env(t, 500f) * 0.5f + Sine(t, 820f) * Env(t, 120f) * 0.5f + Sine(t, 410f) * Env(t, 60f) * 0.3f);
        }

        public static AudioClip Overset()
        {
            return Make("Overset", 0.25f, t => Sine(t, 230f) * Env(t, 35f) * 0.7f + Noise() * Env(t, 250f) * 0.5f);
        }

        public static AudioClip Scrape()
        {
            float lp = 0f;
            return Make("Scrape", 0.07f, t =>
            {
                lp += (Noise() - lp) * 0.25f;
                return lp * Env(t, 45f) * 0.55f;
            });
        }

        public static AudioClip Reset()
        {
            return Make("Reset", 0.25f, t => Tick(t, 0f, 1f) + Tick(t, 0.025f, 0.9f) + Tick(t, 0.05f, 0.8f) + Tick(t, 0.085f, 0.7f) + Tick(t, 0.13f, 0.6f));
        }

        public static AudioClip Counter()
        {
            return Make("Counter", 0.30f, t => Sine(t, 95f) * Env(t, 10f) * 0.55f + Noise() * Env(t, 60f) * 0.2f);
        }

        public static AudioClip Open()
        {
            return Make("Open", 0.70f, t =>
                Sine(t, 120f) * Env(t, 14f) * 0.8f + Noise() * Env(t, 200f) * 0.6f +
                (t > 0.12f ? Sine(t - 0.12f, 1900f) * Env(t - 0.12f, 18f) * 0.35f : 0f));
        }

        public static AudioClip Button()
        {
            return Make("Button", 0.04f, t => Noise() * Env(t, 400f) * 0.4f + Sine(t, 2200f) * Env(t, 250f) * 0.3f);
        }
    }
}

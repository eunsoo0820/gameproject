using System;
using System.Collections.Generic;
using UnityEngine;

namespace Drift
{
    public sealed class DriftAudio
    {
        private readonly List<AudioClip> clips = new List<AudioClip>();
        private readonly AudioSource waves, gulls, effects, alarm;
        private readonly AudioClip crackle, beep;
        public DriftAudio(Transform root)
        {
            waves = Source(root, "Waves", .35f); gulls = Source(root, "Gulls", .16f);
            effects = Source(root, "Radio Effects", .38f); alarm = Source(root, "Alarm", .2f);
            waves.clip = Create("Sea Wash", 8, 0); waves.loop = true;
            gulls.clip = Create("Seagulls", 11, 1); gulls.loop = true;
            alarm.clip = Create("Radio Alarm", 1.5f, 2); alarm.loop = true;
            crackle = Create("Broken Radio", 4, 3); beep = Create("Radio Disconnect", .4f, 4);
        }
        private static AudioSource Source(Transform parent, string name, float volume)
        {
            var source = new GameObject(name).AddComponent<AudioSource>(); source.transform.SetParent(parent, false);
            source.playOnAwake = false; source.volume = volume; source.spatialBlend = 0; return source;
        }
        private AudioClip Create(string name, float seconds, int kind)
        {
            const int rate = 22050;
            float[] samples = new float[Mathf.CeilToInt(seconds * rate)];
            var random = new System.Random(171 + kind); float filtered = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate; float noise = (float)random.NextDouble() * 2 - 1;
                filtered += .075f * (noise - filtered);
                float edge = Mathf.Min(1, Mathf.Min(t * 20, (seconds - t) * 20));
                if (kind == 0) samples[i] = filtered * (1.2f + .75f * Mathf.Sin(t * Mathf.PI / 4));
                else if (kind == 1)
                {
                    float phase = t % 3.6f;
                    float envelope = phase < .8f ? Mathf.Sin(phase / .8f * Mathf.PI) : 0;
                    samples[i] = Mathf.Sin(2 * Mathf.PI * (1200 * t + 70 * Mathf.Sin(t * 14))) * envelope * .3f * edge;
                }
                else if (kind == 2) samples[i] = Mathf.Sin(t * Mathf.PI * 2 * 650) * (t % .75f < .3f ? .32f : 0) * edge;
                else if (kind == 3) samples[i] = noise * .25f * (Mathf.Sin(t * 31) > 0 ? 1 : .1f) * edge;
                else samples[i] = Mathf.Sin(t * Mathf.PI * 2 * 900) * .35f * edge;
            }
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, rate, false); clip.SetData(samples, 0); clips.Add(clip); return clip;
        }
        public void StartSea() { waves.Play(); gulls.Play(); }
        public void SetAlarm(bool active) { if (active && !alarm.isPlaying) alarm.Play(); else if (!active) alarm.Stop(); }
        public void Radio() { alarm.Stop(); effects.PlayOneShot(crackle); }
        public void Disconnect() => effects.PlayOneShot(beep);
        public void Stop() { waves.Stop(); gulls.Stop(); effects.Stop(); alarm.Stop(); }
        public void Dispose() { Stop(); foreach (var clip in clips) UnityEngine.Object.Destroy(clip); clips.Clear(); }
    }
}

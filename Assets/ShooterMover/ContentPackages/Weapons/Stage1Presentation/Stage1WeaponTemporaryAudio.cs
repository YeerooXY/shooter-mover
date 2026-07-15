using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShooterMover.ContentPackages.Weapons.Stage1Presentation
{
    internal sealed class Stage1WeaponTemporaryAudio
    {
        private readonly Transform owner;
        private readonly Dictionary<string, AudioClip> clips =
            new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private AudioSource[] voices;

        public Stage1WeaponTemporaryAudio(Transform ownerTransform)
        {
            owner = ownerTransform;
        }

        public void Play(Stage1WeaponCuePlan plan)
        {
            EnsureVoices();
            for (int index = 0; index < plan.AudioCount; index++)
            {
                Stage1WeaponCueRequest request = plan.GetAudioRequest(index);
                Stage1WeaponIdentityCue identity;
                if (!Stage1WeaponPresentationCatalog.TryGetByAudioId(request.CueId, out identity))
                    continue;

                AudioSource source = voices[index];
                source.Stop();
                source.clip = GetClip(identity);
                source.volume = 0.16f;
                source.Play();
            }
        }

        public void Stop()
        {
            if (voices == null) return;
            foreach (AudioSource source in voices)
                if (source != null) source.Stop();
        }

        public void Dispose()
        {
            Stop();
            foreach (AudioClip clip in clips.Values)
                if (clip != null) UnityEngine.Object.Destroy(clip);
            clips.Clear();
        }

        private AudioClip GetClip(Stage1WeaponIdentityCue identity)
        {
            AudioClip clip;
            if (clips.TryGetValue(identity.AudioId, out clip) && clip != null)
                return clip;

            const int sampleRate = 22050;
            int sampleCount = Mathf.CeilToInt((float)identity.DurationSeconds * sampleRate);
            float[] samples = new float[sampleCount];
            for (int index = 0; index < sampleCount; index++)
            {
                float progress = index / (float)sampleCount;
                float envelope = Mathf.Clamp01(progress / 0.08f) * (1f - progress);
                samples[index] = Mathf.Sin(
                    2f * Mathf.PI * (float)identity.FrequencyHz * index / sampleRate)
                    * envelope
                    * 0.45f;
            }

            clip = AudioClip.Create(identity.AudioId, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            clips[identity.AudioId] = clip;
            return clip;
        }

        private void EnsureVoices()
        {
            if (voices != null) return;

            voices = new AudioSource[Stage1WeaponCueArbiter.MaximumAudioVoices];
            for (int index = 0; index < voices.Length; index++)
            {
                GameObject child = new GameObject(
                    "WP-010 Temporary Weapon Voice " + (index + 1));
                child.transform.SetParent(owner, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.priority = 180;
                voices[index] = source;
            }
        }
    }
}

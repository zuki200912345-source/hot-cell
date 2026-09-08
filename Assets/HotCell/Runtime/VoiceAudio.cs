using System;
using System.Collections;
using UnityEngine;

namespace HotCell
{
    /// <summary>
    /// Local suit/facility sound and a temporary LAN proximity-voice transport.
    /// Voice packets are 400 unsigned PCM8 mono samples at 8 kHz (50 ms).
    /// This component owns no network connection and never plays its own voice.
    /// </summary>
    public sealed class VoiceAudio : MonoBehaviour
    {
        public Action<byte[]> SendVoice;
        public Func<int, Transform> FindSpeaker;
        public Func<Vector3, Vector3, bool> IsOccluded;
        public int LocalSlot { get; set; } = -1;
        public float DoseRate, DoseFraction, Exertion;
        public bool VentilationRunning = true, Alarm;
        public string MicrophoneStatus { get; private set; } = "Hold V to enable microphone";

        private const int VoiceRate = 8000;
        private const int PacketSamples = 400;
        private const int MaximumCapturePackets = 3;
        private const int SynthesisRate = 22050;
        private readonly RemoteSpeaker[] speakers = new RemoteSpeaker[6];
        private readonly System.Random random = new System.Random(74019);
        private readonly AudioClip[] synthClips = new AudioClip[5];
        private AudioSource fan, breathing, ventilation, alarm, geiger;
        private AudioClip microphoneClip;
        private string microphoneDevice;
        private float[] microphoneSamples;
        private int microphoneReadPosition, microphoneChunkFrames;
        private float lastCapturePoll, microphoneStartedAt, clickWait = 1f, nextSpatialUpdate;
        private bool wasTalking, permissionDenied, permissionPending, audioInitialized;
        private volatile bool acceptingVoice;
        private Camera listenerCamera;

        private sealed class RemoteSpeaker
        {
            // The audio thread sees only this lock and ordinary managed data.
            // At most 300 ms can be queued; late packets displace old speech.
            private readonly object gate = new object();
            private readonly float[] samples = new float[PacketSamples * 6];
            private int read, count;
            private bool disposed, active;
            public AudioSource Source;
            public AudioLowPassFilter Filter;
            public AudioClip Clip;

            public void Enqueue(byte[] packet)
            {
                lock (gate)
                {
                    if (disposed || !active) return;
                    int overflow = count + packet.Length - samples.Length;
                    if (overflow > 0)
                    {
                        read = (read + overflow) % samples.Length;
                        count -= overflow;
                    }
                    int write = (read + count) % samples.Length;
                    for (int i = 0; i < packet.Length; i++)
                    {
                        samples[write] = (packet[i] - 128) / 128f;
                        write = (write + 1) % samples.Length;
                    }
                    count += packet.Length;
                }
            }

            public void Read(float[] output)
            {
                lock (gate)
                {
                    int available = disposed || !active ? 0 : Math.Min(count, output.Length);
                    for (int i = 0; i < available; i++)
                    {
                        output[i] = samples[read];
                        read = (read + 1) % samples.Length;
                    }
                    count -= available;
                    Array.Clear(output, available, output.Length - available);
                }
            }

            public void Clear(bool dispose = false)
            {
                lock (gate)
                {
                    read = count = 0;
                    disposed |= dispose;
                }
            }

            public void SetActive(bool enabled)
            {
                lock (gate)
                {
                    active = enabled && !disposed;
                    if (!active) read = count = 0;
                }
            }
        }

        private void Awake()
        {
            for (int slot = 0; slot < speakers.Length; slot++)
            {
                var remote = new RemoteSpeaker();
                remote.Source = MakeSource("Crew voice " + (slot + 1), true, true);
                remote.Source.minDistance = 2f;
                remote.Source.maxDistance = 18f;
                remote.Source.rolloffMode = AudioRolloffMode.Linear;
                remote.Source.dopplerLevel = 0f;
                remote.Source.volume = 0f;
                remote.Filter = remote.Source.gameObject.AddComponent<AudioLowPassFilter>();
                remote.Filter.cutoffFrequency = 2200f;
                remote.Filter.lowpassResonanceQ = 1f;
                remote.Clip = AudioClip.Create("PCM8 crew " + slot, VoiceRate, 1, VoiceRate,
                    true, remote.Read);
                remote.Source.clip = remote.Clip;
                speakers[slot] = remote;
            }

            synthClips[0] = Synthesize("Suit fan", 2f, 0);
            synthClips[1] = Synthesize("Breathing", 4f, 1);
            synthClips[2] = Synthesize("Ventilation", 4f, 2);
            synthClips[3] = Synthesize("Six second evacuation alarm", 6f, 3);
            synthClips[4] = Synthesize("Geiger click", 0.032f, 4);
            fan = MakeSource("Suit fan", false, true, synthClips[0]);
            breathing = MakeSource("Suit breathing", false, true, synthClips[1]);
            ventilation = MakeSource("Facility ventilation", false, true, synthClips[2]);
            alarm = MakeSource("Facility alarm", false, true, synthClips[3]);
            geiger = MakeSource("Dosimeter clicks", false, false, synthClips[4]);
            fan.volume = 0.065f;
            breathing.volume = 0.11f;
            ventilation.volume = 0.12f;
            alarm.volume = 0.24f;
            geiger.volume = 0.32f;
            audioInitialized = true;
        }

        private void OnEnable()
        {
            if (!audioInitialized) return;
            acceptingVoice = true;
            foreach (RemoteSpeaker remote in speakers)
            {
                remote.SetActive(true);
                remote.Source.Play();
            }
            fan.Play();
            breathing.Play();
            if (VentilationRunning) ventilation.Play();
            if (Alarm) alarm.Play();
            clickWait = 1f;
        }

        private AudioSource MakeSource(string sourceName, bool spatial, bool loop, AudioClip clip = null)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = spatial ? 1f : 0f;
            source.clip = clip;
            return source;
        }

        /// <summary>Safe to invoke from a transport thread. All other APIs belong on the main thread.</summary>
        public void Receive(int slot, byte[] pcm8)
        {
            if (!acceptingVoice || slot < 0 || slot >= speakers.Length || slot == LocalSlot ||
                pcm8 == null || pcm8.Length != PacketSamples) return;
            RemoteSpeaker remote = speakers[slot];
            if (remote != null) remote.Enqueue(pcm8);
        }

        private void Update()
        {
            UpdateMicrophone();
            UpdateAmbience();
            if (Time.unscaledTime >= nextSpatialUpdate)
            {
                nextSpatialUpdate = Time.unscaledTime + 0.05f;
                UpdateSpeakerPositions();
            }
        }

        private void UpdateMicrophone()
        {
            bool talking = PushToTalkRequested();
            if (talking && !wasTalking && !permissionPending && !permissionDenied)
                StartCoroutine(AuthorizeAndRecord());
            if (!talking && microphoneClip != null) StopMicrophone();
            wasTalking = talking;
            if (!talking || microphoneClip == null) return;

            int position = Microphone.GetPosition(microphoneDevice);
            if (position <= 0 && Time.unscaledTime - microphoneStartedAt > 3f)
            {
                StopMicrophone();
                MicrophoneStatus = "Microphone unavailable; release V to retry";
                return;
            }
            if (position < 0) return;
            int available = (position - microphoneReadPosition + microphoneClip.samples) % microphoneClip.samples;
            // A long frame can cross the circular clip more than once. In that case,
            // discard the backlog and retain only one fresh packet.
            if (Time.unscaledTime - lastCapturePoll > 0.25f ||
                available > microphoneChunkFrames * MaximumCapturePackets)
            {
                available = Math.Min(available, microphoneChunkFrames);
                microphoneReadPosition = (position - available + microphoneClip.samples) % microphoneClip.samples;
            }
            lastCapturePoll = Time.unscaledTime;
            int processed = 0;
            while (available >= microphoneChunkFrames && processed++ < MaximumCapturePackets)
            {
                if (!microphoneClip.GetData(microphoneSamples, microphoneReadPosition))
                {
                    StopMicrophone();
                    MicrophoneStatus = "Microphone sample read failed; release V to retry";
                    return;
                }
                var packet = new byte[PacketSamples];
                int channels = microphoneClip.channels;
                for (int sample = 0; sample < PacketSamples; sample++)
                {
                    // Usually the microphone already records at 8 kHz mono. Average
                    // extra channels and resample if the device returns another rate.
                    float sourceFrame = sample * (microphoneChunkFrames / (float)PacketSamples);
                    int first = Math.Min((int)sourceFrame, microphoneChunkFrames - 1);
                    int second = Math.Min(first + 1, microphoneChunkFrames - 1);
                    float mix = sourceFrame - first;
                    float value = 0f;
                    for (int channel = 0; channel < channels; channel++)
                        value += Mathf.Lerp(microphoneSamples[first * channels + channel],
                            microphoneSamples[second * channels + channel], mix);
                    value /= channels;
                    packet[sample] = (byte)Mathf.Clamp(Mathf.RoundToInt(value * 127f + 128f), 0, 255);
                }
                microphoneReadPosition = (microphoneReadPosition + microphoneChunkFrames) % microphoneClip.samples;
                available -= microphoneChunkFrames;
                SendVoice?.Invoke(packet);
            }
        }

        private bool PushToTalkRequested()
        {
            return isActiveAndEnabled && Application.isFocused && LocalSlot >= 0 &&
                LocalSlot < speakers.Length && SendVoice != null && Input.GetKey(KeyCode.V);
        }

        private IEnumerator AuthorizeAndRecord()
        {
            permissionPending = true;
            MicrophoneStatus = "Checking microphone permission";
#if UNITY_WEBGL && !UNITY_EDITOR
            permissionDenied = true;
            permissionPending = false;
            MicrophoneStatus = "Voice capture requires a desktop build";
            yield break;
#else
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            permissionPending = false;
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                permissionDenied = true;
                MicrophoneStatus = "Microphone permission denied; enable it in system settings and restart";
                yield break;
            }
            if (!PushToTalkRequested())
            {
                MicrophoneStatus = "Ready — hold V to talk";
                yield break;
            }
            string[] devices = Microphone.devices;
            if (devices.Length == 0)
            {
                MicrophoneStatus = "No microphone detected; connect one and hold V";
                yield break;
            }
            microphoneDevice = devices[0];
            try
            {
                microphoneClip = Microphone.Start(microphoneDevice, true, 1, VoiceRate);
            }
            catch (Exception)
            {
                microphoneClip = null;
            }
            if (microphoneClip == null)
            {
                MicrophoneStatus = "Microphone unavailable; release V to retry";
                yield break;
            }
            microphoneChunkFrames = Math.Max(1, Mathf.RoundToInt(microphoneClip.frequency * 0.05f));
            int bufferLength = microphoneChunkFrames * microphoneClip.channels;
            if (microphoneSamples == null || microphoneSamples.Length != bufferLength)
                microphoneSamples = new float[bufferLength];
            microphoneReadPosition = 0;
            microphoneStartedAt = lastCapturePoll = Time.unscaledTime;
            MicrophoneStatus = "Transmitting — release V to stop";
#endif
        }

        private void StopMicrophone()
        {
            if (microphoneClip != null)
            {
                Microphone.End(microphoneDevice);
                Destroy(microphoneClip);
                microphoneClip = null;
                MicrophoneStatus = "Ready — hold V to talk";
            }
            microphoneReadPosition = 0;
        }

        private void UpdateSpeakerPositions()
        {
            if (listenerCamera == null || !listenerCamera.isActiveAndEnabled) listenerCamera = Camera.main;
            Transform listener = listenerCamera != null ? listenerCamera.transform : transform;
            for (int slot = 0; slot < speakers.Length; slot++)
            {
                RemoteSpeaker remote = speakers[slot];
                Transform speaker = FindSpeaker?.Invoke(slot);
                if (speaker == null || slot == LocalSlot)
                {
                    remote.Source.volume = 0f;
                    remote.Clear();
                    continue;
                }
                remote.Source.transform.position = speaker.position + Vector3.up * 1.45f;
                Vector3 toListener = listener.position - remote.Source.transform.position;
                bool occluded = IsOccluded != null && IsOccluded(listener.position, remote.Source.transform.position);
                float facing = toListener.sqrMagnitude > 0.001f
                    ? Vector3.Dot(speaker.forward, toListener.normalized) : 1f;
                float directionalVolume = Mathf.Lerp(0.4f, 1f, (facing + 1f) * 0.5f);
                remote.Source.volume = directionalVolume * (occluded ? 0.15f : 0.88f);
                remote.Filter.cutoffFrequency = occluded ? 650f : 2200f;
            }
        }

        private void UpdateAmbience()
        {
            float effort = Mathf.Clamp01(Exertion);
            float dose = Mathf.Clamp01(DoseFraction);
            breathing.pitch = 0.8f + effort * 0.9f + dose * 0.4f;
            breathing.volume = 0.10f + effort * 0.17f + dose * 0.11f;
            fan.pitch = 0.95f + effort * 0.15f;
            fan.volume = 0.055f + effort * 0.018f;
            if (VentilationRunning && !ventilation.isPlaying) ventilation.Play();
            if (!VentilationRunning && ventilation.isPlaying) ventilation.Stop();
            if (Alarm && !alarm.isPlaying) alarm.Play();
            if (!Alarm && alarm.isPlaying) alarm.Stop();

            float clicksPerSecond = 0.2f + 35f * Mathf.Sqrt(Mathf.Clamp01(Mathf.Max(0f, DoseRate)));
            // Integrate the current rate every frame: entering a hot zone does not
            // wait for a previously scheduled, low-dose interval to finish.
            clickWait -= clicksPerSecond * Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            int clicksThisFrame = 0;
            while (clickWait <= 0f && clicksThisFrame++ < 4)
            {
                geiger.PlayOneShot(synthClips[4]);
                // Exponentially distributed intervals produce distinct, irregular clicks.
                clickWait += -(float)Math.Log(Math.Max(0.0001, random.NextDouble()));
            }
            if (clickWait <= 0f) clickWait = 0.1f;
        }

        private AudioClip Synthesize(string clipName, float seconds, int kind)
        {
            int length = Mathf.RoundToInt(seconds * SynthesisRate);
            var samples = new float[length];
            float noise = 0f;
            for (int i = 0; i < length; i++)
            {
                float time = i / (float)SynthesisRate;
                float white = (float)random.NextDouble() * 2f - 1f;
                noise = noise * 0.88f + white * 0.12f;
                float sample;
                switch (kind)
                {
                    case 0:
                        sample = noise * 0.7f + Mathf.Sin(time * 2f * Mathf.PI * 115f) * 0.08f;
                        break;
                    case 1:
                        float phase = time / seconds;
                        float inhale = phase < 0.4f ? Mathf.Sin(phase / 0.4f * Mathf.PI) : 0f;
                        float exhale = phase > 0.49f && phase < 0.95f
                            ? Mathf.Sin((phase - 0.49f) / 0.46f * Mathf.PI) * 0.72f : 0f;
                        sample = (noise * 0.86f + white * 0.10f) * (inhale + exhale);
                        break;
                    case 2:
                        sample = Mathf.Sin(time * 2f * Mathf.PI * 50f) * 0.20f +
                            Mathf.Sin(time * 2f * Mathf.PI * 100f) * 0.08f + noise * 0.45f;
                        break;
                    case 3:
                        float pulse = time % 1.5f;
                        float envelope = pulse < 0.9f
                            ? Mathf.Min(Mathf.Min(pulse * 30f, (0.9f - pulse) * 30f), 1f) : 0f;
                        float frequency = pulse < 0.45f ? 620f : 830f;
                        sample = Mathf.Sin(time * 2f * Mathf.PI * frequency) * envelope * 0.48f;
                        break;
                    default:
                        sample = (white * 0.8f + Mathf.Sin(time * 2f * Mathf.PI * 2800f) * 0.2f) *
                            Mathf.Exp(-time * 260f);
                        break;
                }
                // Fade loop boundaries and click ends to avoid accidental edge pops.
                float fade = Mathf.Min(1f, Mathf.Min(i / 120f, (length - 1 - i) / 120f));
                samples[i] = sample * fade;
            }
            AudioClip clip = AudioClip.Create(clipName, length, 1, SynthesisRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                StopMicrophone();
                wasTalking = false;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                StopMicrophone();
                wasTalking = false;
            }
        }

        private void OnDisable()
        {
            acceptingVoice = false;
            StopAllCoroutines();
            permissionPending = false;
            wasTalking = false;
            StopMicrophone();
            foreach (RemoteSpeaker remote in speakers)
            {
                if (remote == null) continue;
                remote.SetActive(false);
                if (remote.Source != null) remote.Source.Stop();
            }
            if (fan != null) fan.Stop();
            if (breathing != null) breathing.Stop();
            if (ventilation != null) ventilation.Stop();
            if (alarm != null) alarm.Stop();
            if (geiger != null) geiger.Stop();
        }

        private void OnDestroy()
        {
            acceptingVoice = false;
            StopMicrophone();
            foreach (RemoteSpeaker remote in speakers)
            {
                if (remote == null) continue;
                remote.Clear(true);
                if (remote.Clip != null) Destroy(remote.Clip);
                if (remote.Source != null) Destroy(remote.Source.gameObject);
            }
            foreach (AudioClip clip in synthClips)
                if (clip != null) Destroy(clip);
            if (fan != null) Destroy(fan.gameObject);
            if (breathing != null) Destroy(breathing.gameObject);
            if (ventilation != null) Destroy(ventilation.gameObject);
            if (alarm != null) Destroy(alarm.gameObject);
            if (geiger != null) Destroy(geiger.gameObject);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AiMusicWorkstation.Desktop.Services
{
    public class StemPlayer : IDisposable
    {
        private int _semitoneShift;
        public int SemitoneShift
        {
            get => _semitoneShift;
            set
            {
                _semitoneShift = value;
                // Rebuild chain with new pitch if something is loaded
                if (Channels.Count > 0 && CurrentStemsPath != null)
                {
                    bool wasPlaying = IsPlaying;
                    TimeSpan position = CurrentTime;
                    RebuildWithPitch(position, wasPlaying);
                }
            }
        }

        private bool _isLooping;
        public bool IsLooping
        {
            get => _isLooping;
            set
            {
                _isLooping = value;
                foreach (var ch in Channels.Values)
                {
                    if (ch.Looper != null)
                        ch.Looper.EnableLooping = value;
                }
            }
        }

        public WaveOutEvent? OutputDevice { get; private set; }
        public MixingSampleProvider? Mixer { get; private set; }
        private readonly Dictionary<string, StemChannel> Channels = new();

        private void RebuildWithPitch(TimeSpan seekTo, bool autoPlay)
        {
            // Save volume state
            var volumes = Channels.ToDictionary(k => k.Key, v => v.Value.UserVolume);
            var mutes = Channels.ToDictionary(k => k.Key, v => v.Value.IsMuted);
            var solos = Channels.ToDictionary(k => k.Key, v => v.Value.IsSolo);

            // Stop and clear old streams
            if (OutputDevice != null) { OutputDevice.Stop(); OutputDevice.Dispose(); OutputDevice = null; }
            foreach (StemChannel c in Channels.Values)
            {
                c.Reader?.Dispose();
            }
            Channels.Clear();

            // Rebuild with pitch-shift
            float pitchFactor = (float)Math.Pow(2.0, _semitoneShift / 12.0);
            var sources = new List<ISampleProvider>();
            if (CurrentStemsPath == null) return;
            bool isFile = File.Exists(CurrentStemsPath);
            string[] stemNames = isFile ? new[] { "backing" } : new[] { "drums", "bass", "vocals", "other", "mp3", "wav" };

            foreach (string stem in stemNames)
            {
                string? p = isFile
                    ? CurrentStemsPath
                    : Path.Combine(CurrentStemsPath, $"{stem}.mp3");

                if (!isFile && !File.Exists(p))
                {
                    p = Path.Combine(CurrentStemsPath, $"{stem}.wav");
                }

                if (File.Exists(p))
                {
                    var reader = new AudioFileReader(p)
                    {
                        CurrentTime = seekTo
                    };

                    var looper = new LoopStream(reader)
                    {
                        EnableLooping = _isLooping
                    };

                    var channel = new StemChannel
                    {
                        Reader = reader,
                        Looper = looper,
                        UserVolume = volumes.TryGetValue(stem, out float value) ? value : (isFile ? 1.0f : 0.8f),
                        IsMuted = mutes.ContainsKey(stem) && mutes[stem],
                        IsSolo = solos.ContainsKey(stem) && solos[stem]
                    };
                    Channels.Add(stem, channel);

                    ISampleProvider provider = looper.ToSampleProvider();

                    // Apply pitch-shift (skip if factor = 1.0 to save CPU)
                    if (Math.Abs(_semitoneShift) > 0)
                    {
                        provider = new SmbPitchShiftingSampleProvider(provider) { PitchFactor = pitchFactor };
                    }

                    sources.Add(provider);
                }
            }

            if (sources.Count == 0)
            {
                return;
            }

            Mixer = new MixingSampleProvider(sources);
            OutputDevice = new WaveOutEvent();
            OutputDevice.Init(Mixer);
            OutputDevice.PlaybackStopped += (s, e) => PlaybackStopped?.Invoke(this, EventArgs.Empty);

            UpdateMix();
            if (autoPlay)
            {
                OutputDevice.Play();
            }
        }

        public string? CurrentStemsPath { get; private set; }

        // This property is used by MainWindow to know if sliders should be shown
        public bool IsSingleFileMode => Channels.ContainsKey("backing") && !Channels.ContainsKey("drums");

        public event EventHandler? PlaybackStopped;
        public bool IsPlaying => OutputDevice?.PlaybackState == PlaybackState.Playing;

        private class StemChannel
        {
            public AudioFileReader? Reader { get; set; }
            public LoopStream? Looper { get; set; }
            public float UserVolume { get; set; } = 0.8f;
            public bool IsMuted { get; set; }
            public bool IsSolo { get; set; }
        }

        public TimeSpan CurrentTime
        {
            get => Channels.Count > 0 && Channels.Values.First().Reader != null ? Channels.Values.First().Reader!.CurrentTime : TimeSpan.Zero;
            set
            {
                foreach (var ch in Channels.Values)
                {
                    if (ch.Reader == null) continue;
                    var t = value;
                    if (t >= ch.Reader.TotalTime)
                    {
                        t = ch.Reader.TotalTime - TimeSpan.FromMilliseconds(1);
                    }

                    if (t < TimeSpan.Zero)
                    {
                        t = TimeSpan.Zero;
                    }

                    ch.Reader.CurrentTime = t;
                }
            }
        }

        public TimeSpan TotalTime => Channels.Count > 0 && Channels.Values.First().Reader != null ? Channels.Values.First().Reader!.TotalTime : TimeSpan.Zero;

        public void LoadStems(string pathInput)
        {
            CurrentStemsPath = pathInput;
            DisposeOldStreams();

            var sources = new List<ISampleProvider>();
            bool isFile = File.Exists(pathInput);

            if (isFile)
            {
                // LOAD SINGLE FILE (Backing/Original)
                try
                {
                    var reader = new AudioFileReader(pathInput);
                    var looper = new LoopStream(reader)
                    {
                        EnableLooping = _isLooping
                    };

                    var channel = new StemChannel { Reader = reader, Looper = looper, UserVolume = 1.0f };
                    Channels.Add("backing", channel);
                    sources.Add(looper.ToSampleProvider());
                }
                catch (Exception ex) { System.Windows.MessageBox.Show("Error loading audio: " + ex.Message); return; }
            }
            else
            {
                // LOAD STEMS FOLDER
                string[] stems = { "drums", "bass", "vocals", "other" };
                foreach (string stem in stems)
                {
                    string p = Path.Combine(pathInput, $"{stem}.mp3");
                    if (!File.Exists(p))
                    {
                        p = Path.Combine(pathInput, $"{stem}.wav");
                    }

                    if (File.Exists(p))
                    {
                        var reader = new AudioFileReader(p);
                        var looper = new LoopStream(reader)
                        {
                            EnableLooping = _isLooping
                        };
                        var channel = new StemChannel { Reader = reader, Looper = looper };
                        Channels.Add(stem, channel);
                        sources.Add(looper.ToSampleProvider());
                    }
                }
            }

            if (sources.Count == 0)
            {
                return;
            }

            Mixer = new MixingSampleProvider(sources);
            OutputDevice = new WaveOutEvent();
            OutputDevice.Init(Mixer);
            OutputDevice.PlaybackStopped += (s, e) => PlaybackStopped?.Invoke(this, EventArgs.Empty);

            UpdateMix(); // Set initial volumes
        }

        // --- EXPORT ---
        public void ExportMix(string outputPath, float drumsVol, float bassVol, float otherVol, float vocalsVol)
        {
            if (string.IsNullOrEmpty(CurrentStemsPath))
            {
                return;
            }

            // We force the mixer to run at 44.1kHz Stereo
            var mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            var readers = new List<AudioFileReader>();

            try
            {
                string[] stems = { "drums", "bass", "other", "vocals" };
                float[] volumes = { drumsVol, bassVol, otherVol, vocalsVol };

                for (int i = 0; i < stems.Length; i++)
                {
                    string p = Path.Combine(CurrentStemsPath, $"{stems[i]}.mp3");
                    if (!File.Exists(p))
                    {
                        p = Path.Combine(CurrentStemsPath, $"{stems[i]}.wav");
                    }

                    if (File.Exists(p))
                    {
                        var reader = new AudioFileReader(p)
                        {
                            Volume = volumes[i]
                        };
                        readers.Add(reader);

                        // SOLUTION: We call ToSampleProvider() to remove the ambiguity
                        mixer.AddMixerInput(reader.ToSampleProvider());
                    }
                }

                // Save the mixed file
                WaveFileWriter.CreateWaveFile16(outputPath, mixer);
            }
            finally
            {
                foreach (var r in readers)
                {
                    r.Dispose();
                }
            }
        }

        public void Play()
        {
            if (OutputDevice?.PlaybackState != PlaybackState.Playing)
            {
                OutputDevice?.Play();
            }
        }
        public void Pause()
        {
            OutputDevice?.Pause();
        }

        public void Stop()
        {
            OutputDevice?.Stop();
            ResetPosition();
        }

        public void ResetPosition()
        {
            foreach (var ch in Channels.Values)
            {
                if (ch.Reader != null)
                    ch.Reader.CurrentTime = TimeSpan.Zero;
            }
        }

        public void Reinitialize()
        {
            if (OutputDevice == null || Channels.Count == 0)
            {
                return;
            }

            var sources = new List<ISampleProvider>();
            float masterVol = OutputDevice.Volume;

            OutputDevice.Stop();
            OutputDevice.Dispose();

            float pitchFactor = (float)Math.Pow(2.0, _semitoneShift / 12.0);

            foreach (var kvp in Channels)
            {
                if (kvp.Value.Reader == null || kvp.Value.Looper == null) continue;
                kvp.Value.Reader.CurrentTime = TimeSpan.Zero;
                ISampleProvider provider = kvp.Value.Looper.ToSampleProvider();
                if (Math.Abs(_semitoneShift) > 0)
                {
                    provider = new SmbPitchShiftingSampleProvider(provider) { PitchFactor = pitchFactor };
                }

                sources.Add(provider);
            }

            Mixer = new MixingSampleProvider(sources);
            OutputDevice = new WaveOutEvent
            {
                Volume = masterVol
            };
            OutputDevice.Init(Mixer);
            OutputDevice.PlaybackStopped += (s, e) => PlaybackStopped?.Invoke(this, EventArgs.Empty);

            UpdateMix();
        }

        public void RestartAndPlay()
        {
            if (string.IsNullOrEmpty(CurrentStemsPath))
            {
                return;
            }

            var volumes = Channels.ToDictionary(k => k.Key, v => v.Value.UserVolume);
            var mutes = Channels.ToDictionary(k => k.Key, v => v.Value.IsMuted);
            var solos = Channels.ToDictionary(k => k.Key, v => v.Value.IsSolo);
            float masterVol = OutputDevice?.Volume ?? 1f;
            bool wasLooping = _isLooping;

            DisposeOldStreams();
            _isLooping = wasLooping;

            var sources = new List<ISampleProvider>();
            bool isFile = File.Exists(CurrentStemsPath);
            string[] stems = isFile ? new[] { "backing" } : new[] { "drums", "bass", "vocals", "other" };

            foreach (string stem in stems)
            {
                string p = isFile ? CurrentStemsPath : Path.Combine(CurrentStemsPath, $"{stem}.mp3");
                if (!isFile && !File.Exists(p))
                {
                    p = Path.Combine(CurrentStemsPath, $"{stem}.wav");
                }

                if (File.Exists(p))
                {
                    var reader = new AudioFileReader(p)
                    {
                        CurrentTime = TimeSpan.Zero
                    };
                    var looper = new LoopStream(reader) { EnableLooping = _isLooping };
                    var channel = new StemChannel
                    {
                        Reader = reader,
                        Looper = looper,
                        UserVolume = volumes.TryGetValue(stem, out float value) ? value : (isFile ? 1.0f : 0.8f),
                        IsMuted = mutes.ContainsKey(stem) && mutes[stem],
                        IsSolo = solos.ContainsKey(stem) && solos[stem]
                    };
                    Channels.Add(stem, channel);
                    sources.Add(looper.ToSampleProvider());
                }
            }

            if (sources.Count == 0)
            {
                return;
            }

            Mixer = new MixingSampleProvider(sources);
            OutputDevice = new WaveOutEvent
            {
                Volume = masterVol
            };
            OutputDevice.Init(Mixer);
            OutputDevice.PlaybackStopped += (s, e) => PlaybackStopped?.Invoke(this, EventArgs.Empty);
            UpdateMix();
            OutputDevice.Play();
        }

        public void SetMasterVolume(float volume)
        {
            if(OutputDevice != null)
                OutputDevice.Volume = Math.Clamp(volume, 0, 1);
        }

        public void SetVolume(string name, float volume)
        {
            if (Channels.TryGetValue(name, out var value))
            {
                value.UserVolume = volume;
                UpdateMix();
            }
        }

        public void SetMute(string name, bool isMuted)
        {
            if (Channels.TryGetValue(name, out var value))
            {
                value.IsMuted = isMuted;
                UpdateMix();
            }
        }

        public void SetSolo(string name, bool isSolo)
        {
            if (Channels.TryGetValue(name, out var value))
            {
                value.IsSolo = isSolo;
                UpdateMix();
            }
        }

        private void UpdateMix()
        {
            bool anySolo = Channels.Values.Any(static c => c.IsSolo);
            foreach (var ch in Channels.Values)
            {
                if (ch.Reader == null) continue;
                float finalVolume = ch.UserVolume;
                if (anySolo && !ch.IsSolo)
                {
                    finalVolume = 0;
                }

                if (ch.IsMuted)
                {
                    finalVolume = 0;
                }

                ch.Reader.Volume = finalVolume;
            }
        }

        private void DisposeOldStreams()
        {
            if (OutputDevice != null) { OutputDevice.Stop(); OutputDevice.Dispose(); OutputDevice = null; }
            foreach (var c in Channels.Values) { c.Reader?.Dispose(); }
            Channels.Clear();
        }

        public void Dispose()
        {
            DisposeOldStreams();
            GC.SuppressFinalize(this);
        }
    }
}

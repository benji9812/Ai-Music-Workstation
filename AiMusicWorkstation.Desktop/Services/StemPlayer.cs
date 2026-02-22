using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AiMusicWorkstation.Desktop.Services
{
    public class StemPlayer : IDisposable
    {
        private int _semitoneShift = 0;
        public int SemitoneShift
        {
            get => _semitoneShift;
            set
            {
                _semitoneShift = value;
                // Rebuild chain med ny pitch om något är laddat
                if (_channels.Count > 0 && CurrentStemsPath != null)
                {
                    bool wasPlaying = IsPlaying;
                    TimeSpan position = CurrentTime;
                    RebuildWithPitch(position, wasPlaying);
                }
            }
        }

        private void RebuildWithPitch(TimeSpan seekTo, bool autoPlay)
        {
            // Spara volym-state
            var volumes = _channels.ToDictionary(k => k.Key, v => v.Value.UserVolume);
            var mutes = _channels.ToDictionary(k => k.Key, v => v.Value.IsMuted);
            var solos = _channels.ToDictionary(k => k.Key, v => v.Value.IsSolo);

            // Stoppa och rensa gamla streams
            if (_outputDevice != null) { _outputDevice.Stop(); _outputDevice.Dispose(); _outputDevice = null; }
            foreach (var c in _channels.Values) c.Reader.Dispose();
            _channels.Clear();

            // Bygg om med pitch-shift
            float pitchFactor = (float)Math.Pow(2.0, _semitoneShift / 12.0);
            var sources = new List<ISampleProvider>();
            bool isFile = File.Exists(CurrentStemsPath);
            string[] stemNames = isFile ? new[] { "backing" } : new[] { "drums", "bass", "vocals", "other" };

            foreach (var stem in stemNames)
            {
                string p = isFile
                    ? CurrentStemsPath
                    : Path.Combine(CurrentStemsPath, $"{stem}.mp3");

                if (!isFile && !File.Exists(p))
                    p = Path.Combine(CurrentStemsPath, $"{stem}.wav");

                if (File.Exists(p))
                {
                    var reader = new AudioFileReader(p);
                    reader.CurrentTime = seekTo;

                    var looper = new LoopStream(reader);
                    looper.EnableLooping = IsLooping;

                    var channel = new StemChannel
                    {
                        Reader = reader,
                        Looper = looper,
                        UserVolume = volumes.ContainsKey(stem) ? volumes[stem] : (isFile ? 1.0f : 0.8f),
                        IsMuted = mutes.ContainsKey(stem) && mutes[stem],
                        IsSolo = solos.ContainsKey(stem) && solos[stem]
                    };
                    _channels.Add(stem, channel);

                    ISampleProvider provider = looper.ToSampleProvider();

                    // Applicera pitch-shift (hoppa över om faktor = 1.0 för att spara CPU)
                    if (Math.Abs(_semitoneShift) > 0)
                        provider = new SmbPitchShiftingSampleProvider(provider) { PitchFactor = pitchFactor };

                    sources.Add(provider);
                }
            }

            if (sources.Count == 0) return;

            _mixer = new MixingSampleProvider(sources);
            _outputDevice = new WaveOutEvent();
            _outputDevice.Init(_mixer);
            _outputDevice.PlaybackStopped += (s, e) => PlaybackStopped?.Invoke(this, EventArgs.Empty);

            UpdateMix();
            if (autoPlay) _outputDevice.Play();
        }

        private WaveOutEvent _outputDevice;
        private MixingSampleProvider _mixer;

        public string CurrentStemsPath { get; private set; }

        // Denna egenskap används av MainWindow för att veta om sliders ska visas
        public bool IsSingleFileMode => _channels.ContainsKey("backing") && !_channels.ContainsKey("drums");

        public event EventHandler PlaybackStopped;
        public bool IsPlaying => _outputDevice?.PlaybackState == PlaybackState.Playing;

        private bool _isLooping = false;
        public bool IsLooping
        {
            get => _isLooping;
            set
            {
                _isLooping = value;
                foreach (var ch in _channels.Values)
                {
                    if (ch.Looper != null) ch.Looper.EnableLooping = value;
                }
            }
        }

        private class StemChannel
        {
            public AudioFileReader Reader { get; set; }
            public LoopStream Looper { get; set; }
            public float UserVolume { get; set; } = 0.8f;
            public bool IsMuted { get; set; } = false;
            public bool IsSolo { get; set; } = false;
        }

        private Dictionary<string, StemChannel> _channels = new Dictionary<string, StemChannel>();

        public TimeSpan CurrentTime
        {
            get => _channels.Count > 0 ? _channels.Values.First().Reader.CurrentTime : TimeSpan.Zero;
            set
            {
                foreach (var ch in _channels.Values)
                {
                    TimeSpan t = value;
                    if (t >= ch.Reader.TotalTime) t = ch.Reader.TotalTime - TimeSpan.FromMilliseconds(1);
                    if (t < TimeSpan.Zero) t = TimeSpan.Zero;
                    ch.Reader.CurrentTime = t;
                }
            }
        }

        public TimeSpan TotalTime => _channels.Count > 0 ? _channels.Values.First().Reader.TotalTime : TimeSpan.Zero;

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
                    var looper = new LoopStream(reader);
                    looper.EnableLooping = IsLooping;

                    var channel = new StemChannel { Reader = reader, Looper = looper, UserVolume = 1.0f };
                    _channels.Add("backing", channel);
                    sources.Add(looper.ToSampleProvider());
                }
                catch (Exception ex) { System.Windows.MessageBox.Show("Error loading audio: " + ex.Message); return; }
            }
            else
            {
                // LOAD STEMS FOLDER
                string[] stems = { "drums", "bass", "vocals", "other" };
                foreach (var stem in stems)
                {
                    string p = Path.Combine(pathInput, $"{stem}.mp3");
                    if (!File.Exists(p)) p = Path.Combine(pathInput, $"{stem}.wav");

                    if (File.Exists(p))
                    {
                        var reader = new AudioFileReader(p);
                        var looper = new LoopStream(reader);
                        looper.EnableLooping = IsLooping;
                        var channel = new StemChannel { Reader = reader, Looper = looper };
                        _channels.Add(stem, channel);
                        sources.Add(looper.ToSampleProvider());
                    }
                }
            }

            if (sources.Count == 0) return;

            _mixer = new MixingSampleProvider(sources);
            _outputDevice = new WaveOutEvent();
            _outputDevice.Init(_mixer);
            _outputDevice.PlaybackStopped += (s, e) => PlaybackStopped?.Invoke(this, EventArgs.Empty);

            UpdateMix(); // Sätt initiala volymer
        }

        // --- EXPORT ---
        public void ExportMix(string outputPath, float drumsVol, float bassVol, float otherVol, float vocalsVol)
        {
            if (string.IsNullOrEmpty(CurrentStemsPath)) return;

            // Vi tvingar mixern att köra i 44.1kHz Stereo
            var mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            var readers = new List<AudioFileReader>();

            try
            {
                string[] stems = { "drums", "bass", "other", "vocals" };
                float[] volumes = { drumsVol, bassVol, otherVol, vocalsVol };

                for (int i = 0; i < stems.Length; i++)
                {
                    string p = Path.Combine(CurrentStemsPath, $"{stems[i]}.mp3");
                    if (!File.Exists(p)) p = Path.Combine(CurrentStemsPath, $"{stems[i]}.wav");

                    if (File.Exists(p))
                    {
                        var reader = new AudioFileReader(p);
                        reader.Volume = volumes[i];
                        readers.Add(reader);

                        // LÖSNING: Vi anropar ToSampleProvider() för att ta bort tvetydigheten
                        mixer.AddMixerInput(reader.ToSampleProvider());
                    }
                }

                // Spara den mixade filen
                WaveFileWriter.CreateWaveFile16(outputPath, mixer);
            }
            finally
            {
                foreach (var r in readers) r.Dispose();
            }
        }

        public void Play() { if (_outputDevice?.PlaybackState != PlaybackState.Playing) _outputDevice?.Play(); }
        public void Pause() => _outputDevice?.Pause();

        public void Stop()
        {
            _outputDevice?.Stop();
            foreach (var ch in _channels.Values) ch.Reader.Position = 0;
        }

        public void SetMasterVolume(float volume)
        {
            if (_outputDevice != null) _outputDevice.Volume = Math.Clamp(volume, 0, 1);
        }

        public void SetVolume(string name, float volume)
        {
            if (_channels.ContainsKey(name))
            {
                _channels[name].UserVolume = volume;
                UpdateMix();
            }
        }

        public void SetMute(string name, bool isMuted)
        {
            if (_channels.ContainsKey(name))
            {
                _channels[name].IsMuted = isMuted;
                UpdateMix();
            }
        }

        public void SetSolo(string name, bool isSolo)
        {
            if (_channels.ContainsKey(name))
            {
                _channels[name].IsSolo = isSolo;
                UpdateMix();
            }
        }

        private void UpdateMix()
        {
            bool anySolo = _channels.Values.Any(c => c.IsSolo);
            foreach (var ch in _channels.Values)
            {
                float finalVolume = ch.UserVolume;
                if (anySolo && !ch.IsSolo) finalVolume = 0;
                if (ch.IsMuted) finalVolume = 0;
                ch.Reader.Volume = finalVolume;
            }
        }

        private void DisposeOldStreams()
        {
            if (_outputDevice != null) { _outputDevice.Stop(); _outputDevice.Dispose(); _outputDevice = null; }
            foreach (var c in _channels.Values) { c.Reader.Dispose(); }
            _channels.Clear();
        }

        public void Dispose() => DisposeOldStreams();
    }
}
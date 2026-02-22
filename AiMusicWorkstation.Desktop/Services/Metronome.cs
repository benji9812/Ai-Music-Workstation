using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace AiMusicWorkstation.Desktop.Services
{
    public class Metronome : IDisposable
    {
        private CancellationTokenSource _cts;
        private bool _isRunning;

        private WaveOutEvent _output;
        private BufferedWaveProvider _buffer;

        public bool IsRunning => _isRunning;
        public int TimeSignature { get; set; } = 4;
        public int CountInBars { get; set; } = 1;

        public event Action<int> OnBeat;
        public event Action OnCountInComplete;

        public Metronome()
        {
            _buffer = new BufferedWaveProvider(
                WaveFormat.CreateIeeeFloatWaveFormat(44100, 1))
            {
                DiscardOnBufferOverflow = true,
                BufferLength = 44100
            };
            _output = new WaveOutEvent();
            _output.Init(_buffer);
            _output.Play();
        }

        public void ResetEvents()
        {
            OnBeat = null;
            OnCountInComplete = null;
        }

        public void Start(double bpm, bool withCountIn = false)
        {
            Stop();
            _isRunning = true;
            _cts = new CancellationTokenSource();
            Task.Run(() => TickLoop(bpm, withCountIn, _cts.Token));
        }

        public void Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
        }

        private async Task TickLoop(double bpm, bool withCountIn, CancellationToken token)
        {
            double intervalMs = 60000.0 / bpm;
            int beat = 1;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            double nextTick = 0;

            if (withCountIn)
            {
                int countInBeats = TimeSignature * CountInBars;
                for (int i = 0; i < countInBeats; i++)
                {
                    if (token.IsCancellationRequested) return;
                    PlayClick(i % TimeSignature == 0);
                    OnBeat?.Invoke(i % TimeSignature + 1);
                    nextTick += intervalMs;
                    int wait = (int)(nextTick - sw.Elapsed.TotalMilliseconds);
                    if (wait > 0)
                        await Task.Delay(wait, token).ContinueWith(_ => { });
                }
                OnCountInComplete?.Invoke();
            }

            while (!token.IsCancellationRequested)
            {
                PlayClick(beat == 1);
                OnBeat?.Invoke(beat);
                beat = beat % TimeSignature + 1;
                nextTick += intervalMs;
                int wait = (int)(nextTick - sw.Elapsed.TotalMilliseconds);
                if (wait > 0)
                    await Task.Delay(wait, token).ContinueWith(_ => { });
            }
        }


        private void PlayClick(bool isAccent = false)
        {
            try
            {
                float freq = isAccent ? 1800f : 1200f;
                int sampleRate = 44100;
                int durationMs = 12;
                int samples = sampleRate * durationMs / 1000;

                var floatBuffer = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / sampleRate;
                    float envelope = (float)Math.Exp(-t * 200);
                    floatBuffer[i] = (float)(Math.Sin(2 * Math.PI * freq * t) * envelope * 0.8f);
                }

                byte[] bytes = new byte[samples * 4];
                Buffer.BlockCopy(floatBuffer, 0, bytes, 0, bytes.Length);
                _buffer.AddSamples(bytes, 0, bytes.Length);
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            _output?.Stop();
            _output?.Dispose();
        }
    }
}

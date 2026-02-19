using NAudio.Wave;
using System;

namespace AiMusicWorkstation.Desktop.Services
{
    /// <summary>
    /// En wrapper runt en ljudström som spolar tillbaka automatiskt när den tar slut.
    /// </summary>
    public class LoopStream : WaveStream
    {
        private WaveStream sourceStream;

        public LoopStream(WaveStream sourceStream)
        {
            this.sourceStream = sourceStream;
            this.EnableLooping = false;
        }

        public bool EnableLooping { get; set; }

        public override WaveFormat WaveFormat => sourceStream.WaveFormat;
        public override long Length => sourceStream.Length;
        public override long Position
        {
            get => sourceStream.Position;
            set => sourceStream.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalBytesRead = 0;

            while (totalBytesRead < count)
            {
                int bytesRead = sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);

                if (bytesRead == 0)
                {
                    if (sourceStream.Position == 0 || !EnableLooping)
                    {
                        // Nådde slutet och looping är AV (eller filen är tom) -> Sluta läs
                        break;
                    }

                    // Nådde slutet och looping är PÅ -> Spola tillbaka!
                    sourceStream.Position = 0;
                }
                totalBytesRead += bytesRead;
            }
            return totalBytesRead;
        }
    }
}
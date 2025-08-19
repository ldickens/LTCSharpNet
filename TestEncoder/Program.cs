using NAudio;
using NAudio.Wave;
using System.Diagnostics;

namespace TestEncoder
{
    class CircularBuffer
    {
        private readonly byte[] buffer;

        private readonly object lockObject;

        private int writePosition;

        private int readPosition;

        private int byteCount;

        public int MaxLength => buffer.Length;

        public int Count
        {
            get
            {
                lock (lockObject)
                {
                    return byteCount;
                }
            }
        }

        public int Write(byte[] data, int offset, int count)
        {
            lock(lockObject)
            {
                int num = 0;
                if (count > buffer.Length - byteCount)
                {
                    count = buffer.Length - byteCount;
                }

                int num2 = Math.Min(buffer.Length - writePosition, count);
                Array.Copy(data, offset, buffer, writePosition, count);
                writePosition += num2;
                writePosition %= buffer.Length; // Not sure why this is here apart from extra protection could test to see impact.

                if (count > num2)
            }
        }
    }

    class Source : IWaveProvider
    {
        LTCSharpNet.Encoder FEncoder;

        public Source()
        {
            FEncoder = new LTCSharpNet.Encoder(48000, 30, LTCSharpNet.TVStandard.TV525_60i, LTCSharpNet.BGFlags.NONE);
        }



        public int Read(byte[] buffer, int offset, int count)
        {
            lock (FEncoder)
            {
                Console.Write("bip");
                FEncoder.encodeFrame();
                int size = FEncoder.getBuffer(buffer, offset);
                Console.WriteLine(size);
                return size;
            }
        }

        public LTCSharpNet.Encoder Encoder
        {
            get
            {
                return this.FEncoder;
            }
        }

        public WaveFormat WaveFormat
        {
            get
            {
                return new WaveFormat(48000, 1);
            }
        }

        public void SetTimecode(LTCSharpNet.Timecode timecode)
        {
            lock (FEncoder)
            {
                FEncoder.setTimecode(timecode);
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var waveOut = new WaveOut();
            var encoder = new Source();
            //Console.WriteLine(WaveOut.GetCapabilities(0));
            //Console.ReadLine();
            waveOut.Init(encoder);
            waveOut.Play();

            Stopwatch timer = new Stopwatch();
            timer.Start();

            while (timer.Elapsed < new TimeSpan(1, 0, 0))
            {
                encoder.SetTimecode(new LTCSharpNet.Timecode(
                    timer.Elapsed.Hours,
                    timer.Elapsed.Minutes,
                    timer.Elapsed.Seconds,
                    (int) ((float)timer.Elapsed.Milliseconds / 1000.0f * 30.0f)));
                Thread.Sleep(1/30);
            }
            timer.Stop();

            waveOut.Stop();
            waveOut.Dispose();
        }
    }
}

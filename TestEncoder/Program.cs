using NAudio;
using NAudio.Wave;
using System.Diagnostics;

namespace TestEncoder
{
    //class CircularBuffer
    //{
    //    private readonly byte[] buffer;

    //    private readonly object lockObject;

    //    private int writePosition;

    //    private int readPosition;

    //    private int byteCount;

    //    public int MaxLength => buffer.Length;

    //    public int Count
    //    {
    //        get
    //        {
    //            lock (lockObject)
    //            {
    //                return byteCount;
    //            }
    //        }
    //    }

    //    public int Write(byte[] data, int offset, int count)
    //    {
    //        lock(lockObject)
    //        {
    //            int num = 0;
    //            if (count > buffer.Length - byteCount)
    //            {
    //                count = buffer.Length - byteCount;
    //            }

    //            int num2 = Math.Min(buffer.Length - writePosition, count);
    //            Array.Copy(data, offset, buffer, writePosition, count);
    //            writePosition += num2;
    //            writePosition %= buffer.Length; // Not sure why this is here apart from extra protection could test to see impact.

    //            if (count > num2)
    //        }
    //    }
    //}

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
                FEncoder.incrementFrame();
                FEncoder.encodeFrame();
                int size = FEncoder.getBuffer(buffer, offset);
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

        public void SetBufferSize(int framesBuffered, double sampleRate, double fps)
        {
            lock (FEncoder)
            {
                FEncoder.setBufferSize(sampleRate, fps / framesBuffered);
            }
        }

        public void IncrementFrame()
        {
            lock (FEncoder)
            {
                FEncoder.incrementFrame();
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var waveOut = new WaveOut();
            var encoder = new Source();
            //int bufferedFrames = 4;
            double fps = 30d;


            //encoder.SetBufferSize(bufferedFrames, encoder.WaveFormat.SampleRate, fps);
            waveOut.DesiredLatency = 40;
            waveOut.Init(encoder);
            waveOut.Play();

            Stopwatch timer = new Stopwatch();
            var time = new LTCSharpNet.Timecode(
                timer.Elapsed.Hours,
                timer.Elapsed.Minutes,
                timer.Elapsed.Seconds,
                (int)((float)timer.Elapsed.Milliseconds / 1000.0f * fps));
            encoder.SetTimecode(time);

            while (true)
            {
            }
            timer.Stop();

            waveOut.Stop();
            waveOut.Dispose();
        }
    }
}

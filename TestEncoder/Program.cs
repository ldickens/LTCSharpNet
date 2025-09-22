using NAudio;
using NAudio.Utils;
using NAudio.Wave;
using System.Collections.Immutable;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;

namespace TestEncoder
{

    class Source : IWaveProvider
    {
        LTCSharpNet.Encoder m_encoder;
        string m_currentTC = "";
        //private readonly object m_lock = new();
        bool m_encoding = false;
        long m_fps = 0;
        long m_frametime = 0;
        Stopwatch m_timer = new();
        //Stopwatch m_timer2 = new();
        private const int m_bufSizeSeconds = 2;
        int m_bufSize; // number of frame stored in buffer
        TCFrameBufferManager m_bufferManager;

        public Source(long fps)
        {
            m_encoder = new LTCSharpNet.Encoder(48000, fps, LTCSharpNet.TVStandard.TV525_60i, LTCSharpNet.BGFlags.NONE);
            m_fps = fps;
            m_bufSize = (int)fps * m_bufSizeSeconds; // 
            m_frametime = (TimeSpan.TicksPerSecond / m_fps);
            m_bufferManager = new TCFrameBufferManager(m_bufSize);
        }

        public void StartEncoding()
        {
            byte[] tmp = new byte[1600];
            m_encoder.getBuffer(tmp, 0);
            m_circularBuffer.Write(tmp, 0, tmp.Length);
            m_timer.Start();


            _ = Task.Run(() =>
            {

                int now = m_timer.Elapsed.Milliseconds;
                int lastFrame = m_timer.Elapsed.Milliseconds;

                while (true)
                {
                    now = m_timer.Elapsed.Milliseconds;
                    int delta = now - lastFrame;
                    lastFrame = now;

                    if (m_timer.ElapsedTicks > m_frametime)
                    {

                        m_timer.Restart();
                        m_encoder.incrementFrame();
                        m_encoder.encodeFrame();

                        m_encoder.getBuffer(tmp, 0);
                        m_circularBuffer.Reset();
                        //Console.WriteLine("UpdatedFrame");
                    }
                    else if (delta < 33)
                    {
                        Thread.Sleep(33 - delta);
                    }
                    m_circularBuffer.Write(tmp, 0, tmp.Length);
                }
            });
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            //Console.WriteLine($"Took this long to start getting new frame {m_timer}");
            //Console.WriteLine(m_currentTC);
            //Console.WriteLine(m_timer2.ToString());
            int size = m_circularBuffer.Read(buffer, offset, count);
            //Console.WriteLine($"Request buffer size = {count}");
            //Console.WriteLine($"Remaining circ Buffer = {m_circularBuffer.Count}");

            if (size == 0) { Console.WriteLine($"BAD: bufSize = {m_circularBuffer.Count}"); }

            return size;
        }

        public LTCSharpNet.Encoder Encoder
        {
            get
            {
                return this.m_encoder;
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
            m_encoder.setTimecode(timecode);
            m_currentTC = new LTCSharpNet.Timecode().ToString();
        }

        public void SetBufferSize(int framesBuffered, double sampleRate, double fps)
        {
            m_encoder.setBufferSize(sampleRate, fps / framesBuffered);
            //m_encoder.getBuffer(buf, 0);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            int fps = 30;
            var waveOut = new WaveOutEvent() { DeviceNumber = 10 };
            var encoder = new Source(fps);
            waveOut.DesiredLatency = 40;
            waveOut.Init(encoder);
            encoder.SetTimecode(new LTCSharpNet.Timecode(0, 0, 0, 0));
            encoder.StartEncoding();
            waveOut.Play();

            while (true) { }

            waveOut.Stop();
            waveOut.Dispose();
        }
    }
}

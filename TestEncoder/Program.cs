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
        bool m_encoding = false;
        long m_fps = 0;
        long m_frameDeltaTime = 0;
        Stopwatch m_timer = new();
        private const int m_bufSizeSeconds = 2;
        int m_bufSize; // number of frames stored in buffer
        TCFrameBufferManager m_bufferManager;
        int m_frameSizeBytes = 1600; // Number of bytes in a tc frame
        bool m_encodingTimecode = true;

        public Source(long fps)
        {
            m_encoder = new LTCSharpNet.Encoder(48000, fps, LTCSharpNet.TVStandard.TV525_60i, LTCSharpNet.BGFlags.NONE);
            m_fps = fps;
            m_bufSize = (int)fps * m_bufSizeSeconds; // 
            m_frameDeltaTime = (1000000 / m_fps); // 1 microsecond / fps
            m_bufferManager = new TCFrameBufferManager(m_bufSize);

            FillBuffers();
        }

        public void StartEncoding()
        {
            m_timer.Start();

            _ = Task.Run(() =>
            {

                int now = m_timer.Elapsed.Microseconds;
                int lastFrame = now;

                while (m_encodingTimecode)
                {
                    now = m_timer.Elapsed.Microseconds;
                    int delta = now - lastFrame;
                    lastFrame = now;

                    if (delta > m_frameDeltaTime)
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
                    m_bufferManager.Write(tmp);
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
        }

        private void FillBuffers()
        {
            bool success = true;
            byte[] tmp = new byte[m_frameSizeBytes];

            for (int i = 0; i <m_bufSize; i++)
            {
                m_encoder.getBuffer(tmp, 0);
                success &= m_bufferManager.Write(tmp);
            }

            Debug.Assert(success, "FillBuffers is failing to fill all buffers or overflowing");
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

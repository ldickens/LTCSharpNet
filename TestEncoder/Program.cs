using NAudio;
using NAudio.Utils;
using NAudio.Wave;
using System.Collections.Immutable;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TestEncoder
{

    class Source : IWaveProvider
    {
        LTCSharpNet.Encoder m_encoder;
        bool m_encoding = false;
        long m_fps = 0;
        double m_frameDeltaTime = 0;
        Stopwatch m_timer = new();
        private const int m_bufSizeSeconds = 2;
        int m_bufSize; // number of frames stored in buffer
        TCFrameBufferManager m_bufferManager;
        int m_frameSizeBytes = 1600; // Number of bytes in a tc frame
        bool m_encodingTimecode = true;

        public Source(long fps, LTCSharpNet.Timecode startTime)
        {
            m_encoder = new LTCSharpNet.Encoder(48000, fps, LTCSharpNet.TVStandard.TV525_60i, LTCSharpNet.BGFlags.NONE);
            m_fps = fps;
            SetTimecode(startTime);
            m_bufSize = (int)fps * m_bufSizeSeconds; // 
            m_frameDeltaTime = (TimeSpan.FromSeconds(1).TotalMicroseconds / m_fps); // 1 second in microseconds / fps
            m_bufferManager = new TCFrameBufferManager(m_bufSize);

            FillBuffers();
        }

        public async void StartEncoding()
        {
            m_timer.Start();
            double target = m_frameDeltaTime;

            await Task.Run(() =>
            {
                byte[] tmp = new byte[m_frameSizeBytes];

                while (m_encodingTimecode)
                {
                    double now = m_timer.Elapsed.TotalMicroseconds;

                    if (now >= target)
                    {
                        m_bufferManager.IncrementActiveBuffer();
                        m_encoder.incrementFrame();
                        m_encoder.encodeFrame();
                        m_encoder.getBuffer(tmp, 0);
                        m_bufferManager.Write(tmp);

                        target += m_frameDeltaTime;

                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
            });
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            Buffer.BlockCopy(m_bufferManager.ReadActiveBuffer(count), 0, buffer, 0, count);

            return buffer.Length;
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

            for (int i = 0; i < m_bufSize; i++)
            {
                m_encoder.encodeFrame();
                m_encoder.getBuffer(tmp, 0);
                Console.WriteLine(m_encoder.getTimecode().ToString());

                m_encoder.incrementFrame();
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
            waveOut.DesiredLatency = 40;
            var encoder = new Source(fps, new LTCSharpNet.Timecode(0, 0, 0, 0));

            waveOut.Init(encoder);
            encoder.StartEncoding();
            waveOut.Play();

            while (true) { }

            waveOut.Stop();
            waveOut.Dispose();
        }
    }
}

using NAudio;
using NAudio.Utils;
using NAudio.Wave;
using System.Diagnostics;
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
        const int m_bufSize = 1600*30;
        CircularBuffer m_circularBuffer = new(m_bufSize);

        public Source(long fps)
        {
            m_encoder = new LTCSharpNet.Encoder(48000, fps, LTCSharpNet.TVStandard.TV525_60i, LTCSharpNet.BGFlags.NONE);
            m_fps = fps;
            m_frametime = (TimeSpan.TicksPerSecond / m_fps);
        }

        public void StartEncoding()
        {
            Task.Run(() =>
            {
                byte[] tmp = new byte[1600];

                while (true)
                {
                    if (m_circularBuffer.MaxLength == m_circularBuffer.Count)
                    {
                        //Thread.Sleep(2);
                        continue;
                    }

                    if (!m_encoding)
                    {
                        m_timer.Start();
                        //m_timer2.Start();
                        m_encoding = true;
                        m_encoder.getBuffer(tmp, 0);
                    }
                    else if (m_timer.ElapsedTicks > m_frametime)
                    {

                        m_timer.Restart();
                        m_circularBuffer.Reset();
                        m_encoder.incrementFrame();
                        m_encoder.encodeFrame();

                        m_encoder.getBuffer(tmp, 0);
                        //m_currentTC = m_encoder.getTimecode().ToString();
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
            Thread.Sleep(100);
			waveOut.Play();

            _ = Task.Run(() =>
            {
                Stopwatch timer = new();
                timer.Start();
                while (true)
                {
                    Console.WriteLine(timer.Elapsed);
                    Thread.Sleep(1000);
                }
            });

            while (true) { }

			waveOut.Stop();
			waveOut.Dispose();
        }
    }
}

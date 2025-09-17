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
    public class TCFrameBufferManager
    {
        byte[,] m_frameBuffers;
        TCRingBuffer m_activeBuffer;
        int m_activeBufferIndex = 0;
        int m_numOfFrameBuffers = 0;
        int m_activeBufferDataIndex = 0;
        int m_activeBufferDataNext = 0;
        const int m_sizeOfFrameBuffer = 1600;

        public TCRingBuffer ActiveBuffer => m_activeBuffer;

        public TCFrameBufferManager(int numOfFrames)
        {
            m_numOfFrameBuffers = numOfFrames;
            m_frameBuffers = new byte[m_numOfFrameBuffers, m_sizeOfFrameBuffer];

            byte[] tmp = new byte[m_sizeOfFrameBuffer];
            Buffer.BlockCopy(m_frameBuffers, 0, tmp, 0, numOfFrames);
            m_activeBuffer = new(m_sizeOfFrameBuffer, tmp);
        }

        private byte[] GetTCFrameBuffer(int index)
        {
            byte[] tmp = new byte[m_sizeOfFrameBuffer];
            for (int i = 0; i < m_sizeOfFrameBuffer; i++)
            {
                tmp[i] = m_frameBuffers[index, i];
            }
            return tmp;
        }

        public void IncrementActiveBuffer()
        {
            m_activeBufferIndex++;
            m_activeBuffer.Write(GetTCFrameBuffer(m_activeBufferIndex));
        }

        public void FillSingularBuffer(int index, byte[] source)
        {
            Buffer.BlockCopy(source, 0, m_frameBuffers, index * m_numOfFrameBuffers, m_sizeOfFrameBuffer);
            if (m_activeBufferIndex == index)
            {
                m_activeBuffer.Write(GetTCFrameBuffer(m_activeBufferIndex));
            }
        }

        public byte[] ReadActiveBuffer(int size)
        {
            byte[] tmp = m_activeBuffer.Read(size);
            Buffer.BlockCopy(tmp, 0, tmp, 0, tmp.Length);
            return tmp;
        }
    }

    public class TCRingBuffer
    {
        private readonly byte[] m_buffer;
        private int m_tail; // next read position
        private const int m_maxSize = 1600; // size of a singular frame of smpte timecode data
        private object m_lock = new();

        public int Capacity { get; }

        public TCRingBuffer(int capacity, byte[] source)
        {
            if (capacity <= 0)
                throw new ArgumentException("Capacity must be greater than zero.", nameof(capacity));

            Capacity = capacity;
            m_buffer = source;
            m_tail = 0;
        }

        public void Write(byte[] source)
        {
            Debug.Assert(source.Length == m_maxSize, $"Timecode frame data provided is not a valid length of {m_maxSize}");
            lock (m_lock)
            {
                Buffer.BlockCopy(source, 0, m_buffer, 0, source.Length);
            }
        }

        public byte[] Read(int size)
        {
            lock (m_lock)
            {
                int count = 0;
                int tmpHead = 0;
                byte[] tmp = new byte[size];
                while (count < size)
                {
                    int chunkSize = m_buffer.Length - m_tail;
                    if (chunkSize > (size - count))
                    {
                        chunkSize = (size - count);
                    }
                    Buffer.BlockCopy(m_buffer, m_tail, tmp, tmpHead, chunkSize);
                    // figure out the m_tail updating position.
                    count += chunkSize;
                    tmpHead = count - 1;
                }
                return tmp;
            }
        }
    }

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
        const int m_bufSize = 1600 * 60;
        CircularBuffer m_circularBuffer = new(m_bufSize);

        public Source(long fps)
        {
            m_encoder = new LTCSharpNet.Encoder(48000, fps, LTCSharpNet.TVStandard.TV525_60i, LTCSharpNet.BGFlags.NONE);
            m_fps = fps;
            m_frametime = (TimeSpan.TicksPerSecond / m_fps);
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

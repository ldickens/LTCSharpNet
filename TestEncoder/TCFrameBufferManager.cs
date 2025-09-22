using System.Diagnostics;

namespace TestEncoder
{
    public class TCFrameBufferManager
    {
        byte[,] m_frameBuffers;
        TCRingBuffer m_activeBuffer;
        int m_activeBufferIndex = 0;
        int m_numOfFrameBuffers = 0;
        int m_activeBufferDataNext = 0;
        const int m_sizeOfFrameBuffer = 1600;

        public TCRingBuffer ActiveBuffer => m_activeBuffer;

        public int FrameBufferSize  => m_sizeOfFrameBuffer;

        public int TotalFrameBuffers => m_frameBuffers.Length / 1600;

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
            byte[] tmp = m_activeBuffer.Read(size, out m_activeBufferDataNext);
            Buffer.BlockCopy(tmp, 0, tmp, 0, tmp.Length);
            return tmp;
        }

        public byte Peak()
        {
            return m_activeBuffer.Peak();
        }
    }

    public class TCRingBuffer
    {
        private readonly byte[] m_buffer;
        private int m_tail; // Read position
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

        public byte Peak()
        {
            return m_buffer[m_tail];
        }

        // Receiving buffer can vary in size, we should provide consecutive data
        // to this buffer which may include multiple duplicates of frame audio tc data.
        public byte[] Read(int size, out int tail)
        {
            lock (m_lock)
            {
                int count = 0;
                int tmpHead = 0; // Write position of receiving buffer
                byte[] tmp = new byte[size];

                while (count < size)
                {
                    int chunkSize = m_buffer.Length - m_tail;

                    if (chunkSize > (size - count))
                    {
                        chunkSize = (size - count);
                    }

                    Buffer.BlockCopy(m_buffer, m_tail, tmp, tmpHead, chunkSize);

                    if (m_tail == m_buffer.Length)
                    {
                        m_tail = 0;
                    }
                    else
                    {
                        m_tail += chunkSize;
                    }

                    count += chunkSize;
                    tmpHead = count;
                }

                tail = m_tail;
                return tmp;
            }
        }
    }
}

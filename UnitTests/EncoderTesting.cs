using System.Diagnostics;
using System.DirectoryServices;
using TestEncoder;

namespace EncoderTests 
{
    [TestClass]
    public sealed class EncoderBufferTesting
    {
        private static TCFrameBufferManager m_bufManager = new(100);

        [ClassInitialize]
        public static void ClassInit(TestContext testContext)
        {
            byte[] tmp = new byte[1600];

            int count = 0;
            while (count < tmp.Length)
            {
                for (byte i = 0; i <= 255;  i++)
                {
                    tmp[count] = i;
                    count++;
                    
                    if (count == tmp.Length) { break; }
                }

            }

            for (int i = 0; i < 100; i++)
            {
                m_bufManager.FillSingularBuffer(i, tmp);
            }
        }

        [TestMethod]
        public void TCFrameBufferManagerConstructorTest()
        {
            TCFrameBufferManager testManager = new(100);
            Assert.IsTrue(testManager.TotalFrameBuffers == 100);
        }

        [TestMethod]
        public void TCRingBufferRead()
        {

            TCRingBuffer active = m_bufManager.ActiveBuffer;
            byte[] firstArray = active.Read(3200, out int tail);

            Assert.IsTrue(SequentialFrameTesting(firstArray, 0));

            byte[] secondArray = active.Read(300, out tail);

            Assert.IsTrue(SequentialFrameTesting(secondArray, active.Peak()));

        }

        [TestMethod]
        public void TCFrameBufferManagerReadActiveBufferTest()
        {
            byte[] firstArray = m_bufManager.ReadActiveBuffer(1234);
            byte[] secondArray = m_bufManager.ReadActiveBuffer(6400);

            Assert.IsTrue(SequentialFrameTesting(firstArray, m_bufManager.Peak()));
            Assert.IsTrue(SequentialFrameTesting(secondArray, m_bufManager.Peak()));
        }

        static private bool SequentialFrameTesting(byte[] testArray, int startNum)
        {
            // check that the arrays have consecutive numbers up to 255
            int controlInt = startNum > 0 ? startNum : 0;

            for (int i = startNum; i < testArray.Length; i++)
            {
                Trace.WriteLine($"Index={i} : {testArray[i]}");
                Assert.IsTrue(i % 255 == controlInt % 255);

                if (i % 255 == controlInt % 255)
                {
                    controlInt++;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }
    }
}

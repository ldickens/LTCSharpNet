using System.Diagnostics;
using TestEncoder;

namespace EncoderTests 
{
    [TestClass]
    public sealed class EncoderBufferTesting
    {
        [TestMethod]
        public void ReadFromTCFrameBufferTesting()
        {
            var bufManager = new TCFrameBufferManager(100);
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
                bufManager.FillSingularBuffer(i, tmp);
            }

            TCRingBuffer active = bufManager.ActiveBuffer;
            byte[] firstArray = active.Read(3200);
            byte[] secondArray = active.Read(300);

            Assert.IsTrue(SequentialFrameTesting(firstArray, 0));

            int secondArrayStartingNum = 3200 % 255;

            Assert.IsTrue(SequentialFrameTesting(secondArray, secondArrayStartingNum));

        }

        static private bool SequentialFrameTesting(byte[] testArray, int startNum)
        {
            // check that the arrays have consecutive numbers up to 255
            int controlInt = startNum > 0 ? startNum : 0;

            for (int i = startNum; i < testArray.Length; i++)
            {
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

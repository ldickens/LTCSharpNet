using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace TestDecoder
{
    internal class Program
	{
		static LTCSharpNet.Decoder FDecoder;

		[STAThread]
		static void Main(string[] args)
		{
			WaveInExample();
			//FileLoadExample();
		}

		static unsafe void waveIn_DataAvailable(object sender, WaveInEventArgs e)
		{
			lock (FDecoder)
			{
				byte[] downSampled = new byte[e.BytesRecorded / 2];
				for (int i = 0; i < e.BytesRecorded / 2; i++)
				{
					downSampled[i] = (byte)(((int)e.Buffer[i * 2] + (int)e.Buffer[i * 2 + 1]) / 2);
				}


				// Logging for data analysis
				//var hex = BitConverter.ToString(downSampled);
				//File.AppendAllTextAsync(Path.Combine(Environment.CurrentDirectory, "logNew.txt", hex, System.Text.Encoding.ASCII);

				FDecoder.Write(downSampled, e.BytesRecorded / 2, 0);
                //Console.Out.WriteLine("AudioReceived");
			}
		}

		static void WaveInExample()
		{
			var waveIn = new WasapiCapture(WasapiCapture.GetDefaultCaptureDevice(), false, 345);
			waveIn.WaveFormat = new WaveFormat(44100, 8, 1);

			Console.WriteLine("Device format: " + waveIn.WaveFormat.ToString());
			Console.WriteLine(waveIn.WaveFormat.BitsPerSample.ToString()); 
			FDecoder = new LTCSharpNet.Decoder(waveIn.WaveFormat.SampleRate, 25, 32);
			waveIn.DataAvailable += waveIn_DataAvailable;
			waveIn.StartRecording();


			while (true) //timer.Elapsed < new TimeSpan(0, 0, 60))
			{
				lock (FDecoder)
				{
					if (FDecoder.GetQueueLength() > 0)
					{
						try
						{
							var frame = FDecoder.Read();
							var timecode = frame.getTimecode();
							Console.WriteLine(timecode.ToString());
						}
						catch (Exception e)
						{
							Console.Write(e);
						}
					}
					else
					{
						Thread.Sleep(10);
					}
				}
			}
		}

		static void FileLoadExample()
		{
			string fileName = @"C:\Users\Leon Dickens\Downloads\LTC_01000000_10mins_30fps_44100x16\LTC_01000000_10mins_30fps_44100x16.wav";
			var wavePlayer = new WaveFileReader(fileName);
			Console.WriteLine("File format: " + wavePlayer.WaveFormat.ToString());

			FDecoder = new LTCSharpNet.Decoder(wavePlayer.WaveFormat.SampleRate, 25, 32);

			int size = 1600;
			byte[] buffer = new byte[size];
			int total = 0;
			while (wavePlayer.Position < wavePlayer.Length)
			{
				var task = wavePlayer.Read(buffer, 0, size);

				FDecoder.WriteAsU16(buffer, size / 2, total);

				total += size / 2;

				try
				{
					var frame = FDecoder.Read();
					var timecode = frame.getTimecode();
					Console.WriteLine(wavePlayer.CurrentTime.ToString() + "\t" + timecode.ToString());
				}
				catch
				{
                    Console.WriteLine("No frames available");
					//no frames available
				}
			}

			Console.WriteLine("END OF FILE");
		}
	}
}

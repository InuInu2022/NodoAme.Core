using System;
using DotnetWorld.API;
using DotnetWorld.API.Structs;

namespace NodoAme
{
	public class WorldParameters
	{
		public double frame_period;
		public int fs;

		public double[]? f0;
		public double[]? time_axis;
		public int f0_length;

		public double[,]? spectrogram;
		public double[,]? aperiodicity;
		public int fft_size;

		public WorldParameters(int fs, double frame_period = 5.0)
		{
			this.fs = fs;
			this.frame_period = frame_period;
		}
	}



	public class WorldUtil
	{
		public enum Estimaion
		{
			/// <summary>
			/// デフォルトの推定法。やや時間がかかるがより正確。
			/// </summary>
			Harvest,
			/// <summary>
			/// 旧来の推定法。高速だが甘い。
			/// </summary>
			Dio
		}

		public void F0EstimationHarvest(double[] x, int x_length, WorldParameters world_parameters)
		{
			var option = new HarvestOption();

			Core.InitializeHarvestOption(option);

			option.frame_period = world_parameters.frame_period;
			option.f0_floor = 71.0;

			world_parameters.f0_length = Core.GetSamplesForDIO(world_parameters.fs,
				x_length, world_parameters.frame_period);
			world_parameters.f0 = new double[world_parameters.f0_length];
			world_parameters.time_axis = new double[world_parameters.f0_length];

			System.Diagnostics.Debug.WriteLine("Analysis");
			Core.Harvest(x, x_length, world_parameters.fs, option,
				world_parameters.time_axis, world_parameters.f0);
		}

		/// <summary>
		/// 基本周波数を推定
		/// </summary>
		/// <param name="estimationType"></param>
		/// <param name="x"></param>
		/// <param name="audioLength"></param>
		/// <param name="wParam"></param>
		/// <returns></returns>
		public static WorldParameters EstimateF0(
			Estimaion estimationType,
			double[] x,
			int audioLength,
			WorldParameters wParam
		)
		{
			switch (estimationType)
			{
				case Estimaion.Harvest:
					var opt = new HarvestOption();
					Core.InitializeHarvestOption(opt);
					opt.frame_period = wParam.frame_period;
					opt.f0_floor = 90.0;	//声の周波数の下のライン

					wParam.f0_length = Core.GetSamplesForDIO(
						wParam.fs,
						audioLength,
						wParam.frame_period
					);
					wParam.f0 = new double[wParam.f0_length];
					wParam.time_axis = new double[wParam.f0_length];

					System.Diagnostics.Debug.WriteLine("Analysis");
					Core.Harvest(
						x,
						audioLength,
						wParam.fs,
						opt,
						wParam.time_axis,
						wParam.f0
					);

					break;
				default:
					break;
			}

			return wParam;
		}

		/// <summary>
		/// 音声のスペクトル包絡を推定
		/// </summary>
		/// <param name="x"></param>
		/// <param name="audioLength"></param>
		/// <param name="wParam"></param>
		/// <returns></returns>
		public static WorldParameters EstimateSpectralEnvelope(
			double[] x,
			int audioLength,
			WorldParameters wParam
		)
		{
			var opt = new CheapTrickOption();

            Core.InitializeCheapTrickOption(wParam.fs, opt);

            opt.q1 = -0.15;
            opt.f0_floor = 71.0;

            wParam.fft_size = Core.GetFFTSizeForCheapTrick(wParam.fs, opt);
            wParam.spectrogram = new double[wParam.f0_length, (wParam.fft_size / 2) + 1];
            Core.CheapTrick(
				x,
				audioLength,
				wParam.fs,
				wParam.time_axis,
				wParam.f0,
				wParam.f0_length,
				opt,
				wParam.spectrogram);
			return wParam;
		}

		/// <summary>
		/// Read wav file
		/// </summary>
		/// <param name="filename">a path to internal synthesized wav file</param>
		/// <returns>table of fs and nbit</returns>
		/// <exception cref="ArgumentException">file not found.</exception>
		public static (int fs, int nbit, int len, double[] x) ReadWav(string filename)
		{
			var isExist = System.IO.File.Exists(filename);
			if (!isExist) throw new ArgumentException("A internal wav file is not found.");

			var audioLength = Tools.GetAudioLength(filename);
			if(audioLength<0){
				using var rd = new NAudio.Wave.WaveFileReader(filename);
				audioLength = Convert.ToInt32(rd.Length);
			}
			double[] x = new double[audioLength];
			Tools.WavRead(filename, out int fs, out int nbit, x);
			return (fs, nbit, audioLength, x);
		}
	}
}

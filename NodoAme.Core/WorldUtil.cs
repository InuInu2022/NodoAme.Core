using System;
using System.Collections.Generic;
using System.Linq;
using DotnetWorld.API;
using DotnetWorld.API.Structs;
using NLog;

namespace NodoAme;

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

public static class WorldUtil
{
	private static readonly Logger logger = LogManager.GetCurrentClassLogger();

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
		IEnumerable<double> x,
		int audioLength,
		WorldParameters wParam
	)
	{
		if(estimationType != Estimaion.Harvest){
			throw new NotSupportedException();
		}

		var opt = new HarvestOption();
		DotnetWorld.API.Core.InitializeHarvestOption(opt);
		opt.frame_period = wParam.frame_period;
		opt.f0_floor = 90.0;    //声の周波数の下のライン

		wParam.f0_length = DotnetWorld.API.Core.GetSamplesForDIO(
			wParam.fs,
			audioLength,
			wParam.frame_period
		);
		wParam.f0 = new double[wParam.f0_length];
		wParam.time_axis = new double[wParam.f0_length];

		System.Diagnostics.Debug.WriteLine("Analysis");
		try
		{
			DotnetWorld.API.Core.Harvest(
				x.ToArray(),
				audioLength,
				wParam.fs,
				opt,
				wParam.time_axis,
				wParam.f0
			);
		}
		catch (System.Exception)
		{
			logger.Warn("Estimate Harvest failed!");
			throw;
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

		DotnetWorld.API.Core.InitializeCheapTrickOption(wParam.fs, opt);

		opt.q1 = -0.15;
		opt.f0_floor = 71.0;

		wParam.fft_size = DotnetWorld.API.Core.GetFFTSizeForCheapTrick(wParam.fs, opt);
		wParam.spectrogram = new double[wParam.f0_length, (wParam.fft_size / 2) + 1];
		DotnetWorld.API.Core.CheapTrick(
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
		if (!isExist) {
			const string msg = "A internal wav file is not found.";
			logger.Error(msg);
			throw new ArgumentException(msg);
		}

		var audioLength = Tools.GetAudioLength(filename);
		if(audioLength<0){
			using var rd = new NAudio.Wave.WaveFileReader(filename);
			try
			{
				audioLength = Convert.ToInt32(rd.Length);
			}
			catch (Exception ex)
			{
				logger
					.Warn($"A wav file '{filename}' length is too long.:{ex.Message}");
			}
		}

		var x = new double[audioLength];
		Tools.WavRead(filename, out int fs, out int nbit, x);
		return (fs, nbit, audioLength, x);
	}
}

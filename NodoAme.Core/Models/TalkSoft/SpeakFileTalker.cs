using System.Linq;
using System.IO;
using System;
using System.Threading.Tasks;
using NLog;
using NAudio.Wave;
using LibSasara;

namespace NodoAme.Models;

public class SpeakFileTalker: IDisposable,ITalkManager
{
	private static readonly Logger logger = LogManager.GetCurrentClassLogger();
	private bool disposedValue;

	public static async ValueTask<SpeakFileTalker> FactoryAsync(){
		return await Task.Run(() => new SpeakFileTalker());
	}

	public async ValueTask<Label[]> GetPhonemesAsync(string path)
	{
		var (labels, _) = await GetPhonemesAndLengthAsync(path);
		return labels;
	}

	public async ValueTask<(Label[] labels, double length)> GetPhonemesAndLengthAsync(string path)
	{
		if (path is null || !File.Exists(path))
		{
			var msg = $"file {path}が見つかりません。";
			logger.Error(msg);
			throw new FileNotFoundException(msg);
		}

		//load label file
		var lab = await SasaraLabel.LoadAsync(path);
		var labels = lab
			.Lines
			.Select(v => new Label(
				v.Phoneme,
				//labファイル内とAPI経由では時間が異なる
				v.From / 10000000,
				v.To / 10000000
			))
			.ToArray();
		var length = lab.Lines
			//labファイル内とAPI経由では時間が異なる
			.Select(v => v.Length / 10000000)	//TODO: check length rate
			.Sum();
		return (labels, length);
	}

	public async ValueTask<double> SpeakAsync(string path)
	{
		if(path is null || !File.Exists(path)){
			var msg = $"file {path}が見つかりません。";
			logger.Error(msg);
			//throw new FileNotFoundException(msg);
			return -1;
		}

		//TODO: play wav file
		using var audioFile = new AudioFileReader(path);
		using var wo = new WaveOutEvent();
		wo.Init(audioFile);
		wo.Play();
		while (wo.PlaybackState == PlaybackState.Playing)
		{
			await Task.Delay(500);
		}

		return audioFile.TotalTime.TotalSeconds;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposedValue)
		{
			return;
		}

		if (disposing)
		{
			// マネージド状態を破棄します (マネージド オブジェクト)
		}

		// アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
		// 大きなフィールドを null に設定します
		disposedValue = true;
	}

	void IDisposable.Dispose()
	{
		// このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
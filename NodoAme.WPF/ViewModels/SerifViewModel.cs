using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Epoxy;
using Epoxy.Synchronized;
using System;

namespace NodoAme.ViewModels;

[ViewModel]
public class SerifViewModel
{
	public string? SourceText { get; set; }
	public string? ConvertedText { get; set; }
	public string? SerifTime { get; set; }
	public string DropFileName { get; set; } = "";
	public string DropFileSerif { get; set; } = "";
	public MainWindowViewModel? ParentVM { get; set; }
	public bool EnabledSerifInput { get; set; }
	public bool EnabledPreview { get; set; }
	public bool EnabledExport { get; set; }
	public Visibility ExportProgress { get; set; } = Visibility.Collapsed;
	public Visibility PreviewProgress { get; set; } = Visibility.Collapsed;
	public Command PreviewTalk { get; set; }
	public Command ExportTrackFile { get; set; }
	public Command Drop { get; set; }

	public Visibility SourceTextVisible { get; set; }
		= Visibility.Visible;

	public Visibility DropFileNameView { get; set; }
		= Visibility.Collapsed;

	public Command ResetDropFile { get; set; }
	public Command ExportPreviewWav { get; set; }
	public Command CheckEnterAndAddRow { get; private set; }

	public string CurrentEngineType { get; set; } = "";
	public dynamic? CurrentEngine { get; set; }
	private string? LastSourceText { get; set; }
	private SongCast? LastCast { get; set; }

	public bool HasSoundFile {
		get =>
			!string.IsNullOrEmpty(SoundFilePath) &&
				File.Exists(SoundFilePath);
	}

	public bool HasLabelFile {
		get =>
			!string.IsNullOrEmpty(LabelFilePath) &&
				File.Exists(LabelFilePath);
	}

	private string SoundFilePath { get; set; } = "";
	private string LabelFilePath { get; set; } = "";

	private string SerifFilePath { get; set; } = "";

	public SerifViewModel()
	{
		PreviewTalk = CommandFactory.Create<RoutedEventArgs>(
			async _ =>
			{
				if(CurrentEngineType is TalkEngine.SOUNDFILE
					&& string.IsNullOrEmpty(SoundFilePath))	{
					return;
				}else if (SourceText is null){
					return;
				}

				EnabledPreview = false;
				EnabledExport = false;
				PreviewProgress = Visibility.Visible;
				SerifTime = (CurrentEngineType is TalkEngine.SOUNDFILE) ?
					await PreviewTalkAsync(SoundFilePath) :
					await ParentVM!.PreviewTalkFromListAsync(this.SourceText);
				EnabledPreview = true;
				EnabledExport = true;
				PreviewProgress = Visibility.Collapsed;
			}
		);

		ExportTrackFile = CommandFactory.Create<RoutedEventArgs>(async _ =>
		{
			if(CurrentEngineType is TalkEngine.SOUNDFILE
				&& string.IsNullOrEmpty(SoundFilePath))	{
				return;
			}else if (SourceText is null){
				return;
			}

			EnabledExport = false;
			EnabledPreview = false;
			ExportProgress = Visibility.Visible;
			var cast = ParentVM!.ExportSongCastItems![ParentVM.ExportSongCastSelected];
			const double alpha = 0.0;   //TODO:support alpha
			if(CurrentEngineType is TalkEngine.SOUNDFILE){
				await ExportFileAsync(
					SoundFilePath,
					cast.Id!,
					alpha,
					isTrack: true,
					songCast: cast,
					serifText: DropFileSerif
				);
			}else{
				//TODO: use `this.ExportFileAsync()`
				await ParentVM!.ExportFileFromListAsync(
					this.SourceText,
					cast.Id!,
					alpha,
					isTrack: true,
					songCast: cast
				);
			}

			EnabledExport = true;
			EnabledPreview = true;
			ExportProgress = Visibility.Collapsed;
		});

		Drop = CommandFactory
			.Create(DropEvent());

		ResetDropFile =
			Command.Factory.CreateSync(
				() =>
				{
					DropFileName = string.Empty;
					DropFileSerif = string.Empty;
					SourceTextVisible = Visibility.Visible;
					DropFileNameView = Visibility.Collapsed;
					EnabledExport = true;
					EnabledSerifInput = true;
					EnabledPreview = true;
					ConvertedText = "";
					SoundFilePath = string.Empty;
					LabelFilePath = string.Empty;
					CurrentEngineType = ParentVM?.TalkSoftItems?[ParentVM.TalkSoftSelected]?.Id ?? TalkEngine.OPENJTALK;
				});

		ExportPreviewWav = CommandFactory
			.Create<RoutedEventArgs>(
				async _ =>
				{
					if (SourceText is null) { return; }

					EnabledExport = false;
					await ParentVM!
						.ExportPreviewWavFromListAsync(this.SourceText);

					EnabledExport = true;
				}
		);

		CheckEnterAndAddRow =
			Command.Factory.CreateSync<RoutedEventArgs>(
				e =>
				{
					Debug.WriteLine("e:" + e.ToString());
					var k = (KeyEventArgs)e;
					if (k.Key != Key.Enter)
					{
						return;
					}

					k.Handled = true;
				});
	}

	private Func<DragEventArgs, ValueTask> DropEvent()
	{
		return async e =>
		{
			if (!e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				return;
			}

			string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
			await Task.Run(() =>
				Debug.WriteLine($"dropfile:{paths[0]}"));

			#region sound file

			var target = paths[0];
			DropFileName = Path.GetFileName(target);

			if (File.Exists(target))
			{
				EnabledSerifInput = false;
				EnabledPreview = true;
				CurrentEngineType = NodoAme.TalkEngine.SOUNDFILE;
				SoundFilePath = target;
			}
			else
			{
				EnabledSerifInput = false;
				EnabledPreview = false;
				SoundFilePath = string.Empty;
			}

			#endregion

			#region lab file

			var lab = Path.ChangeExtension(target, "lab");
			if (File.Exists(lab))
			{
				SourceTextVisible = Visibility.Collapsed;
				DropFileNameView = Visibility.Visible;
				LabelFilePath = lab;
				EnabledExport = true;
				ConvertedText = await ConvertAsync(lab);
				LastSourceText = ConvertedText;

				//TODO:

			}
			else
			{
				SourceTextVisible = Visibility.Collapsed;
				DropFileNameView = Visibility.Visible;
				EnabledExport = false;
				DropFileName = $"⚠️{Path.GetFileName(target)}";
				ConvertedText = $"lab file {lab} is NOT FOUND!";
				LabelFilePath = string.Empty;
			}

			#endregion

			#region serif file

			var serif = Path.ChangeExtension(target, ".txt");
			if(File.Exists(serif)){
				DropFileSerif = File.ReadAllText(serif);
			}else{
				DropFileSerif = string.Empty;
			}

			#endregion
		};
	}

	//TODO: port from MainWindowViewModel
	private async ValueTask<string> PreviewTalkAsync(string serifOrPath){
		if (string.IsNullOrEmpty(CurrentEngineType) || ParentVM is null) { return string.Empty; }

		CurrentEngine = await ParentVM!
			.GenerateWrapperAsync(
				CurrentEngineType,
				new()
			);
		return await CurrentEngine
			.SpeakAsync(serifOrPath);
	}

	//TODO: port from MainWindowViewModel
	private async ValueTask<string> ConvertAsync(string serifOrPath){
		if (string.IsNullOrEmpty(CurrentEngineType) || ParentVM is null) { return string.Empty; }

		try
		{
			CurrentEngine = await ParentVM!
				.GenerateWrapperAsync(
					CurrentEngineType,
					new()	//TODO: port options
				);
		}
		catch
		{
			return string.Empty;
		}

		return await PhonemeConverter
			.ConvertAsync(
				talkEngine: CurrentEngine,
				sourceText: serifOrPath,
				isUseSeparaterSpace: ParentVM.IsUseSeparaterSpace,
				isCheckJapaneseSyllabicNasal: ParentVM.IsCheckJapaneseSyllabicNasal,
				isCheckJapaneseNasalSonantGa: ParentVM.IsCheckJananeseNasalGa,
				vowelOption: ParentVM.VowelOption,
				isDebugOutput: false,
				isConvertToHiragana: ParentVM.IsConvertToHiragana
			);
	}

	//TODO: port from MainWindowViewModel
	private async ValueTask ExportFileAsync(
		string sourcePath,
		string castId,
		double alpha,
		bool isTrack = false,
		SongCast? songCast = null,
		string? engineName = null,
		string? serifText = null
	)
	{
		CurrentEngine = await ParentVM!.GenerateWrapperAsync(
			engineName ?? CurrentEngineType!,
			new()	//TODO: port options
		);

		Debug.WriteLine("Export!");
		var exportFileType = //ExportCastItems.ElementAt(ExportCastSelected).ExportFile;
		ParentVM!.ExportSongCastItems![ParentVM!.ExportSongCastSelected].ExportFile;

		var engine = CurrentEngine as ITalkWrapper;

		string serif;
		if (CurrentEngineType is TalkEngine.SOUNDFILE)
		{
			if (!string.IsNullOrEmpty(serifText))
			{
				serif = serifText!;
			}
			else
			{
				serif = Path.GetFileNameWithoutExtension(sourcePath);
			}
		}
		else
		{
			serif = "";
		}

		await engine!.ExportFileAsync(
			new(serif, castId)
			{
				Alpha = alpha,
				IsExportAsTrack = isTrack,
				IsOpenCeVIO = ParentVM!.IsOpenCeVIOWhenExport,
				ExportPath = ParentVM!.PathToSaveDirectory,
				ExportMode = ParentVM!.SongExportLyricsMode,
				Cast = songCast,
				NoteAdaptMode = ParentVM!.AdaptingNoteToPitchMode,
				NoteSplitMode = ParentVM!.NoteSplitMode,
				FileType = (exportFileType != 0) ? exportFileType : Models.ExportFileType.CCS,
				BreathSuppress = ParentVM!.BreathSuppress,
				SongVoiceStyles = ParentVM!.SongVoiceStyleParams,
				NoPitch = ParentVM!.NoPitchMode,
				NoSoundVowelsModes = ParentVM!.NoSoundVowelMode,
				Dynamics = ParentVM!.ExportScoreDynamics,
				Tempo = ParentVM!.ExportFileTempo,
				SoundFilePath = sourcePath,
				LabelFilePath = Path.ChangeExtension(sourcePath, "lab")
			}
		);

		if (ParentVM!.IsExportSerifText)
		{
			await engine.ExportSerifTextFileAsync(
				serif,
				ParentVM!.PathToExportSerifTextDir!,
				ParentVM!.DefaultExportSerifTextFileName!,
				songCast?.Name ?? "ANYONE"
			);
		}

		MainWindow.Logger.Info($"File export finished: {ParentVM!.PathToSaveDirectory}\n{serif}");
	}

	[PropertyChanged(nameof(SourceText))]
	private async ValueTask SourceTextChangedAsync(string sourceText)
	{
		//空文字列なら抜ける
		if (string.IsNullOrEmpty(sourceText) || string.IsNullOrWhiteSpace(sourceText)){
			EnabledExport = false;
			EnabledPreview = false;
			ConvertedText = "";
			SerifTime = "";
			return;
		}

		EnabledExport = true;
		EnabledPreview = true;

		//同じ文字列なら処理を抜ける
		if(this.LastSourceText == sourceText
			&& this.LastCast != ParentVM!.ExportSongCastItems![ParentVM.ExportSongCastSelected]) {return;}

		this.LastSourceText = sourceText;
		this.ConvertedText = await ParentVM!.ConvertFromListAsync(sourceText);
		//var s = ConvertFromList(sourceText);
		//if(IsTalkSoftComboEnabled)InitVoices();
		//return true;//new ValueTask();
	}
}

#nullable disable
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Windows;
using Epoxy;
using Epoxy.Synchronized;
using NLog;
using NLog.Config;
using NLog.Targets;
using NodoAme.Models;


namespace NodoAme.ViewModels
{
	[ViewModel]
	public class MainWindowViewModel
	{
		

		public string WindowTitle { get; set; }

		public string SourceText { get; set; }
		public string ConvertedText { get; set; }
		public ObservableCollection<SerifViewModel> Serifs { get; private set; }
		public int SelectedSerifIndex { get; private set; }
		public ObservableCollection<TalkSoft> TalkSoftItems { get; private set; }
		public int TalkSoftSelected { get; set; } = 0;

		public ObservableCollection<TalkSoftParam> TalkSoftParams { get; set; }

		public ObservableCollection<TalkSoftVoice> TalkVoiceItems { get; private set; }
		public int TalkVoiceSelected { get; set; } = 0;

		public ObservableCollection<TalkSoftVoiceStylePreset> TalkVoiceStylePresetsItems { get; private set; }
		public int VoiceStylePresetsSelected { get; set; } = 0;

		public ObservableCollection<SongCast> ExportCastItems { get; private set; }
		public int ExportCastSelected { get; set; } = 0;

		private Settings setting;
		private Wrapper talkEngine;
		private string currentEngine;

		private JapaneseRule japaneseRules;

		private ObservableCollection<TalkSoft> _talksofts = new ObservableCollection<TalkSoft>();
		private ObservableCollection<TalkSoftVoice> _voices = new ObservableCollection<TalkSoftVoice>();
		private ObservableCollection<TalkSoftVoiceStylePreset> _styles = new ObservableCollection<TalkSoftVoiceStylePreset>();

		#region checkboxes

		public bool IsUseSeparaterSpace { get; set; } = true;
		public bool IsTalkSoftComboEnabled { get; set; }
		public bool IsPreviewButtonEnabled { get; set; }
		public bool IsPreviewComboEnabled { get; set; }

		public bool IsStylePresetsComboEnabled { get; set; } = false;

		public bool IsConvertToPhoneme { get; set; } = true;
		public bool IsConvertToHiragana { get; set; } = false;

		public Command Test { get; set; }


		/// <summary>
		/// 「ん」を変換するかどうか
		/// </summary>
		public bool IsCheckJapaneseSyllabicNasal { get; set; }
		/// <summary>
		/// 「が」行の鼻濁音を変換するかどうか
		/// </summary>
		public bool IsCheckJananeseNasalGa { get; set; } = false;

		public VowelOptions VowelOption { get; set; } = VowelOptions.DoNothing;
		public bool IsCheckJapaneseRemoveNonSoundVowel { get; set; } = false;
		public bool IsCheckJapaneseSmallVowel { get; set; } = false;


		public string PathToSaveDirectory { get; set; } = "./out/";
		public bool IsOpenCeVIOWhenExport { get; set; } = true;

		public bool IsExportAsTrac { get; set; } = true;

		#endregion

		#region commands
		//---------------------------------------------------
		public Command Ready { get; private set; }
		public Command PreviewTalk { get; private set; }
		/// <summary>
		/// 変換ボタン
		/// </summary>
		public Command ConvertToPhonemes { get; private set; }

		public Command ExportTrackFile { get; private set; }

		public Command ExportPreviewWav { get; private set; }


		public Command CopyRow { get; private set; }
		public Command PasteRow { get; private set; }
		public Command AddRow { get; private set; }
		public Command DeleteRow { get; private set; }

		public Command CheckEnterAndAddRow { get; set; }

		public Command OpenLicenses { get; set; }

		public Command SelectExportDirectory { get; set; }

		public Command ExportSusuru { get; set; }

		//---------------------------------------------------
		#endregion



		public MainWindowViewModel()
		{
			
			WindowTitle = GetWindowTitle();
			MainWindow.Logger.Info($"window open: {WindowTitle}");

			//this.IsPreviewButtonEnabled = true;
			this.SourceText = "サンプル：僕らの気持ちが、明日へ向かいます。チンプンカンプンな本に大変！";
			this.ConvertedText = "";

			//test
			this.Serifs = new ObservableCollection<SerifViewModel>();



			this.setting = LoadSettings();
			this.japaneseRules = LoadJapanaseRule();
			InitTalkSofts();

			this.Serifs
				.Add(new SerifViewModel { ParentVM = this, SourceText = "サンプル：僕らの気持ちが、明日へ向かいます。チンプンカンプンな本に大変！" });

			//*
			this.Serifs
				.Add(new SerifViewModel { ParentVM = this, SourceText = "ほげほげふがふが日本語English" });
			for (var i = 0; i < 28; i++)
			{
				this.Serifs
					.Add(new SerifViewModel { ParentVM = this, SourceText = "" });
			}
			//*/

			EnableSerifButtons(TalkSoftSelected);

			// A handler for window loaded
			this.Ready = Command.Factory.CreateSync((Action<RoutedEventArgs>)(e =>
			{


				// A handler for preview button
				//this.PreviewTalk = CommandFactory.Create<RoutedEventArgs>(async _ => await PreviewTalkAsync());

				//this.ExportTrackFile = CommandFactory.Create<RoutedEventArgs>(async _ => await ExportFileAsync(isTrack: true));


				/*
				this.ConvertToPhonemes = Command
					.Factory
					.CreateSync<RoutedEventArgs>(ConvertTo);
				*/

				/*
				this.ConvertToPhonemes = CommandFactory.Create<RoutedEventArgs>((Func<RoutedEventArgs, ValueTask>)(async e =>
				{
					await this.ConvertToAsync((RoutedEventArgs)e);
				}));
				*/





				this.Test = Command.Factory.CreateSync((RoutedEventArgs e) =>
				{
					Debug.WriteLine("Test Clicked!");
				});





				Debug.WriteLine("ready...!");
			}));

			//open license folder
			this.OpenLicenses = CommandFactory.Create<RoutedEventArgs>(_ =>
			{
				Process.Start(Path.GetFullPath("./Licenses/"));
				return new ValueTask();
			});

			this.SelectExportDirectory = CommandFactory.
				Create<RoutedEventArgs>(OpenSelectExportDirDialog());

			this.ExportSusuru = CommandFactory.Create<RoutedEventArgs>(ExportSusuruTrack());

		}




		private Func<RoutedEventArgs, ValueTask> OpenSelectExportDirDialog()
		{
			return _ =>
			{
				using var cofd = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog()
				{
					Title = "出力フォルダをえらんでね",
					InitialDirectory = Path.GetFullPath(PathToSaveDirectory),
					IsFolderPicker = true,
				};
				if (cofd.ShowDialog() != Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
				{
					return new ValueTask();
				}

				PathToSaveDirectory = cofd.FileName;
				return new ValueTask();
			};
		}

		private string GetWindowTitle(){
			var assembly = Assembly.GetExecutingAssembly().GetName();
			var version = assembly.Version;

			return $"{assembly.Name} {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
		}

		private Task ExportFileAsync(bool isTrack)
		{
			throw new NotImplementedException();
		}

		private void TestAction(RoutedEventArgs e)
		{
			Debug.WriteLine("called test button.");
		}

		private Settings LoadSettings()
		{
			return LoadJson<Settings>("NodoAme.Settings.json");
		}

		private JapaneseRule LoadJapanaseRule()
		{
			return LoadJson<JapaneseRule>(@"dic\japanese.json");
		}


		private T LoadJson<T>(
			string pathToJson
		)
		{
			using StreamReader sr = new StreamReader(
				pathToJson,
				System.Text.Encoding.GetEncoding("utf-8")
			);
			string allLine = sr.ReadToEnd();
			sr.Close();

			if (String.IsNullOrEmpty(allLine))
			{
				MessageBox.Show(
					"Jsonの中身が空です",
					"Jsonエラー",
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
				MainWindow.Logger.Error($"Jsonの中身が空です。path to json: {pathToJson}");
				return default;//null;
			}
			try
			{
				T settings = JsonSerializer
					.Deserialize<T>(
						allLine,
						new JsonSerializerOptions
						{
							Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
							WriteIndented = true,
						}
					);
				return settings;
			}
			catch (JsonException e)
			{
				Debug.WriteLine(e.Message);
				MessageBox.Show(
					e.Message,
					"Json読み取りエラー",
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
				MainWindow.Logger.Error($"Json読み取りエラー: {e.Message}");
				return default;
			}
		}


		private void InitTalkSofts()
		{
			if (this.setting is null) return;

			foreach (TalkSoft t in this.setting.TalkSofts)
			{
				if(t.Hidden ?? false)continue;
				_talksofts.Add(t);
			}

			//apply to combobox
			TalkSoftItems = _talksofts;

			//TalkSoftComboBox.SelectedIndex = 0;
			IsTalkSoftComboEnabled = true;
			TalkSoftSelected = 0;

			//InitVoices();

			//export song cast combo
			if (ExportCastItems is null) ExportCastItems = new ObservableCollection<SongCast>();
			foreach (var cast in this.setting.ExportSongCasts)
			{
				ExportCastItems.Add(cast);
			}

			EnableSerifButtons(TalkSoftSelected);
		}

		[PropertyChanged(nameof(TalkSoftSelected))]
		private async ValueTask TalkSoftChangedAsync(int index)
		{
			if (IsTalkSoftComboEnabled)
			{
				TalkSoftParams = new ObservableCollection<TalkSoftParam>();
				var list = TalkSoftItems[index].TalkSoftParams;
				foreach (var item in list)
				{
					TalkSoftParams.Add(item);
				}
				await InitVoicesAsync();


				//enable preview & export
				EnableSerifButtons(index);
			}

			//return new ValueTask();
		}

		private void EnableSerifButtons(int index)
		{
			var canPreview = TalkSoftItems[index].EnabledPreview ?? false;
			var canExport = TalkSoftItems[index].EnabledExport ?? false;
			foreach (var item in Serifs)
			{
				item.EnabledPreview = canPreview;
				item.EnabledExport = canExport;
			}
		}

		[PropertyChanged(nameof(TalkVoiceSelected))]
		private async ValueTask TalkVoiceChangedAsync(int index)
		{
			if (IsPreviewComboEnabled) await InitVoiceStylesAsync();
		}

		[PropertyChanged(nameof(VoiceStylePresetsSelected))]
		private async ValueTask VoiceStyleChangedAsync(int index)
		{
			if (IsStylePresetsComboEnabled)
			{
				//change current style preset
				switch (this.currentEngine)
				{
					case TalkEngine.CEVIO:
					case TalkEngine.VOICEVOX:
						this.talkEngine.VoiceStyle = _styles.ElementAt(index);
						break;
					case TalkEngine.OPENJTALK:
						this.talkEngine = await Wrapper.Factory(
							this.currentEngine,
							_talksofts.ElementAt(TalkSoftSelected),
							_voices.ElementAt(TalkVoiceSelected),
							_styles.ElementAt(index)
						);

						break;
					default:
						break;
				}
				
			}
		}

		[PropertyChanged(nameof(VowelOption))]
		private ValueTask VowelOptionChangedAsync(VowelOptions option)
		{
			IsCheckJapaneseRemoveNonSoundVowel = IsCheckJapaneseSmallVowel = false;
			switch (option)
			{
				case VowelOptions.Remove:
					IsCheckJapaneseRemoveNonSoundVowel = true;
					return new ValueTask();
				case VowelOptions.Small:
					IsCheckJapaneseSmallVowel = true;
					return new ValueTask();
				case VowelOptions.DoNothing:
				default:
					return new ValueTask();
			}

		}

		[PropertyChanged(nameof(ExportCastSelected))]
		private ValueTask ExportCastSelectedChangedAsync(int index)
		{
			//if(IsTalkSoftComboEnabled)InitVoices();
			return new ValueTask();
		}



		private async ValueTask InitVoicesAsync()
		{
			IsPreviewComboEnabled = false;
			IsPreviewButtonEnabled = false;

			var ts = _talksofts
				.ElementAt(TalkSoftSelected);

			if (ts.TalkSoftVoices != null)
			{
				IsPreviewComboEnabled = true;
				TalkVoiceItems = new ObservableCollection<TalkSoftVoice>(ts.TalkSoftVoices);
				TalkVoiceSelected = 0;

				_voices.Clear();
				foreach (var i in ts.TalkSoftVoices)
				{
					_voices.Add(i);
				}

				this.currentEngine = TalkEngine.OPENJTALK;

				IsPreviewButtonEnabled = true;
			}
			else if (ts.Interface != null)
			{
				if (ts.Interface.Type == "API" 
					&& ts.Interface.Engine == TalkEngine.CEVIO)
				{
					//CeVIO Talk API interface
					this.currentEngine = TalkEngine.CEVIO;

					this.talkEngine = await Wrapper.Factory(
						this.currentEngine,
						_talksofts.ElementAt(TalkSoftSelected)
					);

					_voices = talkEngine.GetAvailableCasts();

					IsPreviewComboEnabled = true;
					TalkVoiceItems = _voices;
					TalkVoiceSelected = 0;

					IsPreviewButtonEnabled = true;
				}
				if(ts.Interface.Type == "REST"
					&& ts.Interface.Engine == TalkEngine.VOICEVOX){
					//VOICEVOX REST API interface
					this.currentEngine = TalkEngine.VOICEVOX;

					//await Task.Run(() =>
					//{
						this.talkEngine = await Wrapper.Factory(
							this.currentEngine,
							_talksofts.ElementAt(TalkSoftSelected)
						);
					//});

					_voices = talkEngine.GetAvailableCasts();
					//await InitVoiceStylesAsync();

					IsPreviewComboEnabled = true;
					TalkVoiceItems = _voices;
					TalkVoiceSelected = 0;

					IsPreviewButtonEnabled = true;
				}
			}
			else
			{
				this.currentEngine = "";
			}

			MainWindow.Logger.Info($"InitVoice finished.");
		}

		private async ValueTask InitVoiceStylesAsync()
		{


			IsStylePresetsComboEnabled = false;
			var ts = _talksofts
				.ElementAt(TalkSoftSelected);

			if (ts.Interface != null)
			{
				if (ts.Interface.Type == "API" && ts.Interface.Engine == TalkEngine.CEVIO)
				{

					if (TalkVoiceSelected < 0) return;
					if(_voices.Count == 0)return;


					this.talkEngine.TalkVoice = _voices
						.ElementAt(TalkVoiceSelected);

					var styles = await Task.Run(
						()=> this.talkEngine.GetStyles()
					);

					this._styles.Clear();
					_styles = styles;

					IsStylePresetsComboEnabled = true;
					TalkVoiceStylePresetsItems = _styles;
					VoiceStylePresetsSelected = 0;

				}
				else if(ts.Interface.Type=="REST" 
				&& ts.Interface.Engine == TalkEngine.VOICEVOX){
					if (TalkVoiceSelected < 0) return;
					if(_voices.Count == 0)return;


					this.talkEngine.TalkVoice = _voices
						.ElementAt(TalkVoiceSelected);

					var styles = await Task.Run(
						()=> this.talkEngine.GetStyles()
					);

					this._styles.Clear();
					_styles = styles;

					IsStylePresetsComboEnabled = true;
					TalkVoiceStylePresetsItems = _styles;
					VoiceStylePresetsSelected = 0;
				}
				else
				{
					IsStylePresetsComboEnabled = false;
				}
			}else if (ts.TalkSoftVoices != null){
				//if(ts.TalkSoftVoices)
				if (TalkVoiceSelected < 0) return;

				var styles = new ObservableCollection<TalkSoftVoiceStylePreset>();
				foreach(var s in ts.TalkSoftVoices[TalkVoiceSelected].Styles){
					styles.Add(s);
				}

				//var styles = this.talkEngine.GetStyles();

				this._styles.Clear();
				_styles = styles;
				IsStylePresetsComboEnabled = true;
				TalkVoiceStylePresetsItems = _styles;
				VoiceStylePresetsSelected = 0;
			}

			MainWindow.Logger.Info("InitVoiceStyles finished.");
		}

		private async ValueTask PreviewTalkAsync()
		{

			this.talkEngine = await GenerateWrapper(
				this.currentEngine,
				_talksofts.ElementAt(TalkSoftSelected),
				_voices.ElementAt(TalkVoiceSelected)
			);

			await talkEngine.Speak(this.SourceText);

		}

		public async ValueTask PreviewTalkFromList(string serifText)
		{
			this.talkEngine = await GenerateWrapper(
				this.currentEngine,
				_talksofts.ElementAt(TalkSoftSelected),
				_voices.ElementAt(TalkVoiceSelected),
				_styles.ElementAt(VoiceStylePresetsSelected)
			);

			talkEngine.VoiceStyle = _styles.ElementAt(VoiceStylePresetsSelected);
			await talkEngine.Speak(serifText);

		}

		public async ValueTask ExportFileFromList(
			string serifText,
			string castId,
			double alpha,
			bool isTrack = false
		)
		{
			this.talkEngine = await GenerateWrapper(
				this.currentEngine,
				_talksofts.ElementAt(TalkSoftSelected),
				_voices.ElementAt(TalkVoiceSelected),
				_styles.ElementAt(VoiceStylePresetsSelected)
			);

			Debug.WriteLine("Export!");
			await this.talkEngine.ExportFileAsync(
				serifText,
				castId,
				alpha,
				isTrack,
				IsOpenCeVIOWhenExport,
				PathToSaveDirectory);

			MainWindow.Logger.Info($"File export finished: {PathToSaveDirectory}\n{serifText}");
			//return new ValueTask();
		}

		public async ValueTask ExportPreviewWavFromList(string serifText)
		{
			this.talkEngine = await GenerateWrapper(
				this.currentEngine,
				_talksofts.ElementAt(TalkSoftSelected),
				_voices.ElementAt(TalkVoiceSelected),
				_styles.ElementAt(VoiceStylePresetsSelected)
			);

			talkEngine.VoiceStyle = _styles.ElementAt(VoiceStylePresetsSelected);
			await talkEngine.Speak(serifText);
			await talkEngine.PreviewSave(serifText);
		}

		private Func<RoutedEventArgs, ValueTask> ExportSusuruTrack()
		{
			return async _ =>
			{
				this.talkEngine = await GenerateWrapper(
					this.currentEngine,
					_talksofts.ElementAt(TalkSoftSelected),
					_voices.ElementAt(TalkVoiceSelected),
					_styles.ElementAt(VoiceStylePresetsSelected)
				);
				await talkEngine.ExportSpecialFile(
					ExportCastItems[ExportCastSelected].Id,
					IsExportAsTrac,
					IsOpenCeVIOWhenExport,
					PathToSaveDirectory
				);

				MainWindow.Logger.Info($"Special file export finished: {PathToSaveDirectory}");
				//return new ValueTask();
			};
		}

		private async ValueTask<Wrapper> GenerateWrapper(
			string engine,
			TalkSoft soft,
			TalkSoftVoice voice = null,
			TalkSoftVoiceStylePreset style = null
		)
		{
			var isNotGenerated = false;
			if (this.talkEngine is null){
				isNotGenerated = true;
			}
			else if (
				this.currentEngine != engine ||
				this.talkEngine.TalkSoft != soft
			){
				isNotGenerated = true;
			}

			if (isNotGenerated){
				return await Wrapper.Factory(engine, soft, voice, style);
			}
			else{
				if (voice != null) this.talkEngine.TalkVoice = voice;
				if (style != null) this.talkEngine.VoiceStyle = style;
				return this.talkEngine;
			}

		}


		private async ValueTask ConvertToAsync(RoutedEventArgs eventArgs)
		{
			this.talkEngine = await GenerateWrapper(
				this.currentEngine,
				_talksofts.ElementAt(TalkSoftSelected),
				_voices.ElementAt(TalkVoiceSelected)
			);

			//converted text show
			ConvertedText = await PhenomeConverter.ConvertAsync(
				talkEngine: talkEngine,
				sourceText: this.SourceText,
				isUseSeparaterSpace: IsUseSeparaterSpace,
				isCheckJapaneseSyllabicNasal: IsCheckJapaneseSyllabicNasal,
				isCheckJapaneseNasalSonantGa: IsCheckJananeseNasalGa,
				vowelOption: VowelOption,
				isDebugOutput: false
			);
		}

		public async ValueTask<string> ConvertFromListAsync(string sourceText)
		{
			this.talkEngine = await GenerateWrapper(
				this.currentEngine,
				_talksofts.ElementAt(TalkSoftSelected),
				_voices.ElementAt(TalkVoiceSelected)
			);
			return await PhenomeConverter.ConvertAsync(
				talkEngine: talkEngine,
				sourceText: sourceText,
				isUseSeparaterSpace: IsUseSeparaterSpace,
				isCheckJapaneseSyllabicNasal: IsCheckJapaneseSyllabicNasal,
				isCheckJapaneseNasalSonantGa: IsCheckJananeseNasalGa,
				vowelOption: VowelOption,
				isDebugOutput: false
			);
		}




		[PropertyChanged(nameof(IsUseSeparaterSpace))]
		private ValueTask IsUseSeparaterSpaceChangedAsync(bool useSpace)
		{
			if (PhenomeConverter.CurrentPhonemes != null)
			{
				ConvertedText = PhenomeConverter.ChangeSeparater(useSpace);
			}
			return new ValueTask();
		}
	}
}

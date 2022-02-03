#nullable disable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Windows;
using Epoxy;
using Epoxy.Synchronized;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Config;
using NLog.Targets;
using NodoAme.Models;


namespace NodoAme.ViewModels
{
	[ViewModel]
	public class MainWindowViewModel
	{
		
		static public Logger logger = LogManager.GetCurrentClassLogger();
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

		public IConfigurationRoot Config { get; private set; }
		public Models.UserSettings UserSettings { get; set; }

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

		public bool IsConvertToPhoneme { get; set; }
		public bool IsConvertToHiragana { get; set; }

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
		public int DefaultSerifLines { get; set; }

		public string PathToSaveDirectory { get; set; }

		public ExportLyricsMode SongExportLyricsMode { get; set; }
		public IEnumerable<ExportLyricsMode> ExportLyricsModeList { get; set; }
			= Enum.GetValues(typeof(ExportLyricsMode)).Cast<ExportLyricsMode>();
		public bool IsOpenCeVIOWhenExport { get; set; } = true;

		public bool IsExportAsTrac { get; set; } = true;
		public bool IsExportSerifText { get; set; }
		public string PathToExportSerifTextDir { get; set; }
		public string DefaultExportSerifTextFileName { get; set; }
		public Pile<System.Windows.Controls.TextBox> SerifTextFileNamePile { get; set; }
		public int TextPointOfInsertMetatextToFileName { get; set; }

		public NoteAdaptMode AdaptingNoteToPitchMode { get; set; }
		public IEnumerable<NoteAdaptMode> NoteAdaptModeList { get; set; }
			= Enum.GetValues(typeof(NoteAdaptMode)).Cast<NoteAdaptMode>();

		#endregion

		#region commands
		//---------------------------------------------------
		public Command Ready { get; private set; }
		public Command Close { get; private set; }
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

		public Command OpenWebsite { get; set; }

		public Command SelectExportDirectory { get; set; }

		public Command SelectExportSerifTextDir { get; set; }

		public Command InsertMetaTextToSerifTextFileName { get; set; }

		public Command ExportSusuru { get; set; }

		

		//---------------------------------------------------
		#endregion



		public MainWindowViewModel()
		{

			WindowTitle = GetWindowTitle();
			logger.Info($"window open: {WindowTitle}");

			LoadUserSettings();

			//this.IsPreviewButtonEnabled = true;
			this.SourceText = "サンプル：僕らの気持ちが、明日へ向かいます。チンプンカンプンな本に大変！";
			this.ConvertedText = "";

			//test
			this.Serifs = new ObservableCollection<SerifViewModel>();



			this.setting =  LoadSettings().Result;
			this.japaneseRules = LoadJapanaseRule().Result;
			InitTalkSofts();

			this.Serifs
				.Add(new SerifViewModel { ParentVM = this, SourceText = "サンプル：僕らの気持ちが、明日へ向かいます。チンプンカンプンな本に大変！" });

			//*
			this.Serifs
				.Add(new SerifViewModel { ParentVM = this, SourceText = "ほげほげふがふが日本語English" });
			int lines = DefaultSerifLines - 2;
			if(lines<1)lines = 1;	//check
			for (var i = 0; i < lines; i++)
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

			Close = CommandFactory.Create<RoutedEventArgs>(
				async (e) => await this.UserSettings.SaveAsync()
			);

			//open license folder
			this.OpenLicenses = CommandFactory.Create<RoutedEventArgs>(_ =>
			{
				Process.Start(Path.GetFullPath("./Licenses/"));
				return new ValueTask();
			});

			OpenWebsite = CommandFactory.Create<RoutedEventArgs>(_ =>
			{
				Process.Start("https://inuinu2022.github.io/NodoAme.Home/#/");
				return new ValueTask();
			});

			this.SelectExportDirectory = CommandFactory
				.Create<RoutedEventArgs>(OpenSelectExportDirDialog());

			SelectExportSerifTextDir = CommandFactory
				.Create<RoutedEventArgs>(OpenSelectExportSerifTextDirDialog);

			InsertMetaTextToSerifTextFileName = CommandFactory
				.Create<string>(InsertMetaText);

			SerifTextFileNamePile = PileFactory.Create<System.Windows.Controls.TextBox>();

			this.ExportSusuru = CommandFactory.Create<RoutedEventArgs>(ExportSusuruTrack());

		}



		private void LoadUserSettings()
		{
			//UserSettings
			//const string UserSettings.FileName = UserSettings.FileName;
			var path = UserSettings.UserSettingsPath;
			if(!File.Exists(path)){
				var us = new Models.UserSettings();
				us.CreateFile(path);
				logger.Info($"{UserSettings.UserSettingsFileName} generated.");
			}


			try
			{
				var builder = new ConfigurationBuilder()
				.SetBasePath(Directory.GetCurrentDirectory())
					.AddJsonFile(UserSettings.UserSettingsFileName, false);
				Config = builder.Build();
				UserSettings = Config.Get<UserSettings>();
				logger.Info("usersettings.json loading success");
			}
			catch (Exception ex)
			{
				var _ = ShowErrorMessageBox(
					title:"保存した設定の読み込みに失敗",
					message:$"保存した設定の読み込みに失敗しました。{ex.Message}"
				);
				logger.Error($"usersetting.json loading failed:{ex.Message}");
			}

			//assign
			DefaultSerifLines = UserSettings.DefaultSerifLines;
			PathToSaveDirectory = UserSettings.PathToSaveDirectory;
			IsUseSeparaterSpace = UserSettings.IsUseSeparaterSpace;
			IsConvertToPhoneme = UserSettings.IsConvertToPhoneme;
			IsConvertToHiragana = UserSettings.IsConvertToHiragana;
			IsCheckJapaneseSyllabicNasal = UserSettings.IsCheckJapaneseSyllabicNasal;
			IsCheckJananeseNasalGa = UserSettings.IsCheckJananeseNasalGa;
			VowelOption = UserSettings.VowelOption;
			IsCheckJapaneseRemoveNonSoundVowel = UserSettings.IsCheckJapaneseRemoveNonSoundVowel;
			IsCheckJapaneseSmallVowel = UserSettings.IsCheckJapaneseSmallVowel;
			IsOpenCeVIOWhenExport = UserSettings.IsOpenCeVIOWhenExport;
			IsExportAsTrac = UserSettings.IsExportAsTrac;
			IsExportSerifText = UserSettings.IsExportSerifText;
			PathToExportSerifTextDir = UserSettings.PathToExportSerifTextDir;
			DefaultExportSerifTextFileName = UserSettings.DefaultExportSerifTextFileName;
			SongExportLyricsMode = UserSettings.SongExportLyricsMode;
			AdaptingNoteToPitchMode = UserSettings.AdaptingNoteToPitchMode;

			CheckUserSettingsWhenDebug();	//実装もれのチェック

			logger.Info("UserSettings load finished.");
		}

		[ConditionalAttribute("DEBUG")]
		private void CheckUserSettingsWhenDebug(){
			var props = UserSettings.GetType().GetProperties();
			foreach (var prop in props)
			{
				var ignore = prop.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>();
				var isHide = prop.GetCustomAttribute<Models.HideForUserAttribute>();
				if (!(ignore is null) || !(isHide is null))
				{
					continue;	//jsonignore/hideforuserは無視
				}
				if(this.GetType().GetProperty(prop.Name) is null){
					throw new MissingFieldException($"{prop.Name} is not yet implimented!");
				}
			}
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

				UserSettings.PathToSaveDirectory
					= PathToSaveDirectory
					= cofd.FileName;
				var __ = UserSettings.SaveAsync();

				return new ValueTask();
			};
		}

		private ValueTask OpenSelectExportSerifTextDirDialog(RoutedEventArgs arg)
		{
			using var cofd = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog()
			{
				Title = "セリフテキストの出力フォルダをえらんでね",
				InitialDirectory = Path.GetFullPath(PathToExportSerifTextDir),
				IsFolderPicker = true,
			};
			if (cofd.ShowDialog() != Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
			{
				return new ValueTask();
			}

			UserSettings.PathToExportSerifTextDir
				= PathToExportSerifTextDir
				= cofd.FileName;
			var __ = UserSettings.SaveAsync();

			return new ValueTask();
		}

		private async ValueTask InsertMetaText(string buttonName)
		{

			var meta = buttonName switch
			{
				"insertCastName" => MetaTexts.CASTNAME,
				"insertDate" => MetaTexts.DATE,
				"insertSerif" => MetaTexts.SERIF,
				"insertTracName" => MetaTexts.TRACKNAME,
				_ => ""
			};

			if(buttonName=="insertReset"){
				DefaultExportSerifTextFileName = UserSettings.SERIF_FILE_NAME;
				return;
			}

			var index = await SerifTextFileNamePile.RentAsync(async box => await Task.Run(() => box.SelectionStart));

			DefaultExportSerifTextFileName =
				DefaultExportSerifTextFileName
					.Insert(index, meta);
		}

		private string GetWindowTitle(){
			var assembly = Assembly.GetExecutingAssembly().GetName();

			var versionInfo = Assembly
				.GetExecutingAssembly()
				.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute))
				.Cast<AssemblyInformationalVersionAttribute>()
				.FirstOrDefault();

			//return $"{assembly.Name} {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
			return $"{assembly.Name} ver. {versionInfo.InformationalVersion}";
		}

		private Task ExportFileAsync(bool isTrack)
		{
			throw new NotImplementedException();
		}

		private void TestAction(RoutedEventArgs e)
		{
			Debug.WriteLine("called test button.");
		}

		private async Task<Settings> LoadSettings()
		{
			return await LoadJsonAsync<Settings>("NodoAme.Settings.json");
		}

		private async Task<JapaneseRule> LoadJapanaseRule()
		{
			return await LoadJsonAsync<JapaneseRule>(@"dic\japanese.json");
		}


		private async ValueTask<T> LoadJsonAsync<T>(
			string pathToJson
		)
		{

			using var sr = new StreamReader(
				pathToJson,
				System.Text.Encoding.UTF8
			);
			var allLine = sr.ReadToEnd();
			sr.Close();

			try
			{
				var opt = new JsonSerializerOptions
				{
					Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
					WriteIndented = true
				};
				opt.Converters.Add(new JsonStringEnumConverter());

				T settings = JsonSerializer
					.Deserialize<T>(
						allLine,
						opt
					);
				return settings;
			}
			catch (JsonException e)
			{
				Debug.WriteLine(e.Message);
				await ShowErrorMessageBox(
					title:"Json読み取りエラー",
					message:e.Message
				);
				MainWindow.Logger.Error($"Json読み取りエラー: {e.Message}");
				return default;
			}
		}

		public async ValueTask ShowErrorMessageBox(
			string title,
			string message,
			Exception e = null
		){
			await Task.Run(() =>
			{
				MessageBox.Show(
					message ?? $"エラーが発生しました。{e}",
					title ?? "ERROR",
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
			});
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
				//EnableSerifButtons(index);
			}

			//return new ValueTask();
		}

		private void EnableSerifButtons(
			int index,
			bool isForceDisable = false
		)
		{
			var canPreview = TalkSoftItems[index].EnabledPreview ?? false;
			var canExport = TalkSoftItems[index].EnabledExport ?? false;
			foreach (var item in Serifs)
			{
				item.EnabledPreview = !isForceDisable && canPreview;
				item.EnabledExport = !isForceDisable && canExport;
				item.EnabledSerifInput = !isForceDisable;
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
		private async ValueTask VowelOptionChangedAsync(VowelOptions option)
		{
			IsCheckJapaneseRemoveNonSoundVowel = IsCheckJapaneseSmallVowel = false;
			switch (option)
			{
				case VowelOptions.Remove:
					IsCheckJapaneseRemoveNonSoundVowel = true;
					//return new ValueTask();
					break;
				case VowelOptions.Small:
					IsCheckJapaneseSmallVowel = true;
					//return new ValueTask();
					break;
				case VowelOptions.DoNothing:
				default:
					
					
					//return new ValueTask();
					break;
			}
			if(!(UserSettings is null)){
				UserSettings.VowelOption = option;
				await UserSettings.SaveAsync();
			}
			//return new ValueTask();

		}

		[PropertyChanged(nameof(ExportCastSelected))]
		private ValueTask ExportCastSelectedChangedAsync(int index)
		{
			//if(IsTalkSoftComboEnabled)InitVoices();
			if(ExportCastItems is null)return new ValueTask();

			var current = ExportCastItems[index];
			SongExportLyricsMode = current.LyricsMode;
			return new ValueTask();
		}



		private async ValueTask InitVoicesAsync()
		{
			IsPreviewComboEnabled = false;
			IsPreviewButtonEnabled = false;
			EnableSerifButtons(TalkSoftSelected,true);

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
				EnableSerifButtons(
					TalkSoftSelected,
					!this.talkEngine.IsActive
				);
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
					EnableSerifButtons(
						TalkSoftSelected,
						!this.talkEngine.IsActive
					);
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
					EnableSerifButtons(
						TalkSoftSelected,
						!this.talkEngine.IsActive
					);
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
			bool isTrack = false,
			SongCast songCast = null
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
				PathToSaveDirectory,
				SongExportLyricsMode,
				songCast,
				AdaptingNoteToPitchMode
				);
			
			if(IsExportSerifText){
				await talkEngine.ExportSerifTextFileAsync(
					serifText,
					PathToExportSerifTextDir,
					DefaultExportSerifTextFileName,
					songCast.Name
				);
			}

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

		public async ValueTask<string> ConvertFromListAsync(string sourceText)
		{
			this.talkEngine = await GenerateWrapper(
				this.currentEngine,
				_talksofts.ElementAt(TalkSoftSelected),
				_voices.ElementAt(TalkVoiceSelected)
			);
			return await PhonemeConverter.ConvertAsync(
				talkEngine: talkEngine,
				sourceText: sourceText,
				isUseSeparaterSpace: IsUseSeparaterSpace,
				isCheckJapaneseSyllabicNasal: IsCheckJapaneseSyllabicNasal,
				isCheckJapaneseNasalSonantGa: IsCheckJananeseNasalGa,
				vowelOption: VowelOption,
				isDebugOutput: false,
				isConvertToHiragana: IsConvertToHiragana
			);
		}

		[PropertyChanged(nameof(DefaultSerifLines))]
		private async ValueTask DefaultSerifLinesChangedAsync(int value){
			UserSettings.DefaultSerifLines = value;
			await UserSettings.SaveAsync();
		}

		[PropertyChanged(nameof(SongExportLyricsMode))]
		private async ValueTask SongExportLyricsModeChangedAsync(ExportLyricsMode value){
			UserSettings.SongExportLyricsMode = value;
			await UserSettings.SaveAsync();
		}

		[PropertyChanged(nameof(IsExportSerifText))]
		private async ValueTask IsExportSerifTextChangedAsync(bool value){
			UserSettings.IsExportSerifText = value;
			await UserSettings.SaveAsync();
		}
		
		[PropertyChanged(nameof(DefaultExportSerifTextFileName))]
		private async ValueTask DefaultExportSerifTextFileNameChangedAsync(string value){
			if(string.IsNullOrEmpty(value))value = UserSettings.SERIF_FILE_NAME;
			UserSettings.DefaultExportSerifTextFileName = value;
			await UserSettings.SaveAsync();
		}

		[PropertyChanged(nameof(IsConvertToHiragana))]
		private async ValueTask IsConvertToHiraganaChangedAsync(bool value){
			UserSettings.IsConvertToHiragana = value;
			await UserSettings.SaveAsync();
		}

		[PropertyChanged(nameof(IsConvertToPhoneme))]
		private async ValueTask IsConvertToPhonemeChangedAsync(bool value){
			UserSettings.IsConvertToPhoneme = value;
			await UserSettings.SaveAsync();
		}

		[PropertyChanged(nameof(IsCheckJapaneseSyllabicNasal))]
		private async ValueTask IsCheckJapaneseSyllabicNasalChangedAsync(bool value){
			UserSettings.IsCheckJapaneseSyllabicNasal = value;
			await UserSettings.SaveAsync();
		}

		[PropertyChanged(nameof(IsCheckJananeseNasalGa))]
		private async ValueTask IsCheckJananeseNasalGaChangedAsync(bool value){
			UserSettings.IsCheckJananeseNasalGa = value;
			await UserSettings.SaveAsync();
		}

		[PropertyChanged(nameof(IsCheckJapaneseRemoveNonSoundVowel))]
		private async ValueTask IsCheckJapaneseRemoveNonSoundVowelChangedAsync(bool value){
			UserSettings.IsCheckJapaneseRemoveNonSoundVowel = value;
			await UserSettings.SaveAsync();
		}

		[PropertyChanged(nameof(IsCheckJapaneseSmallVowel))]
		private async ValueTask IsCheckJapaneseSmallVowelChangedAsync(bool value){
			UserSettings.IsCheckJapaneseSmallVowel = value;
			await UserSettings.SaveAsync();
		}

		[PropertyChanged(nameof(IsOpenCeVIOWhenExport))]
		private async ValueTask IsOpenCeVIOWhenExportChangedAsync(bool value){
			UserSettings.IsOpenCeVIOWhenExport = value;
			await UserSettings.SaveAsync();
		}

		[PropertyChanged(nameof(IsExportAsTrac))]
		private async ValueTask IsExportAsTracChangedAsync(bool value){
			UserSettings.IsExportAsTrac = value;
			await UserSettings.SaveAsync();
		}
		

		[PropertyChanged(nameof(IsUseSeparaterSpace))]
		private ValueTask IsUseSeparaterSpaceChangedAsync(bool useSpace)
		{
			if (PhonemeConverter.CurrentPhonemes != null)
			{
				ConvertedText = PhonemeConverter.ChangeSeparater(useSpace);
			}
			if(!(UserSettings is null)){
				UserSettings.IsUseSeparaterSpace = useSpace;
				var _ = UserSettings.SaveAsync();
			}
			
			
			return new ValueTask();
		}

		[PropertyChanged(nameof(AdaptingNoteToPitchMode))]
		private async ValueTask IsAdaptingNoteToPitchChangedAsync(NoteAdaptMode value){
			UserSettings.AdaptingNoteToPitchMode = value;
			await UserSettings.SaveAsync();
		}
	}
}

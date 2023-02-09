using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.Win32;

using NAudio.Wave;

using NLog;
using NodoAme.Models;
using SharpOpenJTalk;

namespace NodoAme;

/// <summary>
/// トークエンジンラッパー
/// </summary>
public class Wrapper : ITalkWrapper
{
	//private const double NOTE_OFFSET = 1.6;
	/// <summary>
	/// 1小節開始目のindex値。LogF0/C0など。
	/// </summary>
	//private const int TRACK_PARAM_OFFSET_INDEX = 320;
	private const double INDEX_SPAN_TIME = 0.005;
	private const int SEC_RATE = 10000000;
	private const int SAMPLE_RATE = 48000;

	//private readonly char[] ENGLISH_SPLITTER = new char[] { '.', '?' };

	//private const string TalkEngine.CEVIO = "CeVIO";
	//private const string TalkEngine.OPENJTALK = "OpenJTalk";
	private string castToExport = "CSNV-JPF-THR1";
	private readonly string engineType;

	public TalkSoftVoice? TalkVoice { get; set; }
	public TalkSoftVoiceStylePreset? VoiceStyle { get; set; }
	public IList<TalkVoiceStyleParam>? VoiceStyleParams { get; set; }
	public TalkSoft TalkSoft { get; set; }
	public bool IsActive { get; set; }

	private dynamic? engine;
	/// <summary>
	/// A current CeVIO dll, dynamic loaded
	/// </summary>
	private Assembly? assembly;

	private ObservableCollection<TalkSoftVoice>? voices = new();
	private IList<string>? lastLabels;
	private Task<dynamic?>? lastTaskGetLabel;
	private readonly CancellationTokenSource cancelSource = new();

	private static readonly Logger logger = LogManager.GetCurrentClassLogger();

	private Wrapper(
		string type,
		TalkSoft soft,
		TalkSoftVoice? voice = null,
		TalkSoftVoiceStylePreset? style = null,
		IList<TalkVoiceStyleParam>? styleParams = null
	)
	{
		engineType = type;
		TalkSoft = soft;
		TalkVoice = voice ?? null;
		VoiceStyle = style;
		VoiceStyleParams = styleParams;
	}

	public static async ValueTask<Wrapper> FactoryAsync(
		string type,
		TalkSoft soft,
		TalkSoftVoice? voice = null,
		TalkSoftVoiceStylePreset? style = null,
		IList<TalkVoiceStyleParam>? styleParams = null
	)
	{
		var wrapper = new Wrapper(type, soft, voice, style, styleParams);
		await wrapper.InitAsync();
		return wrapper;
	}

	private async ValueTask InitAsync()
	{
		var soft = TalkSoft;
		var voice = TalkVoice;
		var style = VoiceStyle;

		IsActive = false;

		switch (engineType)
		{
			case TalkEngine.CEVIO:
				{
					if (soft.Interface is null) break;
					if (soft.Interface.Service is null) break;
					if (soft.Interface.Talker is null) break;

					//CeVIO Talk API interface 呼び出し
					string cevioPath =
						Environment.ExpandEnvironmentVariables(soft.Interface.EnvironmentProgramVar)
							+ soft.Interface.DllPath
							+ soft.Interface.DllName;
					Debug.WriteLine($"cevioPath:{cevioPath}");
					if (!File.Exists(cevioPath))
					{
						logger.Warn("error dialog opend:");
						MessageDialog.Show(
							$"{engineType}が見つかりませんでした。",
							$"{engineType}の呼び出しに失敗",

							MessageDialogType.Error
						);
						logger
							.Error($"CeVIO Dll not found:{engineType}の呼び出しに失敗");

						return;
					}

					try
					{
						assembly = Assembly.LoadFrom(cevioPath);
					}
					catch (Exception e)
					{
						logger.Warn($"error dialog opend: {e?.Message}");
						MessageDialog.Show(
							$"{engineType}を呼び出せませんでした。{e?.Message}",
							$"{engineType}の呼び出しに失敗",

							MessageDialogType.Error
						);
						logger
							.Fatal($"{e?.Message}");
						return;
					}

					Type? t = assembly.GetType(soft.Interface.Service);
					if (t is null)
					{
						logger.Warn("error dialog opend: ");
						MessageDialog.Show(
							$"{engineType}を呼び出せませんでした。",
							$"{engineType}の呼び出しに失敗",

							MessageDialogType.Error
						);
						logger
							.Error($"CeVIO Dll cannot call:{engineType}の呼び出しに失敗");
						return;
					}

					try
					{
						MethodInfo startHost = t.GetMethod("StartHost");
						var result = startHost.Invoke(null, new object[] { false });

						if ((int)result > 1)
						{
							logger.Warn("error dialog opend: ");
							MessageDialog.Show(
								$"{engineType}を起動できませんでした。理由code:{result}",
								$"{engineType}の起動に失敗",

								MessageDialogType.Error
							);
							logger
								.Error($"{engineType}を起動できませんでした。理由code:{result}");
							return;
						}
					}
					catch (System.Exception e)
					{
						var msg = $"{engineType}を起動できませんでした。理由:{e.Message}";
						MessageDialog.Show(
							msg,
							$"{engineType}の起動に失敗",

							MessageDialogType.Error
							);
						logger
							.Error(msg);
						throw new Exception(msg);
					}

					Type? t2 = assembly.GetType(soft.Interface.Agent);

					PropertyInfo property = t2.GetProperty("AvailableCasts");
					string[] names = (string[])property.GetValue(null, new object[] { });
					foreach (var n in names)
					{
						Debug.WriteLine(n);
						logger.Info($"Installed cevio cast: {n}");
						voices!
							.Add(new TalkSoftVoice { Id = $"Cast_{n}", Name = $"{n}" });
					}

					//CeVIOはインストールされているが、トークがない場合
					if (names.Length == 0)
					{
						var noCast = $"{engineType}のトークボイスが見つかりません。{engineType}のボイスをしゃべりの参考に使用するにはトークエディタとトークボイスが必要です。";
						MessageDialog.Show(
							noCast,
							$"{engineType}のトークボイスが見つかりません",

							MessageDialogType.Error
						);
						logger
							.Error(noCast);
						return;
					}

					//Type.GetTypeFromProgID(soft.Interface.Talker);
					try
					{
						Type? talker = assembly.GetType(soft.Interface.Talker);
						this.engine = Activator.CreateInstance(talker, new object[] { names[0] });
					}
					catch (Exception ex)
					{
						MessageDialog.Show(
							ex.Message,
							$"{engineType}の起動に失敗",

							MessageDialogType.Error
							);
						logger
							.Error($"can't awake cevio talker. {ex.Message}");
						throw;
					}

					IsActive = true;
					Debug.WriteLine($"engine:{engine.GetType()}");

					break;
				}

			case TalkEngine.VOICEVOX:
				{
					engine = await Voicevox
						.FactoryAsync(engineType, soft, voice, style);
					var vv = engine as Voicevox;
					if (vv!.IsActive)
					{
						//GetAvailableCasts
						foreach (var n in vv.AvailableCasts!)
						{
							voices!
								.Add(new TalkSoftVoice { Id = $"Cast_{n}", Name = $"{n}" });
						}
					}

					IsActive = vv.IsActive;
					break;
				}

			case TalkEngine.SOUNDFILE:
				{
					engine = await SpeakFileTalker
						.FactoryAsync();

					break;
				}

			case TalkEngine.OPENJTALK:
			default:
				{
					bool isInitialized = false;
					await Task.Run(() =>
					{
						this.engine = new OpenJTalkAPI();
						isInitialized = engine?.Initialize(
							soft.DicPath,
							//voice!.Styles.ElementAt()
							style?.Path ?? voice!.Styles![0].Path
						)
							?? false;
					});
					IsActive = isInitialized;

					if (!isInitialized)
					{
						var msg = $"{engineType} Initialize Failed";
						logger.Warn("error dialog opend: ");
						MessageDialog.Show(
							msg,
							msg,

							MessageDialogType.Error
						);
						logger
							.Error(msg);
						this.engine?.Dispose();
						throw new Exception(msg);
					}

					break;
				}
		}

		SetEngineParam(true);
	}

	~Wrapper()
	{
		if (this.engine?.GetType().ToString() == "System.__ComObject")
		{
			System.Runtime.InteropServices.Marshal.ReleaseComObject(engine);
		}
		else if (typeof(OpenJTalkAPI) == this.engine?.GetType())
		{
			this.engine.Dispose();
		}
	}

	public ObservableCollection<TalkSoftVoice>? GetAvailableCasts()
	{
		if (voices is null || voices.Count == 0)
		{
			logger.Warn("error dialog opend: ");
			MessageDialog.Show(
				"現在、利用できるボイスがありません！",
				"利用できるボイスがありません",

				MessageDialogType.Error
			);
			logger
				.Error("現在、利用できるボイスがありません！");
			return new();
		}

		return this.voices;
	}

	public async ValueTask<IList<string>> GetLabelsAsync(string sourceText)
	{
		//dynamic list;
		if (this.engine is null)
		{
			logger.Error("GetLabelsAsync(): this.engine is null");
			throw new NullReferenceException();
		}

		switch (engineType)
		{
			case TalkEngine.CEVIO:
				{
					//var talker = engine;

					engine.Cast = TalkVoice!.Name;

					/*
					var phs = engine.GetPhonemes(sourceText);
					for (int i = 0; i < phs.Length; i++)
					{
						var ph = phs.At(i);
						Debug.WriteLine(ph.Phoneme);
					}
					*/
					if (
						lastTaskGetLabel?.IsCompleted == false &&
							cancelSource.Token.CanBeCanceled)
					{
						cancelSource.Cancel();
					}

					this.lastTaskGetLabel = Task.Run(
						() => cancelSource.Token.IsCancellationRequested
							? null
							: engine.GetPhonemes(sourceText),
						cancelSource.Token);

					dynamic? ps = null;
					try
					{
						ps = await lastTaskGetLabel;
					}
					catch (OperationCanceledException)
					{
						Debug.WriteLine($"\n{nameof(OperationCanceledException)} thrown\n");
						return new List<string>() { "" };
					}

					if (ps is null) { return this.lastLabels!; }

					this.lastLabels = MakePsudoLabels(ps);
					return this.lastLabels;
				}
			//break;
			case TalkEngine.VOICEVOX:
				{
					var vv = this.engine as Voicevox;
					var vps = await vv!.GetPhonemes(sourceText);
					Debug.WriteLine(vps);
					return MakePsudoLabels((dynamic)vps);
				}

			case TalkEngine.SOUNDFILE:
				{
					//use LibSasara.Lab
					var sft = engine as SpeakFileTalker;
					var ps = await sft!.GetPhonemesAsync(sourceText);
					Debug.WriteLine(
						ps
							.Select(v => v.Phoneme)
							.ToArray()
					);
					return MakePsudoLabels(ps)!;
				}

			case TalkEngine.OPENJTALK:
			default:
				return await Task.Run(
					() =>
					{
						var jtalk = engine as OpenJTalkAPI;

						try
						{
							var s = //await Task.Run(
									//()=>
								jtalk?.GetLabels(sourceText)
								//)
								;
							return s.ToList();
						}
						catch (Exception ex)
						{
							logger.Error(ex.Message);

							//return new List<string>();
							throw;
						}
					}
				);
				//break;
		}
		//return list;
	}

	/// <summary>
	/// CeVIOの音素データをフルコンテクストラベル形式に擬似的に変換する
	/// </summary>
	/// <param name="phs">CeVIOの音素データ</param>
	/// <returns>フルコンテクストラベル形式</returns>
	private List<string>? MakePsudoLabels(/*ICeVIOPhonemeDataArray*/dynamic phs)
	{
		var list = new List<string>();
		for (int i = 0; i < phs.Length; i++)
		{
			var ph = phs[i];

			string s = $"{GetPhoneme(phs, i - 2)}^{GetPhoneme(phs, i - 1)}-{ph.Phoneme}+{GetPhoneme(phs, i + 1)}={GetPhoneme(phs, i + 2)}/";

			list.Add(s);
			//list += s;
		}

		return list;
	}

	private string GetPhoneme(dynamic phonemes, int index)
	{
		if (index < 0) { return "xx"; }
		else if (phonemes.Length - 1 < index) { return "xx"; }
		else
		{
			return phonemes[index].Phoneme;
		}
	}

	public ObservableCollection<TalkSoftVoiceStylePreset> GetStylePresets()
	{
		var styles = new ObservableCollection<TalkSoftVoiceStylePreset>();
		switch (engineType)
		{
			case TalkEngine.CEVIO:
				{
					Type? talker = assembly!.GetType(TalkSoft.Interface!.Talker);
					Debug.WriteLine($"cast: {this.TalkVoice!.Name!}");
					this.engine = Activator.CreateInstance(
						talker,
						new object[] { this.TalkVoice!.Name! }
					);
					var comps = engine.Components;
					foreach (var c in comps)
					{
						//Debug.WriteLine($"c:{c}");
						styles.Add(new TalkSoftVoiceStylePreset { Id = c.Id, Name = c.Name, Value = c.Value });
					}

					//return styles;

					break;
				}

			case TalkEngine.OPENJTALK:
				{
					foreach (var style in this.TalkVoice!.Styles!)
					{
						styles.Add(style);
					}

					break;
				}

			case TalkEngine.VOICEVOX:
				{
					var vv = engine as Models.Voicevox;
					var temp = vv!
						.VoicevoxCasts
						.Find(cast => cast.Name == this.TalkVoice!.Name!)
						.Styles;
					foreach (var s in temp!)
					{
						styles.Add(new ()
							{
								Name = s.Name,
								Id = s.Id.ToString()
							});
					}

					break;
				}

			case TalkEngine.SOUNDFILE:
			default:
				//return styles;
				break;
		}

		return styles;
	}

	public ObservableCollection<TalkVoiceStyleParam> GetVoiceStyles()
	{
		var styles = new ObservableCollection<TalkVoiceStyleParam>();
		switch (engineType)
		{
			case TalkEngine.CEVIO:
				{
					Type? talker = assembly!.GetType(TalkSoft.Interface!.Talker);
					Debug.WriteLine($"cast: {this.TalkVoice!.Name!}");
					this.engine = Activator.CreateInstance(
						talker,
						new object[] { this.TalkVoice!.Name! }
					);
					var comps = engine.Components;
					foreach (var c in comps)
					{
						//Debug.WriteLine($"c:{c}");
						styles.Add(new ()
							{
								Id = c.Id,
								Name = c.Name,
								Value = c.Value,
								DefaultValue = c.Value,
								Min = 0,
								Max = 100,
								SmallChange = 1
							});
					}

					//return styles;

					break;
				}

			case TalkEngine.SOUNDFILE:
			case TalkEngine.OPENJTALK:
			case TalkEngine.VOICEVOX:
			default:
				//return styles;
				break;
		}

		return styles;
	}

	/// <summary>
	/// TTSに発話させる
	/// </summary>
	/// <param name="text">発話させるテキスト</param>
	/// <param name="withSave"></param>
	/// <returns>秒数</returns>
	public async ValueTask<string> SpeakAsync(
		string text,
		bool withSave = false
	)
	{
		if (this.engine is null)
		{
			logger.Error("SpeakAsync(): this.engine is null");
			return "ERROR";
			throw new NullReferenceException("SpeakAsync(): this.engine is null");
		}

		double time = 0;
		switch (engineType)
		{
			case TalkEngine.CEVIO:
				{
					engine.Cast = TalkVoice!.Name;
					Debug.WriteLine($"CAST:{engine.Cast}");
					SetEngineParam();
					SetVoiceStyle(false);

					time = engine.GetTextDuration(text);
					var state = engine.Speak(text);

					if (withSave)
					{
						var dialog = new SaveFileDialog
						{
							FileName = $"{GetSafeFileName(text)}.wav",
							Filter = "*.wav",
							Title = "Save preview voice"
						};
						dialog.ShowDialog();
					}
					//state.Wait();
					break;
				}

			case TalkEngine.OPENJTALK:
				{
					//var engine = new OpenJTalkAPI();
					if (engine is not OpenJTalkAPI jtalk)
					{
						logger.Warn("error dialog opend");
						MessageDialog.Show(
							"現在、利用できるボイスがありません！",
							"利用できるボイスがありません",

							MessageDialogType.Error
						);
						const string msg = "OpenJTalk.Speak(): this.engine is null";
						logger.Error(msg);
						throw new Exception(msg);
					}

					jtalk.FramePeriod = 240;
					jtalk.SamplingFrequency = SAMPLE_RATE;
					jtalk.Volume = 0.9;

					SetEngineParam();

					//var jtalk = new OpenJTalkAPI();
					//jtalk.Synthesis(text, false, true);
					await Task.Run(() =>
					{
						jtalk.Synthesis(text, false, true);
						List<byte> buf = jtalk.WavBuffer;

						//play audio wav data
						using var ms = new MemoryStream(buf.ToArray());
						var rs = new RawSourceWaveStream(
							ms,
							new WaveFormat(SAMPLE_RATE, 16, 1)
						);
						time = rs.TotalTime.TotalSeconds;
						var wo = new WaveOutEvent();
						wo.Init(rs);
						wo.Play();
						while (wo.PlaybackState == PlaybackState.Playing)
						{
							Thread.Sleep(500);
						}

						wo.Dispose();
					});
					break;
				}

			case TalkEngine.VOICEVOX:
				{
					SetEngineParam();
					SetVoiceStyle();
					var vv = this.engine as Voicevox;
					vv!.Cast = TalkVoice!.Name!;

					time = await vv!.SpeakAsync(text);
					break;
				}

			case TalkEngine.SOUNDFILE:
				{
					var sft = this.engine as SpeakFileTalker;
					var path = text;
					time = await sft!.SpeakAsync(path);
					break;
				}

			default:

				break;
		}

		return Convert.ToString(time);
	}

	public async ValueTask PreviewSaveAsync(string serifText)
	{
		await SpeakAsync(serifText, true);
	}

	public void SetVoiceStyle(
		bool usePreset = true
	)
	{
		if (this.VoiceStyle is null)
		{
			logger.Warn("no voice styles.");
			return;
		}

		switch (this.engineType)
		{
			case TalkEngine.CEVIO:
				{
					if (usePreset)
					{
						//プリセット
						foreach (var c in engine!.Components)
						{
							if (this.VoiceStyle.Id == c.Id)
							{
								c.Value = 100;  //感情値ValueをMAXに
								Debug.WriteLine($"Current Style:{c.Name}");
							}
							else
							{
								c.Value = 0;    //感情値Valueをゼロに
							}
						}
					}
					else
					{
						IList<TalkVoiceStyleParam>? voiceStyleParams = this.VoiceStyleParams;
						//感情合成
						foreach (var c in engine!.Components)
						{
							var p = voiceStyleParams.First(v => v.Id == c.Id);
							if (p is null) continue;
							Debug.WriteLine($"VoiceStyle: {p.Name} {p.Value}");
							c.Value = (uint)p.Value;
						}
					}

					break;
				}

			case TalkEngine.VOICEVOX:
				{
					var vv = this.engine as Voicevox;
					vv!.Style = this.VoiceStyle;
					//this.VoiceStyle =
					break;
				}

			case TalkEngine.SOUNDFILE:
			default:
				//なにもしない
				break;
		}
	}

	private void SetEngineParam(bool isInit = false)
	{
		var tp = this.TalkSoft.TalkSoftParams;
		if (tp is null) { return; }

		//値を割当
		foreach (var p in tp!)
		{
			PropertyInfo prop = engine!.GetType().GetProperty(p.Id);
			var newParam = isInit ?
				p.DefaultValue :    //初期値
				p.Value             //UIの値
				;
			newParam ??= 0;             //null check
			var notNullDouble = newParam ?? 0;

			switch (engineType)
			{
				case TalkEngine.CEVIO:
					{
						uint val = (uint)Math.Round(notNullDouble);
						prop.SetValue(
							engine,
							val
						);
						break;
					}

				case TalkEngine.OPENJTALK:
				case TalkEngine.VOICEVOX:
					{
						prop.SetValue(
							engine,
							newParam
						);
						break;
					}

				case TalkEngine.SOUNDFILE:
				default:
					break;
			}

			//UI反映
			p.Value = newParam;
		}
	}

	/// <summary>
	/// ファイルにエクスポートする
	/// </summary>
	public async ValueTask<bool> ExportFileAsync(ExportFileOption option)
	{
		if (this.engine is null)
		{
			//throw new NullReferenceException();
			return false;//new ValueTask<bool>(false);
		}

		var sw = new System.Diagnostics.Stopwatch();
		sw.Start();

		//出力SongCast
		this.castToExport = option.CastId;
		Debug.WriteLine($"songcast:{option.CastId}");

		//テンプレートファイル読み込み
		var tmplTrack = option.IsExportAsTrack ?
			XElement.Load("./template/temp.track.xml") :   //ccst file
			XElement.Load("./template/temp.track.xml")     //TODO:ccs file（現在はトラックのみ）
			;
		var guid = Guid.NewGuid().ToString("D");    //GUIDの生成

		sw.Stop();
		Debug.WriteLine($"TIME[read xml]:{sw.ElapsedMilliseconds}");
		sw.Restart();

		//TODO:エンジンごとの解析部分とccst加工部分を分ける
		if (engineType == TalkEngine.CEVIO)
		{
			engine.Cast = TalkVoice!.Name;
			SetVoiceStyle(false);
		}
		else if (engineType == TalkEngine.VOICEVOX)
		{
			engine.Style = this.VoiceStyle;
			SetVoiceStyle(false);
		}

		SetEngineParam();

		//serifLen = engine.GetTextDuration(serifText);
		var dandp = (engineType is TalkEngine.SOUNDFILE) ?
			await GetTextDurationAndPhonemesFromFileAsync(option.LabelFilePath)	 :
			await GetTextDurationAndPhonemesAsync(option.SerifText);
		///
		double serifLen = dandp.duration;
		var phs = dandp.phs;
		//var labels = await GetLabelsAsync(serifText);

		Debug.WriteLine($"--TIME[tmg eliminate(text duration)]:{sw.ElapsedMilliseconds}");

		//TODO: .labファイル出力
		MakeLabFile(phs);

		//Estimate F0
		var parameters = (engineType is TalkEngine.SOUNDFILE) ?
			await EstimateFileAsync(option.SoundFilePath) :
			await EstimateF0Async(option.SerifText);

		//Note elements
		var scoreNodes = tmplTrack.Descendants("Score");
		var scoreRoot = scoreNodes.First();

		//声質(Alpha)指定
		ProjectWriter.WriteAttributeAlpha(scoreRoot, engineType);

		//感情(Emotion)設定
		ProjectWriter.WriteAttributeEmotion(
			option.Cast,
			option.SongVoiceStyles,
			scoreRoot);

		//tmplTrack.Element("Score");
		//<Note Clock="3840" PitchStep="7" PitchOctave="4" Duration="960" Lyric="ソ" DoReMi="true" Phonetic="m,a" />
		var duration = NoteUtil
			.GetTickDuration(
				serifLen,
				option.Tempo);
		Debug.WriteLine($"duration :{duration}");

		//Convert phonemes from En to Ja
		if (option.ExportMode == ExportLyricsMode.EN_TO_JA)
		{
			phs = PhonemeConverter.EnglishToJapanese(phs);
		}

		//split notes
		(List<dynamic>? notesList, int phNum)
			= await ProjectWriter.SplitPhonemesToNotesAsync(phs, option.ExportMode, option.NoteSplitMode);
		Debug.WriteLine($"--TIME[tmg eliminate(split notes)]:{sw.ElapsedMilliseconds}");

		if (notesList is null)
		{
			return false;
		}
		///<summary>
		/// timing node root
		/// </summary>
		var timingNode = new XElement(
			"Timing",
			new XAttribute("Length", (phNum * 5) + 10)
		);

		double noteOffset = 60 / option.Tempo * 4;

		//Scoreを計算して書き込む
		ProjectWriter.CulcScores(
			option.ExportMode,
			option.NoteAdaptMode,
			option.NoteSplitMode,
			parameters,
			scoreRoot,
			notesList,
			timingNode,
			engineType,
			noteOffset, //NOTE_OFFSET,
			option.NoSoundVowelsModes,
			option.Tempo
		);

		//トラック全体のDynamicsを書き込む or 上書き
		ProjectWriter.WriteElementsDynamics(
			scoreRoot,
			option.Dynamics
		);
		//トラック全体のTempoを上書き
		//デフォルトはよくあるBPM=150
		ProjectWriter.WriteElementsTempo(
			tmplTrack,
			option.Tempo
		);

		// TMGの線を書き込む
		ProjectWriter.WriteElementsTiming(tmplTrack, timingNode);
		sw.Stop();
		Debug.WriteLine($"TIME[end tmg eliminate]:{sw.ElapsedMilliseconds}");
		sw.Restart();

		//LogF0 elements

		//LogF0, Alpha等のルート要素を取得
		var parameterRoot = tmplTrack
			.Descendants("Parameter")
			.First();
		double paramLen = GetParametersLength(serifLen, option.Tempo);

		//F0をピッチ線として書き込む
		XElement logF0Node = ProjectWriter
			.WriteElementsLogF0(
				parameters,
				parameterRoot,
				paramLen,
				engineType,
				(int)(noteOffset / INDEX_SPAN_TIME),
				//TRACK_PARAM_OFFSET_INDEX,
				option.NoPitch
			);
		sw.Stop();
		Debug.WriteLine($"TIME[end f0]:{sw.ElapsedMilliseconds}");
		sw.Restart();

		//VOL elements
		XElement volumeNode = ProjectWriter.WriteElementsC0(
			option.BreathSuppress,
			phs,
			parameterRoot,
			paramLen,
			(int)(noteOffset / INDEX_SPAN_TIME),
			INDEX_SPAN_TIME,
			option.NoSoundVowelsModes
		);
		sw.Stop();
		Debug.WriteLine($"TIME[end vol]:{sw.ElapsedMilliseconds}");
		sw.Restart();

		//TODO:エンジンごとの解析部分とccst加工部分を分ける
		//TODO:ccst加工部分の共通処理化

		//Unit elements
		ProjectWriter.WriteElementsUnit(tmplTrack, guid, serifLen, castToExport);

		//Group elements
		ProjectWriter.WriteElementsGroup(option.SerifText, option.Cast, tmplTrack, guid, castToExport);

		//tssprj
		var tssprj = option.FileType == ExportFileType.TSSPRJ
			? ProjectWriter.WriteTssprj(
				option.Cast,
				scoreRoot,
				timingNode,
				logF0Node,
				volumeNode)
			: Array.Empty<byte>();

		sw.Stop();
		Debug.WriteLine($"TIME[end genarate xml]:{sw.ElapsedMilliseconds}");
		sw.Restart();

		//set trac file name
		dynamic exportData = option.FileType switch
		{
			ExportFileType.TSSPRJ => tssprj,
			ExportFileType.CCS => tmplTrack,
			_ => tmplTrack
		};
		await ExportCoreAsync(
			option.SerifText,
			option.ExportPath,
			exportData,
			option.IsOpenCeVIO,
			option.FileType,
			option.Cast
		);

		sw.Stop();
		Debug.WriteLine($"TIME[end export file.]:{sw.ElapsedMilliseconds}");
		//sw.Restart();

		logger.Info($"Export file sucess!{option.ExportPath}:{option.SerifText}");
		return true;//new ValueTask<bool>(true);
	}

	/// <summary>
	/// F0を解析する
	/// </summary>
	/// <param name="serifText"></param>
	/// <returns></returns>
	/// <exception cref="NotImplementedException"></exception>
	private async ValueTask<WorldParameters> EstimateF0Async(string serifText)
	{
		WorldParameters parameters;

		if (engineType == TalkEngine.CEVIO
		 || engineType == TalkEngine.VOICEVOX)
		{
			//出力音声をテンポラリ出力し、WORLDで解析する
			parameters = await EstimateAsync(serifText);
		}
		else if (engineType == TalkEngine.OPENJTALK)
		{
			//Open JTalk はf0を直接取得
			parameters = new WorldParameters(100);
			engine!.Synthesis(serifText, false, true);
			double[] fa = this.engine.GetLF0Array();
			parameters.f0 = fa;
			parameters.f0_length = fa.Length;
		}
		else
		{
			throw new NotImplementedException();
		}

		return parameters;
	}

	public static double GetParametersLength(double serifLen, double tempo)
	{
		var serifIndexs = Math.Round(serifLen / INDEX_SPAN_TIME);
		var noteOffset = 60.0 / tempo * 4;
		var iOffset = (int)(noteOffset / INDEX_SPAN_TIME);
		var offsetSerifIndex = serifIndexs + iOffset;
		var d = Math.Ceiling(offsetSerifIndex / iOffset);
		return (d + 1) * iOffset;
	}

	/// <summary>
	/// 文字列の長さ（秒）と音素リストを取得
	/// </summary>
	/// <param name="serifText"></param>
	/// <returns></returns>
	private async ValueTask<(double duration, List<Models.Label> phs)> GetTextDurationAndPhonemesAsync(string serifText)
	{
		var len = 0.0;
		List<Models.Label>? phs = new();
		if (string.IsNullOrEmpty(serifText))
		{
			DefaultPhoneme(phs);
			return (len, phs);
		}

		switch (engineType)
		{
			case TalkEngine.CEVIO:
				{
					//len
					len = engine!.GetTextDuration(serifText);
					//phonemes
					var phonemes = engine.GetPhonemes(serifText);
					foreach (var item in phonemes)
					{
						phs.Add(new Models.Label(
							item.Phoneme,
							item.StartTime,
							item.EndTime
						));
					}

					break;
				}

			case TalkEngine.VOICEVOX:
				{
					var vv = this.engine as Voicevox;
					(var phonemes, len) = await vv!.GetPhonemesAndLength(serifText);
					phs = (phonemes as Label[]).ToList();
					break;
				}

			case TalkEngine.OPENJTALK:
				{
					OpenJTalkAPI? jtalk = engine;
					if (jtalk is null)
					{
						logger.Error(
							"OpenJTalk is null");
						break;
					}

					jtalk.SamplingFrequency = SAMPLE_RATE;
					jtalk.Volume = 0.9;
					jtalk.FramePeriod = 240;
					SetEngineParam();

					try
					{
						var result = jtalk.Synthesis(serifText, false, true);
						if (!result)
						{
							throw new SystemException("Open JTalk Systhesis is not success!");
						}
					}
					catch (Exception e)
					{
						logger.Error(
							$"OpenJTalk Error: {e?.Message}");
						MessageDialog.Show(
							$"申し訳ありません。内蔵ボイスでエラーが発生しました。\n詳細：{e?.Message}",
							"内蔵ボイスエラー",

							MessageDialogType.Error
						);
						break;
					}

					List<List<string>> lbl = jtalk.Labels;

					//合成エラー反映
					if (lbl is null
						|| lbl.Count == 0
						|| lbl.Last() is null)
					{
						const string error = "can't get labels";
						logger.Error(
							$"OpenJTalk Error: {error}");
						MessageDialog.Show(
							$"申し訳ありません。内蔵ボイスでエラーが発生しました。\n詳細：{error}",
							"内蔵ボイスエラー",

							MessageDialogType.Error
						);
						break;
					}

					//len
					var s = lbl[1]
						.Last()
						.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1];
					if (!double.TryParse(s, out len))
					{
						len = 0.0;
					}
					else
					{
						len /= SEC_RATE;
					}
					//phonemes
					phs = lbl[1]
						.ConvertAll(str =>
						{
							var a = str.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
							var sTime = double.Parse(a[0]) / SEC_RATE;
							var eTime = double.Parse(a[1]) / SEC_RATE;
							var p = a[2]
								.Split(new char[] { '/' })[0]
								.Split(new char[] { '^', '-', '+', '=' })[2];
							return new Label(p, sTime, eTime);
						});
					break;
				}

			default:
				break;
		}

		return (len, phs);

		static void DefaultPhoneme(List<Label> phs)
		{
			phs.Add(new("sil", 0, 0));
		}
	}

	private async ValueTask<(double duration, List<Models.Label> phs)> GetTextDurationAndPhonemesFromFileAsync(string path){
		var sft = engine as SpeakFileTalker;
		(var phonemes, var len) = await sft!.GetPhonemesAndLengthAsync(path);
		return (len, phs: (phonemes as Label[]).ToList());
	}

	private async ValueTask ExportCoreAsync(
		string trackFileName,
		string exportPath,
		dynamic/*XElement or byte[]*/ tmplTrack,
		bool isOpenCeVIO,
		ExportFileType fileType,
		SongCast cast
	)
	{
		logger.Info($"export start: '{exportPath}', {trackFileName}");
		var safeName = GetSafeFileName(trackFileName);
		var outDirPath = exportPath;
		var outFile = fileType switch
		{
			ExportFileType.CCS => $"{GetSafeFileName(cast.Id!)}_{safeName}.ccst",
			ExportFileType.TSSPRJ => $"{GetSafeFileName(cast.CharaNameAsAlphabet!)}_{safeName}.tssprj",
			_ => $"{GetSafeFileName(cast.Id!)}_{safeName}.ccst"
		};
		if (!Directory.Exists(outDirPath))
		{
			try
			{
				Directory.CreateDirectory(outDirPath);
			}
			catch (System.Exception e)
			{
				logger.Warn($"error dialog opend: {e?.Message}");
				MessageDialog.Show(
					$"「{outDirPath}」に新しくフォルダを作ることができませんでした。保存先はオプションで設定できます。\n詳細：{e?.Message}",
					"フォルダの作成に失敗！",

					MessageDialogType.Error
				);
				logger.Error($"failed to create a export directory:{outDirPath}");
				logger.Error($"{e?.Message}");

				return;
			}
		}

		//check path output
		var outPath = "";
		try
		{
			outPath = Path.Combine(outDirPath, outFile);
		}
		catch (Exception e)
		{
			MessageDialog.Show(
				$"ファイルの書き出し先（{outDirPath}, {outFile}）を上手く作れませんでした。\n詳細：{e.Message}",
				"ファイルの書き出し先作成に失敗！",

				MessageDialogType.Error
			);
			logger.Error($"failed to create a output path:{outDirPath}, {outFile}");
			logger.Error($"{e.Message}");

			return;
		}

		//save
		if (fileType == ExportFileType.CCS)
		{
			//save for cevio ccs
			var xml = tmplTrack as XElement;
			await Task.Run(() =>
			{
				try
				{
					xml!.Save(outPath);
				}
				catch (Exception e)
				{
					MessageDialog.Show(
						$"「{outPath}」にファイルを作ることができませんでした。保存先はオプションで設定できます。\n詳細：{e.Message}",
						"ファイルの作成に失敗！",

						MessageDialogType.Error
				   );
					logger.Error($"failed to create a file:{outPath}");
					logger.Error($"{e.Message}");

					return;
				}
			});
		}
		else if (fileType == ExportFileType.TSSPRJ)
		{
			//save for voisona tssprj
			var bin = tmplTrack as byte[];
			try
			{
				using var writer = new BinaryWriter(new FileStream(outPath, FileMode.Create));
				writer.Write(bin);
			}
			catch (Exception e)
			{
				MessageDialog.Show(
					$"「{outPath}」にファイルを作ることができませんでした。保存先はオプションで設定できます。\n詳細：{e.Message}",
					"ファイルの作成に失敗！",

					MessageDialogType.Error
			   );
				logger.Error($"failed to create a file:{outPath}");
				logger.Error($"{e.Message}");

				return;
			}
		}

		if (isOpenCeVIO)
		{
			//CeVIOにファイルを渡す
			var psi = new ProcessStartInfo()
			{
				FileName = Path.GetFullPath(outPath)//,
													//UseShellExecute = true
			};
			await Task.Run(() => Process.Start(psi));
		}
	}

	/// <summary>
	/// カレーうどんをすするファイルを出力する
	/// </summary>
	/// <param name="isExportAsTrack"></param>
	/// <param name="isOpenCeVIO"></param>
	/// <param name="exportPath"></param>
	/// <returns></returns>
	public async ValueTask<bool> ExportSpecialFileAsync(
		SongCast cast,
		bool isExportAsTrack = true,
		bool isOpenCeVIO = false,
		string exportPath = "",
		ExportFileType fileType = ExportFileType.CCS,
		ExportLyricsMode exportMode = ExportLyricsMode.KANA
	)
	{
		if (this.engine is null)
		{
			return false;//new ValueTask<bool>(false);
		}

		//トラック＆ファイル名に使用
		const string TRACK_FILE_NAME = "SUSURU";

		//出力SongCast
		this.castToExport = cast.Id!;
		Debug.WriteLine($"songcast:{cast.Id}");

		//テンプレートxml読み込み
		var tmplTrack = isExportAsTrack ?
			await Task.Run(() => XElement.Load("./template/temp.SUSURU.xml")) : //ccst file
			await Task.Run(() => XElement.Load("./template/temp.SUSURU.xml"))   //TODO:ccs file（現在はトラックのみ）
			;
		var guid = Guid.NewGuid().ToString("D");    //GUIDの生成

		var unitNode = tmplTrack.Descendants("Unit").First();
		unitNode.SetAttributeValue("Group", guid);
		unitNode.SetAttributeValue("CastId", castToExport);

		var groupNode = tmplTrack.Descendants("Group").First();
		groupNode.SetAttributeValue("Name", TRACK_FILE_NAME);
		groupNode.SetAttributeValue("Id", guid);
		groupNode.SetAttributeValue("CastId", castToExport);

		var noteNode = tmplTrack.Descendants("Note").First();
		string lyricZu = exportMode switch
		{
			ExportLyricsMode.KANA or ExportLyricsMode.EN_TO_JA => "ず",
			_ => "zu"
		};
		noteNode.SetAttributeValue("Lyric", lyricZu);

		await ExportCoreAsync(
			TRACK_FILE_NAME,
			exportPath,
			tmplTrack,
			isOpenCeVIO,
			fileType,
			cast
		);

		return true;//new ValueTask<bool>(true);
	}

	/// <summary>
	/// セリフファイルをtextで出力する
	/// </summary>
	/// <param name="serifText"></param>
	/// <param name="exportPath"></param>
	/// <param name="fileNamePattern">ファイル名のパターン（CeVIO互換予定）</param>
	/// <returns></returns>
	public async ValueTask<bool> ExportSerifTextFileAsync(
		string serifText,
		string exportPath,
		string fileNamePattern,
		string SongCastName
	)
	{
		var safeName = GetSafeFileName(serifText);
		var outDirPath = exportPath;

		fileNamePattern = string.IsNullOrEmpty(fileNamePattern) ? UserSettings.SERIF_FILE_NAME : fileNamePattern;
		var outFile = fileNamePattern;
		outFile = FileNameReplace(outFile, MetaTexts.SERIF, safeName);
		outFile = FileNameReplace(outFile, MetaTexts.CASTNAME, SongCastName ?? "ANY");
		outFile = FileNameReplace(outFile, MetaTexts.DATE, DateTime.Now.ToLocalTime().ToString("yyyymmdd"));
		//TODO:outFile = FileNameReplace(outFile, "$連番$", "$連番$");
		outFile = FileNameReplace(outFile, MetaTexts.TRACKNAME, safeName);

		if (!Directory.Exists(outDirPath))
		{
			try
			{
				Directory.CreateDirectory(outDirPath);
			}
			catch (System.Exception e)
			{
				MessageDialog.Show(
					$"「{outDirPath}」に新しくフォルダを作ることができませんでした。保存先はオプションで設定できます。\n詳細：{e.Message}",
					"フォルダの作成に失敗！",

					MessageDialogType.Error
				);
				logger.Error($"failed to create a export directory:{outDirPath}");
				logger.Error($"{e.Message}");

				return false;
			}
		}
		var outPath = Path.Combine(outDirPath, outFile);

		try
		{
			using var writer = new StreamWriter(
				outPath,
				false,
				System.Text.Encoding.UTF8
			);
			await writer.WriteAsync(serifText);
		}
		catch (System.Exception e)
		{
			MessageDialog.Show(
				$"セリフファイルの保存に失敗しました。ファイル「{outFile}」を「{outDirPath}」に保存できませんでした。\n詳細：{e.Message}",
				"セリフファイルの保存に失敗！",

				MessageDialogType.Error
			);
			logger.Error($"failed to save a serif file:{outPath}");
			logger.Error($"{e.Message}");

			return false;
		}

		return true;
	}

	internal string FileNameReplace(
		string outFile,
		string pattern,
		string replaceTo
	)
	{
		return Regex.Replace(
			outFile,
			Regex.Escape(pattern),
			replaceTo ?? "ANY"
		);
	}

	private async ValueTask<bool> ExportWaveToFileAsync(
		string serifText,
		string pathToSave
	)
	{
		bool result = true;
		switch (engineType)
		{
			case TalkEngine.CEVIO:
				{
					result = await Task.Run(
						() => engine!.OutputWaveToFile(serifText, pathToSave)
					);
					break;
				}

			case TalkEngine.OPENJTALK:
				{
					List<byte> buf = engine!.WavBuffer;
					//var ms = new MemoryStream(buf.ToArray());
					//var rs = new RawSourceWaveStream(ms, new WaveFormat(SAMPLE_RATE, 16, 1));
					//WaveFileWriter.CreateWaveFile(pathToSave, rs);

					using var writer = new WaveFileWriter(pathToSave, new WaveFormat(SAMPLE_RATE, 16, 1));
					//writer.WriteData(buf.ToArray(), 0, buf.ToArray().Length);
					await writer.WriteAsync(buf.ToArray(), 0, buf.ToArray().Length);
					break;
				}

			case TalkEngine.VOICEVOX:
				{
					var vv = engine as Voicevox;
					result = await vv!.OutputWaveToFile(serifText, pathToSave);
					break;
				}

			default:
				{
					result = false;
					break;
				}
		}

		logger
			.Info($"Success save wav file.:{pathToSave}:{serifText}");
		return result;
	}

	/// <summary>
	/// ピッチ推定
	/// </summary>
	/// <param name="serifText"></param>
	/// <returns></returns>
	/// <exception cref="Exception"></exception>
	private async ValueTask<WorldParameters> EstimateAsync(string serifText)
	{
		var tempName = Path.GetTempFileName();

		var resultOutput = await ExportWaveToFileAsync(serifText, tempName);
		if (!resultOutput)
		{
			var msg = $"Faild to save temp file!:{tempName}";
			logger.Error(msg);
			throw new Exception(msg);
		}

		var param = await EstimateFileAsync(tempName);

		if (tempName != null && File.Exists(tempName))
		{
			await Task.Run(() => File.Delete(tempName));  //remove temp file
		}

		return param;
	}

	private async ValueTask<WorldParameters> EstimateFileAsync(string path){
		var (fs, nbit, len, x) = await Task.Run(() => WorldUtil.ReadWav(path));
		var parameters = new WorldParameters(fs);
		//ピッチ推定
		await Task.Run(() => WorldUtil.EstimateF0(
			WorldUtil.Estimaion.Harvest,
			x,
			len,
			parameters
		));
		//音色推定
		await Task.Run(() => WorldUtil.EstimateSpectralEnvelope(
			x,
			len,
			parameters
		));

		return parameters;
	}

	private static string GetSafeFileName(string serifText)
	{
		var illigalStr = Path.GetInvalidFileNameChars();
		illigalStr = illigalStr.Concat(Path.GetInvalidPathChars()).ToArray<char>();
		return string.Concat(serifText.Where(c => !illigalStr.Contains(c)));
	}

	private static void MakeLabFile(dynamic phs)
	{
		var lab = "";
		foreach (var ph in phs)
		{
			lab += $"{ph.StartTime * SEC_RATE} {ph.EndTime * SEC_RATE} {ph.Phoneme}\n";
		}
		Debug.WriteLine("lab:\n" + lab);
		//TODO:.labファイル出力
	}
}
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using Microsoft.Win32;
using NAudio.Wave;
using SharpOpenJTalk;
using NodoAme.Models;
using NLog;

namespace NodoAme
{

	/// <summary>
	/// トークソフトのデータを表現する
	/// </summary>
	public class TalkSoft{
		[JsonPropertyName("id")]
		public string? Id {get;set;}
		[JsonPropertyName("name")]
		public string? Name { get; set; }
		/// <summary>
		/// TTSのDropDown listから非表示にします
		/// </summary>
		[JsonPropertyName("hidden")]
		public bool? Hidden { get; set; }
		[JsonPropertyName("enabledPreview")]
		public bool? EnabledPreview { get; set; }
		[JsonPropertyName("enabledExport")]
		public bool? EnabledExport { get; set; }
		[JsonPropertyName("voices")]
		public IList<TalkSoftVoice>? TalkSoftVoices {get;set;}
		[JsonPropertyName("interface")]
		public TalkSoftInterface? Interface {get;set;}
		[JsonPropertyName("dic")]
		public string? DicPath { get; set; }
		[JsonPropertyName("voiceParam")]
		public IList<TalkSoftParam>? TalkSoftParams { get; set; }
	}

	public class TalkSoftVoice{
		[JsonPropertyName("id")]
		public string? Id {get;set;}
		[JsonPropertyName("name")]
		public string? Name { get; set; }
		[JsonPropertyName("path")]
		public string? Path {get;set;}
		[JsonPropertyName("styles")]
		public IList<TalkSoftVoiceStylePreset>? Styles { get; set; }
	}


	/// <summary>
	/// トークソフトの基本パラメータ
	/// ソフト毎にパラメータを変えられる
	/// </summary>
	public class TalkSoftParam{
		[JsonPropertyName("id")]
		public string? Id {get;set;}
		[JsonPropertyName("name")]
		public string? Name { get; set; }
		[JsonPropertyName("min")]
		public double Min { get; set; }
		[JsonPropertyName("max")]
		public double Max { get;set;}

		[JsonPropertyName("defaultValue")]
		public double? DefaultValue { get; set; }

		public double? Value { get; set; }
	}

	public class TalkSoftVoiceStylePreset{
		[JsonPropertyName("id")]
		public string? Id {get;set;}
		[JsonPropertyName("name")]
		public string? Name { get; set; }
		public uint? Value { get; set; }

		[JsonPropertyName("path")]
		public string? Path {get;set;}
	}

	public class TalkSoftInterface{
		[JsonPropertyName("type")]
		public string? Type {get;set;}
		[JsonPropertyName("engine")]
		public string? Engine {get;set;}
		[JsonPropertyName("env_prog")]
		public string? EnvironmentProgramVar { get; set; }
		[JsonPropertyName("dll")]
		public string? DllName { get; set; }
		[JsonPropertyName("dll_dir")]
		public string? DllPath { get; set; }

		[JsonPropertyName("service")]
		public string? Service {get;set;}
		[JsonPropertyName("talker")]
		public string? Talker {get;set;}
		[JsonPropertyName("agent")]
		public string? Agent {get;set;}
		[JsonPropertyName("restHost")]
		public string? RestHost { get; set; }
	}


	/// <summary>
	/// トークエンジン列挙
	/// </summary>
	public static class TalkEngine{
		public const string CEVIO = "CeVIO";
		public const string OPENJTALK = "OpenJTalk";
		public const string VOICEVOX = "VOICEVOX";
	}

	/// <summary>
	/// トークエンジンラッパー
	/// </summary>
	public class Wrapper{
		private const double NOTE_OFFSET = 1.6;
		/// <summary>
		/// 1小節開始目のindex値。LogF0/C0など。
		/// </summary>
		private const int TRACK_PARAM_OFFSET_INDEX = 320;
		private const double INDEX_SPAN_TIME = 0.005;
		private const int SEC_RATE = 10000000;
		private const int SAMPLE_RATE = 48000;
		//private const string TalkEngine.CEVIO = "CeVIO";
		//private const string TalkEngine.OPENJTALK = "OpenJTalk";
		private string CastToExport = "CSNV-JPF-THR1";
		private string engineType;
		public TalkSoftVoice? TalkVoice { get; set; }
		public TalkSoftVoiceStylePreset? VoiceStyle { get; set; }
		public TalkSoft TalkSoft { get; set; }
		public bool IsActive { get; set; }

		private dynamic? engine;
		/// <summary>
		/// A current CeVIO dll, dynamic loaded
		/// </summary>
		private Assembly? assembly;

		private ObservableCollection<TalkSoftVoice>? voices = new ObservableCollection<TalkSoftVoice>();

		private static readonly Logger logger = LogManager.GetCurrentClassLogger();
 

		internal Wrapper(
			string type,
			TalkSoft soft,
			TalkSoftVoice? voice = null,
			TalkSoftVoiceStylePreset? style = null
		){
			engineType = type;
			TalkSoft = soft;
			TalkVoice = voice ?? null;
			VoiceStyle = style!=null ? VoiceStyle : null;
			//var _ = Init();
		}

		public static async ValueTask<Wrapper> Factory(
			string type,
			TalkSoft soft,
			TalkSoftVoice? voice = null,
			TalkSoftVoiceStylePreset? style = null
		)
		{
			var wrapper = new Wrapper(type, soft, voice, style);
			await wrapper.Init();
			return wrapper;
		}


		private async ValueTask Init(){
			var soft = TalkSoft;
			var voice = TalkVoice;
			var style = VoiceStyle;

			IsActive = false;

			switch(engineType){
				case TalkEngine.CEVIO:
					if(soft.Interface is null)break;
					if(soft.Interface.Service is null)break;
					if(soft.Interface.Talker is null)break;

					//CeVIO Talk API interface 呼び出し
					string cevioPath = 
						Environment.ExpandEnvironmentVariables(soft.Interface.EnvironmentProgramVar)
						+ soft.Interface.DllPath
						+ soft.Interface.DllName;
					Debug.WriteLine($"cevioPath:{cevioPath}");
					if(!File.Exists(cevioPath)){
						MessageBox.Show(
                            $"{engineType}が見つかりませんでした。",
                            $"{engineType}の呼び出しに失敗",
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error
                        );
						logger
							.Error($"CeVIO Dll not found:{engineType}の呼び出しに失敗");
						
						return;
					}
					try{
						assembly = Assembly.LoadFrom(cevioPath);
					}catch(Exception e){
						MessageBox.Show(
							$"{engineType}を呼び出せませんでした。{e.Message}",
                            $"{engineType}の呼び出しに失敗",
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error
                        );
						logger
							.Fatal($"{e.Message}");
						return;
					}
					Type? t = assembly.GetType(soft.Interface.Service);
					if(t is null){
						MessageBox.Show(
							$"{engineType}を呼び出せませんでした。",
                            $"{engineType}の呼び出しに失敗",
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error
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
							MessageBox.Show(
								$"{engineType}を起動できませんでした。理由code:{result}",
								$"{engineType}の起動に失敗",
								MessageBoxButton.OK,
								MessageBoxImage.Error
							);
							logger
							.Error($"{engineType}を起動できませんでした。理由code:{result}");
							return;
						}
                    }
					catch (System.Exception e)
					{
						var msg = $"{engineType}を起動できませんでした。理由:{e.Message}";
						MessageBox.Show(
								msg,
								$"{engineType}の起動に失敗",
								MessageBoxButton.OK,
								MessageBoxImage.Error
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
							.Add(new TalkSoftVoice{Id=$"Cast_{n}", Name=$"{n}"});
					}

					//CeVIOはインストールされているが、トークがない場合
					if(names is null || names.Length == 0){
						var noCast = $"{engineType}のトークボイスが見つかりません。{engineType}のボイスをしゃべりの参考に使用するにはトークエディタとトークボイスが必要です。";
						MessageBox.Show(
                            noCast,
                            $"{engineType}のトークボイスが見つかりません",
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error
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
						MessageBox.Show(
								ex.Message,
								$"{engineType}の起動に失敗",
								MessageBoxButton.OK,
								MessageBoxImage.Error
							);
						logger
							.Error($"can't awake cevio talker. {ex.Message}");
						throw;
					}

					IsActive = true;
					Debug.WriteLine($"engine:{engine.GetType()}");

					break;
				case TalkEngine.VOICEVOX:


					this.engine = await Models.Voicevox.Factory(engineType, soft, voice, style);
					var vv = this.engine as Voicevox;
					if(vv!.IsActive){
						//GetAvailableCasts
						foreach(var n in vv.AvailableCasts!){
							voices!
								.Add(new TalkSoftVoice{Id=$"Cast_{n}", Name=$"{n}"});
						}
					}
					IsActive = vv.IsActive;
					break;
				case TalkEngine.OPENJTALK:
				default:
					this.engine = new OpenJTalkAPI();
					bool isInitialized = engine?.Initialize(
							soft.DicPath,
							//voice!.Styles.ElementAt()
							style?.Path ?? voice!.Path
						) ?? false;
					IsActive = isInitialized;
					
					if (!isInitialized)
					{
						var msg = $"{engineType} Initialize Failed";
						MessageBox.Show(
							msg,
							msg,
							MessageBoxButton.OK, 
							MessageBoxImage.Error
						);
						logger
							.Error(msg);
						throw new Exception(msg);
					}
					break;
			}

			SetEngineParam(true);
		}

		~Wrapper(){
			if(this.engine?.GetType().ToString() == "System.__ComObject"){
				System.Runtime.InteropServices.Marshal.ReleaseComObject(engine);
			}
			else if(typeof(OpenJTalkAPI) == this.engine?.GetType()){
				this.engine.Dispose();
			}

		}
		public ObservableCollection<TalkSoftVoice>? GetAvailableCasts(){
			if (voices is null || voices.Count == 0)
			{
				MessageBox.Show(
					"現在、利用できるボイスがありません！",
					"利用できるボイスがありません",
					MessageBoxButton.OK, 
					MessageBoxImage.Error
				);
				logger
					.Error("現在、利用できるボイスがありません！");
				return voices;
			}

			return this.voices;
		}

		public async ValueTask<IList<string>> GetLabelsAsync(string sourceText){
			//dynamic list;
			if(this.engine is null){
				logger.Error("GetLabelsAsync(): this.engine is null");
				throw new NullReferenceException();
			}
			switch(engineType){
				case TalkEngine.CEVIO:
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

					var ps = await Task.Run(
						()=>engine.GetPhonemes(sourceText));
					return MakePsudoLabels(ps);
					//break;
				case TalkEngine.VOICEVOX:
					var vv = this.engine as Voicevox;
					var vps = await vv!.GetPhonemes(sourceText);
					Debug.WriteLine(vps);
					return MakePsudoLabels((dynamic)vps);
					//TalkVoice.Id
					//break;
				case TalkEngine.OPENJTALK:
				default:
					return await Task.Run(
						() => {
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
		private dynamic? MakePsudoLabels(/*ICeVIOPhonemeDataArray*/dynamic phs)
		{
			dynamic list = new List<string>();
			for (int i = 0; i < phs.Length; i++)
			{
				var ph = phs[i];
				
				string s = $"{GetPhoneme(phs, i-2)}^{GetPhoneme(phs, i-1)}-{ph.Phoneme}+{GetPhoneme(phs, i+1)}={GetPhoneme(phs, i+2)}/";

				list.Add(s);
				//list += s;
			}


			return list;
		}

		private string GetPhoneme(dynamic phonemes, int index){
			if (index < 0) { return "xx"; }
			else if (phonemes.Length - 1 < index) { return "xx"; }
			else
			{
				return phonemes[index].Phoneme;
			}
		}

		public ObservableCollection<TalkSoftVoiceStylePreset> GetStyles(){
			var styles = new ObservableCollection<TalkSoftVoiceStylePreset>();
			switch (engineType)
			{
				case TalkEngine.CEVIO:
					Type? talker = assembly!.GetType(TalkSoft.Interface!.Talker);
					Debug.WriteLine($"cast: {this.TalkVoice!.Name!}");
					this.engine = Activator.CreateInstance(
						talker,
						new object[]{this.TalkVoice!.Name!}
					);
					var comps = engine.Components;
					foreach (var c in comps)
					{
						//Debug.WriteLine($"c:{c}");
						styles.Add(new TalkSoftVoiceStylePreset { Id = c.Id, Name = c.Name, Value = c.Value });
					}

					//return styles;

					break;
				case TalkEngine.OPENJTALK:
					foreach (var style in this.TalkVoice!.Styles!)
					{
						styles.Add(style);
					}
					break;
				case TalkEngine.VOICEVOX:
					var vv = engine as Models.Voicevox;
					var temp = vv!
						.VoicevoxCasts
						.Find(cast => cast.Name == this.TalkVoice!.Name!)
						.Styles;
					foreach (var s in temp!)
					{
						styles.Add(new TalkSoftVoiceStylePreset
						{
							Name = s.Name,
							Id = s.Id.ToString()
						});
					}
					break;
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
		public async ValueTask Speak(string text, bool withSave = false){
			if (this.engine is null)
			{
				logger.Error("Speak(): this.engine is null");
				throw new NullReferenceException("Speak(): this.engine is null");
			}

			switch(engineType){
				case TalkEngine.CEVIO:
					engine.Cast = TalkVoice!.Name;
					Debug.WriteLine($"CAST:{engine.Cast}" );
					SetEngineParam();
					SetVoiceStyle();

					var state = engine.Speak(text);

					if(withSave){
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
				case TalkEngine.OPENJTALK:
					//var engine = new OpenJTalkAPI();
					if (!(engine is OpenJTalkAPI jtalk))
					{
						MessageBox.Show(
							"現在、利用できるボイスがありません！",
							"利用できるボイスがありません",
							MessageBoxButton.OK,
							MessageBoxImage.Error
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

					jtalk.Synthesis(text, false, true);
					List<byte> buf = jtalk.WavBuffer;

					//play audio wav data
					{
						using var ms = new MemoryStream(buf.ToArray());
						var rs = new RawSourceWaveStream(
							ms,
							new WaveFormat(SAMPLE_RATE, 16, 1)
						);
						var wo = new WaveOutEvent();
						wo.Init(rs);
						wo.Play();
						while (wo.PlaybackState == PlaybackState.Playing)
						{
							Thread.Sleep(500);
						}
						wo.Dispose();
					}
					break;
				case TalkEngine.VOICEVOX:
					SetEngineParam();
					SetVoiceStyle();
					var vv = this.engine as Voicevox;
					vv!.Cast = TalkVoice!.Name!;
					
					await vv!.SpeakAsync(text);
					break;
				default:

					break;
			}
		}

		public async ValueTask PreviewSave(string serifText){
			await Speak(serifText, true);
		}

		private void SetVoiceStyle()
		{
			if (this.VoiceStyle is null)
			{
				logger.Warn("no voice styles.");
				return;
			};

			switch(this.engineType)
			{
				case TalkEngine.CEVIO:
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
						//Debug.WriteLine($"c:{c}");
						//styles.Add(new TalkSoftVoiceStylePreset { Id = c.Id, Name = c.Name, Value = c.Value });
					}
					break;
				case TalkEngine.VOICEVOX:
					var vv = this.engine as Voicevox;
					vv!.Style = this.VoiceStyle;
					//this.VoiceStyle = 
					break;
				default:
					//なにもしない
					break;
			}
		}

		private void SetEngineParam(bool isInit = false)
		{
			var tp = this.TalkSoft.TalkSoftParams;

			//TODO:Voicevox対応
			if(engineType==TalkEngine.VOICEVOX){return;}

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
						uint val = (uint)Math.Round(notNullDouble);
						prop.SetValue(
							engine,
							val
						);
						break;
					case TalkEngine.OPENJTALK:
						prop.SetValue(
							engine,
							newParam
						);
						break;
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
		/// <param name="serifText"></param>
		/// <param name="isExportAsTrack">trueの場合はccst</param>
		/// <returns></returns>
		/// <exception cref="NullReferenceException"></exception>
		public async ValueTask<bool> ExportFileAsync(
			string serifText,
			string castId,
			double alpha,
			bool isExportAsTrack = true,
			bool isOpenCeVIO = false,
			string exportPath = ""
		)
		{
			if (this.engine is null)
			{
				//throw new NullReferenceException();
				return false;//new ValueTask<bool>(false);
			}

			var sw = new System.Diagnostics.Stopwatch();
			sw.Start();

			//出力SongCast
			this.CastToExport = castId;
			Debug.WriteLine($"songcast:{castId}");



			//テンプレートxml読み込み
			var tmplTrack = isExportAsTrack ? 
				XElement.Load(@"./template/temp.track.xml") :	//ccst file
				XElement.Load(@"./template/temp.track.xml")		//TODO:ccs file（現在はトラックのみ）
				;
			var guid = Guid.NewGuid().ToString("D");    //GUIDの生成

			sw.Stop();
			Debug.WriteLine($"TIME[read xml]:{sw.ElapsedMilliseconds}");
			sw.Restart();

			///
			double serifLen = 0.0;

			//TODO:エンジンごとの解析部分とccst加工部分を分ける
			//TODO:ccst加工部分の共通処理化
			switch (engineType)
			{
				case TalkEngine.CEVIO:
				case TalkEngine.OPENJTALK:
				case TalkEngine.VOICEVOX:
					if (engineType == TalkEngine.CEVIO)
					{
						engine.Cast = TalkVoice!.Name;
						SetVoiceStyle();
					}else if(engineType == TalkEngine.VOICEVOX){
						engine.Style = this.VoiceStyle;
					}
					SetEngineParam();

					//serifLen = engine.GetTextDuration(serifText);
					var dandp = await this.GetTextDurationAndPhonemes(serifText);
					serifLen = dandp.duration;
					var phs = dandp.phs;    //TODO: use this.GetLabels()
					var labels = await GetLabelsAsync(serifText);

					Debug.WriteLine($"--TIME[tmg eliminate(text duration)]:{sw.ElapsedMilliseconds}");

					//TODO: .labファイル出力
					MakeLabFile(phs);

					//Note elements
					//TODO: csや他ソフト向けにF0を解析してnoteを割り当て、付与する
					var scoreNodes = tmplTrack.Descendants("Score");
					var scoreRoot = scoreNodes.First();

					//声質(Alpha)指定
					scoreRoot.SetAttributeValue("Alpha", 0.0);
					if(engineType == TalkEngine.CEVIO || engineType == TalkEngine.OPENJTALK){
						var alphaParam = TalkSoft
						.TalkSoftParams
						.First(a => a.Id == "Alpha");
						var newAlpha = (alphaParam.Value / 100 * 2) - 1.0;
						if (engineType == TalkEngine.OPENJTALK) newAlpha = 0;   //TODO:暫定対応

						scoreRoot.SetAttributeValue("Alpha", newAlpha.ToString());
					}

					//tmplTrack.Element("Score");
					//<Note Clock="3840" PitchStep="7" PitchOctave="4" Duration="960" Lyric="ソ" DoReMi="true" Phonetic="m,a" />
					var duration = GetTickDuration(serifLen);
					Debug.WriteLine($"duration :{duration}");

					//pauの区切りでノートに分割する
					var notesList = new List<dynamic>();
					var noteList = new List<dynamic>();
					var phNum = 0;
					for (int i = 0; i < phs.Count; i++)
					{
						switch (phs[i].Phoneme)
						{
							case "sil": //ignore phoneme
								break;
							case "pau": //split note
								notesList.Add(new List<dynamic>(noteList));
								noteList.Clear();
								phNum++;
								break;
							default:    //append
								noteList.Add(phs[i]);
								phNum++;
								break;
						}
					}
					notesList.Add(new List<dynamic>(noteList));

					Debug.WriteLine($"--TIME[tmg eliminate(split notes)]:{sw.ElapsedMilliseconds}");

					if (notesList is null) return false;// new ValueTask<bool>(false);

					///<summary>timing node root</summary>
					var timingNode = new XElement("Timing",
						new XAttribute("Length", (phNum * 5) + 10)
					);

					var phCount = 0;
					var pauCount = 0;
					foreach (List<dynamic> nList in notesList)
					{
						var phText = "";
						var noteLen = 0;
						var startClock = Double.MaxValue;
						var startPhonemeTime = Double.MaxValue;


						for (int i = 0; i < nList.Count; i++)
						{
							var ph = nList[i];
							var start = ph.StartTime;
							if (startPhonemeTime > start)
							{
								//最初の音素の開始時刻を指定
								startClock = Math.Round(GetTickDuration(start));
								startPhonemeTime = start;
							}
							phText += ph.Phoneme.ToLower() + ",";   //TODO:「ん」対応
							noteLen += GetTickDuration(ph.EndTime - ph.StartTime);


							//timing elements
							phCount++;
							var count = (5 * (phCount + pauCount)) - 1;
							var timingData = new XElement("Data",
								new XAttribute("Index", count.ToString()),
								NOTE_OFFSET + start
							);
							timingNode.Add(timingData);

							//途中の青線も指定しないと消えてしまう
							//単純分割だとCSでちゃんと聞こえない

							var spanTime = ph.EndTime - start;

							foreach (int t in Enumerable.Range(1, 4))
							{
								var first = spanTime / 2;
								var add = t switch
								{
									1 => first,
									_ => first + first * 1 / 5 * t
								};
								timingNode
									.Add(new XElement(
										"Data",
										NOTE_OFFSET + start + add
									));
								Debug.WriteLine($"add:{add}");
							}
							//}
						}
						pauCount++;
						phText = phText.TrimEnd(",".ToCharArray());
						Debug.WriteLine($"phText :{phText}");

						//note
						var note = new XElement("Note",
							new XAttribute("Clock", 3840 + startClock),
							new XAttribute("PitchStep", "7"),
							new XAttribute("PitchOctave", "4"),
							new XAttribute("Duration", noteLen),
							new XAttribute("Lyric", phText.Replace(",", "")/*serifText*/),
							new XAttribute("DoReMi", "false"),
							new XAttribute("Phonetic", phText)
						);
						scoreRoot.Add(note);


					}

					


					//Timing elements
					//TMGの線を書き込む
					var songRoot = tmplTrack
						.Descendants("Song")
						.First();
					songRoot
						.Add(new XElement("Parameter", timingNode));
					
					sw.Stop();
					Debug.WriteLine($"TIME[end tmg eliminate]:{sw.ElapsedMilliseconds}");
					sw.Restart();

					//F0
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
						engine.Synthesis(serifText, false, true);
						double[] fa = this.engine.GetLF0Array();
						parameters.f0 = fa;
						parameters.f0_length = fa.Length;
					}
					else
					{
						throw new NotImplementedException();
					}

					//LogF0, Alpha等のルート要素を取得
					var parameterRoot = tmplTrack
						.Descendants("Parameter")
						.First();
					double paramLen = GetParametersLength(serifLen);

					//LogF0 elements
					//F0をピッチ線として書き込む
					#region write_logf0
					var logF0Node = new XElement("LogF0",
						new XAttribute("Length", paramLen)
					);

					//double lastLogF0 = 0;
					//int repeatCount = 0;
					for (int i = 0; i < parameters.f0_length; i++)
					{
						var logF0 = engineType switch
						{
							TalkEngine.CEVIO
								 => Math.Log(parameters.f0![i]),
							TalkEngine.OPENJTALK
								 => parameters.f0![i],
							TalkEngine.VOICEVOX
								 => Math.Log(parameters.f0![i]),
							_ => 0
						};
						if (parameters.f0![i] <= 0) { continue; }

						var node = new XElement("Data",
							new XAttribute("Index", (TRACK_PARAM_OFFSET_INDEX + i).ToString()),
							logF0.ToString()
						);
						logF0Node.Add(node);

					}


					parameterRoot.Add(logF0Node);
					sw.Stop();
					Debug.WriteLine($"TIME[end f0]:{sw.ElapsedMilliseconds}");
					sw.Restart();
					#endregion


					//VOL elements
					//Volumeの線を書き込む
					#region write_c0
					//volume(C0) node root


					var volumeNode = new XElement("C0",
						new XAttribute("Length", paramLen)
					);

					const string VOL_ZERO = "-2.4";

					//CeVIOのバグ開始部分のVOLを削る
					var startVol = new XElement("Data",
							new XAttribute("Index", 0),
							new XAttribute("Repeat", TRACK_PARAM_OFFSET_INDEX / 4), //4分の1拍無音化
							VOL_ZERO
						);
					volumeNode.Add(startVol);

					//無声母音のVOLを削る
					var reg = new Regex("[AIUEO]", RegexOptions.Compiled);

					var noSoundVowels = phs
						.Where(l => !(l.Phoneme is null))
						.Where(l => l.Phoneme!.Length == 1 && char.IsUpper(Convert.ToChar(l.Phoneme)))
						.Where(l => reg.IsMatch(l.Phoneme));
					foreach (var ph in noSoundVowels)
					{
						var s = ph.StartTime is null ? 0.0 : (double)ph.StartTime! / INDEX_SPAN_TIME;
						var index = Math.Round(s, 0, MidpointRounding.AwayFromZero);
						var e = ph.EndTime is null ? 0.0 : (double)ph.EndTime! / INDEX_SPAN_TIME;
						var eIndex = Math.Round(e, 0, MidpointRounding.AwayFromZero);
						var rep = eIndex - index;

						var tVol = new XElement("Data",
							new XAttribute("Index", TRACK_PARAM_OFFSET_INDEX + index),
							new XAttribute("Repeat", rep),
							VOL_ZERO
						);
						volumeNode.Add(tVol);
					}

					//TODO:間の吐息を抑える

					//CeVIOのバグ終了部分のVOLを削る
					var endVol = new XElement("Data",
							new XAttribute("Index", paramLen - TRACK_PARAM_OFFSET_INDEX),
							new XAttribute("Repeat", TRACK_PARAM_OFFSET_INDEX), //1小節
							VOL_ZERO
						);
					volumeNode.Add(endVol);


					parameterRoot.Add(volumeNode);

					sw.Stop();
					Debug.WriteLine($"TIME[end vol]:{sw.ElapsedMilliseconds}");
					sw.Restart();
					#endregion

					break;
				/*
				case ENGINE_OPENJTALK:
					SetEngineParam();
					
					var dandp2 = this.GetTextDurationAndPhonemes(serifText);
					serifLen = dandp2.duration;
					var phs2 = dandp2.phs;
					break;
				*/
				default:
					break;
			}

			//TODO:エンジンごとの解析部分とccst加工部分を分ける
			//TODO:ccst加工部分の共通処理化

			//Unit elements
			#region unit_elements
			var unitNode = tmplTrack.Descendants("Unit").First();
			var span = new TimeSpan(0, 0, (int)Math.Ceiling(serifLen));
			var hhmmss = span.ToString(@"hh\:mm\:ss");
			unitNode.SetAttributeValue("Group", guid);
			unitNode.SetAttributeValue(
				"Duration",
				hhmmss              //track duration
			);
			//Castを選択可能にする
			unitNode.SetAttributeValue("CastId", CastToExport);
			#endregion

			//Group elements
			//トラック名とIDを書き込む
			var groupNode = tmplTrack.Descendants("Group").First();
			groupNode.SetAttributeValue("Name", serifText);
			groupNode.SetAttributeValue("Id", guid);
			groupNode.SetAttributeValue("CastId", CastToExport);

			sw.Stop();
			Debug.WriteLine($"TIME[end genarate xml]:{sw.ElapsedMilliseconds}");
			sw.Restart();

			//set trac file name
			await ExportCore(
				serifText,
				exportPath,
				tmplTrack,
				isOpenCeVIO
			);

			sw.Stop();
			Debug.WriteLine($"TIME[end export file.]:{sw.ElapsedMilliseconds}");
			//sw.Restart();

			logger.Info($"Export file sucess!{exportPath}:{serifText}");
			return true;//new ValueTask<bool>(true);

		}

		private static double GetParametersLength(double serifLen)
		{
			var serifIndexs = Math.Round(serifLen / INDEX_SPAN_TIME);
			var offsetSerifIndex = serifIndexs + TRACK_PARAM_OFFSET_INDEX;
			var d = Math.Ceiling(offsetSerifIndex / TRACK_PARAM_OFFSET_INDEX);
			return (d+1) * TRACK_PARAM_OFFSET_INDEX;

		}

		private async ValueTask<(double duration, List<Models.Label> phs)> GetTextDurationAndPhonemes(string serifText){
			var len = 0.0;
			List<Models.Label>? phs = new List<Models.Label>();
			switch (engineType)
			{
				case TalkEngine.CEVIO:
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
				case TalkEngine.VOICEVOX:
					var vv = this.engine as Voicevox;
					(phonemes,len) = await vv!.GetPhonemesAndLength(serifText);
					phs = (phonemes as Label[]).ToList();
					break;
				case TalkEngine.OPENJTALK:
					engine!.SamplingFrequency = SAMPLE_RATE;
					engine.Volume = 0.9;
					engine.FramePeriod = 240;
					SetEngineParam();

					/*
					var jtalk = new OpenJTalkAPI();
					jtalk.Synthesis(text, false, true);
					jtalk.WavBuffer
					*/

					engine.Synthesis(serifText, false, true);
					List<List<string>> lbl = engine.Labels;
					//len
					var s = lbl[1].Last<string>().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1];
					if(!Double.TryParse(s, out len)){
						len = 0.0;
					}else{
						len /= SEC_RATE;
					}
					//phonemes
					phs = lbl[1]
						.ConvertAll(str =>
						{
							var a = str.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
							var sTime = Double.Parse(a[0])/SEC_RATE;
							var eTime = Double.Parse(a[1])/SEC_RATE;
							var p = a[2]
								.Split(new char[] { '/' })[0]
								.Split(new char[] { '^', '-', '+', '=' })[2];
							return new Label(p, sTime, eTime);
						});
					break;
				default:
					break;
			}
			return(len, phs);
		}

		private async ValueTask ExportCore(
			string trackFileName,
			string exportPath,
			XElement tmplTrack,
			bool isOpenCeVIO
		){
			var safeName = GetSafeFileName(trackFileName);
			var outDirPath = exportPath;
			var outFile = $"{GetSafeFileName(CastToExport)}_{safeName}.ccst";
			if(!Directory.Exists(outDirPath)){
				try
				{
					Directory.CreateDirectory(outDirPath);
				}
				catch (System.Exception e)
				{
					logger.Error($"failed to create a export directory:{outDirPath}");
					logger.Error($"{ e.Message }");
					throw;
				}
			}
			var outPath = Path.Combine(outDirPath, outFile);
			//save
			await Task.Run(() =>tmplTrack.Save(outPath));

			if(isOpenCeVIO){
				//CeVIOにファイルを渡す
				var psi = new ProcessStartInfo()
				{
					FileName = Path.GetFullPath(outPath)//,
					//UseShellExecute = true
				};
				await Task.Run(()=>Process.Start(psi));
			}
		}

		/// <summary>
		/// カレーうどんをすするファイルを出力する
		/// </summary>
		/// <param name="castId"></param>
		/// <param name="isExportAsTrack"></param>
		/// <param name="isOpenCeVIO"></param>
		/// <param name="exportPath"></param>
		/// <returns></returns>
		public async ValueTask<bool> ExportSpecialFile(
			string castId,
			bool isExportAsTrack = true,
			bool isOpenCeVIO = false,
			string exportPath = ""
		){
			if (this.engine is null)
			{
				return false;//new ValueTask<bool>(false);
			}

			//トラック＆ファイル名に使用
			const string TRACK_FILE_NAME = "SUSURU";

			//出力SongCast
			this.CastToExport = castId;
			Debug.WriteLine($"songcast:{castId}");

			//テンプレートxml読み込み
			var tmplTrack = isExportAsTrack ? 
				await Task.Run(() => XElement.Load("./template/temp.SUSURU.xml")) :	//ccst file
				await Task.Run(() => XElement.Load("./template/temp.SUSURU.xml"))	//TODO:ccs file（現在はトラックのみ）
				;
			var guid = Guid.NewGuid().ToString("D");    //GUIDの生成

			var unitNode = tmplTrack.Descendants("Unit").First();
			unitNode.SetAttributeValue("Group", guid);
			unitNode.SetAttributeValue("CastId", CastToExport);

			var groupNode = tmplTrack.Descendants("Group").First();
			groupNode.SetAttributeValue("Name", TRACK_FILE_NAME);
			groupNode.SetAttributeValue("Id", guid);
			groupNode.SetAttributeValue("CastId", CastToExport);

			await ExportCore(
				TRACK_FILE_NAME,
				exportPath,
				tmplTrack,
				isOpenCeVIO
			);

			return true;//new ValueTask<bool>(true);
		}

		private async ValueTask<bool> ExportWaveToFileAsync(
			string serifText,
			string pathToSave
		){
			bool result = true;
			switch (engineType)
			{
				case TalkEngine.CEVIO:
					result = await Task.Run(
						()=>engine!.OutputWaveToFile(serifText, pathToSave)
					);
					break;
				case TalkEngine.OPENJTALK:
					List<byte> buf = engine!.WavBuffer;
					//var ms = new MemoryStream(buf.ToArray());
					//var rs = new RawSourceWaveStream(ms, new WaveFormat(SAMPLE_RATE, 16, 1));
					//WaveFileWriter.CreateWaveFile(pathToSave, rs);

					using (WaveFileWriter writer = new WaveFileWriter(pathToSave, new WaveFormat(SAMPLE_RATE, 16, 1)))
					{
						//writer.WriteData(buf.ToArray(), 0, buf.ToArray().Length);
						await writer.WriteAsync(buf.ToArray(), 0, buf.ToArray().Length);
					}
					break;
				case TalkEngine.VOICEVOX:
					var vv = engine as Voicevox;
					result = await vv!.OutputWaveToFile(serifText, pathToSave);
					break;
				default:
					result = false;
					break;
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
			var (fs, nbit, len, x) = await Task.Run(()=> WorldUtil.ReadWav(tempName));
			var parameters = new WorldParameters(fs);
			//ピッチ推定
			await Task.Run(()=> WorldUtil.EstimateF0(
				WorldUtil.Estimaion.Harvest,
				x,
				len,
				parameters
			));
			//音色推定
			await Task.Run(()=> WorldUtil.EstimateSpectralEnvelope(
				x,
				len,
				parameters
			));
			if (tempName != null && File.Exists(tempName))
			{
				await Task.Run(()=> File.Delete(tempName));  //remove temp file
			}

			return parameters;
		}

		private static string GetSafeFileName(string serifText)
		{
			var illigalStr = Path.GetInvalidFileNameChars();
			illigalStr = illigalStr.Concat(Path.GetInvalidPathChars()).ToArray<char>();
			return string.Concat(serifText.Where(c => !illigalStr.Contains(c)));
		}

		/// <summary>
		/// Get MIDI Tick duration
		/// </summary>
		/// <param name="serifLen">serif duration seconds.</param>
		/// <returns></returns>
		private static double GetTickDuration(double serifLen)
		{
			return 960 / 0.4 * serifLen;
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
}
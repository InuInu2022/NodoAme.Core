using System.Globalization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

using MathNet.Numerics.Statistics;

using NodoAme.Models;
using Tssproj;

namespace NodoAme;

public static class ProjectWriter{
	public static void CulcScores(
		ExportLyricsMode exportMode,
		NoteAdaptMode noteAdaptMode,
		NoteSplitModes noteSplitMode,
		WorldParameters parameters,
		XElement scoreRoot,
		List<dynamic>? notesList,
		XElement timingNode,
		string engineType,
		double noteOffset,
		NoSoundVowelsModes noSoundVowelMode,
		double tempo)
	{
		var phCount = 0;
		var pauCount = 0;
		var notesListCount = 0;
		foreach (List<dynamic> nList in notesList.Cast<List<dynamic>>())
		{
			var phText = "";
			var noteLen = 0;
			var startClock = double.MaxValue;
			var startPhonemeTime = double.MaxValue;

			for (int i = 0; i < nList.Count; i++)
			{
				var ph = nList[i];
				if (ph is null)
				{
					continue;
				}

				string pText = ph.Phoneme ?? "";
				double start = ph.StartTime ?? 0.0;
				double end = ph.EndTime ?? 0.0;
				if (startPhonemeTime > start)
				{
					//最初の音素の開始時刻を指定
					startClock = Math.Round(NoteUtil.GetTickDuration(start, tempo));
					startPhonemeTime = start;
				}

				if (!PhonemeUtil.IsPau(ph))
				{
					var addPhoneme = pText;
					if(PhonemeUtil.IsNoSoundVowel(pText)
						&& noSoundVowelMode == NoSoundVowelsModes.VOLUME){
						addPhoneme = pText.ToLower();
					}

					phText += addPhoneme + ",";
					noteLen += (int)NoteUtil.GetTickDuration(end - start, tempo);
					phCount++;
				}
				else
				{
					pauCount++;
				}

				//timing elements
				var count = (5 * (phCount + pauCount)) - 1;
				var timingData =
					new XElement(
						"Data",
						new XAttribute("Index", count.ToString()),
						noteOffset + start
					);
				timingNode.Add(timingData);
				Debug.WriteLine($"Index: {count} [{ph.Phoneme}]");

				//途中の青線も指定しないと消えてしまう
				//単純分割だとCSでちゃんと聞こえない

				var spanTime = ph.EndTime - start;

				foreach (int t in Enumerable.Range(1, 4))
				{
					var first = spanTime / 2;
					var add = t switch
					{
						1 => first,
						_ => first + (first * 1 / 5 * t)
					};
					timingNode
						.Add(new XElement(
							"Data",
							//new XAttribute("Index", count+t),
							noteOffset + start + add
						));
					//Debug.WriteLine($"add:{add}");
				}

				//最後の処理
				if (noteSplitMode is not NoteSplitModes.SPLIT_ONLY_OLD
					&& i + 1 == nList.Count)
				{
					var lastTimingData = new XElement(
						"Data",
						noteOffset + ph.EndTime
					);
					timingNode.Add(lastTimingData);
				}
			}

			if (noteSplitMode is NoteSplitModes.IGNORE_NOSOUND)
			{
				pauCount++;
			}

			phText = phText.TrimEnd(",".ToCharArray());
			Debug.WriteLine($"phText :{phText}");

			//CS対策：CSは有効な文字種の歌詞でないとちゃんと発音しない（音素指定でも）
			var lyricText = exportMode switch
			{
				ExportLyricsMode.KANA
					=> PhonemeConverter
						.ConvertToKana(
							phText
								.Replace(PhonemeUtil.PAU, "")
								//silは無効な文字列に変換
								.Replace(PhonemeUtil.SIL, "あ")
								.Replace(",", "")),
				//ExportLyricsMode.ALPHABET
				//	=> serifText.Split(ENGLISH_SPLITTER)[notesListCount],
				_ => phText
					//silは無効な文字列に変換
					.Replace(PhonemeUtil.SIL, "_")
					.Replace(",", ""),
			};

			notesListCount++;
			Debug.WriteLine($"lyric: {lyricText}");

			var pitches = parameters.f0.Where(d => d > 0);
			pitches = engineType switch
			{
				TalkEngine.OPENJTALK =>
					pitches.Select(p => Math.Exp(p)),
				_ => pitches
			};

			(var octave, var step) = noteAdaptMode switch
			{
				NoteAdaptMode.AVERAGE =>
					NoteUtil.FreqToPitchOctaveAndStep(pitches.Average()),
				NoteAdaptMode.MEDIAN =>
					NoteUtil.FreqToPitchOctaveAndStep(pitches.Median()),
				_ => (4, 7)
			};

			//note
			const int offset = 3840;	//960*4
			var last = scoreRoot.Elements("Note").LastOrDefault();
			if(last?.HasAttributes == true && last.Attribute("Clock") is not null)
			{
				//前のノートと差が小さい場合は詰める
				//
				var clock = last.Attribute("Clock").Value;
				var len = last.Attribute("Duration").Value;
				var isSafeStart = double.TryParse(
					clock,
					out double start);
				var isSafeDuration = double.TryParse(
					len,
					out double duration);

				if(isSafeStart && isSafeDuration){
					var lastEnd = start + duration;
					startClock =
						offset + (int)startClock - lastEnd < 50 ?
						lastEnd - offset :
						startClock;
				}
			}

			if(noteLen == 0){
				//長さが０なら休符
				continue;
			}

			var note = new XElement(
				"Note",
				new XAttribute("Clock", offset + startClock),
				new XAttribute("PitchStep", step),
				new XAttribute("PitchOctave", octave),
				new XAttribute("Duration", noteLen),
				new XAttribute("Lyric", lyricText),
				new XAttribute("DoReMi", "false"),
				new XAttribute("Phonetic", phText)
			);
			scoreRoot.Add(note);
		}
	}

	/// <summary>
    /// 感情(Emotion)設定
    /// </summary>
    /// <param name="cast"></param>
    /// <param name="songVoiceStyles"></param>
    /// <param name="scoreRoot"></param>
	public static void WriteAttributeEmotion(
		SongCast? cast,
		ObservableCollection<SongVoiceStyleParam>? songVoiceStyles,
		XElement scoreRoot)
	{
		if (cast?.HasEmotion == null
			|| cast.HasEmotion != true)
		{
			return;
		}

		var emo = songVoiceStyles.First(v => v.Id == "Emotion");
		var emo1 = emo.Value;
		var emo0 = emo.Max - emo.Value;
		scoreRoot.SetAttributeValue("Emotion0", emo0);  //↓
		scoreRoot.SetAttributeValue("Emotion1", emo1);  //↑
	}

	/// <summary>
    /// 声質(Alpha)指定
    /// </summary>
    /// <param name="scoreRoot"></param>
	public static void WriteAttributeAlpha(
		ObservableCollection<SongVoiceStyleParam>? songVoiceStyles,
		XElement scoreRoot
	){
		const double ALP_DEFAULT = 0.55;
		var xmlAlpha = ALP_DEFAULT + (0.05 * songVoiceStyles.First(v => v.Id == "Alpha").Value);
		scoreRoot
			.SetAttributeValue(
				"Alpha",
				xmlAlpha
			);
	}

	/// <summary>
    /// ハスキー設定
    /// </summary>
    /// <param name="songVoiceStyles"></param>
    /// <param name="scoreRoot"></param>
	public static void WriteAttributeHusky(
		ObservableCollection<SongVoiceStyleParam>? songVoiceStyles,
		XElement scoreRoot
	){
		scoreRoot
			.SetAttributeValue(
				"Husky",
				songVoiceStyles.First(v => v.Id == "Husky").Value
			);
	}

	/// <summary>
	/// F0をピッチ線として書き込む
	/// </summary>
	/// <param name="parameters"></param>
	/// <param name="parameterRoot"></param>
	/// <param name="paramLen"></param>
	/// <returns></returns>
	public static XElement WriteElementsLogF0(
		WorldParameters parameters,
		XElement parameterRoot,
		double paramLen,
		string engineType,
		int trackParamOffsetIndex,
		NoPitchModes noPitch,
		SongExportPresets presets = SongExportPresets.NONE
	)
	{
		#region write_logf0

		var logF0Node = new XElement(
			"LogF0",
			new XAttribute("Length", paramLen)
		);

		//ささやきの時は全部NoData
		if(presets is SongExportPresets.WHISPER){
			logF0Node.Add(
				new XElement(
					"NoData",
					new XAttribute(
						"Repeat",
						paramLen)
				)
			);
			parameterRoot.Add(logF0Node);

			return logF0Node;
		}

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
				TalkEngine.SOUNDFILE
					=> Math.Log(parameters.f0![i]),
				_ => 0
			};

			if (noPitch == NoPitchModes.NONE &&
				parameters.f0![i] <= 0) {
				continue;
			}

			var node = (parameters.f0![i] > 0) ?
				new XElement(
					"Data",
					new XAttribute("Index", (trackParamOffsetIndex + i).ToString()),
					logF0.ToString(CultureInfo.InvariantCulture)
				) :
				//default pitch remove
				new XElement(
					"NoData",
					new XAttribute("Index", (trackParamOffsetIndex + i).ToString())
				)
			;
			logF0Node.Add(node);
		}

		parameterRoot.Add(logF0Node);

		#endregion
		return logF0Node;
	}

	public static void WriteElementsDynamics(
		XElement scoreRoot,
		ScoreDynamics dynamics,
		int clock = 0
	)
	{
		var nodes = scoreRoot
			.Elements("Dynamics");
		var exists = nodes
			.Any(v => v.HasAttributes
				&& v.Attribute("Clock").Value == clock.ToString())
			;

		if(exists){
			var target = nodes
				.First(v => v.Attribute("Clock").Value == clock.ToString());
			target
				.Attribute("Value")
				.SetValue((int)dynamics);
		}else if( nodes.Any() ){
			scoreRoot
				.AddFirst(CreateDynamicsElement(dynamics, clock));
		}
		else{
			var next = nodes
				.First(v => int.Parse(v.Attribute("Clock").Value) > clock);
			next.AddBeforeSelf(CreateDynamicsElement(dynamics, clock));
		}
	}

	private static XElement CreateDynamicsElement(ScoreDynamics dynamics, int clock){
		return new XElement(
			"Dynamics",
			new XAttribute("Clock", clock),
			new XAttribute("Value", (int)dynamics)
		);
	}

	public static void WriteElementsTempo(
		XElement tmplTrack,
		double tempo = 150.0
	){
		var songRoot = tmplTrack
			.Descendants("Song")
			.First();
		var nodes = songRoot
			.Element("Tempo")
			.Elements("Sound");
		var first = nodes
			.First(v => int.Parse(v.Attribute("Clock").Value) == 0);
		first.SetAttributeValue("Tempo",tempo);
	}

	/// <summary>
    /// TMGの線を書き込む
    /// </summary>
    /// <param name="tmplTrack"></param>
    /// <param name="timingNode"></param>
	public static void WriteElementsTiming(
		XElement tmplTrack,
		XElement timingNode)
	{
		#region write_timing

		//Timing elements
		//TMGの線を書き込む
		var songRoot = tmplTrack
			.Descendants("Song")
			.First();
		songRoot
			.Add(new XElement("Parameter", timingNode));

		#endregion
	}

	/// <summary>
    /// Volumeの線を書き込む
    /// </summary>
    /// <param name="breathSuppress"></param>
    /// <param name="phs"></param>
    /// <param name="parameterRoot"></param>
    /// <param name="paramLen"></param>
    /// <param name="trackParamOffsetIndex"></param>
    /// <param name="indexSpanTime"></param>
    /// <returns></returns>
	public static XElement WriteElementsC0(
		BreathSuppressMode breathSuppress,
		List<Label> phs,
		XElement parameterRoot,
		double paramLen,
		int trackParamOffsetIndex,
		double indexSpanTime,
		NoSoundVowelsModes noSoundVowelsModes,
		ExportFileOption? option = null
	)
	{
		//Volumeの線を書き込む
		#region write_c0

		//volume(C0) node root
		var volumeNode = new XElement(
			"C0",
			new XAttribute("Length", paramLen)
		);

		const string VOL_ZERO = "-2.4";

		//CeVIOのバグ開始部分のVOLを削る
		var startVolumeRep = breathSuppress switch
		{
			BreathSuppressMode.NO_BREATH =>
				//最初の音素開始まで無音
				//Math.Round((double)(phs.Find(v => v.Phoneme! == "sil")!.EndTime! / INDEX_SPAN_TIME), 0, MidpointRounding.AwayFromZero)
				trackParamOffsetIndex
				,
			BreathSuppressMode.NONE or _ =>
				//4分の1拍無音化
				trackParamOffsetIndex / 4
		};
		var startVol = new XElement(
			"Data",
			new XAttribute(
				"Index",
				0),
			new XAttribute(
				"Repeat",
				startVolumeRep),
			VOL_ZERO
		);
		volumeNode.Add(startVol);

		//無声母音/ブレス音のVOLを削る
		var reg = breathSuppress switch
		{
			BreathSuppressMode.NO_BREATH =>
				new Regex("[AIUEO]|pau|sil", RegexOptions.Compiled),
			BreathSuppressMode.NONE or _ =>
				new Regex("[AIUEO]", RegexOptions.Compiled),
		};

		var noSoundVowels = phs
			.AsParallel()
			.Where(l =>
				l.Phoneme is not null &&
					noSoundVowelsModes is not NoSoundVowelsModes.PHONEME &&
					reg.IsMatch(l.Phoneme)
			)
			;
		foreach (var ph in noSoundVowels)
		{
			CulcTimes(indexSpanTime, ph, out double index, out double rep);

			var tVol = new XElement(
				"Data",
				new XAttribute("Index", trackParamOffsetIndex + index),
				new XAttribute("Repeat", rep),
				VOL_ZERO
			);
			volumeNode.Add(tVol);
		}

		//whisper preset
		if (option?.SongExportPreset == SongExportPresets.WHISPER){
			var soundsVowels = phs
				.AsParallel()
				.Where(l =>
					l.Phoneme is not null &&
						!reg.IsMatch(l.Phoneme)
				);

			var vol = option?
				.Cast?
				.SongExportPreset
				.First(v => v.Id == SongExportPresets.WHISPER)
				.ClipVol
				;

			foreach(var ph in soundsVowels)
			{
				CulcTimes(indexSpanTime, ph, out double index, out double rep);

				var tVol = new XElement(
					"Data",
					new XAttribute("Index", trackParamOffsetIndex + index),
					new XAttribute("Repeat", rep),
					vol?.ToString(CultureInfo.InvariantCulture) ?? "5.0"
				);
				volumeNode.Add(tVol);
			}

			//順番を並び替える
			var ordered = volumeNode
				.Descendants("Data")
				.OrderBy(e =>
					Convert.ToInt32(e.Attribute("Index").Value))
				;
			volumeNode.ReplaceNodes(ordered);
		}

		//CeVIOのバグ終了部分のVOLを削る
		var lastTime = phs.Last().EndTime ?? 0.0;
		var lastIndex = Math.Round(lastTime / indexSpanTime, 0, MidpointRounding.AwayFromZero);
		var endVol = new XElement(
			"Data",
			new XAttribute("Index", trackParamOffsetIndex + lastIndex),
			new XAttribute("Repeat", paramLen - (lastIndex + trackParamOffsetIndex)), //1小節
			VOL_ZERO
			);
		volumeNode.Add(endVol);

		parameterRoot.Add(volumeNode);

		#endregion
		return volumeNode;

		static void CulcTimes(
			double indexSpanTime,
			Label? ph,
			out double index,
			out double rep
		)
		{
			var s = ph?.StartTime is null ? 0.0 : (double)ph.StartTime! / indexSpanTime;
			index = Math.Round(s, 0, MidpointRounding.AwayFromZero);
			var e = ph?.EndTime is null ? 0.0 : (double)ph.EndTime! / indexSpanTime;
			var eIndex = Math.Round(e, 0, MidpointRounding.AwayFromZero);
			rep = eIndex - index;
		}
	}

	/// <summary>
    /// Unit要素に書き加える
    /// </summary>
    /// <param name="tmplTrack"></param>
    /// <param name="guid"></param>
    /// <param name="serifLen"></param>
    /// <param name="castToExport"></param>
	public static void WriteElementsUnit(
		XElement tmplTrack,
		string guid,
		double serifLen,
		string castToExport)
	{
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
		unitNode.SetAttributeValue("CastId", castToExport);

		#endregion
	}

	/// <summary>
    /// トラック名とIDを書き込む
    /// </summary>
    /// <param name="serifText"></param>
    /// <param name="cast"></param>
    /// <param name="tmplTrack"></param>
    /// <param name="guid"></param>
    /// <param name="CastToExport"></param>
	public static void WriteElementsGroup(
		string serifText,
		SongCast? cast,
		XElement tmplTrack,
		string guid,
		string CastToExport)
	{
		//トラック名とIDを書き込む
		var groupNode = tmplTrack.Descendants("Group").First();
		groupNode.SetAttributeValue("Name", serifText);
		groupNode.SetAttributeValue("Id", guid);
		groupNode.SetAttributeValue("CastId", CastToExport);
		if (cast?.SongSoft == TalkSoftName.CEVIO_CS)
		{
			groupNode.SetAttributeValue("Volume", 10);
		}
	}

	public static byte[] WriteTssprj(
		SongCast? cast,
		XElement scoreRoot,
		XElement timingNode,
		XElement logF0Node,
		XElement volumeNode)
	{
		var tssprj = Array.Empty<byte>();

		tssprj = File.ReadAllBytes("./template/Template.tssprj");

		var r = scoreRoot;
		var es = scoreRoot.Elements();

		//ボイス情報の置き換え
		if (
			//cast情報が空なら置き換えない
			cast?.CharaNameAsAlphabet is not null
				&& cast.Id is not null
				&& cast.VoiceVersion is not null
		)
		{
			tssprj = tssprj
				.AsSpan()
				.ReplaceVoiceLibrary(
					cast.CharaNameAsAlphabet,
					cast.Id,
					cast.VoiceVersion
				);
		}

		var notes = scoreRoot
			.Elements()
			.Where(v => v.Name == "Note")
			.Select(v => new Tssproj.Note(
				int.Parse(v.Attribute("Clock").Value),
				int.Parse(v.Attribute("Duration").Value),
				v.Attribute("Lyric").Value,
				v.Attribute("Phonetic").Value,
				int.Parse(v.Attribute("PitchOctave").Value),
				int.Parse(v.Attribute("PitchStep").Value)
			))
			.ToList();
		tssprj = tssprj.AsSpan().ReplaceNotes(notes);

		Debug.WriteLine($"tmg len: {timingNode.Attribute("Length").Value}");
		var timing = new Timing(
			int.Parse(timingNode.Attribute("Length").Value),
			GetDataList(timingNode)
		);

		Debug.WriteLine($"pit len: {logF0Node.Attribute("Length").Value}");
		var pitch = new Pitch(
			int.Parse(logF0Node.Attribute("Length").Value),
			GetDataList(logF0Node)
		);

		Debug.WriteLine($"vol len: {volumeNode.Attribute("Length").Value}");
		var volume = new Volume(
			int.Parse(volumeNode.Attribute("Length").Value),
			GetDataList(volumeNode)
		);

		return tssprj
			.AsSpan()
			.ReplaceParamters(timing, pitch, volume);
	}

	public static async ValueTask<(List<dynamic> notesList, int phNum)>
	SplitPhonemesToNotesAsync(
		List<Label> phs,
		ExportLyricsMode mode,
		NoteSplitModes splitMode
	)
	{
		var notesList = new List<dynamic>();
		var noteList = new List<dynamic>();
		var phNum = 0;

		switch (splitMode)
		{
			//文節単位分割
			case NoteSplitModes.SPLIT_ONLY_OLD:
			case NoteSplitModes.SPLIT_SILIENTNOTE:
			case NoteSplitModes.IGNORE_NOSOUND:
			{
				await Task.Run(() =>
				{
					for (int i = 0; i < phs.Count; i++)
					{
						var v = phs[i];
						switch (v.Phoneme)
						{
							case "sil": //ignore phoneme
							{
								if (splitMode == NoteSplitModes.SPLIT_SILIENTNOTE)
								{
									if(noteList.Count>0){
										//前にノーツがあるなら分割
										phNum = Split();
									}
									//sil単体のノートを追加
									noteList.Add(v);
									phNum = Split();
								}

								break;
							}

							case "pau": //split note
							{
								if (splitMode is
									NoteSplitModes.SPLIT_ONLY_OLD or
									NoteSplitModes.SPLIT_SILIENTNOTE)
								{
									noteList.Add(v);
								}

								phNum = Split();
								break;
							}

							default:    //append
							{
								noteList.Add(v);
								phNum++;
								break;
							}
						}
					}
				});
				notesList.Add(new List<dynamic>(noteList));

				break;
			}

			//音節単位分割
			case NoteSplitModes.SYLLABLE:
			{
				for (int i = 0; i < phs.Count; i++)
				{
					var v = phs[i];

					switch (v.Phoneme)
					{
						case "sil":
							break;

						//split note
						case "pau":
							{
								noteList.Add(v);
								phNum = Split();
								break;
							}
						//日本語の「ん」「っ」の時は音節の区切りとみなす
						case "N":
						case "cl":
							{
								noteList.Add(v);
								phNum = Split();
								break;
							}

						//append
						default:
							{
							var isVowel = PhonemeUtil.IsVowel(v);
							var isAdd = i < phs.Count - 1 &&

								phs[i+1].Phoneme is "N" or "cl";

							if(isVowel && !isAdd){
								noteList.Add(v);
								phNum = Split();

								break;
							}

							noteList.Add(v);
							phNum++;
							break;
							}
					}
				}

				break;
			}
		}

		return (notesList, phNum);

		int Split()
		{
			notesList.Add(new List<dynamic>(noteList));
			noteList.Clear();
			phNum++;
			return phNum;
		}
	}

	private static List<Data> GetDataList(XElement baseNode)
	{
		var data = baseNode
			.Elements()
			.Select(v => new Tssproj.Data(
				double.Parse(v.Value, CultureInfo.InvariantCulture),
				v.HasAttributes && v.Attribute("Index") is not null ?
					int.Parse(v.Attribute("Index").Value) : null,
				v.HasAttributes && v.Attribute("Repeat") is not null ?
					int.Parse(v.Attribute("Repeat").Value) : null
			))
			.ToList();
		return data;
	}
}
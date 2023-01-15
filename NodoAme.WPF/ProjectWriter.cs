using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using MathNet.Numerics.Statistics;

using NodoAme.Models;

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
		double noteOffset)
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
					startClock = Math.Round(NoteUtil.GetTickDuration(start));
					startPhonemeTime = start;
				}

				if (!PhonemeUtil.IsPau(ph))
				{
					var addPhoneme = PhonemeUtil.IsNoSoundVowel(pText) switch
					{
						true => pText.ToLower(),    //無声母音は小文字化
						false => pText
					};
					phText += addPhoneme + ",";
					noteLen += (int)NoteUtil.GetTickDuration(end - start);
					phCount++;
				}
				else
				{
					pauCount++;
				}

				//timing elements
				var count = (5 * (phCount + pauCount)) - 1;
				var timingData = new XElement(
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
							noteOffset + start + add
						));
					//Debug.WriteLine($"add:{add}");
				}

				//最後の処理
				if (i + 1 == nList.Count)
				{
					var lastTimingData = new XElement(
						"Data",
						noteOffset + ph.EndTime
					);
					timingNode.Add(lastTimingData);
				}
			}

			if (noteSplitMode == NoteSplitModes.IGNORE_NOSOUND)
			{
				pauCount++;
			}

			phText = phText.TrimEnd(",".ToCharArray());
			Debug.WriteLine($"phText :{phText}");

			//CS対策：CSは有効な文字種の歌詞でないとちゃんと発音しない（音素指定でも）
			var lyricText = exportMode switch
			{
				ExportLyricsMode.KANA
					=> PhonemeConverter.ConvertToKana(phText.Replace("pau", "").Replace(",", "")),
				//ExportLyricsMode.ALPHABET
				//	=> serifText.Split(ENGLISH_SPLITTER)[notesListCount],
				_ => phText.Replace(",", ""),
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
			var note = new XElement(
				"Note",
				new XAttribute("Clock", 3840 + startClock),
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
		int trackParamOffsetIndex)
	{
		#region write_logf0

		var logF0Node = new XElement(
			"LogF0",
			new XAttribute("Length", paramLen)
		);

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

			var node = new XElement(
				"Data",
				new XAttribute("Index", (trackParamOffsetIndex + i).ToString()),
				logF0.ToString()
			);
			logF0Node.Add(node);
		}

		parameterRoot.Add(logF0Node);

		#endregion
		return logF0Node;
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
		double indexSpanTime
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
			.Where(l => l.Phoneme is not null && reg.IsMatch(l.Phoneme))
			;
		foreach (var ph in noSoundVowels)
		{
			var s = ph.StartTime is null ? 0.0 : (double)ph.StartTime! / indexSpanTime;
			var index = Math.Round(s, 0, MidpointRounding.AwayFromZero);
			var e = ph.EndTime is null ? 0.0 : (double)ph.EndTime! / indexSpanTime;
			var eIndex = Math.Round(e, 0, MidpointRounding.AwayFromZero);
			var rep = eIndex - index;

			var tVol = new XElement(
				"Data",
				new XAttribute("Index", trackParamOffsetIndex + index),
				new XAttribute("Repeat", rep),
				VOL_ZERO
			);
			volumeNode.Add(tVol);
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
}
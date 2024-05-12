using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Tssproj;

public enum TssValueType: byte{
    Int32 = 01,
    Bool = 02,
    Double = 04,
    String = 05,

    Unknown = 99,
}


public static class TssProjExt
{

    /*
     == tssprj format memo ==
     単純なkey=valueは key(ascii string) - 区切りバイト列 - value(データ型のバイト数)　で記録されている
        Syrabic, Mode, Sound>Clock, Timing>Clock,

     親子の区切り？ 0x000103  0x00000103 (0x00 - 0x000103 かも)
        0x000107 (子が7つ)
            Notem, VoiceInformation
        0x000104 (子が7つ)
            GlobalParamters
        0x000103 (0x00 - 0x000103)
            該当key: Song, Score
        0x000102 (子が2つ)
            Parameter, Data, NeuralVocoderList
        0x000101 (子が一つ)
            Timing, LogF0

     区切り (3byte目は後続データのbyte数+1)
        //[0x0001] - [byte数+1] - [data型]
        0x00010501 (int32？)
        0x00010102
        0x00010904 (double(float64)？)
        0x0001**05 (string/utf8)


    Node - 0x000107
        Clock - 0x00010501 - int32
        Duration -
        PitchStep -
        PitchOctave -
        Lyric - 0x00011105 - utf8string - 0x00
        Syrabic - 0x0010501 - int32
        Phoneme - 0x00011405 - ascii - 0x00
        - 0x00

     Parameter - 0x000102
        Timing - 0x000101 - Length - 0x00010501 - int32 - 0x0136
            Data - 0x000102|0x000101 (- Index - 0x00010501 - int32) - Value - 0x00010904 - double - 0x00
        LogF0 - 0x000101 - Length - 0x00010501 - int32 - 0x028701
            Data - 0x000102|0x000101 (- Index - 0x00010501 - int32) - Value - 0x00010904 - double - 0x00
        C0 - 0x000101 - Length - 0x00010501 - int32 - 0x011C(01 (byte)28)

        [ChildName] - NULL - 0x01 - [#Attributes] - (Atributes -) 0x01 - [#GrandChildren]
    */

	public static byte[] ReplaceNotes(
		this Span<byte> bin,
		List<Note> notes
	)
	{
		var sIndex = bin.LastIndexOf(System.Text.Encoding.UTF8.GetBytes("Score"));
		var eIndex = bin.LastIndexOf(System.Text.Encoding.UTF8.GetBytes("Parameter"));

		//var scoreText = System.Text.Encoding.UTF8.GetBytes("Score");
		//var scoreIndex = bin.LastIndexOf(scoreText);


		var score = new TssTree("Score");
		//score.AddAttribute("Alpha", 0.55);
		var key = new TssTree("Key");
		key.AddAttribute("Clock", 0);
		key.AddAttribute("Fifths", 0);
		key.AddAttribute("Mode", 0);
		var dynamics = new TssTree("Dynamics");
		dynamics.AddAttribute("Clock", 0);
		dynamics.AddAttribute("Value", 5);
		score.Children.Add(key);
		score.Children.Add(dynamics);
		notes.ForEach(v => score.Children.Add(v));
		Console.WriteLine($"score children: {score.Count}");

		var before = bin.Slice(0, sIndex);
		var after = bin.Slice(eIndex);
		/*
		var insertNotes = notes
            .Select(v => v.GetBytes())
            .Aggregate((a, b) => a.Concat(b).ToArray())
            .AsSpan();
        */

		return before
			.ToArray()
			.Concat(score.GetBytes())
			.Concat(after.ToArray())
			.ToArray();
	}

	public static byte[] ReplaceParamters(
		this Span<byte> bin,
		Timing? timings = null,
		Pitch? pitches = null,
		Volume? volumes = null
	){
		var sIndex = bin.LastIndexOf(System.Text.Encoding.UTF8.GetBytes("Parameter")) -1;
		var eIndex = bin.Length - 1;

		var paramters = new TssTree("Parameter");
		// Timing -> C0 -> LogF0
		if(timings is not null)paramters.Children.Add(timings);
		if(volumes is not null)paramters.Children.Add(volumes);
		if(pitches is not null)paramters.Children.Add(pitches);

		var before = bin.Slice(0, sIndex);
		var after = bin.Slice(eIndex);

		return before
			.ToArray()
			.Concat(paramters.GetBytes())
			.Concat(after.ToArray())
			.ToArray();
	}

	public static byte[] ReplaceVoiceLibrary(
		this Span<byte> bin,
		string charaNameAsAlphabet,
		string voiceLibName,
		string voiceVer
	){
		var span = bin
			.ReplaceTextValue("CharacterName", "VoiceFileName", charaNameAsAlphabet)
			.AsSpan<byte>();
		span = span
			.ReplaceTextValue("VoiceFileName", "Language", voiceLibName)
			.AsSpan<byte>()
			.ReplaceTextValue("VoiceVersion", "ActiveAfterThisVersion", voiceVer)
			.AsSpan<byte>()
			;

		return span.ToArray();
	}

	private static byte[] ReplaceTextValue(
		this Span<byte> bin,
		string key,
		string nextKey,
		string text
	){
		var sIndex = bin.LastIndexOf(Encoding.UTF8.GetBytes(key));
		var eIndex = bin.LastIndexOf(Encoding.UTF8.GetBytes(nextKey));

		var before = bin.Slice(0, sIndex);
		var after = bin.Slice(eIndex);

		var replaced = new TssKeyValue(key, text);
		Debug.WriteLine($"replaced[{key}]: {replaced.GetBytes().Length}");

		return before
			.ToArray()
			.Concat(replaced.GetBytes())
			.Concat(after.ToArray())
			.ToArray();
	}
}
